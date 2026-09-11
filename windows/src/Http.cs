// The HTTP layer.
//
// HttpWebRequest rather than HttpClient, because HttpClient arrived in .NET 4.5
// and the floor here is 4.0 so the app can run on Windows 7. Calls are blocking
// and always made from a worker thread; the UI never waits on one.
//
// Every request is checked against the loopback rule before it is sent. The
// product promise is that nothing leaves the machine, and the cheapest way to
// keep a promise like that is to make the transport itself unable to break it.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace ClipSyncAI
{
    /// A handle a caller can use to abort a request in flight, which is how the
    /// stop button on a streaming reply works.
    internal sealed class HttpCall
    {
        internal HttpWebRequest Request;
        private volatile bool _cancelled;

        public bool Cancelled { get { return _cancelled; } }

        public void Cancel()
        {
            _cancelled = true;
            HttpWebRequest r = Request;
            if (r != null)
            {
                try { r.Abort(); }
                catch (Exception) { }
            }
        }
    }

    internal static class Http
    {
        static Http()
        {
            try
            {
                ServicePointManager.Expect100Continue = false;
                ServicePointManager.DefaultConnectionLimit = 8;
                // The cloud engine talks TLS to API hosts; .NET 4.0 only tries
                // TLS 1.0 unless told otherwise. The enum members arrived in
                // 4.5, hence the explicit values.
                try
                {
                    ServicePointManager.SecurityProtocol =
                        (SecurityProtocolType)(192 | 768 | 3072);
                }
                catch (Exception) { }
            }
            catch (Exception) { }
        }

        /// True only for an address that cannot leave this machine. Hostnames
        /// are rejected outright apart from localhost, because resolving a name
        /// is the part that could quietly point somewhere else.
        public static bool IsLoopback(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            Uri u;
            if (!Uri.TryCreate(url, UriKind.Absolute, out u)) return false;
            if (u.Scheme != "http" && u.Scheme != "https") return false;
            string h = u.Host;
            if (string.Equals(h, "localhost", StringComparison.OrdinalIgnoreCase)) return true;
            IPAddress ip;
            if (IPAddress.TryParse(h, out ip)) return IPAddress.IsLoopback(ip);
            return false;
        }

        private static HttpWebRequest Open(string url, string method, int timeoutMs)
        {
            if (!IsLoopback(url))
            {
                throw new InvalidOperationException(
                    "ClipSyncAI only talks to a server on this machine. Refused: " + url);
            }
            HttpWebRequest r = (HttpWebRequest)WebRequest.Create(url);
            r.Method = method;
            r.Timeout = timeoutMs;
            r.ReadWriteTimeout = timeoutMs;
            r.Proxy = null; // never route a loopback call through a system proxy
            r.KeepAlive = true;
            r.UserAgent = "ClipSyncAI/1.0 (Windows)";
            r.Accept = "application/json";
            return r;
        }

        public static string Get(string url, int timeoutMs)
        {
            HttpWebRequest r = Open(url, "GET", timeoutMs);
            using (WebResponse resp = r.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                return sr.ReadToEnd();
            }
        }

        public static string PostJson(string url, string json, int timeoutMs)
        {
            HttpWebRequest r = Open(url, "POST", timeoutMs);
            WriteBody(r, json);
            using (WebResponse resp = r.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                return sr.ReadToEnd();
            }
        }

        private static void WriteBody(HttpWebRequest r, string json)
        {
            byte[] body = Encoding.UTF8.GetBytes(json ?? "");
            r.ContentType = "application/json";
            r.ContentLength = body.Length;
            using (Stream s = r.GetRequestStream()) s.Write(body, 0, body.Length);
        }

        /// Posts and hands back one response line at a time. Both wire formats
        /// the app speaks are line delimited, so a reader that yields lines
        /// covers Ollama's newline delimited JSON and the OpenAI style event
        /// stream without either needing its own transport.
        ///
        /// StreamReader.ReadLine is what makes this correct across chunk
        /// boundaries: a token split down the middle by TCP is rejoined here
        /// rather than being handed onward as broken JSON.
        public static void PostLines(string url, string json, int firstByteTimeoutMs,
            int idleTimeoutMs, HttpCall call, Action<string> onLine)
        {
            HttpWebRequest r = Open(url, "POST", firstByteTimeoutMs);
            r.ReadWriteTimeout = idleTimeoutMs;
            r.Accept = "application/json, text/event-stream";
            if (call != null) call.Request = r;
            WriteBody(r, json);
            using (WebResponse resp = r.GetResponse())
            using (Stream st = resp.GetResponseStream())
            using (StreamReader sr = new StreamReader(st, Encoding.UTF8))
            {
                while (true)
                {
                    if (call != null && call.Cancelled) return;
                    string line = sr.ReadLine();
                    if (line == null) return;
                    if (line.Length == 0) continue;
                    onLine(line);
                }
            }
        }

        /// Reads an error response body, which is where a local server explains
        /// itself. A message like "model not found" is worth showing verbatim.
        public static string BodyOf(WebException ex)
        {
            try
            {
                if (ex == null || ex.Response == null) return "";
                using (Stream s = ex.Response.GetResponseStream())
                using (StreamReader sr = new StreamReader(s, Encoding.UTF8))
                {
                    return sr.ReadToEnd();
                }
            }
            catch (Exception)
            {
                return "";
            }
        }

        // ── Remote-capable requests ─────────────────────────────────────────
        //
        // The two opt-in remote features — the cloud AI engine and the model
        // downloader — are the only code that may talk to a host that is not
        // on this machine. Open() refuses such addresses; the methods below
        // deliberately do not, and they are reachable only from those two
        // callers. Cloud calls carry their own authorization headers.

        public static HttpWebRequest OpenRemote(string url, string method, int timeoutMs)
        {
            HttpWebRequest r = (HttpWebRequest)WebRequest.Create(url);
            r.Method = method;
            r.Timeout = timeoutMs;
            r.ReadWriteTimeout = timeoutMs;
            r.Proxy = null; // route cloud traffic directly, never via a proxy
            r.KeepAlive = true;
            r.UserAgent = "ClipSyncAI/1.0 (Windows)";
            r.Accept = "application/json";
            return r;
        }

        public static void AddHeaders(HttpWebRequest r, IDictionary<string, string> headers)
        {
            if (headers == null) return;
            foreach (KeyValuePair<string, string> kv in headers)
            {
                switch (kv.Key.ToLowerInvariant())
                {
                    case "authorization": r.Headers["Authorization"] = kv.Value; break;
                    case "x-api-key": r.Headers["x-api-key"] = kv.Value; break;
                    case "anthropic-version": r.Headers["anthropic-version"] = kv.Value; break;
                    case "content-type": r.ContentType = kv.Value; break;
                    default: r.Headers[kv.Key] = kv.Value; break;
                }
            }
        }

        public static string GetRemote(string url, IDictionary<string, string> headers, int timeoutMs)
        {
            HttpWebRequest r = OpenRemote(url, "GET", timeoutMs);
            AddHeaders(r, headers);
            using (WebResponse resp = r.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                return sr.ReadToEnd();
            }
        }

        public static string PostRemote(string url, string json,
            IDictionary<string, string> headers, int timeoutMs)
        {
            HttpWebRequest r = OpenRemote(url, "POST", timeoutMs);
            AddHeaders(r, headers);
            WriteBody(r, json);
            using (WebResponse resp = r.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                return sr.ReadToEnd();
            }
        }

        /// Streaming POST for the cloud providers, mirroring PostLines but
        /// without the loopback check and with room for provider headers.
        public static void PostRemoteLines(string url, string json,
            IDictionary<string, string> headers, int firstByteTimeoutMs,
            int idleTimeoutMs, HttpCall call, Action<string> onLine)
        {
            HttpWebRequest r = OpenRemote(url, "POST", firstByteTimeoutMs);
            AddHeaders(r, headers);
            r.ReadWriteTimeout = idleTimeoutMs;
            r.Accept = "application/json, text/event-stream";
            if (call != null) call.Request = r;
            WriteBody(r, json);
            using (WebResponse resp = r.GetResponse())
            using (Stream st = resp.GetResponseStream())
            using (StreamReader sr = new StreamReader(st, Encoding.UTF8))
            {
                while (true)
                {
                    if (call != null && call.Cancelled) return;
                    string line = sr.ReadLine();
                    if (line == null) return;
                    if (line.Length == 0) continue;
                    onLine(line);
                }
            }
        }
    }
}
