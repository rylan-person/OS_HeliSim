#if UNITY_5_6_OR_NEWER
namespace Mapbox.MapboxSdkCs.UnitTest
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;

    // Test-only HTTP peer. TcpListener avoids HttpListener URL ACL configuration;
    // port zero reserves an available loopback port for the lifetime of the test.
    internal sealed class MapboxLoopbackServer : IDisposable
    {
        private readonly TcpListener listener;
        private readonly Thread worker;
        private readonly ManualResetEvent stopping = new ManualResetEvent(false);
        private readonly object clientLock = new object();
        private TcpClient activeClient;
        private int requestsReceived;
        private volatile bool disposed;
        private volatile Exception error;
        private volatile string requestTarget;

        public string Url { get; private set; }
        public int RequestsReceived { get { return Volatile.Read(ref requestsReceived); } }
        public Exception Error { get { return error; } }
        public string RequestTarget { get { return requestTarget; } }

        public MapboxLoopbackServer()
        {
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            Url = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/";
            worker = new Thread(Serve) { IsBackground = true, Name = "Mapbox test HTTP peer" };
            worker.Start();
        }

        private void Serve()
        {
            try
            {
                while (!disposed)
                {
                    using (TcpClient client = listener.AcceptTcpClient())
                    {
                        lock (clientLock)
                        {
                            if (disposed)
                            {
                                return;
                            }
                            activeClient = client;
                        }

                        client.ReceiveTimeout = 2000;
                        client.SendTimeout = 2000;
                        using (NetworkStream stream = client.GetStream())
                        using (StreamReader reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true))
                        {
                            string requestLine = reader.ReadLine();
                            if (requestLine == null)
                            {
                                throw new IOException("Client closed before sending a request.");
                            }

                            string header;
                            do
                            {
                                header = reader.ReadLine();
                            }
                            while (!string.IsNullOrEmpty(header));

                            requestTarget = requestLine.Split(' ')[1];
                            Interlocked.Increment(ref requestsReceived);
                            string path = requestTarget.Split('?')[0];
                            if (path == "/delayed")
                            {
                                // Teardown releases this endpoint and closes the socket;
                                // cancellation cannot race a successful response.
                                stopping.WaitOne();
                            }
                            else if (path != "/disconnect")
                            {
                                bool forbidden = path == "/forbidden";
                                byte[] body = Encoding.UTF8.GetBytes(forbidden ? "forbidden" : "mapbox-loopback-body");
                                string status = forbidden ? "403 Forbidden" : "200 OK";
                                byte[] headers = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + "\r\nContent-Type: text/plain\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n");
                                stream.Write(headers, 0, headers.Length);
                                stream.Write(body, 0, body.Length);
                            }
                        }

                        lock (clientLock)
                        {
                            activeClient = null;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                if (!disposed)
                {
                    error = exception;
                }
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            stopping.Set();
            listener.Stop();
            lock (clientLock)
            {
                if (activeClient != null)
                {
                    activeClient.Close();
                }
            }

            if (!worker.Join(3000))
            {
                throw new TimeoutException("Mapbox loopback worker did not stop.");
            }
            stopping.Dispose();
        }
    }
}
#endif
