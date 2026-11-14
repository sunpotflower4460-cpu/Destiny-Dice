using System;
using System.Globalization;
using System.Text;
using DestinyGarden.Domain;

namespace DestinyGarden.Application
{
    /// <summary>Shared wording and normalization for a roll and its recorded prayer.</summary>
    public static class RollRecord
    {
        public const int PrayerLimit = 80;

        public static string NormalizePrayer(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var line = new StringBuilder();
            var elements = StringInfo.GetTextElementEnumerator(value);
            int count = 0;
            bool space = false;
            while (count < PrayerLimit && elements.MoveNext())
            {
                string element = elements.GetTextElement();
                if (char.IsWhiteSpace(element, 0)) { space = line.Length > 0; continue; }
                if (char.IsControl(element, 0) || (element.Length == 1 && char.IsSurrogate(element[0]))) continue;
                if (space)
                {
                    if (count + 1 >= PrayerLimit) break;
                    line.Append(' ');
                    count++;
                    space = false;
                }
                line.Append(element);
                count++;
            }
            return line.Length == 0 ? null : line.ToString();
        }

        public static string FaceLabel(int faceId, string recipeId, bool amplified)
        {
            if (faceId <= 0) return null;
            string face = faceId.ToString(CultureInfo.InvariantCulture) + "の目";
            return recipeId == null ? face : face + "  ·  " + RecipeCatalog.ActionLabelJa(recipeId) + (amplified ? " ×2" : "");
        }

        public static string WishLabel(string kind, string value)
        {
            if (kind == "face" && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int face) &&
                face >= 1 && face <= LuckySeven.FaceId) return face.ToString(CultureInfo.InvariantCulture) + "の目";
            if (kind != "family") return null;
            return value switch
            {
                "water" => "水の恵み",
                "wind" => "風の恵み",
                "light" => "光の恵み",
                "seed" => "種の恵み",
                _ => null
            };
        }

        public static string LuckyLabel(int faceId, int previousFace) =>
            previousFace > 0 && previousFace + faceId == PrayerStar.LuckySum
                ? $"ラッキーナンバー  {previousFace} ＋ {faceId} ＝ 7"
                : "ラッキーナンバー  7";

        public static string WishComparison(string kind, string value, bool matched)
        {
            string label = WishLabel(kind, value);
            return label == null ? null : "願い " + label + "  ·  " + (matched ? "一致" : kind == "face" ? "別の目" : "別の恵み");
        }
    }
}
