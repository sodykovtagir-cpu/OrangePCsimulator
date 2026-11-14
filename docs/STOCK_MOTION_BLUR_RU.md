# Motion Blur: штатный Unity Post-processing 3.4.0

## Что теперь используется

`GraphicsBootstrap.SetupCamera` передаёт `PP_MotionBlur` в `StockMotionBlur`.
Тот подключает **настоящий PostProcessLayer и MotionBlur из пакета**, уже
зафиксированного в `Packages/manifest.json` / `packages-lock.json` (3.4.0).
Код фильтра Unity не переписан и не скопирован в игровой шейдер.

Настройки профиля:

- `shutterAngle = 270°`: три четверти текущего кадра, а не постоянная 1/40 секунды;
- `sampleCount = 16`;
- штатные native motion vectors + depth, stock velocity/tile/neighbor/reconstruction;
- без включения TAA, FXAA, SMAA и дополнительного deferred fog.

При 180 FPS выдержка соответствует примерно 1/240 секунды, а не 1/40.
Поэтому медленное движение на высоком FPS намеренно остаётся почти резким.
Мы больше не пытаемся сделать одинаковую силу смаза на 60 и 240 FPS.
Поворот камеры не зависит от нажатия клавиш ходьбы: векторы формирует Unity.
Перемещение камеры проецируется с учётом реального положения поверхности,
а не константы `localPosition * 0.14`.

Из `SimpleScreenFx.cs` и `SimpleScreenFx.shader` полностью удалены старые
поля/ветки blur, fixed exposure, сглаживание velocity и camera/native selection.
Остальные эффекты SimpleScreenFx не менялись. При включении только Motion Blur
SimpleScreenFx выключен: второго слоя размытия нет. Штатный слой сохраняет
обычную цепочку image effects (`finalBlitToCameraTarget = false`).

## Жизненный цикл и ограничения

`StockMotionBlur.OnPreCull` выполняется с execution order -10000 до штатного
слоя. Глобальный runtime volume на свободном слое 31 содержит только MotionBlur.
Все управляемые камеры используют один профиль, параметры enabled задаются
непосредственно перед сборкой команд текущей камеры. В проекте нет авторских
PostProcessLayer/Volume на сценах/префабах, их настройки не мигрируются.
Runtime профиль и volume освобождаются после уничтожения последней
подключённой камеры и создаются заново при необходимости.

Фильтр сбрасывается и подавляется при паузе, camera cut/телепорте,
смене FOV/разрешения, пропуске кадров и thumbnail Camera.Render в RT.
Дополнительно первый активный проход после reset может быть пропущен самим
stock renderer. Мониторным камерам с targetTexture эффект не назначается.
При выключении настройки отключается PostProcessLayer, а не только сила эффекта.
Когда выключены все остальные эффекты, SimpleScreenFx тоже отключается.

Если GPU не поддерживает motion vectors / RGHalf или shader недоступен,
эффект не заменяется искусственным смазом: один Warning сообщает причину.
Это особенно важно проверить на целевом Android/GLES/Vulkan устройстве.

## Ресурсы для сборки

`Assets/Resources/OrangePCPostProcessResources.asset` — копия ссылочного
PostProcessResources.asset **из точной версии пакета 3.4.0**, изменено только имя.
Он нужен для Resources.Load / layer.Init при программном создании слоя и
прямых build-time ссылок на shaders/textures. Изображения и шейдеры остаются
в пакете. Лицензия: `docs/third-party/UNITY_POSTPROCESSING_LICENSE.md`.
При смене версии пакета необходимо повторно проверить resource GUIDs.

## Выполненные проверки вне Unity

```bash
python tools/test_motion_blur_stability.py
python tools/test_motion_blur_visibility_gpu.py --package /path/to/extracted/package
```

- 60 assertions над реальным интеграционным C# с lifecycle/API doubles:
  включение/выключение, настройки 270°/16 и live Inspector controls, 180 FPS, поворот без ходьбы,
  пауза, cuts/FOV/resize, thumbnails, несколько камер, cleanup и fallback отказ.
- 14 проверок реального MovePlayer с doubles на 30–240 FPS, включая 180.
- Все 4 оставшихся прохода SimpleScreenFx компилируются/валидируются.
- Все 6 **исходных stock shader fragments** 3.4.0 компилируются/валидируются
  через glslang/SPIR-V и исполняются на Mesa/EGL с platform shim.
- 18 GPU assertions: нулевые векторы, yaw-/pitch-like векторы, ограничение
  цвета, правильная половинная выдержка при 60/120/144/180/240 FPS,
  разные глубины, сильный near blur и резкие дальние поверхности.
- GUID каждой ссылки Resources asset найден в meta точной версии пакета.
- Дополнительно пройдены сохранения/импорт/перезапуск, асинхронное меню,
  screenshot render-state, Android grid compile guards и boot/grid regressions.

GPU-тест подаёт **синтетические** native vectors и linear01 depth. Он проверяет
именно stock-фильтр, но не генерацию векторов игровыми материалами/камерой Unity.
C# doubles не заменяют компиляцию Unity, работу command buffers или callbacks.
Unity Editor, Play Mode и устройство пользователя здесь не запускались.

## Обязательная визуальная проверка сборки

1. На 60 и 180 FPS выключить bloom/AO/зерно/виньетку, включить только Motion Blur.
2. Стоя на месте, сделать быстрые повороты yaw и pitch: смаз должен следовать
   изображению независимо от ввода ходьбы; медленный поворот при 180 FPS может
   быть практически резким при такой выдержке — это ожидаемо.
3. Идти вперёд и боком рядом с предметами, затем смотреть вдаль: дальние
   поверхности не должны одинаково замыливаться вместе с ближними.
4. Остановить движение и мышь: кадр должен стать резким без старого шлейфа.
5. Выключить Motion Blur: PostProcessLayer должен быть disabled, прочие эффекты
   должны продолжать работать по своим toggle.
6. Пауза, сохранение со скриншотом, загрузка/телепорт и смена разрешения: нет
   длинного смаза между несвязанными ракурсами. Проверить экраны мониторов.
7. Повторить в целевом Android Player; проверить Console / Player.log на
   `[StockMotionBlur]`, shader stripping и ошибки PostProcessLayer.

Источники: документация пакета Motion Blur и исходники, использованные для
сверки, плюс скачанный из Unity registry архив com.unity.postprocessing 3.4.0:

- https://docs.unity3d.com/Packages/com.unity.postprocessing@3.4/manual/Motion-Blur.html
- https://github.com/Unity-Technologies/PostProcessing/blob/v2/PostProcessing/Runtime/Effects/MotionBlur.cs
- https://packages.unity.com/com.unity.postprocessing/-/com.unity.postprocessing-3.4.0.tgz


## Настройка заметности в Unity Editor

После отзыва «вроде что-то есть» стандартный пресет поднят с 180° до 270°,
без превышения штатного диапазона 0–360° и без множителя FPS. GPU fixture
с native смещением 6 px показывает, почему небольшое изменение может быть
заметным: при 180° radius=1.5 px и штатный фильтр возвращает исходный цвет,
при 270° radius=2.25 px и реконструкция уже применяется. Это искусственный
пример порога stock shader, не измерение векторов игровой сцены пользователя.

В Play Mode выберите **игровую Camera** → компонент **Stock Motion Blur**:

- `Shutter Angle`: живой регулятор 0–360°, исходно 270°;
- `Sample Count`: 4–32, исходно 16 (качество, а не сила);
- для A/B сравнения поверните камеру с 0°, затем с 270°/360°;
- меню компонента → `Motion Blur / Print active settings` выводит параметры
  **эффективного** слоя, frame time и depth flags в Console. Это не readback GPU.

Inspector-настройки применяются перед рендером каждой камеры. Изменения только
в Play Mode обычно не сохраняются после его остановки; дефолт 270° задан в коде.
Если при 0° и 360° на быстром повороте нет заметной разницы, не увеличивать
выдержку сверх диапазона: нужен захват native motion vectors / Frame Debugger
в Unity, потому что здешний headless тест не подтверждает их генерацию сценой.
