using System;
using System.Collections.Generic;
using System.Text;
using DestinyGarden.Domain;

namespace DestinyGarden.Application
{
    public sealed class CareAftercareNote
    {
        public string EarlierRoundId, LaterRoundId, RecipeId, Habitat, Kind, Observation;
        public int EarlierRevision, LaterRevision, CellId;
    }
    /// <summary>Later events in touched soil, from verified single-run history; proximity is not exclusive causation.</summary>
    public static class CareAftercare
    {
        public const int Window = 12, MaxNotes = 24;
        private sealed class Contact
        {
            public string RoundId, RecipeId, Habitat;
            public int Revision;
        }
        private static readonly string[] Kinds = { "seed-awakened", "clonal-rooted", "recovered", "stressed", "fruited",
            "resident-settled", "resident-emerged", "brood-laid", "brood-lost", "resident-left", "pond-dried", "returned-to-soil", "habitat-shifted" };
        public static List<CareAftercareNote> Project(IList<RoundSummary> history)
        {
            var ordered = new List<RoundSummary>();
            foreach (var round in history) if (round.Resolved && round.WorldRevision > 0 && round.RoundId != null) ordered.Add(round);
            ordered.Sort((a, b) => a.WorldRevision != b.WorldRevision ? a.WorldRevision.CompareTo(b.WorldRevision) : string.CompareOrdinal(a.RoundId, b.RoundId));
            var contacts = new Dictionary<int, Contact>(); var notes = new List<CareAftercareNote>(); var seenRounds = new HashSet<string>();
            foreach (var round in ordered)
            {
                if (!seenRounds.Add(round.RoundId)) continue;
                var seen = new HashSet<string>();
                foreach (var link in round.Relations)
                {
                    if (Array.IndexOf(Kinds, link.Kind) < 0 || !contacts.TryGetValue(link.ToCell, out var contact)) continue;
                    int age = round.WorldRevision - contact.Revision;
                    if (age < 1 || age > Window || !seen.Add(link.Kind + ":" + link.Actor + ":" + link.ToCell)) continue;
                    notes.Add(new CareAftercareNote { EarlierRoundId = contact.RoundId, LaterRoundId = round.RoundId, RecipeId = contact.RecipeId,
                        Habitat = contact.Habitat, EarlierRevision = contact.Revision, LaterRevision = round.WorldRevision, CellId = link.ToCell,
                        Kind = link.Kind, Observation = EcologyObservations.Describe(link) });
                    if (notes.Count > MaxNotes) notes.RemoveAt(0);
                }
                // Only prior rolls are eligible: the current action cannot become its own follow-up.
                foreach (int cell in new List<int>(contacts.Keys)) if (round.WorldRevision - contacts[cell].Revision >= Window) contacts.Remove(cell);
                if (round.Care == null) continue;
                foreach (var impact in round.Care.Impacts)
                    foreach (int cell in impact.Cells)
                    {
                        if (cell < 0 || cell >= GardenMapData.CellCount) continue;
                        contacts[cell] = new Contact { RoundId = round.RoundId, Revision = round.WorldRevision, RecipeId = round.Care.RecipeId, Habitat = impact.Habitat };
                    }
            }
            notes.Reverse(); return notes;
        }
        public static string Describe(IList<CareAftercareNote> notes, string habitat = null, int max = 4)
        {
            if (max <= 0) return "";
            var sb = new StringBuilder(); int shown = 0;
            foreach (var note in notes)
            {
                if (habitat != null && note.Habitat != habitat) continue;
                if (shown++ >= max) break;
                if (shown == 1) sb.AppendLine("\n恵みが届いた土、その後（最近の記録）");
                sb.AppendLine($"{note.EarlierRevision}投目の「{RecipeCatalog.LabelJa(note.RecipeId)}」 / {note.Habitat}・{CareTargets.Position(note.CellId)}");
                sb.AppendLine($"　その{note.LaterRevision - note.EarlierRevision}投後：" + note.Observation);
            }
            if (shown > 0) sb.AppendLine("同じ場所で後から起きたこと。ほかの投や天候も関わる。分類は届けた時の居場所。");
            return sb.ToString();
        }
    }
}
