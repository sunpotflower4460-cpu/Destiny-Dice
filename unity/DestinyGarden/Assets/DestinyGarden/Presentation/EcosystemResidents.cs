using System;
using System.Collections.Generic;
using DestinyGarden.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace DestinyGarden.Presentation
{
    /// <summary>Small, quiet residents at the cells the model actually selected; one batched mesh.</summary>
    public sealed class EcosystemResidents : MonoBehaviour
    {
        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private Material _material;
        private static readonly Color Dark = new Color(0.16f, 0.20f, 0.14f);
        private static readonly Color Green = new Color(0.39f, 0.60f, 0.24f);
        private static readonly Color Brown = new Color(0.52f, 0.37f, 0.23f);

        public void Show(List<EcologyVisit> visits, WorldState state, GardenView view)
        {
            if (_filter == null)
            {
                _filter = gameObject.AddComponent<MeshFilter>();
                _renderer = gameObject.AddComponent<MeshRenderer>();
                _material = new Material(Shader.Find("DestinyGarden/Foliage"));
                _renderer.sharedMaterial = _material;
                _renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            if (_filter.sharedMesh != null) Destroy(_filter.sharedMesh);
            if (state.Ecosystem == null) { _filter.sharedMesh = null; return; }
            var b = new FoliageMesh();
            foreach (var v in visits)
            {
                bool eggs = v.Kind != null && v.Kind.EndsWith("-eggs", StringComparison.Ordinal);
                bool pupa = v.Kind == CommunityRules.Chrysalis || (v.Kind != null && v.Kind.EndsWith("-pupa", StringComparison.Ordinal));
                bool juvenile = v.Kind == CommunityRules.Caterpillar || v.Kind == CommunityRules.LadybirdLarva || v.Kind == CommunityRules.HoverflyLarva ||
                    v.Kind == CommunityRules.Tadpole || v.Kind == CommunityRules.Nymph;
                bool pond = v.Kind == CommunityRules.Algae || v.Kind == CommunityRules.PondPrey;
                if (!eggs && !pupa && !juvenile && !pond && v.Kind != LivingEcosystem.Aphid && v.Kind != LivingEcosystem.Ladybird && v.Kind != LivingEcosystem.Spider &&
                    v.Kind != LivingEcosystem.Frog && v.Kind != LivingEcosystem.Snail && v.Kind != LivingEcosystem.Springtail) continue;
                int cap = v.Kind == LivingEcosystem.Aphid || v.Kind == LivingEcosystem.Springtail ? 3 : 4;
                for (int i = 0; i < v.Cells.Count && i < cap; i++)
                {
                    int cell = v.Cells[i];
                    var at = view.CellTop(cell) + IslandTerrain.PlantJitter(cell) * 0.5f;
                    at.y = IslandTerrain.Height(at.x, at.z) + 0.05f;
                    b.Root = new Vector4(at.x, at.y, at.z, -1f);
                    if (eggs)
                    {
                        at.y += v.Kind == "frog-eggs" || v.Kind == "dragonfly-eggs" ? 0.01f : 0.18f;
                        for (int k = 0; k < 3; k++) b.Ball(at + new Vector3(k * 0.04f, 0f, (k % 2) * 0.035f),
                            new Vector3(0.022f, 0.035f, 0.022f), new Color(0.88f, 0.83f, 0.53f), 0f);
                    }
                    else if (pupa)
                        b.Ball(at + Vector3.up * 0.20f, new Vector3(0.05f, 0.10f, 0.055f), new Color(0.52f, 0.61f, 0.31f), 0f, 8, 4);
                    else if (v.Kind == CommunityRules.Tadpole)
                    {
                        b.Ball(at, new Vector3(0.06f, 0.03f, 0.075f), Dark, 0f);
                        b.Leaf(at, new Vector3(0.2f, 0.01f, -1f), 0.22f, 0.025f, Brown, 0f);
                    }
                    else if (v.Kind == CommunityRules.Nymph)
                    {
                        b.Ball(at, new Vector3(0.055f, 0.025f, 0.11f), Brown, 0f);
                        for (int k = 0; k < 6; k++)
                            b.Leaf(at, new Vector3(k % 2 == 0 ? -1f : 1f, 0f, (k / 2 - 1) * 0.6f), 0.12f, 0.009f, Dark, 0f);
                    }
                    else if (juvenile)
                    {
                        at.y += 0.16f;
                        Color body = v.Kind == CommunityRules.LadybirdLarva ? Dark : Green;
                        int segments = v.Kind == CommunityRules.Caterpillar ? 5 : 3;
                        for (int k = 0; k < segments; k++)
                        {
                            b.Ball(at + Vector3.forward * (k * 0.042f), new Vector3(0.036f, 0.032f, 0.042f), body, 0f);
                            if (v.Kind == CommunityRules.LadybirdLarva)
                                b.Ball(at + new Vector3(0f, 0.028f, k * 0.042f), Vector3.one * 0.016f, new Color(0.92f, 0.54f, 0.15f), 0f);
                        }
                    }
                    else if (v.Kind == CommunityRules.Algae)
                    {
                        for (int k = 0; k < 3; k++) b.Ball(at + new Vector3(k * 0.12f, 0f, k % 2 * 0.09f),
                            new Vector3(0.12f, 0.008f, 0.08f), new Color(0.35f, 0.57f, 0.33f), 0f);
                    }
                    else if (v.Kind == CommunityRules.PondPrey)
                    {
                        for (int k = 0; k < 3; k++) b.Ball(at + new Vector3(k * 0.065f, 0.01f, k % 2 * 0.04f),
                            new Vector3(0.018f, 0.012f, 0.025f), new Color(0.64f, 0.67f, 0.50f), 0f);
                    }
                    else if (v.Kind == LivingEcosystem.Ladybird)
                    {
                        at.y += 0.22f;
                        b.Ball(at, new Vector3(0.085f, 0.055f, 0.10f), new Color(0.88f, 0.21f, 0.12f), 0f, 8, 4);
                        b.Ball(at + Vector3.forward * 0.08f, Vector3.one * 0.042f, Dark, 0f);
                        for (int k = 0; k < 4; k++) b.Ball(at + new Vector3(k % 2 == 0 ? -0.04f : 0.04f, 0.05f, k < 2 ? -0.035f : 0.025f), Vector3.one * 0.018f, Dark, 0f);
                    }
                    else if (v.Kind == LivingEcosystem.Frog)
                    {
                        b.Ball(at, new Vector3(0.18f, 0.10f, 0.20f), Green, 0f, 8, 4);
                        for (int side = -1; side <= 1; side += 2)
                        {
                            b.Ball(at + new Vector3(side * 0.15f, -0.02f, -0.08f), new Vector3(0.10f, 0.065f, 0.10f), Green, 0f);
                            b.Ball(at + new Vector3(side * 0.07f, 0.095f, 0.11f), Vector3.one * 0.04f, new Color(0.80f, 0.84f, 0.40f), 0f);
                            b.Ball(at + new Vector3(side * 0.07f, 0.10f, 0.14f), Vector3.one * 0.02f, Dark, 0f);
                        }
                    }
                    else if (v.Kind == LivingEcosystem.Snail)
                    {
                        b.Ball(at, new Vector3(0.08f, 0.035f, 0.18f), new Color(0.65f, 0.61f, 0.42f), 0f);
                        b.Ball(at + Vector3.up * 0.07f, new Vector3(0.10f, 0.11f, 0.08f), Brown, 0f, 8, 4);
                        b.Ball(at + new Vector3(0f, 0.08f, 0.075f), Vector3.one * 0.04f, new Color(0.80f, 0.66f, 0.42f), 0f);
                    }
                    else if (v.Kind == LivingEcosystem.Spider)
                    {
                        at.y += 0.12f;
                        b.Ball(at, new Vector3(0.06f, 0.04f, 0.08f), Brown, 0f);
                        for (int k = 0; k < 8; k++)
                        {
                            float angle = k * Mathf.PI / 4f;
                            b.Leaf(at, new Vector3(Mathf.Cos(angle), -0.2f, Mathf.Sin(angle)), 0.14f, 0.009f, Dark, 0f);
                        }
                    }
                    else
                    {
                        bool aphid = v.Kind == LivingEcosystem.Aphid;
                        for (int k = 0; k < 3; k++) b.Ball(at + new Vector3(k * 0.045f, aphid ? 0.20f : 0f, k % 2 * 0.035f),
                            new Vector3(0.025f, 0.02f, 0.04f), aphid ? Green : new Color(0.74f, 0.70f, 0.58f), 0f);
                    }
                }
            }
            // The litter visible under plants is the same saved resource that decomposers consume.
            int patches = 0;
            for (int c = 0; c < GardenMapData.CellCount && patches < 16; c++)
            {
                if (state.Ecosystem.Litter[c] < 4) continue;
                patches++;
                var at = view.CellTop(c) + new Vector3(0.18f, 0.015f, -0.12f);
                b.Root = new Vector4(at.x, at.y, at.z, -1f);
                for (int k = 0; k < 3; k++) b.Leaf(at + Vector3.right * (k * 0.12f), new Vector3(0.6f, 0.04f, 0.8f), 0.18f, 0.045f, Brown, 0f);
            }
            _filter.sharedMesh = b.ToMesh("EcosystemResidents");
        }

        private void OnDestroy()
        {
            if (_filter != null && _filter.sharedMesh != null) Destroy(_filter.sharedMesh);
            if (_material != null) Destroy(_material);
        }
    }
}
