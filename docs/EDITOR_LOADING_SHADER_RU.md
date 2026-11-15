# Hidden/Internal-Loading: invalid pass index 1…5 в Editor

## Симптом и границы вывода

Пользователь сообщил ошибки RenderingCommandBuffer / DrawMesh с шейдером Hidden/Internal-Loading и индексами 1…5 сразу после запуска игры в Unity Editor. О том же сбое в APK/EXE данных нет.

Проект использует Unity 2022.3.62f3, Built-in pipeline (m_CustomRenderPipeline = 0) и официальный com.unity.postprocessing 3.4.0. В EditorSettings была включена m_AsyncShaderCompilation = 1.

Официальный шейдер PP 3.4.0 MotionBlur имеет 6 проходов: velocity setup (0), TileMax (1/2/3), NeighborMax (4), reconstruction (5). Это согласуется с присланной последовательностью индексов, но один лог с Internal-Loading **не доказывает**, что все сообщения исходят именно от него. SimpleScreenFx проекта имеет только 4 прохода и не объясняет индексы 4/5 самостоятельно.

По документации Unity, асинхронная компиляция в Editor временно заменяет ещё не готовый вариант шейдера заглушкой. Для advanced rendering Unity описывает несколько способов отказаться от заглушки, включая отключение async для проекта. Стандартный PP выполняет fullscreen-команды через CommandBuffer/DrawMesh, а не обязательно через обычный Graphics.Blit, для которого есть отдельное исключение в документации.

## Изменение

В `ProjectSettings/EditorSettings.asset`:

```yaml
m_AsyncShaderCompilation: 0
```

Это минимальный проектный Editor-only workaround. Не скопирован/не изменён пакет, не переписан шейдер, не удалены проходы Motion Blur. Не добавлены фильтры Console и подавление ошибок. Версия Unity, graphics API, render pipeline, качество и алгоритм постобработки APK/EXE не меняются.

Компромисс: при первом использовании неготового варианта Editor может ждать компиляцию и временно остановиться. Это **не ускорение Editor**, а отказ от временного loading shader. При большом числе вариантов ожидание может быть заметным. Настройка отключает async-компиляцию в Scene/Game views всего проекта, не только у одного эффекта; отдельные сторонние Editor-инструменты могут управлять своим async scope самостоятельно.

## Проверка у пользователя

1. Остановить Play Mode и получить обновление проекта.
2. Проверить **Edit → Project Settings → Editor → Shader Compilation → Asynchronous Shader Compilation**: флажок должен быть выключен.
3. Дождаться завершения импорта/компиляции. Если открытый Editor не подхватил обновлённый asset, закрыть и снова открыть проект.
4. Очистить старые сообщения Console кнопкой Clear и снова запустить игру. Проверить игру с включённым Motion Blur и поворотом камеры, а не только пустую сцену.
5. Если новые Internal-Loading ошибки остаются с выключенным флажком, нужен полный свежий Editor.log и первые shader compiler errors, если они есть. Тогда требуется найти конкретный CommandBuffer/материал в Frame Debugger: нельзя объявлять это исправленным только по проверке YAML.

Не требуется удалять всю Library, менять на URP или отключать сам Motion Blur. Документация/issue tracker содержат сходные сообщения и для URP, и для PostProcessing; это не основание устанавливать URP в этот Built-in проект.

## Выполненные проверки

- `tools/test_editor_shader_compilation.py`: статический guard сначала отклонял включённый async, после изменения проходит; дополнительно проверяет прежние PP 3.4.0, Built-in и интеграцию StockMotionBlur. Это не воспроизведение rendering error.
- `tools/test_stock_motion_blur.py`: реальные C#-методы с API/lifecycle-дублями (не Unity-рендеринг).
- Исполняемого Unity Editor в среде нет; фактическое исчезновение ошибки и задержка первой компиляции **не измерены**.

## Источники

- Unity 2022.3 Manual, Asynchronous shader compilation: https://docs.unity3d.com/2022.3/Documentation/Manual/AsynchronousShaderCompilation.html
- Unity 2022.3 ShaderUtil.CompilePass (доступная более узкая альтернатива для управляемых материалов, здесь не использована): https://docs.unity3d.com/2022.3/Documentation/ScriptReference/ShaderUtil.CompilePass.html
- Точное содержимое официального пакета 3.4.0: https://packages.unity.com/com.unity.postprocessing/-/com.unity.postprocessing-3.4.0.tgz
- Сходная сигнатура в PostProcessing issue PPB-52 (не доказательство конкретной причины этого проекта): https://issuetracker.unity3d.com/issues/motion-blur-affects-a-terrain-when-its-transform-position-is-not-zero-and-is-stationary
