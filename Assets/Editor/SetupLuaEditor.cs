using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

namespace PC.Component.Software.Tools
{
    public class LuaEditorSetup : ScriptableObject
    {
        [MenuItem("Tools/Setup LuaEditor NotepadInput")]
        public static void SetupLuaEditor()
        {
            // Находим префаб LuaEditor
            var prefab = Resources.Load<GameObject>("apps/LuaEditor");
            if (prefab == null)
            {
                Debug.LogError("LuaEditor.prefab не найден в Resources/apps/");
                return;
            }

            // Находим InputField
            var inputField = prefab.GetComponentInChildren<InputField>();
            if (inputField == null)
            {
                Debug.LogError("InputField не найден в LuaEditor");
                return;
            }

            var go = inputField.gameObject;

            // Проверяем есть ли уже NotepadInput
            if (go.GetComponent<NotepadInput>() != null)
            {
                Debug.Log("NotepadInput уже есть на InputField");
                return;
            }

            // Добавляем ScrollRect если нет
            var scrollRect = go.GetComponent<ScrollRect>();
            if (scrollRect == null)
            {
                scrollRect = go.AddComponent<ScrollRect>();
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.movementType = ScrollRect.MovementType.Elastic;
                scrollRect.inertia = true;
                scrollRect.decelerationRate = 0.135f;
                scrollRect.scrollSensitivity = 1f;
            }

            // Добавляем NotepadInput
            var notepad = go.AddComponent<NotepadInput>();
            notepad.minHeight = 100f;
            notepad.maxHeight = 500f;
            notepad.lineHeight = 16f;
            notepad.padding = 4f;

            // Находим Text
            var text = inputField.textComponent;
            if (text != null)
            {
                notepad.textComponent = text;
                notepad.contentRect = text.rectTransform;

                // Настраиваем Text
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Overflow;
            }

            // Настраиваем InputField
            inputField.lineType = InputField.LineType.MultiLineNewline;
            inputField.textComponent = text;

            // Подключаем ссылки
            notepad.inputField = inputField;
            notepad.scrollRect = scrollRect;

            // Сохраняем префаб
            var prefabPath = AssetDatabase.GetAssetPath(prefab);
            PrefabUtility.SaveAsPrefabAsset(go.transform.root.gameObject, prefabPath);

            Debug.Log("✅ LuaEditor настроен: добавлен NotepadInput + ScrollRect");
            Debug.Log("Параметры: minHeight=100, maxHeight=500, lineHeight=16");
        }
    }
}
