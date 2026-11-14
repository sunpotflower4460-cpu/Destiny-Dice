using System.Collections.Generic;
using DestinyGarden.Domain;

namespace DestinyGarden.Application
{
    /// <summary>Player-facing reasons read from a resolved roll's recorded signs.</summary>
    public static class GardenFortune
    {
        public static string SevenReason(int face, int previousFace)
        {
            if (face == LuckySeven.FaceId) return "今回、七つめの面が出た";
            return PrayerStar.IsLuckyNumber(face, previousFace) ? $"前回の{previousFace} ＋ 今回の{face} ＝ 7" : null;
        }

        public static List<(string Number, string Name, string Reason)> OtherSigns(PrayerStarReading star, int face, bool wishedFace)
        {
            var signs = new List<(string Number, string Name, string Reason)>();
            if (star == null) return signs;
            if (star.Triple) signs.Add(($"{face}{face}{face}", "三つぞろい", $"{face}の目が3投続いた"));
            if (star.Total77)
            {
                int milestone = star.FaceTotal / PrayerStar.LuckyTotal * PrayerStar.LuckyTotal;
                signs.Add(($"{milestone}", "出目の合計", $"これまでの{star.FaceTotal - face} ＋ 今回の{face} ＝ {star.FaceTotal}"));
            }
            if (star.Zorome && !star.Triple && !star.LuckyNumber)
                signs.Add(($"{face}{face}", "ゾロ目", $"前回も今回も{face}の目"));
            if (wishedFace && !star.LuckyNumber && signs.Count == 0)
                signs.Add(($"{face}", "願いの目", $"願っていた{face}の目が出た"));
            return signs;
        }

        public static string Primary(int face, PrayerStarReading star, bool wishedFace)
        {
            if (star == null) return null;
            if (star.LuckyNumber && SevenReason(face, star.FaceSum - face) is string reason)
                return "ラッキーナンバー　" + reason;
            var signs = OtherSigns(star, face, wishedFace);
            return signs.Count == 0 ? null : SignLine(signs[0]);
        }

        private static string SignLine((string Number, string Name, string Reason) sign) =>
            sign.Number + " " + sign.Name + "　" + sign.Reason;

        public static string Describe(int face, PrayerStarReading star, bool wishedFace)
        {
            if (star == null) return null;
            var reasons = new List<string>();
            if (star.LuckyNumber && SevenReason(face, star.FaceSum - face) is string reason)
                reasons.Add("ラッキーナンバー　" + reason);
            foreach (var sign in OtherSigns(star, face, wishedFace)) reasons.Add(SignLine(sign));
            return reasons.Count > 0 ? string.Join("\n", reasons) : null;
        }

        internal static string FromRecord(int? face, string intentKind, string intentValue, IDictionary<string, object> fields)
        {
            if (!face.HasValue) return null;
            bool Flag(string key) => fields.TryGetValue(key, out object value) && value is bool yes && yes;
            int Count(string key) => fields.TryGetValue(key, out object value) ? WorldStateSerializer.ToInt(value) : 0;
            var star = new PrayerStarReading
            {
                LuckyNumber = Flag("luckyNumber"), FaceSum = Count("faceSum"), Zorome = Flag("zorome"),
                Triple = Flag("triple"), Total77 = Flag("total77"), FaceTotal = Count("faceTotal")
            };
            bool wishedFace = intentKind == "face" && Flag("wishAnswered") &&
                int.TryParse(intentValue, out int wanted) && wanted == face.Value;
            return Describe(face.Value, star, wishedFace);
        }
    }
}
