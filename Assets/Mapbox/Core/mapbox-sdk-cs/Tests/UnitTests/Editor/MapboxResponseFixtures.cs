#if UNITY_5_6_OR_NEWER
namespace Mapbox.MapboxSdkCs.UnitTest
{
    // Handwritten synthetic protocol fixtures, not recordings or serialized SDK DTOs.
    // Shapes: https://docs.mapbox.com/api/navigation/map-matching/
    // and https://docs.mapbox.com/api/maps/tilesets/#retrieve-tilejson-metadata
    // Values describe test data, not current streets, translations or tileset metadata.
    internal static class MapboxResponseFixtures
    {
        public const string Tracepoints = @"""tracepoints"":[
            {""location"":[0,0],""name"":""Start"",""waypoint_index"":0,""matchings_index"":0,""alternatives_count"":2},
            {""location"":[2,1],""name"":""End"",""waypoint_index"":1,""matchings_index"":0,""alternatives_count"":19}]";
        public const string Basic = @"{""code"":""Ok""," + Tracepoints + @",
            ""matchings"":[{""confidence"":0.75,""weight"":25.5,""weight_name"":""routability"",
            ""distance"":100,""duration"":20,""geometry"":""??_ibE_seK"",
            ""legs"":[{""distance"":100,""duration"":20,""summary"":""Test road"",""steps"":[]}]}]}";
        public const string Full = @"{""code"":""Ok""," + Tracepoints + @",
            ""matchings"":[{""confidence"":0.75,""weight"":25.5,""weight_name"":""routability"",
            ""distance"":100,""duration"":20,""geometry"":""??_t`B_ibE_t`B_ibE"",
            ""legs"":[{""distance"":100,""duration"":20,""summary"":""Test road"",""steps"":[],
            ""annotation"":{""distance"":[40,60],""duration"":[8,12],""speed"":[5,5],""congestion"":[""low"",""moderate""]}}]}]}";
        public const string EnglishSteps = @"[{""distance"":100,""duration"":20,""geometry"":""??_ibE_seK"",
            ""name"":""Test road"",""mode"":""driving"",""maneuver"":{""location"":[0,0],""type"":""depart"",""instruction"":""Head northeast on Test road""},
            ""intersections"":[{""location"":[0,0],""bearings"":[60],""entry"":[true],""out"":0}]},
            {""distance"":0,""duration"":0,""geometry"":""_ibE_seK"",""name"":""Test road"",""mode"":""driving"",
            ""maneuver"":{""location"":[2,1],""type"":""arrive"",""instruction"":""You have arrived at your destination""},""intersections"":[]}]";
        public const string GermanSteps = @"[{""distance"":100,""duration"":20,""name"":""Teststraße"",""mode"":""driving"",
            ""maneuver"":{""location"":[0,0],""type"":""depart"",""instruction"":""Fahren Sie Richtung Nordosten auf Teststraße""},""intersections"":[]},
            {""distance"":0,""duration"":0,""name"":""Teststraße"",""mode"":""driving"",
            ""maneuver"":{""location"":[2,1],""type"":""arrive"",""instruction"":""Sie haben Ihr Ziel erreicht""},""intersections"":[]}]";
        public const string English = @"{""code"":""Ok""," + Tracepoints + @", ""matchings"":[{
            ""confidence"":0.75,""geometry"":""??_ibE_seK"",""legs"":[{""steps"":" + EnglishSteps + @"}]}]}";
        public const string German = @"{""code"":""Ok""," + Tracepoints + @", ""matchings"":[{
            ""confidence"":0.75,""geometry"":""??_t`B_ibE_t`B_ibE"",""legs"":[{""steps"":" + GermanSteps + @",
            ""annotation"":{""distance"":[40,60],""duration"":[8,12],""speed"":[5,5],""congestion"":[""low"",""moderate""]}}]}]}";
        public const string Tidy = @"{""code"":""Ok"",""tracepoints"":[
            {""location"":[0,0],""waypoint_index"":0,""matchings_index"":0,""alternatives_count"":0},null,
            {""location"":[2,1],""waypoint_index"":1,""matchings_index"":0,""alternatives_count"":0}],
            ""matchings"":[{""geometry"":""??_ibE_seK"",""legs"":[{""steps"":[]}]}]}";
        public const string NoSegment = @"{""code"":""NoSegment"",""message"":""Could not find a matching segment for input coordinates"",""matchings"":[]}";
        public const string InvalidInput = @"{""code"":""InvalidInput"",""message"":""Coordinate is invalid: 2,100""}";

        public const string TileCommon = @"""tilejson"":""2.2.0"",""name"":""Synthetic tiles"",""attribution"":""Test attribution"",
            ""bounds"":[-120,-40,150,60],""center"":[12,34,3],""minzoom"":0,""maxzoom"":16,""scheme"":""xyz"",""private"":false,
            ""tiles"":[""https://example.invalid/{z}/{x}/{y}.pbf""]";
        public const string Layers = @"""vector_layers"":[{""id"":""road"",""description"":""Test roads"",
            ""fields"":{""class"":""String"",""lanes"":""Number""},""source"":""synthetic"",""source_name"":""Synthetic roads""}]";
        public const string Streets = @"{" + TileCommon + @", " + Layers + @",
            ""id"":""mapbox.mapbox-streets-v7"",""format"":""pbf"",""created"":946684800000,""modified"":946771200000,""webpage"":""https://example.invalid/streets""}";
        public const string Composite = @"{" + TileCommon + @", " + Layers + @"}";
        public const string Satellite = @"{""tilejson"":""2.2.0"",""name"":""Synthetic raster"",""attribution"":""Test attribution"",
            ""id"":""mapbox.satellite"",""bounds"":[-180,-85,180,85],""center"":[12,34,3],""minzoom"":0,""maxzoom"":22,
            ""scheme"":""xyz"",""private"":false,""tiles"":[""https://example.invalid/{z}/{x}/{y}.jpg""],""webpage"":""https://example.invalid/raster""}";
        public const string Emerald = @"{" + TileCommon + @", " + Layers + @",
            ""id"":""mapbox.emerald"",""format"":""pbf"",""webpage"":""https://example.invalid/emerald""}";
    }
}
#endif
