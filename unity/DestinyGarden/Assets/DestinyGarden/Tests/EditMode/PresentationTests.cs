using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using DestinyGarden.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    public class PresentationTests
    {
        [Test]
        public void SignLabels_ExplainTheRecordedRolls()
        {
            var triple = PrayerStar.Read(5, 5, "water_drop", "water_drop", false, 0, -1, 0, 0, 2, 72);
            var lines = PrayerStarSigil.SignLabels(triple, 5, false);
            CollectionAssert.AreEqual(new[] { "555", "三つぞろい", "5の目が3投続いた", "" }, lines[0]);
            CollectionAssert.AreEqual(new[] { "77", "出目の合計", "これまでの72 ＋ 今回の5 ＝ 77", "" }, lines[1]);
            var zorome = PrayerStar.Read(2, 2, "water_drop", "water_drop", false, 0, -1, 0, 0, 1, 10);
            CollectionAssert.AreEqual(new[] { "22", "ゾロ目", "前回も今回も2の目", "" }, PrayerStarSigil.SignLabels(zorome, 2, false)[0]);
            var plain = PrayerStar.Read(4, 1, "water_drop", "water_drop", false, 0, -1, 0, 0, 1, 10);
            CollectionAssert.AreEqual(new[] { "4", "願いの目", "願っていた4の目が出た", "" }, PrayerStarSigil.SignLabels(plain, 4, true)[0]);
            Assert.AreEqual(0, PrayerStarSigil.SignLabels(plain, 4, false).Count);
        }

        [Test]
        public void LuckyDots_ShowTheCountWithoutWords()
        {
            string dots = PrayerStarSigil.LuckyDots(3);
            StringAssert.Contains("●●●</color>", dots);
            Assert.AreEqual(7, dots.Split('●').Length - 1, "always seven dots");
            string visible = System.Text.RegularExpressions.Regex.Replace(dots, "<[^>]+>", "");
            Assert.AreEqual(new string('●', 7), visible, "no words, no numbers: only the dots are seen");
            Assert.AreEqual(7, PrayerStarSigil.LuckyDots(9).Split('●').Length - 1);
        }

        [Test]
        public void GardenName_FollowsWhatTheGardenBecame()
        {
            var s = new WorldState();
            Assert.AreEqual("まだ名前のない庭", GardenKeepsake.Name(s, null, 0, 0));
            s.Plants.Add(new PlantState { PlantId = "a", CellId = 40, Species = "cosmos", Stage = PlantStage.Flower });
            s.Plants.Add(new PlantState { PlantId = "b", CellId = 41, Species = "daisy", Stage = PlantStage.Leaf });
            string flower = Flora.Get("cosmos").LabelJa;
            Assert.AreEqual(flower + "の庭", GardenKeepsake.Name(s, new HashSet<string>(), 0, 0), "a flower counts double");
            string withButterfly = GardenKeepsake.Name(s, new HashSet<string> { Ecology.Bee, Ecology.Butterfly }, 0, 0);
            Assert.AreEqual(flower + "と" + EcologyObservations.ResidentLabel(Ecology.Butterfly) + "の庭", withButterfly, "the most telling creature");
            StringAssert.StartsWith("虹のかかる", GardenKeepsake.Name(s, null, 3, 0));
            StringAssert.StartsWith("幸運の灯る", GardenKeepsake.Name(s, null, 0, 7));
            StringAssert.StartsWith("虹を見た", GardenKeepsake.Name(s, null, 1, 0));
        }

        [Test]
        public void SafeAreaMovesControlsWithTheCutoutAndKeepsTheHomeBarClear()
        {
            var root = new GameObject("Safe area test", typeof(RectTransform));
            try
            {
                var layout = (RectTransform)root.transform;
                var screen = new Vector2(1000f, 500f);
                GardenSafeArea.Apply(layout, new Rect(60f, 20f, 900f, 480f), screen);
                Assert.That(Vector2.Distance(new Vector2(0.06f, 0.04f), layout.anchorMin), Is.LessThan(0.0001f));
                Assert.That(Vector2.Distance(new Vector2(0.96f, 1f), layout.anchorMax), Is.LessThan(0.0001f));
                Assert.AreEqual(Vector2.zero, layout.offsetMin);
                Assert.AreEqual(Vector2.zero, layout.offsetMax);

                GardenSafeArea.Apply(layout, new Rect(40f, 20f, 900f, 480f), screen);
                Assert.That(layout.anchorMin.x, Is.EqualTo(0.04f).Within(0.0001f));
                Assert.That(layout.anchorMax.x, Is.EqualTo(0.94f).Within(0.0001f));

                GardenSafeArea.Apply(layout, Rect.zero, screen);
                Assert.AreEqual(Vector2.zero, layout.anchorMin);
                Assert.AreEqual(Vector2.one, layout.anchorMax);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void GardenStartsWithThreeActions_AndPanelsDoNotSendARoll()
        {
            var root = new GameObject("Garden UI test");
            var existingEventSystem = EventSystem.current;
            try
            {
                var ui = root.AddComponent<GardenUi>();
                int rolls = 0, wishes = 0, offers = 0;
                string lastKind = null, lastValue = null;
                ui.OnRoll = () => rolls++;
                ui.OnIntentSelected = (kind, value) => { wishes++; lastKind = kind; lastValue = value; };
                ui.OnAddLuckySeven = () => offers++;
                ui.Build();
                var faces = RecipeCatalog.Order.Take(6).ToArray();
                ui.ShowFaces(faces, 0);
                ui.ShowTitleCard(true, opening: true);
                Button Find(string name) => root.GetComponentsInChildren<Button>().Single(button => button.name == "Chip_" + name);
                var opening = root.transform.Find("Canvas/TitleCard/TitleSafeArea/Opening");
                var motifs = opening.Find("Opening blessings").GetComponentsInChildren<Image>();
                CollectionAssert.AreEqual(new[] { "Water", "Wind", "Star", "Seed", "Soil", "Flower", "Moon", "Rainbow" },
                    motifs.Select(image => image.sprite.name));
                Assert.IsFalse(motifs.Any(image => image.raycastTarget));
                var enter = opening.GetComponentsInChildren<Button>().Single();
                var entryLabel = enter.GetComponentInChildren<Text>();
                Assert.AreEqual("小さな庭をひらく", entryLabel.text);
                Assert.That(entryLabel.preferredWidth, Is.LessThanOrEqualTo(entryLabel.rectTransform.rect.width));
                ui.ShowOpeningProgress(1);
                Assert.AreEqual("小さな庭をひらく", entryLabel.text);
                ui.ShowOpeningProgress(2);
                Assert.AreEqual("庭のつづきへ", entryLabel.text);
                // A saved garden can also be left for a new one (it stays in the record): two presses, never by accident.
                int switched = 0;
                ui.OnSwitchVisitor = () => switched++;
                var fresh = opening.GetComponentsInChildren<Button>().Single(b => b.name == "Chip_新しい庭をはじめる");
                var freshLabel = fresh.GetComponentInChildren<Text>();
                Assert.That(freshLabel.preferredWidth, Is.LessThanOrEqualTo(freshLabel.rectTransform.rect.width));
                fresh.onClick.Invoke();
                Assert.AreEqual(0, switched);
                Assert.AreEqual("もう一度押す・今の庭は残ります", freshLabel.text);
                Assert.That(freshLabel.preferredWidth, Is.LessThanOrEqualTo(freshLabel.rectTransform.rect.width));
                fresh.onClick.Invoke();
                Assert.AreEqual(1, switched);
                ui.ShowOpeningProgress(0);
                Assert.AreEqual("小さな庭をひらく", entryLabel.text);
                Assert.IsFalse(fresh.gameObject.activeSelf, "a first garden has nothing to leave");
                enter.onClick.Invoke();
                Assert.IsFalse(ui.TitleCardOpen);
                Assert.AreEqual(0, rolls);

                var controls = root.transform.Find("Canvas/GardenControls");
                // Three actions, and each published face can be tapped to wish for it.
                var strip = controls.Find("PublishedFaces");
                Assert.That(strip.GetComponentsInChildren<Button>().Length, Is.EqualTo(6));
                Assert.That(controls.GetComponentsInChildren<Button>().Length, Is.EqualTo(3 + 6));
                Assert.That(controls.GetComponentsInChildren<Text>().Length, Is.EqualTo(9));
                var roll = Find("サイコロを振る");
                roll.onClick.Invoke();
                Assert.AreEqual(1, rolls);
                Assert.AreEqual(0, wishes);

                Find("メニュー").onClick.Invoke();
                Assert.IsFalse(controls.GetComponent<CanvasGroup>().blocksRaycasts);
                roll.onClick.Invoke();
                Find("庭の記録").onClick.Invoke();
                Find("投ごとの記録").onClick.Invoke();
                root.GetComponentsInChildren<Button>().Single(button => button.name == "PanelBack").onClick.Invoke();
                Assert.IsTrue(Find("投ごとの記録").gameObject.activeInHierarchy);
                roll.onClick.Invoke();
                Assert.AreEqual(1, rolls);

                for (int i = 0; i < 2; i++)
                    root.GetComponentsInChildren<Button>().Single(button => button.name == "PanelBack").onClick.Invoke();
                Find("願いをかける").onClick.Invoke();
                Find("水").onClick.Invoke();
                Assert.AreEqual(1, wishes);
                Assert.AreEqual(1, rolls);
                Assert.IsTrue(controls.GetComponent<CanvasGroup>().blocksRaycasts);

                ui.ShowLuckyOffer(true);
                Assert.IsNotNull(Find("七つめの面を加える").targetGraphic.transform.Find("Seventh face glow"));
                Find("七つめの面を加える").onClick.Invoke();
                Assert.AreEqual(1, offers);
                ui.ShowLuckyOffer(false);
                var luckyFaces = faces.Concat(new[] { "water_drop" }).ToArray();
                ui.ShowFaces(luckyFaces, 0, 7);
                var published = strip.GetComponentsInChildren<Text>();
                Assert.That(published.Length, Is.EqualTo(7));
                CollectionAssert.AreEqual(new[] { "1 たっぷり水", "2 小雨", "3 そよ風", "4 つよい風",
                    "5 やわらかな光", "6 ひとすじの光", "7 たっぷり水×2" }, published.Select(label => label.text.Split('\n')[0]));
                StringAssert.Contains("一か所に深く", published[0].text, "each face says where and how its blessing falls");
                foreach (var label in published)
                {
                    Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width), label.text);
                    Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height), label.text);
                }
                var icons = strip.GetComponentsInChildren<Image>().Where(image => image.name == "Face blessing").ToArray();
                CollectionAssert.AreEqual(new[] { "Water", "Water", "Wind", "Wind", "Star", "Star", "Water" }, icons.Select(i => i.sprite.name));
                var wishBackings = strip.GetComponentsInChildren<Image>(true).Where(image => image.name == "Wished face").ToArray();
                Assert.That(wishBackings.Where(image => image.gameObject.activeInHierarchy),
                    Is.EqualTo(new[] { wishBackings[6] }));
                ui.ShowIntentSelection("family", "water");
                ui.ShowFaces(faces, 1);
                Assert.That(wishBackings.Where(image => image.gameObject.activeInHierarchy),
                    Is.EqualTo(wishBackings.Take(2)));
                ui.ShowIntentSelection("none", null);
                ui.ShowFaces(faces, 0);
                Assert.IsFalse(wishBackings.Any(image => image.gameObject.activeInHierarchy));
                Assert.That(controls.GetComponentsInChildren<Button>().Length, Is.EqualTo(3 + 6));

                // Tap a face to wish for it, tap it again to withdraw; never while rolling.
                var tapFaces = strip.GetComponentsInChildren<Button>();
                int before = wishes;
                tapFaces[3].onClick.Invoke();
                Assert.AreEqual(("face", "4"), (lastKind, lastValue));
                ui.ShowFaces(faces, 0, 4);
                tapFaces[3].onClick.Invoke();
                Assert.AreEqual("none", lastKind);
                ui.SetRollEnabled(false);
                tapFaces[1].onClick.Invoke();
                Assert.AreEqual(before + 2, wishes, "no wish while the die is rolling");
                ui.SetRollEnabled(true);
                Assert.AreEqual(1, rolls);

                // Lucky marks: a gold star on the face making seven, two dots on the face making a ゾロ目; no words.
                var marks = strip.GetComponentsInChildren<Image>(true).Where(image => image.name == "Lucky mark").ToArray();
                Assert.That(marks.Length, Is.EqualTo(7));
                ui.ShowFaces(faces, 0, 0, 5, 2);
                Assert.That(marks.Where(m => m.gameObject.activeInHierarchy).Select(m => m.sprite.name), Is.EqualTo(new[] { "Pair", "LuckyStar" }));
                Assert.IsTrue(marks[4].gameObject.activeInHierarchy && marks[1].gameObject.activeInHierarchy);
                Assert.IsFalse(marks.Any(m => m.raycastTarget));
                ui.ShowFaces(luckyFaces, 0, 0, 0, 3, true);
                Assert.IsTrue(marks[6].gameObject.activeInHierarchy, "the seventh face is always lucky");
                Assert.AreEqual("LuckyStar", marks[6].sprite.name);
                ui.ShowFaces(faces, 2);
                Assert.IsFalse(marks.Any(m => m.gameObject.activeInHierarchy));
                Assert.That(strip.GetComponentsInChildren<Text>().Length, Is.EqualTo(6), "the marks add no text");

                ui.ShowFaces(new[] { "seed_single", "seed_scatter" }.Concat(faces.Skip(2)).ToArray(), 0);
                StringAssert.StartsWith("1 種ひと粒\n", strip.GetComponentsInChildren<Text>()[0].text);
                StringAssert.StartsWith("2 種ふた粒\n", strip.GetComponentsInChildren<Text>()[1].text);
                Find("願いをかける").onClick.Invoke();
                foreach (var label in strip.GetComponentsInChildren<Text>())
                {
                    var wishChoice = root.GetComponentsInChildren<Button>().Where(button => !button.transform.IsChildOf(strip))
                        .Select(button => button.GetComponentInChildren<Text>()).SingleOrDefault(text => text != null && text.text == label.text);
                    Assert.IsNotNull(wishChoice, label.text);
                    Assert.That(wishChoice.preferredWidth, Is.LessThanOrEqualTo(wishChoice.rectTransform.rect.width), label.text);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (existingEventSystem == null && EventSystem.current != null)
                    UnityEngine.Object.DestroyImmediate(EventSystem.current.gameObject);
            }
        }

        [Test]
        public void EngravedRollButtonPressesWithinAStationaryHitArea()
        {
            var root = new GameObject("Garden UI test");
            var existingEventSystem = EventSystem.current;
            try
            {
                var ui = root.AddComponent<GardenUi>();
                ui.Build();
                ui.ShowTitleCard(true, opening: true);
                ui.ShowTitleCard(false);

                var roll = root.transform.Find("Canvas/GardenControls/Chip_サイコロを振る").GetComponent<Button>();
                var hitArea = roll.GetComponent<Image>();
                var cap = roll.targetGraphic.rectTransform;
                var rest = cap.offsetMin;
                var area = hitArea.rectTransform.rect;
                Assert.That(roll.GetComponentsInChildren<Graphic>(true).Where(graphic => graphic.raycastTarget),
                    Is.EqualTo(new[] { hitArea }));

                var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                roll.OnPointerDown(pointer);
                Assert.Less(cap.offsetMin.y, rest.y);
                Assert.AreEqual(area, hitArea.rectTransform.rect);
                ui.SetRollEnabled(false);
                Assert.AreEqual(rest, cap.offsetMin);
                roll.OnPointerUp(pointer);
                ui.SetRollEnabled(true);
                Assert.AreEqual(rest, cap.offsetMin);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (existingEventSystem == null && EventSystem.current != null)
                    UnityEngine.Object.DestroyImmediate(EventSystem.current.gameObject);
            }
        }

        [Test]
        public void MomentCardKeepsTheCommittedBlessingAcrossPreparationAndClearsOnNextRoll()
        {
            var root = new GameObject("Garden UI test");
            var existingEventSystem = EventSystem.current;
            try
            {
                var ui = root.AddComponent<GardenUi>();
                ui.View = root.AddComponent<GardenView>();
                ui.View.ReducedMotion = true;
                ui.Build();
                ui.ShowTitleCard(false);
                var card = root.transform.Find("Canvas/GardenControls/Garden moment");
                Assert.IsFalse(card.gameObject.activeSelf);

                var outcome = new RollOutcome { FaceId = 2, RecipeId = "water_drop", IntentKind = "face", IntentValue = "3" };
                var moment = new GardenMoment
                {
                    Revision = 1, RecipeId = outcome.RecipeId, Headline = "小さな芽が、土から顔を出しました。",
                    Habitat = "水辺"
                };
                ui.ShowMessage(null, outcome, moment);
                var headline = card.Find("Moment headline").GetComponent<Text>();
                var habitat = card.Find("Moment habitat").GetComponent<Text>();
                var result = card.Find("Rolled face and blessing").GetComponent<Text>();
                var wish = card.Find("Wish and lucky number").GetComponent<Text>();
                var mark = card.Find("Blessing seal/Blessing mark").GetComponent<Image>();
                Assert.AreEqual("2の目  ·  たっぷり水", result.text);
                Assert.AreEqual("願い 3の目  ·  別の目", wish.text);
                Assert.AreEqual(1f, result.color.a);
                Assert.AreEqual(moment.Headline, headline.text);
                Assert.AreEqual("水辺", habitat.text);
                Assert.That(card.GetComponentsInChildren<Text>(), Is.EquivalentTo(new[] { headline, habitat, result, wish }));
                Assert.That(800f - ((RectTransform)card).rect.width * 0.5f, Is.GreaterThanOrEqualTo(360f));
                Assert.AreEqual("Water", mark.sprite.name);
                Assert.AreEqual(1f, card.GetComponent<CanvasGroup>().alpha);
                Assert.IsFalse(card.GetComponent<CanvasGroup>().blocksRaycasts);

                ui.ShowFaces(RecipeCatalog.Order.Skip(2).Take(6).ToArray(), 0);
                Assert.AreEqual("Water", mark.sprite.name);
                ui.SetRollEnabled(false);
                Assert.IsFalse(card.gameObject.activeSelf);
                ui.ShowMessage(null, null, moment);
                Assert.AreEqual(moment.Headline, headline.text);
                Assert.IsFalse(result.gameObject.activeSelf);
                Assert.IsFalse(wish.gameObject.activeSelf);
                moment.Habitat = null;
                ui.ShowMessage(null, null, moment);
                Assert.IsFalse(habitat.gameObject.activeSelf);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (existingEventSystem == null && EventSystem.current != null)
                    UnityEngine.Object.DestroyImmediate(EventSystem.current.gameObject);
            }
        }

        private static Dictionary<string, float[]> AllSounds()
        {
            var d = new Dictionary<string, float[]>
            {
                ["sea"] = AudioSynth.Sea(), ["stream"] = AudioSynth.Stream(), ["leaves"] = AudioSynth.LeafWind(),
                ["crickets"] = AudioSynth.Crickets(), ["bell"] = AudioSynth.Bell(), ["water"] = AudioSynth.Water(),
                ["wind"] = AudioSynth.Wind(), ["shimmer"] = AudioSynth.Shimmer(), ["pluck880"] = AudioSynth.Pluck(880),
                ["lucky"] = AudioSynth.LuckySeven(), ["rainbow"] = AudioSynth.Rainbow(),
                ["star5"] = AudioSynth.StarUp(5), ["star7"] = AudioSynth.StarUp(7), ["star8"] = AudioSynth.StarUp(8),
                ["bloom0"] = AudioSynth.Bloom(0), ["bloom7"] = AudioSynth.Bloom(7), ["bloom14"] = AudioSynth.Bloom(14),
                ["sprout3"] = AudioSynth.Sprout(3), ["chirp0"] = AudioSynth.Chirp(0), ["chirp3"] = AudioSynth.Chirp(3)
            };
            for (int i = 0; i < 4; i++) d["pad" + i] = AudioSynth.PadLayer(i);
            return d;
        }

        private static void WriteWav(string path, float[] s)
        {
            using (var w = new BinaryWriter(File.Create(path)))
            {
                int n = s.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + n); w.Write(new[] { 'W', 'A', 'V', 'E' });
                w.Write(new[] { 'f', 'm', 't', ' ' }); w.Write(16); w.Write((short)1); w.Write((short)1);
                w.Write(AudioSynth.Rate); w.Write(AudioSynth.Rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(n);
                foreach (float f in s) w.Write((short)Math.Round(Math.Max(-1f, Math.Min(1f, f)) * 32767));
            }
        }

        [Test]
        public void Sounds_AreFinite_Quiet_AndDeterministic()
        {
            var a = AllSounds(); var b = AllSounds();
            string outDir = Environment.GetEnvironmentVariable("DG_AUDIO_OUT");
            if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
            foreach (var kv in a)
            {
                float peak = 0f;
                foreach (float f in kv.Value) { Assert.IsFalse(float.IsNaN(f) || float.IsInfinity(f), kv.Key + " has a non-finite sample"); peak = Math.Max(peak, Math.Abs(f)); }
                Assert.That(peak, Is.InRange(0.3f, 0.7f), kv.Key + " peak");
                CollectionAssert.AreEqual(kv.Value, b[kv.Key], kv.Key + " must be deterministic");
                if (!string.IsNullOrEmpty(outDir)) WriteWav(Path.Combine(outDir, kv.Key + ".wav"), kv.Value);
            }
        }

        [Test]
        public void TonalLoops_AreSeamless()
        {
            foreach (var name in new[] { "pad0", "pad1", "pad2", "pad3", "crickets" })
            {
                var s = AllSounds()[name];
                float seam = Math.Abs(s[0] - s[s.Length - 1]);
                Assert.Less(seam, 0.08f, name + " seam");
            }
        }

        [Test]
        public void IslandSeed_IsDeterministic_DiffersBetweenGardens_AndKeepsTheGardenClear()
        {
            var a1 = IslandSeed.FromRunId("run-aaaa"); var a2 = IslandSeed.FromRunId("run-aaaa"); var b = IslandSeed.FromRunId("run-bbbb");
            Assert.AreEqual(a1.Value, a2.Value);
            CollectionAssert.AreEqual(a1.River, a2.River);
            Assert.AreEqual(a1.Trees.Length, a2.Trees.Length);
            Assert.AreNotEqual(a1.Value, b.Value);
            int differing = 0;
            for (int i = 0; i < 40; i++)
            {
                var s = IslandSeed.FromValue(1000 + i * 7919);
                // The garden's own ground is never touched: the river hugs it at a distance and ends in the sea.
                foreach (var p in s.River) Assert.GreaterOrEqual(Vector2.Distance(p, IslandSeed.Center), 6.9f, "river stays out of the garden");
                Assert.Greater(Vector2.Distance(s.River[s.River.Length - 1], IslandSeed.Center), 12f, "river reaches the sea");
                Assert.GreaterOrEqual(s.Trees.Length, 8, "trees");
                foreach (var t in s.Trees)
                {
                    float deg = Mathf.Repeat(t.Angle * Mathf.Rad2Deg, 360f);
                    Assert.IsFalse(deg > 235f && deg < 305f, "no tree across the default front view");
                }
                foreach (var st in s.Stones) Assert.That(st.Radius, Is.InRange(9.2f, 11f));
                Assert.That(s.StartPhase, Is.InRange(0f, 1f));
                if (Mathf.Abs(Mathf.DeltaAngle(s.RidgeAngle * Mathf.Rad2Deg, a1.RidgeAngle * Mathf.Rad2Deg)) > 20f) differing++;
            }
            Assert.Greater(differing, 20, "islands should differ in where their highlands lie");
        }

        [TestCase(13)]
        [TestCase(211)]
        [TestCase(11879)]
        public void IslandCoast_VariesAroundTheIsland_WhileTheGardenStaysAboveWater(int value)
        {
            var previous = IslandTerrain.Seed;
            try
            {
                var seed = IslandSeed.FromValue(value);
                IslandTerrain.Configure(seed);
                for (int c = 0; c < GardenMapData.CellCount; c++)
                {
                    if (GardenMapData.SoilCapacity[c] == 0) continue;
                    float expected = 0.30f + GardenMapData.CellHeight[c] / 1140f * 0.85f;
                    Assert.That(IslandTerrain.Height(c % 16, c / 16), Is.EqualTo(expected).Within(0.0001f), $"soil cell {c}");
                }

                float min = 13f, max = 8f;
                for (int i = 0; i < 48; i++)
                {
                    float angle = i * (2f * Mathf.PI / 48f);
                    float land = 8f, sea = 12.8f;
                    Assert.Greater(HeightAt(angle, land), IslandTerrain.SeaLevel);
                    Assert.Less(HeightAt(angle, sea), IslandTerrain.SeaLevel);
                    for (int j = 0; j < 12; j++)
                    {
                        float r = (land + sea) * 0.5f;
                        if (HeightAt(angle, r) > IslandTerrain.SeaLevel) land = r;
                        else sea = r;
                    }
                    min = Mathf.Min(min, land);
                    max = Mathf.Max(max, land);
                }
                Assert.Greater(max - min, 1.9f, "the bays should cut inside the headlands");
                Assert.Greater(IslandTerrain.Height(seed.River[0].x, seed.River[0].y), IslandTerrain.SeaLevel + 0.20f);
                var mouth = seed.River[seed.River.Length - 1];
                Assert.Less(IslandTerrain.Height(mouth.x, mouth.y), IslandTerrain.SeaLevel);
            }
            finally
            {
                IslandTerrain.Configure(previous);
            }

            float HeightAt(float angle, float radius)
            {
                var p = IslandSeed.At(angle, radius);
                return IslandTerrain.Height(p.x, p.y);
            }
        }

        [Test]
        public void TreeForms_KeepCurvedStemsAndLeafFacesOrientedToTheirNormals()
        {
            foreach (TreeForms.Kind kind in Enum.GetValues(typeof(TreeForms.Kind)))
            {
                var tree = TreeForms.Build(kind, 211);
                Assert.AreEqual(tree.V.Count, tree.N.Count, kind.ToString());
                Assert.AreEqual(tree.V.Count, tree.C.Count, kind.ToString());
                Assert.That(tree.T.Count % 3, Is.Zero, kind.ToString());
                for (int i = 0; i < tree.T.Count; i += 3)
                {
                    int a = tree.T[i], b = tree.T[i + 1], c = tree.T[i + 2];
                    var face = Vector3.Cross(tree.V[b] - tree.V[a], tree.V[c] - tree.V[a]);
                    float alignment = Vector3.Dot(face, tree.N[a] + tree.N[b] + tree.N[c]);
                    Assert.That(alignment, Is.GreaterThan(0f), $"{kind}, triangle {i / 3}");
                }
            }
        }

        [Test]
        public void PlantForms_BuildEverySavedSpeciesAndStageWithUsableGeometry()
        {
            var root = new Vector3(7.4f, 0.64f, 8.2f);
            int[] growth = { 0, 500, 1150, 1800, 2500 };
            var stages = (PlantStage[])Enum.GetValues(typeof(PlantStage));
            foreach (var species in Flora.All)
            {
                var plant = new PlantState { PlantId = "mesh-" + species.Id, Species = species.Id };
                var form = PlantForms.Choose(plant, root);
                Assert.AreEqual(species.LabelJa, PlantForms.LabelJa(form));
                foreach (var stage in stages)
                {
                    plant.Stage = stage;
                    plant.Growth = growth[(int)stage];
                    var mesh = new FoliageMesh { Root = new Vector4(root.x, root.y, root.z, -1f) };
                    PlantForms.Build(mesh, plant, root, form);
                    string label = $"{species.Id}, {stage}";
                    Assert.That(mesh.V.Count, Is.GreaterThan(0).And.LessThan(5000), label);
                    Assert.AreEqual(mesh.V.Count, mesh.N.Count, label);
                    Assert.AreEqual(mesh.V.Count, mesh.C.Count, label);
                    Assert.AreEqual(mesh.V.Count, mesh.R.Count, label);
                    Assert.That(mesh.T.Count, Is.GreaterThan(0), label);
                    Assert.That(mesh.T.Count % 3, Is.Zero, label);
                    float highest = root.y;
                    foreach (var vertex in mesh.V) highest = Mathf.Max(highest, vertex.y);
                    Assert.That(highest - root.y, Is.InRange(0.005f, stage == PlantStage.Seed ? 0.065f : 1.6f), label);
                    for (int i = 0; i < mesh.T.Count; i += 3)
                    {
                        var a = mesh.V[mesh.T[i]];
                        var face = Vector3.Cross(mesh.V[mesh.T[i + 1]] - a, mesh.V[mesh.T[i + 2]] - a);
                        if (face.sqrMagnitude <= 1e-15f || float.IsNaN(face.sqrMagnitude))
                            Assert.Fail($"{label}, triangle {i / 3} is degenerate");
                    }
                }
            }
        }

        [Test]
        public void GardenLife_RefreshShowsOnlyTheResidentsInTheReportedSites()
        {
            var root = new GameObject("Resident test");
            try
            {
                var view = root.AddComponent<GardenView>();
                var life = root.AddComponent<GardenLife>();
                life.View = view;
                life.ReducedMotion = true;
                life.Build();
                var garden = WorldState.CreateInitial();
                int site = garden.Plants[0].CellId;
                var visits = new List<EcologyVisit>
                {
                    new EcologyVisit { Kind = Ecology.Bee, Cells = new List<int> { site } },
                    new EcologyVisit { Kind = Ecology.Dragonfly, Cells = new List<int> { site } },
                    new EcologyVisit { Kind = Ecology.Firefly, Cells = new List<int> { site } },
                    new EcologyVisit { Kind = Ecology.Worm, Cells = new List<int> { site } },
                    new EcologyVisit { Kind = Ecology.Bird }
                };
                life.ShowPresence(visits, garden);
                var dragonfly = root.transform.Find("Dragonfly 1");
                var firefly = root.transform.Find("Firefly 1");
                Assert.That(dragonfly.gameObject.activeSelf);
                Assert.That(dragonfly.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(5));
                Assert.That(Vector3.Distance(dragonfly.position, view.CellTop(site)), Is.LessThan(1.5f));
                Assert.That(firefly.Find("Lantern"), Is.Not.Null);
                Assert.That(root.transform.Find("Bee 1").gameObject.activeSelf);
                Assert.That(root.transform.Find("Small bird").gameObject.activeSelf);
                Assert.That(root.transform.Find("Earthworm 1").gameObject.activeSelf);
                Assert.That(root.transform.Find("Earthworm 2").gameObject.activeSelf, Is.False);
                Assert.That(root.transform.Find("Fish 1").gameObject.activeSelf, Is.False);
                life.ShowPresence(new List<EcologyVisit>(), garden);
                Assert.That(dragonfly.gameObject.activeSelf, Is.False);
                Assert.That(firefly.gameObject.activeSelf, Is.False);
                Assert.That(root.transform.Find("Small bird").gameObject.activeSelf, Is.False);
                Assert.That(root.transform.Find("Earthworm 1").gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BlossomPetalSprite_HasAnOpaqueBodyAndTransparentCorners()
        {
            var petal = ProceduralArt.BlossomPetalSprite();
            try
            {
                Assert.That(petal.GetPixelBilinear(0.5f, 0.5f).a, Is.GreaterThan(0.9f));
                Assert.That(petal.GetPixel(0, 0).a, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(petal);
            }
        }

        [Test]
        public void SkyClock_StartsInMorning_ReachesNight_AndWraps()
        {
            var morning = SkyClock.Evaluate(SkyClock.PhaseFor(0));
            Assert.Less(morning.Night, 0.05f);
            var night = SkyClock.Evaluate(SkyClock.PhaseFor(12));
            Assert.Greater(night.Night, 0.5f, "roll 12 of the day is already night");
            var next = SkyClock.Evaluate(SkyClock.PhaseFor(SkyClock.RollsPerDay));
            Assert.AreEqual(morning.SunIntensity, next.SunIntensity, 1e-4f, "one full day later it is morning again");
            for (int r = 0; r < 40; r++)
            {
                var s = SkyClock.Evaluate(SkyClock.PhaseFor(r));
                Assert.That(s.SunIntensity, Is.InRange(0.5f, 3f));
                Assert.That(s.Night, Is.InRange(0f, 1f));
            }
            Assert.Greater(morning.SunVisibility, 0.8f);
        }

        [Test]
        public void SkyClock_UsesTheSavedWeatherForCloudsAndKeepsAContinuousNightHorizon()
        {
            var clear = SkyClock.Evaluate(0.38f, WeatherId.Clear);
            var cloudy = SkyClock.Evaluate(0.38f, WeatherId.Cloudy);
            var drizzle = SkyClock.Evaluate(0.38f, WeatherId.Drizzle);
            Assert.Less(clear.CloudCover, cloudy.CloudCover);
            Assert.Less(cloudy.CloudCover, drizzle.CloudCover);
            Assert.Greater(clear.SunVisibility, drizzle.SunVisibility);
            Assert.AreEqual(clear.Night, drizzle.Night);

            var dusk = SkyClock.Evaluate(0.75f);
            var night = SkyClock.Evaluate(0.88f);
            Assert.Greater(dusk.Twilight, clear.Twilight);
            Assert.Greater(night.MoonVisibility, dusk.MoonVisibility);
            Assert.That(night.MoonDirection.magnitude, Is.EqualTo(1f).Within(0.0001f));
            Assert.Greater(night.Horizon.grayscale, night.Zenith.grayscale);
            var midnight = SkyClock.Evaluate(0f);
            var before = SkyClock.Evaluate(0.9999f);
            Assert.That(Mathf.Abs(midnight.Horizon.grayscale - before.Horizon.grayscale), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(midnight.MoonDirection, before.MoonDirection), Is.LessThan(0.01f));
        }

        [Test]
        public void QualityMonitor_StepsDownOnlyWhenSlow_AfterWarmup_AndNeverPastMax()
        {
            var fast = new QualityMonitor();
            for (int i = 0; i < 60 * 20; i++) Assert.IsFalse(fast.Feed(1f / 60f));
            Assert.AreEqual(0, fast.Level);

            var slow = new QualityMonitor();
            int steps = 0;
            for (int i = 0; i < 20 * 20 * 4; i++) if (slow.Feed(1f / 20f)) steps++; // 20 fps for 80 s
            Assert.AreEqual(QualityMonitor.MaxLevel, slow.Level);
            Assert.AreEqual(QualityMonitor.MaxLevel, steps);

            var hitch = new QualityMonitor();
            for (int i = 0; i < 10; i++) Assert.IsFalse(hitch.Feed(2f), "a hidden tab or loading pause is ignored");
            Assert.AreEqual(0, hitch.Level);
        }

        private sealed class KeepsakeJournal : IJournalWriter
        {
            public readonly List<JournalEntry> Entries = new List<JournalEntry>();
            public string HeadHash => "";
            public long NextSeq => Entries.Count + 1;
            public JournalEntry Append(string kind, string runId, string roundId, Dictionary<string, object> payload)
            {
                var e = new JournalEntry { Seq = Entries.Count + 1, Kind = kind, RunId = runId, RoundId = roundId, Payload = payload };
                Entries.Add(e);
                return e;
            }
        }

        [Test]
        public void KeepsakeCard_TellsTheGardenFromSavedFacts_AndOpensFromTheMenu()
        {
            Assert.IsFalse(GardenKeepsake.CardDue(0));
            Assert.IsFalse(GardenKeepsake.CardDue(47));
            Assert.IsTrue(GardenKeepsake.CardDue(48));
            Assert.IsTrue(GardenKeepsake.CardDue(96));
            Assert.AreEqual("A・B・C・D・E・F・G ほか3", GardenKeepsake.NameList(new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J" }));
            Assert.IsNull(GardenKeepsake.NameList(new List<string>()));

            var journal = new KeepsakeJournal();
            var coord = new GardenCoordinator(journal, new FixedEntropy("keepsake"), new SequentialIds());
            coord.CreateRun();
            coord.Prepare();
            for (int i = 0; i < 48; i++)
            {
                if (coord.LuckySevenOffered) coord.AddLuckySeven();
                coord.CommitIntent("family", i % 2 == 0 ? "water" : "light");
                coord.Roll();
                coord.Prepare();
            }
            var history = HistoryProjector.Project(journal.Entries, coord.RunId);
            var card = GardenKeepsake.BuildCard(PrayerStarSigil.CurrentName(coord), coord.State, history,
                CottageGrowth.Project(coord.State.WorldRevision, history).Label, PrayerStarSigil.LuckyPlants(coord.State));
            Assert.AreEqual(48, card.Rolls);
            Assert.AreEqual(history.Count(r => r.Resolved && r.LuckyNumber), card.LuckyNumbers);
            Assert.AreEqual(coord.RainbowCount, card.Rainbows);
            Assert.AreEqual(history.Count(r => r.Resolved && r.IntentMatched), card.Wishes);
            Assert.Greater(card.Flowers.Count, 0, "48 rolls bring flowers");
            Assert.Greater(card.Creatures.Count, 0, "and creatures");
            Assert.AreEqual(card.Flowers.Count, card.Flowers.Distinct().Count());
            Assert.IsFalse(card.Creatures.Any(c => c == "露" || c == "綿毛" || c.EndsWith("の卵")));
            if (card.Creatures.Contains("蝶")) Assert.AreEqual("蝶", card.Creatures[0], "the creatures that mark a garden come first");
            Assert.IsFalse(string.IsNullOrEmpty(card.House));
            TestContext.WriteLine($"〈{card.Name}〉 {card.House} | {GardenKeepsake.NameList(card.Flowers)} | {GardenKeepsake.NameList(card.Creatures)}");

            var root = new GameObject("Keepsake UI test");
            var existingEventSystem = EventSystem.current;
            try
            {
                var ui = root.AddComponent<GardenUi>();
                int asked = 0, hidden = 0;
                ui.OnOpenKeepsake = () => { asked++; ui.ShowKeepsake(card); };
                ui.OnHideUi = () => hidden++;
                ui.Build();
                ui.ShowTitleCard(false);
                ui.ShowFaces(RecipeCatalog.Order.Take(6).ToArray(), 0);
                Button Find(string name) => root.GetComponentsInChildren<Button>().Single(button => button.name == "Chip_" + name);
                Find("メニュー").onClick.Invoke();
                Find("庭のしおり").onClick.Invoke();
                Assert.AreEqual(1, asked);
                var panel = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Keepsake");
                Assert.IsTrue(panel.gameObject.activeInHierarchy);
                var texts = panel.GetComponentsInChildren<Text>().Where(t => !string.IsNullOrEmpty(t.text)).ToList();
                Assert.IsTrue(texts.Any(t => t.text == $"〈{card.Name}〉"));
                foreach (var t in texts)
                    if (t.horizontalOverflow == HorizontalWrapMode.Overflow)
                        Assert.That(t.preferredWidth, Is.LessThanOrEqualTo(t.rectTransform.rect.width + 1f), t.text);
                    else
                        Assert.That(t.preferredHeight, Is.LessThanOrEqualTo(t.rectTransform.rect.height + 1f), t.text);
                // Back returns to the menu; 続ける closes everything.
                Find("続ける").onClick.Invoke();
                Assert.IsFalse(panel.gameObject.activeInHierarchy);
                Assert.IsTrue(ui.ShowKeepsake(card), "the 48th roll opens it directly");
                Find("記念に残す").onClick.Invoke();
                Assert.AreEqual(1, hidden);
                Assert.IsFalse(panel.gameObject.activeInHierarchy);
                ui.SetRollEnabled(false);
                Assert.IsFalse(ui.ShowKeepsake(card), "never while the die is rolling");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (existingEventSystem == null && EventSystem.current != null)
                    UnityEngine.Object.DestroyImmediate(EventSystem.current.gameObject);
            }
        }
    }
}
