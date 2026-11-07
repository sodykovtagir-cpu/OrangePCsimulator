# Как настроить дакимакуру (модель одной кнопкой)

Короткий ответ: **ручная настройка больше не нужна.** Закинул FBX в
`Assets/Models/`, нажал **Tools → Dakimakura → Застроить дакимакуру** —
модель в префабе, коллайдер под model, проверка прошла. Ничего в Inspector
трогать не требуется.

## Что есть что

| Файл | Зачем |
|---|---|
| `Assets/Models/dakimakura.fbx` | 3D-модель (FBX из Blender/MakeHuman/...). |
| `Assets/Resources/components/Dakimakura.prefab` | Сам предмет: Rigidbody, BoxCollider, `DakimakuraVisual`, `TextureLoader`. |
| `Assets/GameObject/DakimakuraExpert.prefab` | Витрина: `PrintExpert` с `bannerPrefab → Dakimakura.prefab`. |
| `DakimakuraVisual.cs` | Кладет модель в предмет, нормирует размер до `lengthMetres`, подгоняет коллайдер, bindит текстуру. |
| `PrintExpert.cs` | Превью в витрине, покупка, HD-печать (1024×2240 за +500$). |

## Полная настройка — три шага

1. Закинуть модель в `Assets/Models/dakimakura.fbx` (или выбрать свою FBX в
   Project и заменить путь ниже).
2. **Tools → Dakimakura → Застроить дакимакуру.**
3. Проверить: **Tools → Dakimakura → Проверить дакимакуру** — должно быть
   `[ OK ]` по всем строкам.

Готово. Запускаешь игру — модель на месте, витрина работает.

## Что делает «Застроить» автоматически

Раньше каждое из этого делалось руками в Inspector / в уме:

| Было | Теперь |
|---|---|
| Назначать `modelPrefab` (FBX) на префабе предмета в Inspector | Проставляет сам (рефлексом, без кликов) |
| Считать, подходит ли коллайдер предмета | Подгоняет BoxCollider под размер модели (та же формула, что в игре) |
| Дожидаться импорта нового FBX | Перекачивает и переимпортирует FBX сам |
| Модеться на size `lengthMetres` | Берёт `lengthMetres` из префаба предмета (дефолт 1.5) |
| Проверять, что из FBX не завезлись камера/свет | «Проверить дакимакуру» ругается, если `importCameras/importLights ≠ 0` |

## Как подключить текстуру (HD-печать)

Ничего доп не настраивать: в витрине `DakimakuraExpert` выбираешь `.pic`,
ставишь галочку HD (+500$), покупаешь — `TextureLoader` красит материал
темной, что вы selected. Превью вращается сам (previewRigPrefab + `previewLayer`).

## Как подменить модель (новый waifu)

1. Заменить `Assets/Models/dakimakura.fbx` (или выбрать другой FBX в Project).
2. **Tools → Dakimakura → Застроить дакимакуру.**
3. **Проверить дакимакуру** — готово.

## Если модель из Blender

Сначала почитать `FBX_IMPORT_RU.md`: `Apply All Transforms`, Scale 1.00,
`-Z Forward / Y Up`, без камеры и света в экспорте. Импортёр в Unity
теперь всё подхватит автоматически.

## Если «модель не приехала»

1. Запустить **Проверить дакимакуру** — строка `[FAIL]` говорит, что именно.
2. `modelPrefab` пусто → нажать **Застроить дакимакуру**.
3. В консоли ошибка «Dakimakura FBX is not assigned» → тоже Застроить.
4. Модель без меша → проверь FBX: должен быть MeshFilter + Renderer.

## FAQ

- **Модель видна в витрине, но в игре нет** — проверьте `previewRigPrefab`
  и `previewLayer` в `DakimakuraExpert.prefab` (PrintExpert).
- **Модель слишком большая/маленькая** — `lengthMetres` в Inspector
  `Dakimakura.prefab → DakimakuraVisual` (дефолт 1.5). Правишь — и снова
  Застроить.
- **Модель лежит/стоит боком** — поверни корень FBX в Blender (или в FBX
  пакете), перезакинь файл, Застроить.
- **Какой размер предмета?** BoxCollider предмета считается под модель
  автоматически — сколько модель, столько и предмет.
