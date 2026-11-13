using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace PlayNotes
{
    // What a click landed on, in words: the text on the button and where it sits in the scene.
    // "this is wrong" is so often about the thing just pressed, and a picture does not say which object that was.
    //   press "Start" Canvas/Menu/StartButton      a control (uGUI Selectable, UI Toolkit Button)
    //   touch "Orders" Canvas/Board/Title          something else that took the pointer
    //   outside                                    no interface under the pointer
    internal static class ClickLabels
    {
        const int LongestLabel = 24;

        static readonly List<RaycastResult> hits = new List<RaycastResult>();
        static bool lookedForTextMesh;
        static Type textMeshType;
        static PropertyInfo textMeshText;

        internal static string Describe(Vector2 at)
        {
            try
            {
                return FromToolkit(at) ?? FromCanvas(at) ?? "outside";
            }
            catch (Exception)
            {
                // (an interface torn down in the very frame of the click)
                return "outside";
            }
        }

        static string FromCanvas(Vector2 at)
        {
            var events = EventSystem.current;
            if (events == null) return null;
            hits.Clear();
            events.RaycastAll(new PointerEventData(events) { position = at }, hits);
            if (hits.Count == 0) return null;
            var hit = hits[0].gameObject;
            if (hit == null) return null;
            var control = hit.GetComponentInParent<Selectable>();
            var target = control != null ? control.gameObject : hit;
            string words = TextOf(target);
            // (a label is as often the control's neighbour as its child)
            if (words.Length == 0 && target.transform.parent != null) words = TextOf(target.transform.parent.gameObject);
            return (control != null ? "press \"" : "touch \"") + Plain(words) + "\" " + PathOf(target.transform);
        }

        static string TextOf(GameObject go)
        {
            var text = go.GetComponentInChildren<Text>();
            if (text != null && !string.IsNullOrEmpty(text.text)) return text.text;
            // (TextMeshPro by its name, not by a reference: the package may not be in the project)
            if (!lookedForTextMesh)
            {
                lookedForTextMesh = true;
                textMeshType = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro");
                textMeshText = textMeshType?.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
            }
            if (textMeshText == null) return "";
            var mesh = go.GetComponentInChildren(textMeshType);
            return mesh != null ? textMeshText.GetValue(mesh) as string ?? "" : "";
        }

        static string PathOf(Transform t)
        {
            string path = t.name;
            for (int up = 0; up < 2 && t.parent != null; up++)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        static string FromToolkit(Vector2 at)
        {
            var documents = UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            foreach (var document in documents)
            {
                var root = document.rootVisualElement;
                if (root == null || root.panel == null) continue;
                // (a panel counts its rows from the top)
                var point = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(at.x, Screen.height - at.y));
                var hit = root.panel.Pick(point);
                if (hit == null || hit == root) continue;
                var label = hit as TextElement ?? hit.Q<TextElement>();
                string words = label != null ? label.text ?? "" : "";
                bool control = hit is UnityEngine.UIElements.Button || hit.GetFirstAncestorOfType<UnityEngine.UIElements.Button>() != null;
                if (!control) control = hit is BaseBoolField || hit.GetFirstAncestorOfType<BaseBoolField>() != null;
                // (a bare container that fills the screen is the background, not a thing clicked)
                if (!control && words.Length == 0 && string.IsNullOrEmpty(hit.name)) continue;
                return (control ? "press \"" : "touch \"") + Plain(words) + "\" " + PathOf(hit);
            }
            return null;
        }

        static string PathOf(VisualElement e)
        {
            string path = NameOf(e);
            for (int up = 0; up < 2 && e.parent != null; up++)
            {
                e = e.parent;
                path = NameOf(e) + "/" + path;
            }
            return path;
        }

        static string NameOf(VisualElement e) => string.IsNullOrEmpty(e.name) ? e.GetType().Name : e.name;

        // A label as one short line: rich-text tags out, line breaks to spaces, cut at 24 characters.
        internal static string Plain(string words)
        {
            string s = Regex.Replace(words ?? "", "<[^>]*>", "");
            var sb = new StringBuilder(s.Length);
            foreach (char c in s) sb.Append(c == '\n' || c == '\r' || c == '\t' || c == '"' ? ' ' : c);
            s = sb.ToString().Trim();
            return s.Length > LongestLabel ? s.Substring(0, LongestLabel) + "…" : s;
        }
    }
}
