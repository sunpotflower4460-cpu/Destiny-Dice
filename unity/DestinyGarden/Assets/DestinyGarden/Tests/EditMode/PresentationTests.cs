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
        public void GardenStartsWithThreeActions_AndPanelsDoNotSendARoll()
        {
            var root = new GameObject("Garden UI test");
            var existingEventSystem = EventSystem.current;
            try
            {
                var ui = root.AddComponent<GardenUi>();
                int rolls = 0, wishes = 0, offers = 0;
                ui.OnRoll = () => rolls++;
                ui.OnIntentSelected = (_, _) => wishes++;
                ui.OnAddLuckySeven = () => offers++;
                ui.Build();
                var faces = RecipeCatalog.Order.Take(6).ToArray();
                ui.ShowFaces(faces, 0);
                ui.ShowTitleCard(true, opening: true);
                Button Find(string name) => root.GetComponentsInChildren<Button>().Single(button => button.name == "Chip_" + name);
                var opening = root.transform.Find("Canvas/TitleCard/Opening");
                var enter = opening.GetComponentsInChildren<Button>().Single();
                var entryLabel = enter.GetComponentInChildren<Text>();
                Assert.AreEqual("庭づくりを始める", entryLabel.text);
                Assert.That(entryLabel.preferredWidth, Is.LessThanOrEqualTo(entryLabel.rectTransform.rect.width));
                ui.ShowHeader("晴れ", "草地", 4);
                Assert.AreEqual("庭のつづきへ", entryLabel.text);
                ui.ShowHeader("晴れ", "草地", 0);
                Assert.AreEqual("庭づくりを始める", entryLabel.text);
                enter.onClick.Invoke();
                Assert.IsFalse(ui.TitleCardOpen);
                Assert.AreEqual(0, rolls);

                var controls = root.transform.Find("Canvas/GardenControls");
                Assert.That(controls.GetComponentsInChildren<Button>().Length, Is.EqualTo(3));
                Assert.IsEmpty(controls.Find("PublishedFaces").GetComponentsInChildren<Button>());
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
                Find("七つめの面を加える").onClick.Invoke();
                Assert.AreEqual(1, offers);
                ui.ShowLuckyOffer(false);
                var luckyFaces = faces.Concat(new[] { "light_soft" }).ToArray();
                ui.ShowFaces(luckyFaces, 0, 7);
                var published = controls.Find("PublishedFaces").GetComponentsInChildren<Text>();
                Assert.That(published.Length, Is.EqualTo(7));
                Assert.AreEqual("7  柔らかい光×2", published[6].text);
                Assert.That(published[6].preferredWidth, Is.LessThanOrEqualTo(published[6].rectTransform.rect.width));
                Assert.That(controls.GetComponentsInChildren<Button>().Length, Is.EqualTo(3));
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

                var outcome = new RollOutcome { FaceId = 2, RecipeId = "water_drop" };
                var moment = new GardenMoment
                {
                    Revision = 1, RecipeId = outcome.RecipeId, Headline = "小さな芽が、土から顔を出しました。",
                    Habitat = "水辺"
                };
                ui.ShowMessage(null, outcome, moment);
                var caption = card.Find("Moment caption").GetComponent<Text>();
                var mark = card.Find("Blessing seal/Blessing mark").GetComponent<Image>();
                Assert.AreEqual("1投目　·　2の目「しずく」　/　水辺", caption.text);
                Assert.AreEqual("Water", mark.sprite.name);
                Assert.AreEqual(1f, card.GetComponent<CanvasGroup>().alpha);
                Assert.IsFalse(card.GetComponent<CanvasGroup>().blocksRaycasts);

                ui.ShowFaces(RecipeCatalog.Order.Skip(2).Take(6).ToArray(), 0);
                Assert.AreEqual("Water", mark.sprite.name);
                ui.SetRollEnabled(false);
                Assert.IsFalse(card.gameObject.activeSelf);
                ui.ShowMessage(null, null, moment);
                Assert.AreEqual(moment.Caption, caption.text);
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
    }
}
