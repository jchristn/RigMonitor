namespace Test.Shared
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Minimal loopback HTTP server for integration tests. Responds from a path-to-response map and records the
    /// traceparent header of every request so tests can verify W3C context propagation.
    /// Thread safety: safe for concurrent requests.
    /// </summary>
    public class StubHttpServer : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Base URL, for example http://127.0.0.1:54321.
        /// </summary>
        public string BaseUrl
        {
            get
            {
                return _BaseUrl;
            }
        }

        /// <summary>
        /// traceparent header values received, in arrival order (null when absent).
        /// </summary>
        public List<string?> TraceParents
        {
            get
            {
                return _TraceParents.ToList();
            }
        }

        #endregion

        #region Private-Members

        private readonly HttpListener _Listener;
        private readonly string _BaseUrl;
        private readonly Dictionary<string, KeyValuePair<int, string>> _Routes;
        private readonly ConcurrentQueue<string?> _TraceParents = new ConcurrentQueue<string?>();
        private readonly CancellationTokenSource _TokenSource = new CancellationTokenSource();
        private readonly Task _AcceptTask;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Start the server.
        /// </summary>
        /// <param name="routes">Map of path to status code and body. Unknown paths return 404.</param>
        public StubHttpServer(Dictionary<string, KeyValuePair<int, string>> routes)
        {
            _Routes = routes ?? new Dictionary<string, KeyValuePair<int, string>>();
            int port = GetFreePort();
            _BaseUrl = "http://127.0.0.1:" + port;
            _Listener = new HttpListener();
            _Listener.Prefixes.Add(_BaseUrl + "/");
            _Listener.Start();
            _AcceptTask = Task.Run(() => AcceptLoopAsync(_TokenSource.Token));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Find a free loopback TCP port.
        /// </summary>
        /// <returns>Port number.</returns>
        public static int GetFreePort()
        {
            TcpListener probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        /// <summary>
        /// Stop the server.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Stop the listener.
        /// </summary>
        /// <param name="disposing">True when called from Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;
            _Disposed = true;
            if (disposing)
            {
                _TokenSource.Cancel();
                try
                {
                    _Listener.Stop();
                    _Listener.Close();
                }
                catch (Exception)
                {
                }

                _TokenSource.Dispose();
            }
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _Listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                    return;
                }

                _TraceParents.Enqueue(context.Request.Headers["traceparent"]);
                string path = context.Request.Url?.AbsolutePath ?? "/";
                KeyValuePair<int, string> response = _Routes.TryGetValue(path, out KeyValuePair<int, string> found)
                    ? found
                    : new KeyValuePair<int, string>(404, "not found");

                try
                {
                    byte[] body = Encoding.UTF8.GetBytes(response.Value);
                    context.Response.StatusCode = response.Key;
                    context.Response.ContentType = response.Value.StartsWith("{", StringComparison.Ordinal) ? "application/json" : "text/plain";
                    context.Response.ContentLength64 = body.Length;
                    await context.Response.OutputStream.WriteAsync(body, 0, body.Length, token).ConfigureAwait(false);
                    context.Response.Close();
                }
                catch (Exception)
                {
                }
            }
        }

        #endregion
    }
}
