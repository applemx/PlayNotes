using System;
using UnityEngine;

namespace PlayNotes
{
    // A key by its name in the settings ("F8", "Escape", "1"), understood by whichever of Unity's two input systems the project has on.
    internal readonly struct WatchedKey
    {
        /// <summary>The name as it is written into the trail.</summary>
        internal readonly string Name;

        /// <summary>The input system that is on knows this key.</summary>
        internal readonly bool Valid;

#if PLAYNOTES_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
        readonly UnityEngine.InputSystem.Key key;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        readonly KeyCode code;
#endif

        internal WatchedKey(string written)
        {
            Name = Normalize(written);
            bool valid = false;
#if PLAYNOTES_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            key = UnityEngine.InputSystem.Key.None;
            if (IsName(NewName(Name)) && Enum.TryParse(NewName(Name), true, out UnityEngine.InputSystem.Key k) && k != UnityEngine.InputSystem.Key.None)
            {
                key = k;
                valid = true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            code = KeyCode.None;
            if (IsName(OldName(Name)) && Enum.TryParse(OldName(Name), true, out KeyCode c) && c != KeyCode.None)
            {
                code = c;
                valid = true;
            }
#endif
            Valid = valid;
        }

        /// <summary>Went down in this frame.</summary>
        internal bool Pressed()
        {
            if (!Valid) return false;
#if PLAYNOTES_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && key != UnityEngine.InputSystem.Key.None) return keyboard[key].wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (code != KeyCode.None) return Input.GetKeyDown(code);
#endif
            return false;
        }

        /// <summary>The left mouse button went down in this frame; where, in pixels from the bottom left.</summary>
        internal static bool Clicked(out Vector2 at)
        {
            at = default;
#if PLAYNOTES_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
            {
                if (!mouse.leftButton.wasPressedThisFrame) return false;
                at = mouse.position.ReadValue();
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0))
            {
                at = Input.mousePosition;
                return true;
            }
#endif
            return false;
        }

        // One spelling for each key, whatever was typed: "esc" and "Escape", "return" and "Enter", "Alpha1", "Digit1" and "1".
        internal static string Normalize(string written)
        {
            string s = (written ?? "").Trim();
            if (s.Length == 0) return "";
            if (s.Length == 1 && char.IsLetter(s[0])) return s.ToUpperInvariant();
            if (s.Length == 1 && char.IsDigit(s[0])) return s;
            string lower = s.ToLowerInvariant();
            if (lower == "esc" || lower == "escape") return "Escape";
            if (lower == "enter" || lower == "return") return "Enter";
            if (lower == "space" || lower == "spacebar") return "Space";
            if (lower == "tab") return "Tab";
            if ((lower.StartsWith("alpha") || lower.StartsWith("digit")) && lower.Length == 6 && char.IsDigit(lower[5])) return lower.Substring(5);
            if (lower.Length >= 2 && lower.Length <= 3 && lower[0] == 'f' && int.TryParse(lower.Substring(1), out int n) && n >= 1 && n <= 15) return "F" + n;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        // (a name, not a number: Enum.TryParse would take "12" for whichever key happens to be the twelfth)
        static bool IsName(string name) => name.Length > 0 && char.IsLetter(name[0]);

        // (the Input System's names: Digit1, Enter)
        internal static string NewName(string name)
        {
            if (name.Length == 1 && char.IsDigit(name[0])) return "Digit" + name;
            return name;
        }

        // (the old Input Manager's names: Alpha1, Return)
        internal static string OldName(string name)
        {
            if (name.Length == 1 && char.IsDigit(name[0])) return "Alpha" + name;
            if (name == "Enter") return "Return";
            return name;
        }
    }
}
