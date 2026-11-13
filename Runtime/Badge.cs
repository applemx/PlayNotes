#if (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN) && !PLAYNOTES_DISABLED
using UnityEngine;

namespace PlayNotes
{
    // A red dot in the top left corner while a session is on, swelling with the voice it hears, and one line of words beside it.
    // Drawn with OnGUI so that it needs no canvas, no font and no camera of the game's, and lies over everything.
    // The component is switched off whenever there is nothing to show: an idle OnGUI still costs a little every frame.
    [AddComponentMenu("")]
    internal sealed class Badge : MonoBehaviour
    {
        static Texture2D disc;

        internal bool Recording;
        internal float Level;
        internal string Line = "";

        GUIStyle style;

        void Awake()
        {
            useGUILayout = false;
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            float scale = Screen.height / 1080f;
            if (Recording)
            {
                float size = 16f * (1f + Mathf.Clamp01(Level * 10f)) * scale;
                var was = GUI.color;
                GUI.color = new Color(.93f, .16f, .12f, 1f);
                GUI.DrawTexture(new Rect(22f * scale - size / 2f, 22f * scale - size / 2f, size, size), Disc());
                GUI.color = was;
            }
            if (Line.Length == 0) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, wordWrap = false, richText = false };
            style.fontSize = Mathf.Max(12, Mathf.RoundToInt(22f * scale));
            var at = new Rect(44f * scale, 4f * scale, Screen.width - 44f * scale, 36f * scale);
            // (dark behind, white over: to be read on any picture)
            style.normal.textColor = new Color(0f, 0f, 0f, .9f);
            float edge = Mathf.Max(1f, 1.5f * scale);
            GUI.Label(new Rect(at.x + edge, at.y + edge, at.width, at.height), Line, style);
            GUI.Label(new Rect(at.x - edge, at.y + edge, at.width, at.height), Line, style);
            GUI.Label(new Rect(at.x + edge, at.y - edge, at.width, at.height), Line, style);
            GUI.Label(new Rect(at.x - edge, at.y - edge, at.width, at.height), Line, style);
            style.normal.textColor = Color.white;
            GUI.Label(at, Line, style);
        }

        static Texture2D Disc()
        {
            if (disc != null) return disc;
            const int N = 64;
            disc = new Texture2D(N, N, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color32[N * N];
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float dx = x + .5f - N / 2f;
                    float dy = y + .5f - N / 2f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    pixels[y * N + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(N / 2f - 1f - d) * 255));
                }
            }
            disc.SetPixels32(pixels);
            disc.Apply(false, true);
            return disc;
        }
    }
}
#endif
