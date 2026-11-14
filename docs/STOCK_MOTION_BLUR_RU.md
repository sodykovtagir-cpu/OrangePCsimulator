# Motion Blur: штатный Unity Post-processing 3.4.0

## Что теперь используется

`GraphicsBootstrap.SetupCamera` передаёт `PP_MotionBlur` в `StockMotionBlur`.
Тот подключает **настоящий PostProcessLayer и MotionBlur из пакета**, уже
зафиксированного в `Packages/manifest.json` / `packages-lock.json` (3.4.0).
Код фильтра Unity не переписан и не скопирован в игровой шейдер.

Настройки профиля:

- `shutterAngle = 270°` в Inspector означает силу при **опорных 30 FPS**;
- выдержка нормализуется по unscaledDeltaTime, а не укорачивается с ростом FPS;
- `sampleCount = 16` на ПК, мобильный бюджет — не более 8;
- штатные native motion vectors + depth, stock velocity/tile/neighbor/reconstruction;
- без включения TAA, FXAA, SMAA и дополнительного deferred fog.

После уточнения «при 30 FPS видно, при 180 — нет даже на 360°» масштаб
выдержки изменён: теперь сохраняется тот же смаз при одинаковой скорости,
а не одна и та же доля всё более короткого кадра. Штатный фильтр не заменялся.

Формула effectiveAngle = Clamp(inspectorAngle, 0, 360) / (30 * dt).
При Inspector=270 получается 270° на 30 FPS, 540° на 60 FPS, 1620° на 180 FPS.
Во всех случаях это выдержка 25 ms; Inspector=180 соответствует 16.67 ms.
Это художественный FPS-independent blur, не физический затвор в пределах
одного кадра. Нижняя граница dt=1 ms ограничивает аномальный масштаб;
кадры длиннее 100 ms обрабатываются прежним camera-cut reset.

В PP 3.4.0 [Range(0,360)] — только Inspector attribute: FloatParameter.Override /
Interp не обрезает значение, MotionBlurRenderer вычисляет velocityScale=angle/360
без Clamp. Поэтому передаём effective angle **свыше 360**, не меняя пакет.
Обрезка результата до 360 снова убрала бы смаз на высоком FPS.
Предел длины вектора и реконструкция остаются штатными. По-прежнему нет
условного коэффициента скорости ходьбы и накопления старых цветовых кадров.

Поворот камеры не зависит от нажатия клавиш ходьбы: векторы формирует Unity.
Перемещение камеры проецируется с учётом реального положения поверхности,
а не константы `localPosition * 0.14`.

Из `SimpleScreenFx.cs` и `SimpleScreenFx.shader` полностью удалены старые
поля/ветки blur, приблизительная камера, сглаживание velocity и camera/native selection.
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

- 63 assertions над реальным интеграционным C# с lifecycle/API doubles:
  включение/выключение, настройки 270°/16 и live Inspector controls, 180 FPS, поворот без ходьбы,
  пауза, cuts/FOV/resize, thumbnails, несколько камер, cleanup и fallback отказ.
- 14 проверок реального MovePlayer с doubles на 30–240 FPS, включая 180.
- Все 4 оставшихся прохода SimpleScreenFx компилируются/валидируются.
- Все 6 **исходных stock shader fragments** 3.4.0 компилируются/валидируются
  через glslang/SPIR-V и исполняются на Mesa/EGL с platform shim.
- 71 GPU assertions: сначала воспроизводится старое поведение — при одной
  скорости на 30 FPS смаз есть, на 180 FPS даже при 360° его нет. Затем
  проверяется исправление на 30/60/90/120/144/180/240 FPS: одинаковый
  радиус и изображения, yaw и pitch, неподвижная резкость, разные глубины,
  ближний смаз без размытия далёких поверхностей.
- GPU-тест получает effective angles из **скомпилированного реального C#
  OnPreCull**, а не из копии формулы на Python. Проверки обычной физической
  выдержки stock shader оставлены как характеристика исходной реализации.
- GUID каждой ссылки Resources asset найден в meta точной версии пакета.
- Дополнительно пройдены сохранения/импорт/перезапуск, асинхронное меню,
  screenshot render-state, Android grid compile guards и boot/grid regressions.

GPU-тест подаёт **синтетические** native vectors и linear01 depth. Он проверяет
именно stock-фильтр, но не генерацию векторов игровыми материалами/камерой Unity.
C# doubles не заменяют компиляцию Unity, работу command buffers или callbacks.
Unity Editor, Play Mode и устройство пользователя здесь не запускались.

## Обязательная визуальная проверка сборки

1. На 30, 60 и 180 FPS выключить bloom/AO/зерно/виньетку, включить только Motion Blur.
2. Стоя на месте, сделать быстрые повороты yaw и pitch: смаз должен следовать
   изображению независимо от ввода ходьбы. При одинаковой скорости и
   разрешении сравнить силу на 30 и 180 FPS: она не должна пропадать на 180.
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

Просто увеличить угол с 180° до 270° оказалось недостаточно: при 180 FPS
даже 360° — лишь 5.56 ms, что короче 270° при 30 FPS (25 ms).
Теперь пользовательский угол — сила на опорных 30 FPS, а runtime angle
пересчитывается автоматически. Ранний совет ограничивать runtime angle
значением 360 отменён; ограничен только пользовательский регулятор.

В Play Mode выберите **игровую Camera** → компонент **Stock Motion Blur**:

- `Shutter Angle`: живой регулятор 0–360° **на опорных 30 FPS**, исходно 270°;
- `Sample Count`: 4–32, исходно 16 (качество, а не сила);
- для A/B сравнения поверните камеру с 0°, затем с 270°/360°;
- меню компонента → `Motion Blur / Print active settings` выводит параметры
  **эффективного** слоя, reference/effective angles, выдержку в ms, frame time
  и depth flags в Console. При 180 FPS effective angle >360 — ожидаемо.
  Это не readback GPU.

Inspector-настройки применяются перед рендером каждой камеры. Изменения только
в Play Mode обычно не сохраняются после его остановки; дефолт 270° задан в коде.
Если при нормализованном режиме, 0° и 360° на быстром повороте нет разницы,
нужен захват native motion vectors / Frame Debugger
в Unity, потому что здешний headless тест не подтверждает их генерацию сценой.


## После лога с Redmi Note 11 Pro

Добавлен однократный автоматический отчёт `[StockMotionBlur]` после 120
рендеров подключённой камеры: effective settings, activeFrames и suppressedFrames.
Последние считают пропуски из-за pause/cut/resize/длинных кадров; это не GPU-readback.
На телефонах sampleCount ограничен 8 (ПК сохраняет заданное значение),
SimpleScreenFx больше не создаёт промежуточную полноэкранную текстуру только
ради копирования результата. HDR включается по bloom, а не по одному факту
включения blur/виньетки/зерна/AO. При включённом bloom HDR остаётся.
См. также [исправления разрешения и Android picker](ANDROID_RESOLUTION_PICKER_RU.md).
