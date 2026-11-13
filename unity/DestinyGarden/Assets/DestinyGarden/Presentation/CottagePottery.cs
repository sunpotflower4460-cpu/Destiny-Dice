using UnityEngine;

namespace DestinyGarden.Presentation
{
    internal static class CottagePottery
    {
        internal static Mesh Pot() => CottageModel.Revolve("Clay pot", new[]
        {
            new Vector2(0.001f, 0f), new Vector2(0.32f, 0f), new Vector2(0.35f, 0.08f),
            new Vector2(0.38f, 0.44f), new Vector2(0.46f, 0.91f), new Vector2(0.51f, 0.91f),
            new Vector2(0.51f, 1f), new Vector2(0.42f, 1f), new Vector2(0.40f, 0.86f),
            new Vector2(0.30f, 0.25f), new Vector2(0.001f, 0.25f)
        });

        internal static Mesh Basin() => CottageModel.Revolve("Stone water basin", new[]
        {
            new Vector2(0.001f, 0f), new Vector2(0.29f, 0f), new Vector2(0.34f, 0.18f),
            new Vector2(0.38f, 0.42f), new Vector2(0.48f, 0.72f), new Vector2(0.50f, 0.91f),
            new Vector2(0.47f, 1f), new Vector2(0.38f, 0.96f), new Vector2(0.34f, 0.68f),
            new Vector2(0.27f, 0.53f), new Vector2(0.001f, 0.53f)
        });

    }
}
