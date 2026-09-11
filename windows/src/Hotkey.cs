// Hotkey strings.
//
// Settings stores a hotkey the way a person would write it, "Ctrl+Shift+V", so
// the file stays readable and portable. This turns that into the modifier mask
// and virtual key RegisterHotKey wants, and back again for display.

using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed class Hotkey
    {
        public uint Mods;
        public uint Vk;
        public string Text = "";

        private static readonly Dictionary<string, Keys> Aliases = BuildAliases();

        private static Dictionary<string, Keys> BuildAliases()
        {
            Dictionary<string, Keys> d = new Dictionary<string, Keys>(StringComparer.OrdinalIgnoreCase);
            d["esc"] = Keys.Escape;
            d["escape"] = Keys.Escape;
            d["enter"] = Keys.Return;
            d["return"] = Keys.Return;
            d["space"] = Keys.Space;
            d["spacebar"] = Keys.Space;
            d["ins"] = Keys.Insert;
            d["insert"] = Keys.Insert;
            d["del"] = Keys.Delete;
            d["delete"] = Keys.Delete;
            d["pgup"] = Keys.PageUp;
            d["pageup"] = Keys.PageUp;
            d["pgdn"] = Keys.PageDown;
            d["pagedown"] = Keys.PageDown;
            d["tab"] = Keys.Tab;
            d["backspace"] = Keys.Back;
            d["left"] = Keys.Left;
            d["right"] = Keys.Right;
            d["up"] = Keys.Up;
            d["down"] = Keys.Down;
            d["home"] = Keys.Home;
            d["end"] = Keys.End;
            return d;
        }

        /// Returns null when the text names no usable key. A hotkey with only
        /// modifiers is refused: Windows would take it and nothing could ever
        /// fire it.
        public static Hotkey Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string[] parts = text.Split('+');
            uint mods = 0;
            Keys key = Keys.None;
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (p.Length == 0) continue;
                if (Same(p, "ctrl") || Same(p, "control")) { mods |= Native.MOD_CONTROL; continue; }
                if (Same(p, "shift")) { mods |= Native.MOD_SHIFT; continue; }
                if (Same(p, "alt")) { mods |= Native.MOD_ALT; continue; }
                if (Same(p, "win") || Same(p, "windows")) { mods |= Native.MOD_WIN; continue; }
                key = KeyFrom(p);
            }
            if (key == Keys.None || mods == 0) return null;
            Hotkey h = new Hotkey();
            h.Mods = mods;
            h.Vk = (uint)key;
            h.Text = Format(mods, key);
            return h;
        }

        private static bool Same(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static Keys KeyFrom(string token)
        {
            Keys k;
            if (Aliases.TryGetValue(token, out k)) return k;
            if (token.Length == 1)
            {
                char c = char.ToUpperInvariant(token[0]);
                if (c >= 'A' && c <= 'Z') return (Keys)c;
                if (c >= '0' && c <= '9') return (Keys)c;
            }
            try
            {
                return (Keys)Enum.Parse(typeof(Keys), token, true);
            }
            catch (Exception) { return Keys.None; }
        }

        public static string Format(uint mods, Keys key)
        {
            StringBuilder sb = new StringBuilder(24);
            if ((mods & Native.MOD_CONTROL) != 0) sb.Append("Ctrl+");
            if ((mods & Native.MOD_SHIFT) != 0) sb.Append("Shift+");
            if ((mods & Native.MOD_ALT) != 0) sb.Append("Alt+");
            if ((mods & Native.MOD_WIN) != 0) sb.Append("Win+");
            sb.Append(Name(key));
            return sb.ToString();
        }

        private static string Name(Keys key)
        {
            if (key >= Keys.D0 && key <= Keys.D9) return ((char)key).ToString();
            if (key >= Keys.A && key <= Keys.Z) return ((char)key).ToString();
            if (key == Keys.Return) return "Enter";
            if (key == Keys.Escape) return "Esc";
            if (key == Keys.Back) return "Backspace";
            if (key == Keys.Next) return "PgDn";
            if (key == Keys.Prior) return "PgUp";
            return key.ToString();
        }

        /// Reads a key press coming from a settings field, so a hotkey can be
        /// recorded by pressing it rather than typed.
        public static Hotkey FromKeyData(Keys data)
        {
            uint mods = 0;
            if ((data & Keys.Control) == Keys.Control) mods |= Native.MOD_CONTROL;
            if ((data & Keys.Shift) == Keys.Shift) mods |= Native.MOD_SHIFT;
            if ((data & Keys.Alt) == Keys.Alt) mods |= Native.MOD_ALT;
            Keys key = data & Keys.KeyCode;
            if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu) return null;
            if (key == Keys.None || mods == 0) return null;
            Hotkey h = new Hotkey();
            h.Mods = mods;
            h.Vk = (uint)key;
            h.Text = Format(mods, key);
            return h;
        }
    }
}
