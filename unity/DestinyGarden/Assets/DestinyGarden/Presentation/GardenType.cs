using UnityEngine;
using UnityEngine.UI;

namespace DestinyGarden.Presentation
{
    /// <summary>
    /// The garden's type: three bundled faces (SIL OFL 1.1, Resources/Fonts), the same on every platform.
    /// Body — Noto Serif JP Regular: descriptions, notes, records.
    /// Strong — Noto Serif JP SemiBold: headings, buttons, the rolled face (a real weight, never synthetic bold,
    /// which smears Japanese strokes).
    /// Display — Cormorant Garamond SemiBold: the English title and the large numerals of the prayer star.
    /// </summary>
    public static class GardenType
    {
        private static Font _body, _strong, _display;

        public static Font Body => _body != null ? _body : (_body = Load("Fonts/NotoSerifJP-Regular"));
        public static Font Strong => _strong != null ? _strong : (_strong = Load("Fonts/NotoSerifJP-SemiBold") ?? Body);
        public static Font Display => _display != null ? _display : (_display = Load("Fonts/CormorantGaramond-SemiBold") ?? Strong);

        private static Font Load(string path)
        {
            var font = Resources.Load<Font>(path);
            if (font != null) return font;
            // Only if the bundled font is missing: the system's Japanese font, then Unity's own.
            font = Font.CreateDynamicFontFromOSFont(
                new[] { "Hiragino Mincho ProN", "Yu Mincho", "Noto Serif CJK JP", "Hiragino Sans", "Arial" }, 24);
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        /// <summary>Heading or button text: the real SemiBold weight.</summary>
        public static void MakeStrong(Text text)
        {
            if (text == null) return;
            text.font = Strong;
            text.fontStyle = FontStyle.Normal;
        }

        /// <summary>Ordinary text (undoes MakeStrong).</summary>
        public static void MakeBody(Text text)
        {
            if (text == null) return;
            text.font = Body;
            text.fontStyle = FontStyle.Normal;
        }

        /// <summary>Strong when emphasised, body otherwise (e.g. the rolled face among the published faces).</summary>
        public static void Emphasis(Text text, bool strong)
        {
            if (strong) MakeStrong(text); else MakeBody(text);
        }

        /// <summary>The English title and the prayer star's numerals.</summary>
        public static void MakeDisplay(Text text)
        {
            if (text == null) return;
            text.font = Display;
            text.fontStyle = FontStyle.Normal;
        }
    }
}
