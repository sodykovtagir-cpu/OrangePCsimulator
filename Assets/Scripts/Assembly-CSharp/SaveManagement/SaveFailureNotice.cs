using UnityEngine;

namespace SaveManagement
{
    /// <summary>
    /// Red banner across the top of the screen for a save that did not reach the
    /// disk.
    /// </summary>
    /// <remarks>
    /// A background write fails outside of any UI flow, and a Debug.LogError is
    /// invisible to the player: the world would silently keep running on an
    /// older file and every later "saved" line would be a lie. IMGUI draws above
    /// every canvas, so the warning is seen even with the pause menu open, and
    /// the object survives a scene change.
    /// </remarks>
    public sealed class SaveFailureNotice : MonoBehaviour
    {
        const float Seconds = 10f;

        static SaveFailureNotice instance;

        string message;
        float hideAt;

        public static void Show(string message)
        {
            message = string.IsNullOrEmpty(message)
                ? Localization.GetText("Could not save the file. Check free disk space.")
                : message;
            if (!Application.isPlaying)
            {
                // Editor-side writes have no screen to warn on.
                Debug.LogWarning("[Save] " + message);
                return;
            }

            if (instance == null)
            {
                var host = new GameObject("Save Failure Notice");
                DontDestroyOnLoad(host);
                instance = host.AddComponent<SaveFailureNotice>();
            }
            instance.message = message;
            instance.hideAt = Time.unscaledTime + Seconds;
        }

        private void Update()
        {
            if (Time.unscaledTime < hideAt) return;
            instance = null;
            Destroy(gameObject);
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(message)) return;

            float width = Mathf.Min(Screen.width * .8f, 900f);
            var area = new Rect((Screen.width - width) * .5f, Screen.height * .06f,
                width, Mathf.Max(48f, Screen.height * .1f));
            var style = new GUIStyle(GUI.skin.box)
            {
                fontSize = Mathf.Clamp(Screen.height / 42, 14, 30),
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            style.normal.textColor = Color.white;

            var previous = GUI.backgroundColor;
            GUI.backgroundColor = new Color(.65f, .05f, .05f, .95f);
            GUI.Box(area, message, style);
            GUI.backgroundColor = previous;
        }
    }
}
