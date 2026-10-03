"""Renders a PNG preview of a Unity UI prefab directly from its YAML.

Offline helper: it walks the RectTransform hierarchy of the prefab and draws
the Images and Texts with PIL, so the layout can be reviewed without opening
the Unity editor. It is a development tool, never shipped in a build.

Usage: python3 tools/preview_prefab_ui.py <prefab> <RootGameObjectName> <out.png>
"""

import re
import sys

from PIL import Image, ImageDraw, ImageFont


def parse(path):
    text = open(path, encoding="utf-8").read()
    parts = re.split(r"^--- !u!(\d+) &(\d+)(?: stripped)?\n", text, flags=re.M)
    blocks = {}
    for i in range(1, len(parts) - 2, 3):
        blocks[parts[i + 1]] = (int(parts[i]), parts[i + 2])
    return blocks


SCRIPT_NAMES = {
    "fe87c0e1cc204ed48ad3b37840f39efc": "Image",
    "5f7201a12d95ffc409449d95f23cf332": "Text",
    "4e29b1a8efbd4b44bb3f3716e73f07ff": "Button",
    "67db9e8f0e2ae9c40bc1e2b64352a6b4": "Slider",
    "1344c3c82d62a2a41a3576d8abb8e3ea": "RawImage",
}


class Prefab:
    def __init__(self, path):
        self.blocks = parse(path)
        self.names = {}
        self.active = {}
        for fid, (cid, body) in self.blocks.items():
            if cid != 1:
                continue
            match = re.search(r"^  m_Name: (.*)$", body, flags=re.M)
            self.names[fid] = match.group(1).strip() if match else "?"
            act = re.search(r"^  m_IsActive: (\d)", body, flags=re.M)
            self.active[fid] = act.group(1) == "1" if act else True

    def game_object(self, name):
        for fid, value in self.names.items():
            if value == name:
                return fid
        raise KeyError(name)

    def rect_of(self, go_id):
        body = self.blocks[go_id][1]
        return re.search(r"^  - component: \{fileID: (\d+)\}$", body, flags=re.M).group(1)

    def components(self, go_id):
        body = self.blocks[go_id][1]
        return re.findall(r"^  - component: \{fileID: (\d+)\}$", body, flags=re.M)

    def rect_data(self, tr_id):
        body = self.blocks[tr_id][1]
        amin = tuple(float(v) for v in re.search(r"^  m_AnchorMin: \{x: ([\d.]+), y: ([\d.]+)\}$", body, flags=re.M).groups())
        amax = tuple(float(v) for v in re.search(r"^  m_AnchorMax: \{x: ([\d.]+), y: ([\d.]+)\}$", body, flags=re.M).groups())
        pos = tuple(float(v) for v in re.search(r"^  m_AnchoredPosition: \{x: ([\-\d.]+), y: ([\-\d.]+)\}$", body, flags=re.M).groups())
        size = tuple(float(v) for v in re.search(r"^  m_SizeDelta: \{x: ([\-\d.]+), y: ([\-\d.]+)\}$", body, flags=re.M).groups())
        pivot = tuple(float(v) for v in re.search(r"^  m_Pivot: \{x: ([\d.]+), y: ([\d.]+)\}$", body, flags=re.M).groups())
        father = re.search(r"^  m_Father: \{fileID: (\d+)\}$", body, flags=re.M).group(1)
        kids = re.findall(r"^  - \{fileID: (\d+)\}$", body, flags=re.M)
        return amin, amax, pos, size, pivot, father, kids

    def comp_data(self, fid):
        cid, body = self.blocks[fid]
        if cid != 114:
            return None, None
        script = re.search(r"guid: ([0-9a-f]+)", body)
        return SCRIPT_NAMES.get(script.group(1), "MonoBehaviour"), body


def rect_of(prefab, tr_id, parent_rect):
    amin, amax, pos, size, pivot, _, _ = prefab.rect_data(tr_id)
    px, py, pw, ph = parent_rect
    x0 = px + amin[0] * pw
    y0 = py + amin[1] * ph
    x1 = px + amax[0] * pw
    y1 = py + amax[1] * ph
    left = x0 + pos[0] - pivot[0] * size[0] * (1 if amax[0] == amin[0] else 0)
    top = y0 + pos[1] - pivot[1] * size[1] * (1 if amax[1] == amin[1] else 0)
    if amax[0] != amin[0]:
        left = x0 + pos[0]
        width = (x1 - x0) + size[0]
    else:
        width = size[0]
    if amax[1] != amin[1]:
        top = y0 + pos[1]
        height = (y1 - y0) + size[1]
    else:
        height = size[1]
    return (left, top, width, height)


def color_of(body, prefix="m_Color: {r: "):
    match = re.search(re.escape(prefix) + r"([\d.]+), g: ([\d.]+), b: ([\d.]+), a: ([\d.]+)\}", body)
    if not match:
        return (255, 255, 255, 255)
    r, g, b, a = (float(v) for v in match.groups())
    return (int(r * 255), int(g * 255), int(b * 255), int(a * 255))


def render(prefab, tr_id, rect, font, draw_image, draw_text):
    amin, amax, pos, size, pivot, father, kids = prefab.rect_data(tr_id)
    if father != "0" and not parent_visible(prefab, father):
        return
    go_id = re.search(r"^  m_GameObject: \{fileID: (\d+)\}$", prefab.blocks[tr_id][1], flags=re.M).group(1)
    if not prefab.active.get(go_id, True):
        return

    for comp in prefab.components(go_id):
        kind, body = prefab.comp_data(comp)
        if kind == "Image":
            draw_image(rect, body)
        elif kind == "Text":
            content = re.search(r"^  m_Text: (.*)$", body, flags=re.M)
            content = content.group(1).strip() if content else ""
            if content.startswith("'") and content.endswith("'"):
                content = content[1:-1].replace("''", "'")
            size_px = int(re.search(r"    m_FontSize: (\d+)", body).group(1))
            align = int(re.search(r"    m_Alignment: (\d+)", body).group(1))
            draw_text(rect, content, color_of(body), size_px, align, font)

    for kid in kids:
        render(prefab, kid, rect_of(prefab, kid, rect), font, draw_image, draw_text)


def parent_visible(prefab, tr_id):
    body = prefab.blocks[tr_id][1]
    go_id = re.search(r"^  m_GameObject: \{fileID: (\d+)\}$", body, flags=re.M).group(1)
    if not prefab.active.get(go_id, True):
        return False
    father = re.search(r"^  m_Father: \{fileID: (\d+)\}$", body, flags=re.M).group(1)
    if father == "0":
        return True
    return parent_visible(prefab, father)


def main(prefab_path, root_name, out_path, width=850, height=520, panel=None, force_active=True):
    prefab = Prefab(prefab_path)
    if force_active:
        for fid in prefab.active:
            prefab.active[fid] = True
    go_id = prefab.game_object(root_name)
    root_tr = prefab.rect_of(go_id)
    _, _, _, size, _, _, _ = prefab.rect_data(root_tr)

    scale = 2
    img = Image.new("RGBA", (int(size[0]) * scale, int(size[1]) * scale), (26, 26, 32, 255))
    d = ImageDraw.Draw(img, "RGBA")
    try:
        font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 13)
        font_bold = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 13)
    except OSError:
        font = ImageFont.load_default()
        font_bold = font

    window_h = size[1]

    def flip(rect):
        x, y, w, h = rect
        return x, window_h - y - h, w, h

    def draw_image(rect, body):
        x, y, w, h = [v * scale for v in flip(rect)]
        col = color_of(body)
        if col[3] == 0:
            return
        d.rectangle([x, y, x + w, y + h], fill=col)

    def draw_text(rect, content, col, size_px, align, font):
        if not content:
            return
        x, y, w, h = [v * scale for v in flip(rect)]
        fnt = font
        try:
            fnt = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", max(8, int(size_px * scale * 0.72)))
        except OSError:
            pass
        bbox = d.textbbox((0, 0), content, font=fnt)
        tw, th = bbox[2] - bbox[0], bbox[3] - bbox[1]
        if align in (0, 3, 6):
            tx = x + 2
        elif align in (2, 5, 8):
            tx = x + w - tw - 2
        else:
            tx = x + (w - tw) / 2
        ty = y + (h - th) / 2 - bbox[1]
        d.text((tx, ty), content, font=fnt, fill=col)

    if panel:
        panel_go = prefab.game_object(panel)
        panel_tr = prefab.rect_of(panel_go)
        # draw everything, then overlay only the requested panel
        for child in prefab.rect_data(panel_tr)[6]:
            render(prefab, child, rect_of(prefab, child, (0, 0, size[0], size[1])), font, draw_image, draw_text)
    else:
        for child in prefab.rect_data(root_tr)[6]:
            render(prefab, child, rect_of(prefab, child, (0, 0, size[0], size[1])), font, draw_image, draw_text)

    img.save(out_path)
    print("wrote %s (%dx%d)" % (out_path, img.width, img.height))


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2], sys.argv[3],
         panel=sys.argv[4] if len(sys.argv) > 4 else None)
