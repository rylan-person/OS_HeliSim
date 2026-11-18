#if UNITY_5_6_OR_NEWER
namespace Mapbox.MapboxSdkCs.UnitTest
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Mapbox.Map;
    using Mapbox.Platform;
    using Mapbox.Unity.Utilities;
    using NUnit.Framework;

    // A single explicitly expected request. Nothing is dispatched until the test has
    // checked the URL and timeout; callbacks contain no NUnit assertions.
    internal sealed class MapboxResponseFixtureSource : IFileSource, IAsyncRequest
    {
        private Action<Response> callback;
        private string uri;
        private int timeout;
        public int RequestCount { get; private set; }
        public bool IsCompleted { get; private set; }
        public HttpRequestType RequestType { get { return HttpRequestType.Get; } }

        public IAsyncRequest Request(string uri, Action<Response> callback, int timeout = 10,
            CanonicalTileId tileId = new CanonicalTileId(), string tilesetId = null)
        {
            RequestCount++;
            IsCompleted = false;
            this.uri = uri;
            this.callback = callback;
            this.timeout = timeout;
            return this;
        }

        public void Cancel() { callback = null; IsCompleted = true; }

        public static byte[] MakeRasterFixture(int seed)
        {
            var texture = new UnityEngine.Texture2D(32, 32, UnityEngine.TextureFormat.RGB24, false);
            try
            {
                byte[] pixels = new byte[32 * 32 * 3];
                new System.Random(seed).NextBytes(pixels);
                texture.LoadRawTextureData(pixels);
                texture.Apply();
                return UnityEngine.ImageConversion.EncodeToPNG(texture);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        public void RespondTile(string expectedPath, byte[] data, int ordinal, int status = 200)
        {
            Assert.AreEqual(ordinal, RequestCount);
            Assert.AreEqual(10, timeout);
            RespondBytes(expectedPath, "", data, status);
        }

        public void Respond(string expectedPath, string expectedQuery, string json, int status = 200)
        {
            Assert.AreEqual(1, RequestCount, "Expected exactly one request");
            Assert.AreEqual(17, timeout, "Wrapper must forward timeout");
            RespondBytes(expectedPath, expectedQuery, Encoding.UTF8.GetBytes(json), status);
        }

        private void RespondBytes(string expectedPath, string expectedQuery, byte[] data, int status)
        {
            var actual = new Uri(uri);
            Assert.AreEqual("https", actual.Scheme);
            Assert.AreEqual("api.mapbox.com", actual.Host);
            Assert.AreEqual(expectedPath, actual.AbsolutePath);
            CollectionAssert.AreEquivalent(ParseQuery(expectedQuery), ParseQuery(actual.Query),
                "Unexpected, missing, or duplicated request options");
            Assert.IsNotNull(callback);
            var response = Response.FromCache(data);
            response.StatusCode = status;
            if (status != 200) { response.AddException(new Exception("Fixture HTTP " + status)); }
            IsCompleted = true;
            var pending = callback;
            callback = null;
            pending(response);
        }

        private static List<string> ParseQuery(string query)
        {
            var result = new List<string>();
            foreach (var item in query.TrimStart('?').Split('&'))
            {
                if (item.Length > 0) { result.Add(Uri.UnescapeDataString(item)); }
            }
            return result;
        }
    }
}
#endif
