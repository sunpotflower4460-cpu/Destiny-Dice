// Generated from docs/unity-garden/content/oracle-catalog.v0.7.json
// Source SHA-256 (repository bytes): c93040df8e1b46934ba352fc578ff0e08c66cf73c67b7a23cb50cd140f1d6210
namespace DestinyGarden.Domain
{
    public sealed class OracleEntry
    {
        public string Id; public string Kind; public string Scope;
        public string[] AllowedElements; public string[] RequiresAll;
        public string[] CardTags; public int Priority; public string Text;
    }
    public static class OracleCatalogData
    {
        public const string SourceCatalogSha256 = "c93040df8e1b46934ba352fc578ff0e08c66cf73c67b7a23cb50cd140f1d6210";
        public const string CatalogVersion = "oracle-catalog.v0.7";
        public const string SelectionPolicyVersion = "oracle-focus.v0.7";
        public const string DeckVersion = "deck.v0.7";
        public static readonly string[] CardOrder = { "sprout", "roots", "circulation", "space", "harmony", "lantern" };
        public static readonly string[] CardLabelsJa = { "芽生え", "根", "巡り", "余白", "調和", "灯り" };
        public static readonly OracleEntry[] Entries =
        {
            new OracleEntry { Id = "message.input.water", Kind = "fallback", Scope = "input", AllowedElements = new[] { "water" }, RequiresAll = new[] { "input_applied" }, CardTags = new string[0], Priority = 0, Text = "庭に、水を注ぎました。" },
            new OracleEntry { Id = "message.input.wind", Kind = "fallback", Scope = "input", AllowedElements = new[] { "wind" }, RequiresAll = new[] { "input_applied" }, CardTags = new string[0], Priority = 0, Text = "庭に、風を送りました。" },
            new OracleEntry { Id = "message.input.light", Kind = "fallback", Scope = "input", AllowedElements = new[] { "light" }, RequiresAll = new[] { "input_applied" }, CardTags = new string[0], Priority = 0, Text = "庭に、光を向けました。" },
            new OracleEntry { Id = "message.input.seed", Kind = "fallback", Scope = "input", AllowedElements = new[] { "seed" }, RequiresAll = new[] { "input_applied" }, CardTags = new string[0], Priority = 0, Text = "庭に、種を置きました。" },
            new OracleEntry { Id = "message.soil.absorbed", Kind = "factual", Scope = "focus", AllowedElements = new[] { "water", "wind", "light", "seed" }, RequiresAll = new[] { "soil_moistened" }, CardTags = new[] { "roots", "circulation" }, Priority = 20, Text = "水が、土の中へ染み込みました。" },
            new OracleEntry { Id = "message.soil.dry_absorbed", Kind = "factual", Scope = "focus", AllowedElements = new[] { "water", "wind", "light", "seed" }, RequiresAll = new[] { "soil_moistened", "soil_was_dry" }, CardTags = new[] { "roots", "sprout" }, Priority = 30, Text = "乾いていた土に、水が染み込みました。" },
            new OracleEntry { Id = "message.roots.absorbed", Kind = "factual", Scope = "focus", AllowedElements = new[] { "water", "wind", "light", "seed" }, RequiresAll = new[] { "root_zone_wetted" }, CardTags = new[] { "roots" }, Priority = 30, Text = "根元の土に、水が染み込みました。" },
            new OracleEntry { Id = "message.water.puddle", Kind = "factual", Scope = "focus", AllowedElements = new[] { "water", "wind", "light", "seed" }, RequiresAll = new[] { "puddle_formed" }, CardTags = new[] { "space", "circulation" }, Priority = 30, Text = "地面に、水たまりができました。" },
            new OracleEntry { Id = "message.water.flow", Kind = "factual", Scope = "focus", AllowedElements = new[] { "water", "wind", "light", "seed" }, RequiresAll = new[] { "surface_flow_observed" }, CardTags = new[] { "circulation" }, Priority = 20, Text = "水の流れが、庭の中を進みました。" },
            new OracleEntry { Id = "message.water.verified_root_path", Kind = "factual", Scope = "focus", AllowedElements = new[] { "water", "wind", "light", "seed" }, RequiresAll = new[] { "verified_path_to_root", "root_zone_wetted" }, CardTags = new[] { "circulation" }, Priority = 35, Text = "水の通り道が、根元までつながりました。" },
            new OracleEntry { Id = "message.wind.reached", Kind = "factual", Scope = "focus", AllowedElements = new[] { "wind" }, RequiresAll = new[] { "wind_reached_focus" }, CardTags = new[] { "circulation" }, Priority = 20, Text = "庭の一角に、風が通りました。" },
            new OracleEntry { Id = "message.wind.sheltered", Kind = "factual", Scope = "focus", AllowedElements = new[] { "wind" }, RequiresAll = new[] { "wind_sheltered" }, CardTags = new[] { "space" }, Priority = 30, Text = "岩の向こうでは、風がやわらいでいました。" },
            new OracleEntry { Id = "message.wind.moisture_recovered", Kind = "factual", Scope = "focus", AllowedElements = new[] { "wind" }, RequiresAll = new[] { "wind_reached_focus", "moisture_returned_to_band" }, CardTags = new[] { "harmony", "roots" }, Priority = 35, Text = "湿りすぎていた土が、ほどよい湿りに戻りました。" },
            new OracleEntry { Id = "message.light.reached", Kind = "factual", Scope = "focus", AllowedElements = new[] { "light" }, RequiresAll = new[] { "light_reached_focus" }, CardTags = new[] { "lantern" }, Priority = 20, Text = "庭の一角が、光に照らされました。" },
            new OracleEntry { Id = "message.light.sheltered", Kind = "factual", Scope = "focus", AllowedElements = new[] { "light" }, RequiresAll = new[] { "light_reached_focus", "light_attenuated_by_rock" }, CardTags = new[] { "space", "lantern" }, Priority = 30, Text = "岩の陰には、やわらいだ光が届きました。" },
            new OracleEntry { Id = "message.light.growth", Kind = "factual", Scope = "focus", AllowedElements = new[] { "light" }, RequiresAll = new[] { "light_reached_focus", "growth_advanced" }, CardTags = new[] { "lantern", "harmony" }, Priority = 30, Text = "光の届いた場所で、植物の成長が進みました。" },
            new OracleEntry { Id = "message.seed.moist_soil", Kind = "factual", Scope = "focus", AllowedElements = new[] { "seed" }, RequiresAll = new[] { "seed_placed", "soil_in_band_at_seed" }, CardTags = new[] { "sprout", "roots" }, Priority = 30, Text = "種が、湿りのある土に落ちました。" },
            new OracleEntry { Id = "message.seed.pair", Kind = "factual", Scope = "input_pair", AllowedElements = new[] { "seed" }, RequiresAll = new[] { "seed_pair_placed" }, CardTags = new[] { "sprout", "space" }, Priority = 25, Text = "庭の二つの場所に、小さな種が加わりました。" },
            new OracleEntry { Id = "message.plant.germinated", Kind = "factual", Scope = "focus", AllowedElements = new[] { "water", "wind", "light", "seed" }, RequiresAll = new[] { "germinated" }, CardTags = new[] { "sprout" }, Priority = 40, Text = "小さな芽が、土から顔を出しました。" },
            new OracleEntry { Id = "message.plant.leaf_opened", Kind = "factual", Scope = "focus", AllowedElements = new[] { "water", "wind", "light", "seed" }, RequiresAll = new[] { "leaf_opened" }, CardTags = new[] { "sprout" }, Priority = 40, Text = "新しい葉が、ひらきました。" },
            new OracleEntry { Id = "message.plant.bud_formed", Kind = "factual", Scope = "focus", AllowedElements = new[] { "water", "wind", "light", "seed" }, RequiresAll = new[] { "bud_formed" }, CardTags = new[] { "sprout" }, Priority = 40, Text = "小さなつぼみが、姿を見せました。" },
            new OracleEntry { Id = "message.plant.bloomed", Kind = "factual", Scope = "focus", AllowedElements = new[] { "water", "wind", "light", "seed" }, RequiresAll = new[] { "bloomed" }, CardTags = new[] { "sprout" }, Priority = 40, Text = "つぼみがひらき、花になりました。" },
            new OracleEntry { Id = "message.growth.water_and_light", Kind = "factual", Scope = "focus", AllowedElements = new[] { "water", "wind", "light", "seed" }, RequiresAll = new[] { "growth_advanced", "soil_and_light_sufficient_during_growth" }, CardTags = new[] { "harmony", "roots" }, Priority = 35, Text = "水と光がそろった場所で、植物の成長が進みました。" },
            new OracleEntry { Id = "message.plant.shadowed_bud", Kind = "factual", Scope = "focus", AllowedElements = new[] { "light" }, RequiresAll = new[] { "shadowed_bud_unchanged", "light_reached_focus" }, CardTags = new[] { "space" }, Priority = 25, Text = "岩陰のつぼみは、まだ閉じたままでした。" },
            new OracleEntry { Id = "message.weather.rain_light", Kind = "factual", Scope = "focus", AllowedElements = new[] { "light" }, RequiresAll = new[] { "weather_rain_reached_focus", "light_reached_focus" }, CardTags = new[] { "lantern", "harmony" }, Priority = 35, Text = "雨の中に、光が差しました。" },
            new OracleEntry { Id = "message.weather.rain_wind", Kind = "factual", Scope = "focus", AllowedElements = new[] { "wind" }, RequiresAll = new[] { "weather_rain_reached_focus", "wind_reached_focus" }, CardTags = new[] { "circulation", "harmony" }, Priority = 35, Text = "雨の降る場所を、風が通りました。" },
            new OracleEntry { Id = "message.weather.rain_seed", Kind = "factual", Scope = "focus", AllowedElements = new[] { "seed" }, RequiresAll = new[] { "weather_rain_reached_focus", "seed_placed" }, CardTags = new[] { "sprout", "roots" }, Priority = 35, Text = "雨の降る土に、種を置きました。" },
            new OracleEntry { Id = "message.weather.earlier_seed_germinated", Kind = "factual", Scope = "focus", AllowedElements = new[] { "water", "wind", "light", "seed" }, RequiresAll = new[] { "weather_rain_reached_focus", "plant_from_earlier_seed", "germinated" }, CardTags = new[] { "sprout", "harmony" }, Priority = 40, Text = "前に置いた種が、雨の中で芽を出しました。" },
        };
    }
}
