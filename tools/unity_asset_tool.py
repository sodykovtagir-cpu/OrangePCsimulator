#!/usr/bin/env python3
"""
unity_asset_tool.py — библиотека для программного редактирования Unity YAML
(.asset / .prefab / .meta) без запуска Unity Editor.

Зачем: готовые ПК и майнеры собираются из существующих префабов компонентов.
Руками писать 1000-строчные YAML-документы нельзя — легко порвать fileID/guid
и получить "missing script". Этот модуль работает на уровне документов Unity
YAML и генерирует детерминированные fileID, поэтому повторный запуск даёт
идентичный результат (без мусорных диффов).

Ключевые сущности Unity-сериализации, которые тут поддержаны:
  * .meta         — guid ассета (guid стабильно генерируется из имени)
  * ScriptableObject (.asset)   — например ShopItem
  * PrefabInstance (!u!1001)    — вложение чужого префаба внутрь префаба
                                  (именно так BigMiner вкладывает Miner)
  * stripped Transform/MonoBehaviour — ссылки на объекты внутри вложенного
                                  префаба

Публичный API:
    doc = UnityDoc.load(path)          # разбор файла на документы
    doc.find(class_id=..., name=...)   # поиск документов
    doc.save()                         # запись обратно

    make_guid("Ready_OfficePC")        # детерминированный guid
    ShopItemAsset(...).write(path)     # создать ассет магазина
    ReadyBuild(...).write(...)         # собрать готовый ПК как префаб

CLI:
    python3 tools/unity_asset_tool.py list-shop      Assets/MonoBehaviour/Shop.asset
    python3 tools/unity_asset_tool.py show-slots     Assets/Resources/components/Miner.prefab
    python3 tools/unity_asset_tool.py guid           Ready_OfficePC
"""

from __future__ import annotations

import hashlib
import os
import re
import sys
from dataclasses import dataclass, field
from typing import Dict, Iterable, List, Optional, Tuple

# ---------------------------------------------------------------------------
# Константы проекта OrangePCsimulator (guid'ы скриптов из Assets/Scripts)
# ---------------------------------------------------------------------------

SCRIPT_GUIDS = {
    "ShopItem": "484ef020c3930db604de1872bd7b83d7",
    "Shop": "e54a11e49d5a55439688ea8694ab5f2a",
    "Item": "c30496d6ae6d6f907456e10a94e01ec6",
    "Case": "671d1448274186bd3d8efabeb9529196",
    "HardwareSlot": "39ea76bbc8e6a7d7f146fcb3b48b957b",
    "Slot": "f35567f78c6622a51b9c18ad8b24fb7e",
    # Раньше эти две строки были перепутаны местами и «Crate» указывал на
    # скрипт Box. Сверено с Assets/Scripts/Assembly-CSharp/*.cs.meta.
    "Crate": "8a5265d90941f5ea7581d7dbd7e12e35",
    "Receiver": "46655a7f7503170198a223c11988a65a",
    "Box": "d7f903f50f13e21af61667d85cf95dde",
}

# Unity class id -> человекочитаемое имя (только те, что реально встречаются)
CLASS_NAMES = {
    1: "GameObject",
    4: "Transform",
    23: "MeshRenderer",
    33: "MeshFilter",
    54: "Rigidbody",
    64: "MeshCollider",
    65: "BoxCollider",
    82: "AudioSource",
    114: "MonoBehaviour",
    1001: "PrefabInstance",
}

YAML_HEADER = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"


# ---------------------------------------------------------------------------
# Детерминированные идентификаторы
# ---------------------------------------------------------------------------

def make_guid(key: str) -> str:
    """32-символьный hex guid, стабильно выведенный из строки.

    Unity не требует, чтобы guid был случайным — только чтобы он был
    уникальным. Детерминированность даёт воспроизводимые файлы.
    """
    return hashlib.md5(("OrangePCsimulator::" + key).encode("utf-8")).hexdigest()


def make_file_id(key: str) -> int:
    """Положительный 63-битный fileID, стабильно выведенный из строки."""
    digest = hashlib.sha1(("fileID::" + key).encode("utf-8")).digest()
    value = int.from_bytes(digest[:8], "big") & 0x7FFFFFFFFFFFFFFF
    # Unity избегает мелких значений — они зарезервированы под встроенные ассеты.
    return value | (1 << 52)


# ---------------------------------------------------------------------------
# Разбор Unity YAML на документы
# ---------------------------------------------------------------------------

DOC_RE = re.compile(r"^--- !u!(\d+) &(-?\d+)(\s+stripped)?\s*$")


@dataclass
class UnityObject:
    """Один YAML-документ внутри файла Unity."""

    class_id: int
    file_id: int
    stripped: bool
    body: str  # всё после строки "--- !u!..", включая имя типа

    @property
    def type_name(self) -> str:
        first = self.body.split("\n", 1)[0].strip()
        return first.rstrip(":")

    def get(self, key: str) -> Optional[str]:
        m = re.search(rf"^\s*{re.escape(key)}:\s*(.*)$", self.body, re.M)
        return m.group(1).strip() if m else None

    def set(self, key: str, value: str) -> bool:
        pattern = re.compile(rf"^(\s*{re.escape(key)}:)[ \t]*(.*)$", re.M)
        if not pattern.search(self.body):
            return False
        self.body = pattern.sub(lambda m: f"{m.group(1)} {value}", self.body, count=1)
        return True

    @property
    def name(self) -> Optional[str]:
        return self.get("m_Name")

    @property
    def script_guid(self) -> Optional[str]:
        m = re.search(r"m_Script:\s*\{fileID:\s*\d+,\s*guid:\s*(\w+)", self.body)
        return m.group(1) if m else None

    def render(self) -> str:
        head = f"--- !u!{self.class_id} &{self.file_id}"
        if self.stripped:
            head += " stripped"
        return head + "\n" + self.body


@dataclass
class UnityDoc:
    path: str
    header: str
    objects: List[UnityObject]

    # -- загрузка / сохранение ------------------------------------------
    @classmethod
    def load(cls, path: str) -> "UnityDoc":
        with open(path, "r", encoding="utf-8") as fh:
            text = fh.read()
        lines = text.split("\n")
        header_lines: List[str] = []
        objects: List[UnityObject] = []
        current: Optional[UnityObject] = None
        buf: List[str] = []

        for line in lines:
            m = DOC_RE.match(line)
            if m:
                if current is not None:
                    current.body = "\n".join(buf).rstrip("\n") + "\n"
                    objects.append(current)
                current = UnityObject(
                    class_id=int(m.group(1)),
                    file_id=int(m.group(2)),
                    stripped=bool(m.group(3)),
                    body="",
                )
                buf = []
            elif current is None:
                header_lines.append(line)
            else:
                buf.append(line)

        if current is not None:
            current.body = "\n".join(buf).rstrip("\n") + "\n"
            objects.append(current)

        header = "\n".join(header_lines)
        if header and not header.endswith("\n"):
            header += "\n"
        return cls(path=path, header=header, objects=objects)

    def render(self) -> str:
        return self.header + "".join(o.render() for o in self.objects)

    def save(self, path: Optional[str] = None) -> None:
        target = path or self.path
        with open(target, "w", encoding="utf-8") as fh:
            fh.write(self.render())

    # -- поиск -----------------------------------------------------------
    def find(
        self,
        class_id: Optional[int] = None,
        name: Optional[str] = None,
        script_guid: Optional[str] = None,
    ) -> List[UnityObject]:
        out = []
        for o in self.objects:
            if class_id is not None and o.class_id != class_id:
                continue
            if name is not None and o.name != name:
                continue
            if script_guid is not None and o.script_guid != script_guid:
                continue
            out.append(o)
        return out

    def by_id(self, file_id: int) -> Optional[UnityObject]:
        for o in self.objects:
            if o.file_id == file_id:
                return o
        return None

    def root_game_object(self) -> Optional[UnityObject]:
        """Корневой GameObject префаба — тот, чей Transform без m_Father."""
        for t in self.find(class_id=4):
            father = t.get("m_Father") or ""
            if "fileID: 0" in father:
                go_ref = t.get("m_GameObject") or ""
                m = re.search(r"fileID:\s*(-?\d+)", go_ref)
                if m:
                    return self.by_id(int(m.group(1)))
        gos = self.find(class_id=1)
        return gos[0] if gos else None

    # -- специфика проекта -----------------------------------------------
    def slots(self) -> List[Tuple[str, str, int]]:
        """Список слотов префаба: (target, matches, fileID компонента)."""
        out = []
        for o in self.find(class_id=114):
            if o.script_guid not in (
                SCRIPT_GUIDS["HardwareSlot"],
                SCRIPT_GUIDS["Slot"],
            ):
                continue
            target = o.get("target") or ""
            raw = o.get("matches")
            # "matches" — hex-строка байтов; пустое значение = подходит всё.
            # Если следующая строка YAML съехала в это поле, считаем пустым.
            matches = raw if raw and re.fullmatch(r"[0-9a-f]*", raw) else ""
            out.append((target, matches, o.file_id))
        return out


# ---------------------------------------------------------------------------
# .meta файлы
# ---------------------------------------------------------------------------

NATIVE_META = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData:\x20
  assetBundleName:\x20
  assetBundleVariant:\x20
"""

PREFAB_META = """fileFormatVersion: 2
guid: {guid}
PrefabImporter:
  externalObjects: {{}}
  userData:\x20
  assetBundleName:\x20
  assetBundleVariant:\x20
"""


def write_meta(asset_path: str, guid: str, kind: str = "native") -> None:
    """Создать .meta рядом с ассетом (если его ещё нет — иначе сохранить guid)."""
    meta_path = asset_path + ".meta"
    template = NATIVE_META if kind == "native" else PREFAB_META
    with open(meta_path, "w", encoding="utf-8") as fh:
        fh.write(template.format(guid=guid))


def read_meta_guid(asset_path: str) -> Optional[str]:
    meta_path = asset_path + ".meta"
    if not os.path.exists(meta_path):
        return None
    with open(meta_path, "r", encoding="utf-8") as fh:
        for line in fh:
            if line.startswith("guid:"):
                return line.split(":", 1)[1].strip()
    return None


# ---------------------------------------------------------------------------
# ShopItem (.asset)
# ---------------------------------------------------------------------------

SHOP_ITEM_TEMPLATE = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}
  m_Name: {name}
  m_EditorClassIdentifier:\x20
  itemName: '{item_name}'
  price: {price}
  bitcoin: {bitcoin}
  sprite: {{fileID: 21300000, guid: {sprite_guid}, type: 2}}
  spawn: {{fileID: {spawn_file_id}, guid: {spawn_guid}, type: 3}}
  large: {large}
  translateDescription: {translate_description}
  description: {description}
"""


@dataclass
class ShopItemAsset:
    """ScriptableObject магазина (PC.Shop.ShopItem)."""

    name: str
    item_name: str
    price: int
    spawn_guid: str
    spawn_file_id: int
    sprite_guid: str
    bitcoin: float = 0
    large: int = 1
    description: str = ""
    translate_description: int = 1

    def render(self) -> str:
        desc = self.description
        if desc:
            desc = "'" + desc.replace("'", "''") + "'"
        return SHOP_ITEM_TEMPLATE.format(
            script_guid=SCRIPT_GUIDS["ShopItem"],
            name=self.name,
            item_name=self.item_name.replace("'", "''"),
            price=self.price,
            bitcoin=_num(self.bitcoin),
            sprite_guid=self.sprite_guid,
            spawn_file_id=self.spawn_file_id,
            spawn_guid=self.spawn_guid,
            large=self.large,
            translate_description=self.translate_description,
            description=desc,
        )

    def write(self, path: str, guid: Optional[str] = None) -> str:
        with open(path, "w", encoding="utf-8") as fh:
            fh.write(self.render())
        asset_guid = guid or read_meta_guid(path) or make_guid("asset:" + self.name)
        write_meta(path, asset_guid, kind="native")
        return asset_guid


def _num(v) -> str:
    if isinstance(v, float) and v.is_integer():
        return str(int(v))
    return str(v)


# ---------------------------------------------------------------------------
# Shop.asset — страницы магазина
# ---------------------------------------------------------------------------

def shop_pages(shop_path: str) -> List[Tuple[str, List[str]]]:
    """Прочитать страницы Shop.asset: [(pageName, [guid ассетов])]."""
    doc = UnityDoc.load(shop_path)
    obj = doc.find(class_id=114)[0]
    pages: List[Tuple[str, List[str]]] = []
    current: Optional[Tuple[str, List[str]]] = None
    for line in obj.body.split("\n"):
        m = re.match(r"\s*- pageName:\s*(.*)$", line)
        if m:
            current = (m.group(1).strip(), [])
            pages.append(current)
            continue
        m = re.search(r"guid:\s*(\w+)", line)
        if m and current is not None and line.strip().startswith("- {fileID"):
            current[1].append(m.group(1))
    return pages


def shop_add_page(shop_path: str, page_name: str, item_guids: Iterable[str]) -> bool:
    """Добавить (или заменить) страницу в Shop.asset. True — файл изменён."""
    doc = UnityDoc.load(shop_path)
    obj = doc.find(class_id=114)[0]
    block = f"  - pageName: {page_name}\n    item:\n"
    for g in item_guids:
        block += f"    - {{fileID: 11400000, guid: {g}, type: 2}}\n"

    existing = re.search(
        rf"^  - pageName: {re.escape(page_name)}\n(?:    .*\n)*",
        obj.body,
        re.M,
    )
    if existing:
        if existing.group(0) == block:
            return False
        obj.body = obj.body[: existing.start()] + block + obj.body[existing.end():]
    else:
        if not obj.body.endswith("\n"):
            obj.body += "\n"
        obj.body += block
    doc.save()
    return True


# ---------------------------------------------------------------------------
# Сборка готового ПК: префаб-контейнер с вложенными префабами
# ---------------------------------------------------------------------------

# ---------------------------------------------------------------------------
# Математика трансформов
#
# Готовый ПК нельзя "соединить" на уровне YAML: Slot хранит соединение в
# рантайм-полях (isUsing/attachedItem), которые не сериализуются. Соединение
# возникает физически — Slot.OnTriggerEnter ловит коллайдер детали. Поэтому
# сборка собирается геометрически: деталь ставится ровно в insertPos слота,
# и при спавне триггер сам её защёлкивает, как если бы игрок вставил руками.
# ---------------------------------------------------------------------------

Vec3 = Tuple[float, float, float]
Quat = Tuple[float, float, float, float]  # (x, y, z, w)


def q_mul(a: Quat, b: Quat) -> Quat:
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (
        aw * bx + ax * bw + ay * bz - az * by,
        aw * by - ax * bz + ay * bw + az * bx,
        aw * bz + ax * by - ay * bx + az * bw,
        aw * bw - ax * bx - ay * by - az * bz,
    )


def q_rotate(q: Quat, v: Vec3) -> Vec3:
    x, y, z, w = q
    vx, vy, vz = v
    tx = 2 * (y * vz - z * vy)
    ty = 2 * (z * vx - x * vz)
    tz = 2 * (x * vy - y * vx)
    return (
        vx + w * tx + (y * tz - z * ty),
        vy + w * ty + (z * tx - x * tz),
        vz + w * tz + (x * ty - y * tx),
    )


def q_conj(q: Quat) -> Quat:
    x, y, z, w = q
    return (-x, -y, -z, w)


def q_normalize(q: Quat) -> Quat:
    x, y, z, w = q
    n = (x * x + y * y + z * z + w * w) ** 0.5
    if n == 0:
        return (0.0, 0.0, 0.0, 1.0)
    return (x / n, y / n, z / n, w / n)


@dataclass
class TransformNode:
    file_id: int
    game_object_id: int
    local_pos: Vec3
    local_rot: Quat
    local_scale: Vec3
    parent_id: int
    name: str = ""


def _vec(text: Optional[str], default=(0.0, 0.0, 0.0)) -> Vec3:
    if not text:
        return default
    vals = dict(re.findall(r"([xyzw]):\s*(-?[\d.]+(?:[eE][-+]?\d+)?)", text))
    return (
        float(vals.get("x", default[0])),
        float(vals.get("y", default[1])),
        float(vals.get("z", default[2])),
    )


def _quat(text: Optional[str]) -> Quat:
    if not text:
        return (0.0, 0.0, 0.0, 1.0)
    vals = dict(re.findall(r"([xyzw]):\s*(-?[\d.]+(?:[eE][-+]?\d+)?)", text))
    return q_normalize(
        (
            float(vals.get("x", 0)),
            float(vals.get("y", 0)),
            float(vals.get("z", 0)),
            float(vals.get("w", 1)),
        )
    )


class TransformTree:
    """Иерархия трансформов одного префаба + расчёт мировых координат."""

    def __init__(self, doc: UnityDoc):
        self.doc = doc
        self.nodes: Dict[int, TransformNode] = {}
        for t in doc.find(class_id=4):
            go_ref = t.get("m_GameObject") or ""
            father = t.get("m_Father") or ""
            go_id = int(re.search(r"fileID:\s*(-?\d+)", go_ref).group(1))
            parent_id = int(re.search(r"fileID:\s*(-?\d+)", father).group(1))
            go = doc.by_id(go_id)
            self.nodes[t.file_id] = TransformNode(
                file_id=t.file_id,
                game_object_id=go_id,
                local_pos=_vec(t.get("m_LocalPosition")),
                local_rot=_quat(t.get("m_LocalRotation")),
                local_scale=_vec(t.get("m_LocalScale"), (1.0, 1.0, 1.0)),
                parent_id=parent_id,
                name=(go.name if go else "") or "",
            )

    def world(self, file_id: int) -> Tuple[Vec3, Quat]:
        """Поза трансформа в системе координат самого префаба."""
        node = self.nodes.get(file_id)
        if node is None:
            raise KeyError(f"Transform {file_id} не найден в {self.doc.path}")
        if node.parent_id == 0:
            return node.local_pos, node.local_rot
        ppos, prot = self.world(node.parent_id)
        rotated = q_rotate(prot, node.local_pos)
        pos = (ppos[0] + rotated[0], ppos[1] + rotated[1], ppos[2] + rotated[2])
        return pos, q_normalize(q_mul(prot, node.local_rot))

    def root_transform_id(self) -> Optional[int]:
        for node in self.nodes.values():
            if node.parent_id == 0:
                return node.file_id
        return None

    def relative_to_root(self, file_id: int) -> Tuple[Vec3, Quat]:
        """Поза трансформа относительно КОРНЯ префаба.

        При Instantiate(prefab, pos, rot) корню задаётся именно pos/rot, а его
        собственный локальный трансформ в ассете отбрасывается. Поэтому позиции
        слотов надо считать относительно корня, иначе всё уедет на его оффсет.
        """
        pos, rot = self.world(file_id)
        root_id = self.root_transform_id()
        if root_id is None:
            return pos, rot
        rpos, rrot = self.world(root_id)
        inv = q_conj(rrot)
        delta = (pos[0] - rpos[0], pos[1] - rpos[1], pos[2] - rpos[2])
        return q_rotate(inv, delta), q_normalize(q_mul(inv, rot))


# ---------------------------------------------------------------------------
# Вложенные префабы (PrefabInstance)
#
# BigMiner устроен так: сам файл — это инстанс Miner.prefab с модификациями,
# поверх которого во внешнем файле добавлены дополнительные слоты GPU. Такие
# объекты ссылаются на трансформы вложенного префаба через "stripped"-записи.
# Чтобы посчитать позы слотов, надо развернуть вложенный префаб и наложить
# m_Modifications.
# ---------------------------------------------------------------------------

MOD_RE = re.compile(
    r"- target: \{fileID: (-?\d+), guid: (\w+), type: \d+\}\s*\n"
    r"\s+propertyPath: ([\w.\[\]]+)\s*\n"
    r"\s+value:\s*(.*?)\s*\n",
    re.M,
)


class PrefabResolver:
    """Иерархия трансформов с раскрытием вложенных префабов.

    Ключ узла — либо int (fileID во внешнем файле), либо кортеж
    (instance_file_id, source_file_id) для объекта внутри вложенного префаба.
    """

    def __init__(self, path: str, root_dir: str = "."):
        self.root_dir = root_dir
        self.path = path
        self.doc = UnityDoc.load(path)
        self.nodes: Dict[object, TransformNode] = {}
        self.parents: Dict[object, object] = {}
        self.stripped: Dict[int, object] = {}   # outer fileID -> nested key
        self.slots: List[dict] = []
        self._build()

    # -- построение ------------------------------------------------------
    def _build(self) -> None:
        # 1. Собственные трансформы файла
        for t in self.doc.find(class_id=4):
            if t.stripped:
                continue
            go_ref = t.get("m_GameObject") or ""
            father = t.get("m_Father") or ""
            gm = re.search(r"fileID:\s*(-?\d+)", go_ref)
            fm = re.search(r"fileID:\s*(-?\d+)", father)
            if not gm or not fm:
                continue
            go = self.doc.by_id(int(gm.group(1)))
            self.nodes[t.file_id] = TransformNode(
                file_id=t.file_id,
                game_object_id=int(gm.group(1)),
                local_pos=_vec(t.get("m_LocalPosition")),
                local_rot=_quat(t.get("m_LocalRotation")),
                local_scale=_vec(t.get("m_LocalScale"), (1.0, 1.0, 1.0)),
                parent_id=int(fm.group(1)),
                name=(go.name if go else "") or "",
            )
            self.parents[t.file_id] = int(fm.group(1))

        # 2. Карта stripped-записей: outer fileID -> (instance, source fileID)
        for o in self.doc.objects:
            if not o.stripped:
                continue
            src = re.search(
                r"m_CorrespondingSourceObject: \{fileID: (-?\d+), guid: (\w+)",
                o.body,
            )
            inst = re.search(r"m_PrefabInstance: \{fileID: (-?\d+)\}", o.body)
            if src and inst:
                self.stripped[o.file_id] = (int(inst.group(1)), int(src.group(1)))

        # 3. Раскрытие каждого PrefabInstance
        for pi in self.doc.find(class_id=1001):
            self._expand_instance(pi)

        # 4. Объекты внешнего файла могут висеть на трансформе вложенного
        #    префаба — такая ссылка идёт через stripped-запись, подменяем её
        #    на реальный ключ узла, иначе они окажутся «сиротами».
        for key, parent in list(self.parents.items()):
            if isinstance(parent, int) and parent in self.stripped:
                self.parents[key] = self.stripped[parent]

        # 5. Слоты самого файла
        self._collect_slots(self.doc, key_of=lambda fid: fid, prefix=None)

    def _expand_instance(self, pi: UnityObject) -> None:
        src = re.search(r"m_SourcePrefab: \{fileID: \d+, guid: (\w+)", pi.body)
        if not src:
            return
        source_guid = src.group(1)
        source_path = self._path_by_guid(source_guid)
        if not source_path:
            return

        inst_id = pi.file_id
        sub = PrefabResolver(source_path, self.root_dir)

        # Модификации инстанса: propertyPath -> value для каждого source fileID
        mods: Dict[int, Dict[str, str]] = {}
        for fid, _guid, prop, value in MOD_RE.findall(pi.body):
            mods.setdefault(int(fid), {})[prop] = value

        parent_m = re.search(r"m_TransformParent: \{fileID: (-?\d+)\}", pi.body)
        outer_parent = int(parent_m.group(1)) if parent_m else 0

        for src_key, node in sub.nodes.items():
            if not isinstance(src_key, int):
                continue  # вложенность 3-го уровня в проекте не встречается
            key = (inst_id, src_key)
            pos, rot, scale = node.local_pos, node.local_rot, node.local_scale
            name = node.name
            m = mods.get(src_key, {})
            if m:
                pos = _apply_vec_mods(pos, m, "m_LocalPosition")
                rot = _apply_quat_mods(rot, m)
                scale = _apply_vec_mods(scale, m, "m_LocalScale")
                name = m.get("m_Name", name)

            parent_src = node.parent_id
            parent_key: object
            if parent_src == 0:
                parent_key = outer_parent  # корень вложенного префаба
            else:
                parent_key = (inst_id, parent_src)

            self.nodes[key] = TransformNode(
                file_id=src_key,
                game_object_id=node.game_object_id,
                local_pos=pos,
                local_rot=rot,
                local_scale=scale,
                parent_id=0,
                name=name,
            )
            self.parents[key] = parent_key

        # Слоты вложенного префаба
        self._collect_slots(
            sub.doc, key_of=lambda fid: (inst_id, fid), prefix=inst_id
        )

    def _collect_slots(self, doc: UnityDoc, key_of, prefix) -> None:
        for o in doc.find(class_id=114):
            if o.script_guid not in (
                SCRIPT_GUIDS["HardwareSlot"],
                SCRIPT_GUIDS["Slot"],
            ):
                continue
            insert = o.get("insertPos") or ""
            m = re.search(r"fileID:\s*(-?\d+)", insert)
            if not m:
                continue
            insert_id = int(m.group(1))
            key = key_of(insert_id)
            # Внешний файл может ссылаться на трансформ вложенного префаба.
            if key not in self.nodes and insert_id in self.stripped:
                key = self.stripped[insert_id]
            if key not in self.nodes:
                continue
            raw = o.get("matches")
            matches = raw if raw and re.fullmatch(r"[0-9a-f]*", raw) else ""
            self.slots.append(
                {
                    "target": o.get("target") or "",
                    "matches": matches,
                    "slot_file_id": o.file_id
                    if prefix is None
                    else (prefix * 1000003 + o.file_id) % (1 << 62),
                    "insert_key": key,
                }
            )

    def _path_by_guid(self, guid: str) -> Optional[str]:
        cache = getattr(PrefabResolver, "_guid_cache", None)
        if cache is None:
            cache = {}
            base = os.path.join(self.root_dir, "Assets")
            for dirpath, _dirs, files in os.walk(base):
                for fn in files:
                    if not fn.endswith(".prefab.meta"):
                        continue
                    full = os.path.join(dirpath, fn)
                    g = None
                    with open(full, encoding="utf-8", errors="replace") as fh:
                        for line in fh:
                            if line.startswith("guid:"):
                                g = line.split(":", 1)[1].strip()
                                break
                    if g:
                        cache[g] = full[: -len(".meta")]
            PrefabResolver._guid_cache = cache
        return cache.get(guid)

    # -- расчёт поз -------------------------------------------------------
    def pose(self, key) -> Tuple[Vec3, Quat]:
        node = self.nodes.get(key)
        if node is None:
            raise KeyError(f"Transform {key} не найден в {self.path}")
        parent = self.parents.get(key, 0)
        if parent == 0 or parent not in self.nodes:
            return node.local_pos, node.local_rot
        ppos, prot = self.pose(parent)
        rotated = q_rotate(prot, node.local_pos)
        return (
            (ppos[0] + rotated[0], ppos[1] + rotated[1], ppos[2] + rotated[2]),
            q_normalize(q_mul(prot, node.local_rot)),
        )

    def root_key(self):
        for key in self.nodes:
            parent = self.parents.get(key, 0)
            if parent == 0 or parent not in self.nodes:
                return key
        return None

    def relative_to_root(self, key) -> Tuple[Vec3, Quat]:
        pos, rot = self.pose(key)
        rk = self.root_key()
        if rk is None:
            return pos, rot
        rpos, rrot = self.pose(rk)
        inv = q_conj(rrot)
        delta = (pos[0] - rpos[0], pos[1] - rpos[1], pos[2] - rpos[2])
        return q_rotate(inv, delta), q_normalize(q_mul(inv, rot))

    def root_game_object_id(self) -> Optional[int]:
        rk = self.root_key()
        if rk is None:
            return None
        node = self.nodes[rk]
        if isinstance(rk, tuple):
            # Корень — вложенный префаб: во внешнем файле у него stripped-GO.
            for outer_id, nested in self.stripped.items():
                if nested == (rk[0], rk[1]):
                    obj = self.doc.by_id(outer_id)
                    if obj is not None and obj.class_id == 1:
                        return outer_id
            for outer_id, nested in self.stripped.items():
                obj = self.doc.by_id(outer_id)
                if obj is not None and obj.class_id == 1:
                    return outer_id
        return node.game_object_id

    def is_variant(self) -> bool:
        """Prefab Variant: только PrefabInstance, без собственных GameObject.

        Так сделаны RTX4080/4080Ti/RTX5090 — цепочка вариантов поверх RTX3080Ti.
        У таких префабов нет своего корневого GameObject в файле: Unity выдаёт
        ему fileID, вычисляемый при импорте, и в YAML он просто не хранится.
        Поэтому ссылаться на них надо fileID из уже существующих ассетов.
        """
        own_go = [o for o in self.doc.find(class_id=1) if not o.stripped]
        return not own_go and bool(self.doc.find(class_id=1001))


def _apply_vec_mods(base: Vec3, mods: Dict[str, str], prefix: str) -> Vec3:
    x, y, z = base
    if f"{prefix}.x" in mods:
        x = float(mods[f"{prefix}.x"])
    if f"{prefix}.y" in mods:
        y = float(mods[f"{prefix}.y"])
    if f"{prefix}.z" in mods:
        z = float(mods[f"{prefix}.z"])
    return (x, y, z)


def _apply_quat_mods(base: Quat, mods: Dict[str, str]) -> Quat:
    x, y, z, w = base
    if "m_LocalRotation.x" in mods:
        x = float(mods["m_LocalRotation.x"])
    if "m_LocalRotation.y" in mods:
        y = float(mods["m_LocalRotation.y"])
    if "m_LocalRotation.z" in mods:
        z = float(mods["m_LocalRotation.z"])
    if "m_LocalRotation.w" in mods:
        w = float(mods["m_LocalRotation.w"])
    return q_normalize((x, y, z, w))


def slot_placements(doc_or_path, root_dir: str = ".") -> List[dict]:
    """Слоты префаба с позами insertPos в системе координат его корня.

    Принимает путь к префабу (тогда раскрываются вложенные префабы) или уже
    загруженный UnityDoc (совместимость со старым вызовом).
    """
    if isinstance(doc_or_path, UnityDoc):
        path = doc_or_path.path
    else:
        path = doc_or_path
    resolver = PrefabResolver(path, root_dir)
    out = []
    for slot in resolver.slots:
        pos, rot = resolver.relative_to_root(slot["insert_key"])
        out.append(
            {
                "target": slot["target"],
                "matches": slot["matches"],
                "slot_file_id": slot["slot_file_id"],
                "insert_file_id": slot["insert_key"],
                "pos": pos,
                "rot": rot,
            }
        )
    return out


def find_existing_reference(guid: str, root_dir: str = ".") -> Optional[int]:
    """Найти fileID, которым проект уже ссылается на префаб с этим guid.

    Нужно для Prefab Variant'ов: их корневой GameObject не записан в YAML, а
    fileID Unity вычисляет при импорте. Угадывать его нельзя — но в проекте уже
    есть корректные ссылки (Box_*.prefab, ShopItem'ы), сделанные редактором.
    Берём fileID оттуда: это ровно тот идентификатор, который ждёт Unity.
    """
    cache = getattr(find_existing_reference, "_cache", None)
    if cache is None:
        cache = {}
        # Считаем только «настоящие» ссылки вида `поле: {fileID: N, guid: G}`.
        # Строки `- target:` внутри m_Modifications и служебные поля вариантов
        # указывают на внутренности исходного префаба, а не на его корень —
        # если их учитывать, побеждает случайный внутренний объект.
        pattern = re.compile(
            r"^\s*(?!- target:)(?!m_SourcePrefab:)"
            r"(?!m_CorrespondingSourceObject:)(?!m_PrefabInstance:)"
            r"[\w ]+:\s*\{fileID: (-?\d+), guid: (\w+), type: 3\}",
            re.M,
        )
        for dirpath, _dirs, files in os.walk(os.path.join(root_dir, "Assets")):
            for fn in files:
                if not (fn.endswith(".prefab") or fn.endswith(".asset")
                        or fn.endswith(".unity")):
                    continue
                full = os.path.join(dirpath, fn)
                try:
                    text = open(full, encoding="utf-8", errors="replace").read()
                except OSError:
                    continue
                for fid, g in pattern.findall(text):
                    fid_i = int(fid)
                    # fileID: 100100000 — это сам ассет префаба, не GameObject.
                    if fid_i in (100100000, 0):
                        continue
                    cache.setdefault(g, {}).setdefault(fid_i, 0)
                    cache[g][fid_i] += 1
        find_existing_reference._cache = cache

    refs = cache.get(guid)
    if not refs:
        return None
    # Самый частый fileID — корневой GameObject (на него ссылаются Box и магазин).
    return max(refs.items(), key=lambda kv: (kv[1], -kv[0]))[0]


def prefab_spawn_ref(prefab_path: str, root_dir: str = ".") -> Tuple[str, int]:
    """(guid, fileID корневого GameObject) для ссылки на префаб.

    Для обычных префабов корень читается прямо из YAML, для вариантов —
    берётся из существующих ссылок проекта.
    """
    guid = read_meta_guid(prefab_path)
    resolver = PrefabResolver(prefab_path, root_dir)
    if resolver.is_variant():
        fid = find_existing_reference(guid, root_dir)
        if fid is None:
            raise ValueError(
                f"{os.path.basename(prefab_path)} — Prefab Variant, и на него "
                f"ещё нет ссылок в проекте: fileID корня определить нечем. "
                f"Сошлись на него хотя бы из одного ассета в Unity."
            )
        return guid, fid
    root = resolver.root_game_object_id()
    if root is None:
        raise ValueError(f"{prefab_path}: не найден корневой GameObject")
    return guid, root


def item_info(doc: UnityDoc) -> dict:
    """Данные компонента-детали: корневой GameObject, match, slotOffset."""
    root = doc.root_game_object()
    info = {"root": root, "match": None, "slot_offset": (0.0, 0.0, 0.0)}
    for o in doc.find(class_id=114, script_guid=SCRIPT_GUIDS["Item"]):
        go_ref = o.get("m_GameObject") or ""
        m = re.search(r"fileID:\s*(-?\d+)", go_ref)
        if root is not None and m and int(m.group(1)) != root.file_id:
            continue
        match = o.get("match")
        info["match"] = int(match) if match and match.isdigit() else 0
        info["slot_offset"] = _vec(o.get("slotOffset"))
        break
    # Transform корня — чтобы знать его собственный локальный поворот
    for t in doc.find(class_id=4):
        father = t.get("m_Father") or ""
        if "fileID: 0" in father:
            info["root_transform"] = t.file_id
            break
    return info


@dataclass
class PartRef:
    """Компонент, вкладываемый в готовую сборку.

    `host` — имя другой детали сборки, в слоты которой ставится эта. Например
    CPU/RAM/GPU ставятся не в корпус, а в материнскую плату, поэтому у них
    host="ATX". По умолчанию host — корпус (рама майнера).
    """

    prefab_path: str          # путь к .prefab компонента
    slot_target: str          # в какой слот он ставится (Motherboard/GPU/RAM/...)
    slot_index: Optional[int] = None  # номер слота этого типа (None = следующий)
    host: Optional[str] = None        # имя детали-носителя слотов


@dataclass
class ReadyBuild:
    """Описание готовой сборки: корпус + список комплектующих."""

    name: str
    case_prefab: str
    parts: List[PartRef] = field(default_factory=list)

    def resolve(self, root: str) -> List[dict]:
        """Разложить сборку по слотам и посчитать позу каждой детали.

        Все позы считаются в системе координат КОРПУСА (базового префаба).
        Детали, которые ставятся в материнскую плату, пересчитываются через
        позу самой платы — так вложенность CPU/RAM/GPU получается корректной.

        Метод одновременно валидирует сборку: отсутствующий префаб, нехватка
        слотов или несовпадение match/matches вылезут здесь, а не в Unity.
        """
        # Кэш слотов каждого носителя + его поза в координатах корпуса.
        hosts: Dict[str, dict] = {}

        def load_host(rel_path: str, base_pos: Vec3, base_rot: Quat) -> dict:
            doc = UnityDoc.load(os.path.join(root, rel_path))
            by_target: Dict[str, List[dict]] = {}
            for p in slot_placements(doc):
                # Слот задан в координатах носителя -> переводим в координаты корпуса.
                rotated = q_rotate(base_rot, p["pos"])
                entry = dict(p)
                entry["pos"] = (
                    base_pos[0] + rotated[0],
                    base_pos[1] + rotated[1],
                    base_pos[2] + rotated[2],
                )
                entry["rot"] = q_normalize(q_mul(base_rot, p["rot"]))
                by_target.setdefault(p["target"], []).append(entry)
            # Стабильный порядок слотов одного типа — чтобы диффы не «плавали».
            for lst in by_target.values():
                lst.sort(
                    key=lambda p: (
                        round(p["pos"][1], 4),
                        round(p["pos"][0], 4),
                        round(p["pos"][2], 4),
                        p["slot_file_id"],
                    )
                )
            return {"slots": by_target, "used": {}}

        case_name = os.path.splitext(os.path.basename(self.case_prefab))[0]
        hosts[case_name] = load_host(self.case_prefab, (0.0, 0.0, 0.0), (0.0, 0.0, 0.0, 1.0))
        default_host = case_name

        resolved: List[dict] = []
        for order, part in enumerate(self.parts):
            part_full = os.path.join(root, part.prefab_path)
            if not os.path.exists(part_full):
                raise FileNotFoundError(part_full)

            host_name = part.host or default_host
            if host_name not in hosts:
                raise ValueError(
                    f"{self.name}: носитель '{host_name}' для "
                    f"{os.path.basename(part.prefab_path)} ещё не установлен. "
                    f"Материнку нужно объявить в parts раньше её деталей."
                )
            host = hosts[host_name]

            slots_of_type = host["slots"].get(part.slot_target, [])
            idx = part.slot_index if part.slot_index is not None else host["used"].get(
                part.slot_target, 0
            )
            if idx >= len(slots_of_type):
                raise ValueError(
                    f"{self.name}: у '{host_name}' слотов '{part.slot_target}' "
                    f"всего {len(slots_of_type)}, а запрошен №{idx + 1}"
                )
            host["used"][part.slot_target] = idx + 1
            slot = slots_of_type[idx]

            part_doc = UnityDoc.load(part_full)
            info = item_info(part_doc)
            if slot["matches"]:
                allowed = {
                    int(slot["matches"][i:i + 2], 16)
                    for i in range(0, len(slot["matches"]), 2)
                }
                if info["match"] is not None and info["match"] not in allowed:
                    raise ValueError(
                        f"{self.name}: {os.path.basename(part.prefab_path)} "
                        f"(match={info['match']}) не подходит слоту "
                        f"'{part.slot_target}' (matches={sorted(allowed)})"
                    )

            # Slot.SetComponent: rotation = insertPos.rotation,
            #                    position = insertPos.position + worldOffset
            rot = slot["rot"]
            off = q_rotate(rot, info["slot_offset"])
            pos = (
                slot["pos"][0] + off[0],
                slot["pos"][1] + off[1],
                slot["pos"][2] + off[2],
            )

            part_name = os.path.splitext(os.path.basename(part.prefab_path))[0]
            # Для Prefab Variant'ов (RTX4080/4080Ti/5090) корень не записан в
            # YAML — ссылку берём так же, как её делает Unity.
            part_guid, part_root = prefab_spawn_ref(part_full, root)
            resolved.append(
                {
                    "path": part.prefab_path,
                    "name": part_name,
                    "guid": part_guid,
                    "root_file_id": part_root,
                    "target": part.slot_target,
                    "index": idx,
                    "host": host_name,
                    "order": order,
                    "pos": pos,
                    "rot": rot,
                }
            )

            # Установленная деталь сама может нести слоты (материнка, стойка).
            if part_name not in hosts:
                child = load_host(part.prefab_path, pos, rot)
                if child["slots"]:
                    hosts[part_name] = child
        return resolved


# ---------------------------------------------------------------------------
# Генерация префаба-спавнера готовой сборки
# ---------------------------------------------------------------------------

SPAWNER_SCRIPT_GUID = "01aba92678a70fdad133966614ce5f35"  # PC.ReadyBuildSpawner


def quat_to_euler(q: Quat) -> Vec3:
    """Кватернион -> углы Эйлера Unity (ZXY, градусы)."""
    import math

    x, y, z, w = q_normalize(q)
    # Unity применяет вращения в порядке Z, X, Y
    sinx = 2.0 * (w * x - y * z)
    sinx = max(-1.0, min(1.0, sinx))
    ex = math.asin(sinx)
    if abs(sinx) > 0.9999:  # гимбал-лок
        ey = math.atan2(2.0 * (w * y - x * z), 1.0 - 2.0 * (x * x + y * y))
        ez = 0.0
    else:
        ey = math.atan2(2.0 * (w * y + x * z), 1.0 - 2.0 * (x * x + y * y))
        ez = math.atan2(2.0 * (w * z + x * y), 1.0 - 2.0 * (x * x + z * z))

    def norm(a: float) -> float:
        d = math.degrees(a) % 360.0
        return round(d if d >= 0 else d + 360.0, 3)

    return (norm(ex), norm(ey), norm(ez))


def _f(v: float) -> str:
    """Число в стиле Unity YAML: без хвостовых нулей."""
    r = round(v, 5) + 0.0
    if r == int(r):
        return str(int(r))
    return repr(r)


def write_spawner_prefab(
    path: str,
    build_name: str,
    base_prefab_guid: str,
    base_prefab_file_id: int,
    parts: List[dict],
    step_delay: float = 0.05,
) -> Tuple[str, int]:
    """Создать префаб-пустышку с компонентом ReadyBuildSpawner.

    Возвращает (guid ассета, fileID корневого GameObject) — эти значения
    нужны ShopItem'у в поле `spawn`.
    """
    go_id = make_file_id(f"{build_name}:go")
    tr_id = make_file_id(f"{build_name}:transform")
    mb_id = make_file_id(f"{build_name}:spawner")

    parts_yaml = ""
    for p in parts:
        euler = quat_to_euler(p["rot"])
        parts_yaml += (
            f"  - prefab: {{fileID: {p['root_file_id']}, guid: {p['guid']}, type: 3}}\n"
            f"    localPosition: {{x: {_f(p['pos'][0])}, y: {_f(p['pos'][1])}, z: {_f(p['pos'][2])}}}\n"
            f"    localEuler: {{x: {_f(euler[0])}, y: {_f(euler[1])}, z: {_f(euler[2])}}}\n"
            f"    order: {p['order']}\n"
        )
    if not parts_yaml:
        parts_yaml = "  []\n"

    text = (
        YAML_HEADER
        + f"--- !u!1 &{go_id}\n"
        "GameObject:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        "  serializedVersion: 6\n"
        "  m_Component:\n"
        f"  - component: {{fileID: {tr_id}}}\n"
        f"  - component: {{fileID: {mb_id}}}\n"
        "  m_Layer: 0\n"
        f"  m_Name: {build_name}\n"
        "  m_TagString: Untagged\n"
        "  m_Icon: {fileID: 0}\n"
        "  m_NavMeshLayer: 0\n"
        "  m_StaticEditorFlags: 0\n"
        "  m_IsActive: 1\n"
        f"--- !u!4 &{tr_id}\n"
        "Transform:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        f"  m_GameObject: {{fileID: {go_id}}}\n"
        "  serializedVersion: 2\n"
        "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n"
        "  m_LocalPosition: {x: 0, y: 0, z: 0}\n"
        "  m_LocalScale: {x: 1, y: 1, z: 1}\n"
        "  m_ConstrainProportionsScale: 0\n"
        "  m_Children: []\n"
        "  m_Father: {fileID: 0}\n"
        "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n"
        f"--- !u!114 &{mb_id}\n"
        "MonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        f"  m_GameObject: {{fileID: {go_id}}}\n"
        "  m_Enabled: 1\n"
        "  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {SPAWNER_SCRIPT_GUID}, type: 3}}\n"
        "  m_Name:\x20\n"
        "  m_EditorClassIdentifier:\x20\n"
        f"  basePrefab: {{fileID: {base_prefab_file_id}, guid: {base_prefab_guid}, type: 3}}\n"
        "  parts:\n"
        + parts_yaml
        + f"  stepDelay: {_f(step_delay)}\n"
        "  destroyAfterBuild: 1\n"
    )

    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as fh:
        fh.write(text)
    guid = read_meta_guid(path) or make_guid("prefab:" + build_name)
    write_meta(path, guid, kind="prefab")
    return guid, go_id


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def _find_root_game_object(text: str) -> Optional[str]:
    """fileID GameObject'а, чей Transform не имеет родителя (m_Father = 0)."""
    for doc in re.split(r"^--- ", text, flags=re.M)[1:]:
        header = doc.split("\n", 2)[1].strip()
        if header not in ("Transform:", "RectTransform:"):
            continue
        father = re.search(r"m_Father: \{fileID: (\d+)\}", doc)
        if not father or father.group(1) != "0":
            continue
        go = re.search(r"m_GameObject: \{fileID: (\d+)\}", doc)
        if go:
            return go.group(1)
    return None


def write_crate_prefab(
    path: str,
    crate_name: str,
    template_prefab: str,
    content_guid: str,
    content_file_id: int,
) -> Tuple[str, int]:
    """Создать ящик доставки для готовой сборки на основе ящика-образца.

    Обычные крупные товары приезжают не «голым» предметом, а в деревянном
    ящике: ShopItem.spawn ссылается на Crate_*, внутри которого компонент Box
    хранит ссылку на настоящее содержимое, а Crate вскрывается молотком.
    Готовые сборки должны вести себя так же, иначе ПК падает из портала
    и разбивается.

    Ящик — сложный префаб с мешами, коллайдерами и звуком, поэтому он не
    пишется с нуля: берётся ящик соответствующего корпуса, у него меняются
    имя, spawnId и ссылка Box.prefab, а все fileID пересчитываются
    детерминированно, чтобы два ящика не делили идентификаторы объектов.

    Возвращает (guid ассета, fileID корневого GameObject).
    """
    with open(template_prefab, encoding="utf-8") as fh:
        text = fh.read()

    # Корневой GameObject — тот, чей Transform не имеет родителя. Брать
    # «первый GameObject в файле» нельзя: Unity пишет документы в произвольном
    # порядке, и у части ящиков первым идёт обломок BrokenCrate.
    template_root = _find_root_game_object(text)
    if template_root is None:
        raise ValueError(f"{template_prefab}: не найден корневой GameObject")

    # Все локальные fileID документа → новые, уникальные для этого ящика.
    old_ids = sorted(set(re.findall(r"^--- !u!\d+ &(\d+)", text, flags=re.M)))
    mapping = {
        old: str(make_file_id(f"{crate_name}:obj:{old}")) for old in old_ids
    }

    def _swap(match: "re.Match[str]") -> str:
        old = match.group(1)
        return "fileID: " + mapping.get(old, old)

    # Ссылки внутри документа: только «голые» fileID без guid — объекты с
    # guid принадлежат другим ассетам и трогать их нельзя.
    text = re.sub(r"fileID: (\d+)(?!\s*,\s*guid)", _swap, text)
    text = re.sub(
        r"^--- !u!(\d+) &(\d+)",
        lambda m2: f"--- !u!{m2.group(1)} &{mapping[m2.group(2)]}",
        text,
        flags=re.M,
    )

    # Имя ящика и его spawnId: SaveManager грузит предметы по
    # Resources.Load($"Components/{spawnId}"), поэтому они обязаны совпадать
    # с именем файла префаба.
    text = re.sub(
        r"^(\s*)m_Name: Crate_.*$",
        lambda m3: f"{m3.group(1)}m_Name: {crate_name}",
        text,
        count=1,
        flags=re.M,
    )
    text = re.sub(
        r"^(\s*)spawnId: Crate_.*$",
        lambda m4: f"{m4.group(1)}spawnId: {crate_name}",
        text,
        count=1,
        flags=re.M,
    )

    # Содержимое ящика: Box.prefab указывает на спавнер готовой сборки.
    box_marker = f"m_Script: {{fileID: 11500000, guid: {SCRIPT_GUIDS['Box']}, type: 3}}"
    idx = text.find(box_marker)
    if idx == -1:
        raise ValueError(f"{template_prefab}: не найден компонент Box")
    head, tail = text[:idx], text[idx:]
    tail = re.sub(
        r"prefab: \{fileID: \d+, guid: \w+, type: \d+\}",
        f"prefab: {{fileID: {content_file_id}, guid: {content_guid}, type: 3}}",
        tail,
        count=1,
    )
    text = head + tail

    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as fh:
        fh.write(text)

    guid = read_meta_guid(path) or make_guid("prefab:" + crate_name)
    write_meta(path, guid, kind="prefab")

    root_id = int(mapping[template_root]) if template_root else 0
    return guid, root_id


def _cli(argv: List[str]) -> int:
    if len(argv) < 2:
        print(__doc__)
        return 1
    cmd = argv[1]

    if cmd == "guid":
        print(make_guid(argv[2]))
        return 0

    if cmd == "show-slots":
        doc = UnityDoc.load(argv[2])
        from collections import Counter
        counts = Counter(f"{t} (matches={m or '*'})" for t, m, _ in doc.slots())
        root = doc.root_game_object()
        print(f"{argv[2]}")
        print(f"  root GameObject: {root.name if root else '?'} "
              f"(fileID {root.file_id if root else '?'})")
        print(f"  guid: {read_meta_guid(argv[2])}")
        for key, n in sorted(counts.items()):
            print(f"  {n:3d} x {key}")
        return 0

    if cmd == "list-shop":
        for page, items in shop_pages(argv[2]):
            print(f"{page}: {len(items)} шт.")
        return 0

    if cmd == "show":
        doc = UnityDoc.load(argv[2])
        for o in doc.objects:
            cname = CLASS_NAMES.get(o.class_id, str(o.class_id))
            print(f"&{o.file_id:<22} {cname:<16} {o.name or ''}")
        return 0

    print(f"Неизвестная команда: {cmd}")
    print(__doc__)
    return 1


if __name__ == "__main__":
    raise SystemExit(_cli(sys.argv))
