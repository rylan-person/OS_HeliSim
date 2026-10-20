//-----------------------------------------------------------------------
// Copyright (c) 2016 Mapbox. All rights reserved.
//-----------------------------------------------------------------------
#if UNITY_5_6_OR_NEWER
namespace Mapbox.MapboxSdkCs.UnitTest
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;
    using Mapbox.Platform;
    using NUnit.Framework;
    using UnityEngine.TestTools;

    [TestFixture]
    internal class FileSourceTest
    {
        private const int RequestTimeoutSeconds = 5;
        private const int WaitTimeoutSeconds = 10;
        private readonly List<IAsyncRequest> requests = new List<IAsyncRequest>();
        private FileSource fileSource;
        private MapboxLoopbackServer server;

        [SetUp]
        public void SetUp()
        {
            requests.Clear();
            server = new MapboxLoopbackServer();
            fileSource = new FileSource(() => "test-sku", "test-access-token");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (IAsyncRequest request in requests)
            {
                if (!request.IsCompleted)
                {
                    request.Cancel();
                }
            }

            // Stop the listener, release delayed responses and join its worker even
            // when an assertion or the bounded request wait failed.
            server.Dispose();
            Stopwatch timer = Stopwatch.StartNew();
            while (requests.Exists(request => !request.IsCompleted) && timer.Elapsed.TotalSeconds < WaitTimeoutSeconds)
            {
                yield return null;
            }

            Assert.IsFalse(requests.Exists(request => !request.IsCompleted), "Canceled transport did not finish during cleanup.");
            Assert.IsNull(server.Error, "Loopback server failed: " + server.Error);
        }

        [UnityTest]
        public IEnumerator Request()
        {
            Response response = null;
            Send("success?existing=retained", result => response = result);
            yield return WaitForRequests();

            Assert.IsNotNull(response, "Transport did not invoke its callback.");
            Assert.IsFalse(response.HasError, response.ExceptionsAsString);
            Assert.AreEqual(200, response.StatusCode);
            Assert.AreEqual("mapbox-loopback-body", Encoding.UTF8.GetString(response.Data));
            AssertRequestQuery("existing=retained", "access_token=test-access-token", "sku=test-sku");
        }

        [UnityTest]
        public IEnumerator MultipleRequests()
        {
            List<Response> responses = new List<Response>();
            Send("success", responses.Add);
            Send("success", responses.Add);
            Send("success", responses.Add);
            yield return WaitForRequests();

            Assert.AreEqual(3, responses.Count, "Each concurrent request must invoke exactly one callback.");
            CollectionAssert.AreEquivalent(requests, responses.ConvertAll(response => response.Request),
                "Every concurrent request must have its own response.");
            foreach (Response response in responses)
            {
                Assert.IsFalse(response.HasError, response.ExceptionsAsString);
                Assert.AreEqual(200, response.StatusCode);
                Assert.AreEqual("mapbox-loopback-body", Encoding.UTF8.GetString(response.Data));
            }
            AssertRequestQuery("access_token=test-access-token", "sku=test-sku");
        }

        [UnityTest]
        public IEnumerator RequestCancel()
        {
            Response response = null;
            int callbackCount = 0;
            IAsyncRequest request = Send("delayed", result =>
            {
                response = result;
                callbackCount++;
            });

            Stopwatch timer = Stopwatch.StartNew();
            while (server.RequestsReceived == 0 && timer.Elapsed.TotalSeconds < RequestTimeoutSeconds)
            {
                yield return null;
            }

            Assert.AreEqual(1, server.RequestsReceived, "Cancellation must exercise an in-flight request.");
            Assert.IsNull(response, "The delayed endpoint must not finish before cancellation.");
            request.Cancel();
            yield return WaitForRequests();

            Assert.AreEqual(1, callbackCount);
            Assert.IsNotNull(response);
            Assert.IsTrue(response.HasError);
            Assert.IsNotEmpty(response.Exceptions);
            StringAssert.Contains("abort", response.ExceptionsAsString.ToLowerInvariant());
            Assert.AreEqual(0, response.StatusCode);
        }

        [UnityTest]
        public IEnumerator RequestTransportError()
        {
            Response response = null;
            // Closing a real TCP connection without an HTTP response exercises the
            // transport failure path without depending on DNS, proxies or wording.
            Send("disconnect", result => response = result);
            yield return WaitForRequests();

            Assert.IsNotNull(response);
            Assert.IsTrue(response.HasError);
            Assert.IsNotEmpty(response.Exceptions);
            Assert.AreEqual(0, response.StatusCode, "A transport failure has no HTTP status.");
        }

        [UnityTest]
        public IEnumerator RequestForbidden()
        {
            Response response = null;
            // The previous mapbox.com/forbidden URL was an uncontrolled website
            // route returning 404, so it could not establish the HTTP 403 contract.
            Send("forbidden", result => response = result);
            yield return WaitForRequests();

            Assert.IsNotNull(response);
            Assert.IsTrue(response.HasError);
            Assert.AreEqual(403, response.StatusCode);
        }

        [UnityTest]
        public IEnumerator WaitWithNoRequests()
        {
            IEnumerator wait = fileSource.WaitForAllRequests();
            Assert.IsFalse(wait.MoveNext(), "An empty request set must finish immediately.");
            yield break;
        }

        private void AssertRequestQuery(params string[] expected)
        {
            Assert.IsNotNull(server.RequestTarget, "The HTTP peer must receive the request target.");
            var target = new Uri(new Uri(server.Url), server.RequestTarget);
            CollectionAssert.AreEquivalent(expected, target.Query.TrimStart('?').Split('&'),
                "FileSource must preserve existing query values and append the synthetic credentials exactly once.");
        }

        private IAsyncRequest Send(string path, Action<Response> callback)
        {
            IAsyncRequest request = fileSource.Request(server.Url + path, callback, RequestTimeoutSeconds);
            requests.Add(request);
            return request;
        }

        private IEnumerator WaitForRequests()
        {
            IEnumerator wait = fileSource.WaitForAllRequests();
            Stopwatch timer = Stopwatch.StartNew();
            // Match the existing EditMode fixture's frame stepping: WaitForSeconds
            // returned by FileSource is a PlayMode yield instruction.
            while (wait.MoveNext())
            {
                Assert.Less(timer.Elapsed.TotalSeconds, WaitTimeoutSeconds, "FileSource.WaitForAllRequests did not finish.");
                yield return null;
            }
        }
    }
}
#endif
