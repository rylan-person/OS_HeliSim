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
            this.uri = uri;
            this.callback = callback;
            this.timeout = timeout;
            return this;
        }

        public void Cancel() { callback = null; IsCompleted = true; }

        public void Respond(string expectedPath, string expectedQuery, string json, int status = 200)
        {
            Assert.AreEqual(1, RequestCount, "Expected exactly one request");
            Assert.AreEqual(17, timeout, "Wrapper must forward timeout");
            var actual = new Uri(uri);
            Assert.AreEqual("https", actual.Scheme);
            Assert.AreEqual("api.mapbox.com", actual.Host);
            Assert.AreEqual(expectedPath, actual.AbsolutePath);
            CollectionAssert.AreEquivalent(ParseQuery(expectedQuery), ParseQuery(actual.Query),
                "Unexpected, missing, or duplicated request options");
            Assert.IsNotNull(callback);
            var response = Response.FromCache(Encoding.UTF8.GetBytes(json));
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
