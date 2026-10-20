// Copyright (c) 2016 Mapbox. All rights reserved.
#if UNITY_5_6_OR_NEWER
namespace Mapbox.MapboxSdkCs.UnitTest
{
    using Mapbox.Platform.TilesetTileJSON;
    using Mapbox.Utils;
    using NUnit.Framework;

    [TestFixture]
    internal class TileJSONTest
    {
        private static TileJSONResponse Get(string id, string json)
        {
            var source = new MapboxResponseFixtureSource();
            var tileJSON = new TileJSON(source, 17);
            Assert.AreSame(source, tileJSON.FileSource);
            TileJSONResponse result = null;
            int callbacks = 0;
            var handle = tileJSON.Get(id, response => { result = response; callbacks++; });
            Assert.AreSame(source, handle);
            Assert.AreEqual(0, callbacks);
            source.Respond("/v4/" + id + ".json", "secure", json);
            Assert.AreEqual(1, callbacks);
            Assert.IsTrue(handle.IsCompleted);
            return result;
        }

        private static void AssertCommon(TileJSONResponse response, string source, int maxZoom = 16)
        {
            Assert.IsNotNull(response);
            Assert.AreEqual(source, response.Source, "Wrapper must retain the requested source");
            Assert.AreEqual("Test attribution", response.Attribution);
            Assert.AreEqual(0, response.MinZoom);
            Assert.AreEqual(maxZoom, response.MaxZoom);
            Assert.AreEqual("xyz", response.Scheme);
            Assert.AreEqual("2.2.0", response.TileJSONVersion);
            Assert.IsFalse(response.Private);
            Assert.IsNotEmpty(response.Name);
            Assert.GreaterOrEqual(response.Tiles.Length, 1);
            Assert.AreEqual(new Vector2d(34, 12), response.CenterParsed,
                "TileJSON longitude/latitude must be converted into SDK latitude/longitude");
            CollectionAssert.AreEqual(new double[] { 12, 34, 3 }, response.Center);
        }

        private static void AssertVector(TileJSONResponse response)
        {
            Assert.AreEqual(-120, response.BoundsParsed.West);
            Assert.AreEqual(-40, response.BoundsParsed.South);
            Assert.AreEqual(150, response.BoundsParsed.East);
            Assert.AreEqual(60, response.BoundsParsed.North);
            CollectionAssert.AreEqual(new double[] { -120, -40, 150, 60 }, response.Bounds);
            Assert.GreaterOrEqual(response.VectorLayers.Length, 1);
            var layer = response.VectorLayers[0];
            Assert.AreEqual("road", layer.Id);
            Assert.AreEqual("Test roads", layer.Description);
            Assert.AreEqual("synthetic", layer.Source);
            Assert.AreEqual("Synthetic roads", layer.SourceName);
            Assert.AreEqual(2, layer.Fields.Count);
            Assert.AreEqual("String", layer.Fields["class"]);
            Assert.AreEqual("Number", layer.Fields["lanes"]);
            Assert.AreEqual("https://example.invalid/{z}/{x}/{y}.pbf", response.Tiles[0]);
        }

        [Test]
        public void MapboxStreets()
        {
            const string id = "mapbox.mapbox-streets-v7";
            var response = Get(id, MapboxResponseFixtures.Streets);
            AssertCommon(response, id);
            AssertVector(response);
            Assert.AreEqual(id, response.Id);
            Assert.AreEqual("pbf", response.Format);
            Assert.AreEqual("https://example.invalid/streets", response.WebPage);
            Assert.AreEqual(946684800000L, response.Created);
            Assert.AreEqual(946771200000L, response.Modified);
            // Preserve the original presence checks. Exact UTC conversion is an
            // existing SDK limitation: the long overload currently uses ticks.
            Assert.IsTrue(response.CreatedUtc.HasValue);
            Assert.IsTrue(response.ModifiedUtc.HasValue);
        }

        [Test]
        public void ConcatenatedVectorTilesets()
        {
            const string id = "mapbox.mapbox-traffic-v1,mapbox.mapbox-streets-v7";
            var response = Get(id, MapboxResponseFixtures.Composite);
            AssertCommon(response, id);
            AssertVector(response);
            Assert.IsNull(response.Id);
            Assert.IsNull(response.Created);
            Assert.IsNull(response.CreatedUtc);
            Assert.IsNull(response.Modified);
            Assert.IsNull(response.ModifiedUtc);
            Assert.IsNull(response.WebPage);
        }

        [Test]
        public void MapboxSatellite()
        {
            const string id = "mapbox.satellite";
            var response = Get(id, MapboxResponseFixtures.Satellite);
            AssertCommon(response, id, 22);
            Assert.AreEqual(id, response.Id);
            Assert.AreEqual(-180, response.BoundsParsed.West);
            Assert.AreEqual(-85, response.BoundsParsed.South);
            Assert.AreEqual(180, response.BoundsParsed.East);
            Assert.AreEqual(85, response.BoundsParsed.North);
            Assert.AreEqual("https://example.invalid/{z}/{x}/{y}.jpg", response.Tiles[0]);
            Assert.IsNull(response.VectorLayers);
            Assert.IsNull(response.Format, "Legacy raster metadata may omit format");
            Assert.AreEqual("https://example.invalid/raster", response.WebPage);
        }

        [Test]
        public void MapboxEmerald()
        {
            const string id = "mapbox.emerald";
            var response = Get(id, MapboxResponseFixtures.Emerald);
            AssertCommon(response, id);
            AssertVector(response);
            Assert.AreEqual(id, response.Id);
            Assert.AreEqual("https://example.invalid/emerald", response.WebPage);
        }

        [Test]
        public void NullDocumentReturnsNull()
        {
            Assert.IsNull(Get("test.empty", "null"));
        }

        [Test]
        public void MalformedResponseIsRejected()
        {
            var source = new MapboxResponseFixtureSource();
            int callbacks = 0;
            new TileJSON(source, 17).Get("test.invalid", response => callbacks++);
            Assert.Throws<Mapbox.Json.JsonSerializationException>(() => source.Respond("/v4/test.invalid.json", "secure", "{"));
            Assert.AreEqual(0, callbacks);
        }
    }
}
#endif
