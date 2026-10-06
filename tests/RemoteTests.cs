using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using System.Collections.Generic;
using CloudMusicRemote;

class RemoteTests
{
    class Response
    {
        public int Status;
        public string Body;
    }

    static int checks;
    static int port;

    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("Failed: " + name);
        checks++;
        Console.WriteLine("PASS " + name);
    }

    static Response Request(string path, string method, string body, string token, string origin = null, string host = null)
    {
        var request = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + port + path);
        request.Proxy = null;
        request.Method = method;
        request.Timeout = 5000;
        request.KeepAlive = false;
        if (token != null) request.Headers["X-Remote-Token"] = token;
        if (origin != null) request.Headers["Origin"] = origin;
        if (host != null) request.Host = host;
        if (body != null)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            request.ContentType = "application/json";
            request.ContentLength = bytes.Length;
            using (var stream = request.GetRequestStream()) stream.Write(bytes, 0, bytes.Length);
        }
        HttpWebResponse response;
        try { response = (HttpWebResponse)request.GetResponse(); }
        catch (WebException ex)
        {
            if (ex.Response == null) throw;
            response = (HttpWebResponse)ex.Response;
        }
        using (response)
        using (var reader = new StreamReader(response.GetResponseStream()))
        {
            return new Response { Status = (int)response.StatusCode, Body = reader.ReadToEnd() };
        }
    }

    static int Main()
    {
        try
        {
            using (var server = new RemoteServer())
            {
                port = server.Port;
                server.Start();
                var home = Request("/", "GET", null, null);
                Check(home.Status == 200 && home.Body.Contains("/api/control"), "embedded homepage");
                Check(Request("/api/status", "GET", null, null).Status == 401, "status needs credentials");
                Check(Request("/api/status", "GET", null, "incorrect").Status == 401, "incorrect token rejected");
                Check(Request("/api/pair", "POST", "{", null).Status == 400, "malformed JSON");
                Check(Request("/api/pair", "POST", "null", null).Status == 400, "null JSON");
                string pair = "{\"code\":\"" + server.PairCode + "\"}";
                Check(Request("/api/pair", "POST", pair, null, "http://untrusted.invalid").Status == 403, "cross-origin pairing rejected");
                Check(Request("/", "GET", null, null, null, "untrusted.invalid:" + port).Status == 403, "DNS rebinding host rejected");
                var paired = Request("/api/pair", "POST", pair, null);
                Check(paired.Status == 200, "valid pairing");
                var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(paired.Body);
                var token = (string)data["token"];
                Check(Convert.FromBase64String(token).Length == 24, "192-bit access token");
                var status = Request("/api/status", "GET", null, token);
                Check(status.Status == 200 && status.Body.Contains("running"), "authenticated status");
                Check(Request("/api/control", "GET", null, token).Status == 404, "GET cannot control playback");
                Check(Request("/api/control", "POST", "{}", token).Status == 400, "missing action");
                Check(Request("/api/control", "POST", "{\"action\":\"unsupported\"}", token).Status == 400, "unsupported action");
                Check(Request("/api/control", "POST", "{\"action\":\"next\"}", null).Status == 401, "unauthorized control rejected before dispatch");
                for (int i = 0; i < 6; i++)
                {
                    Check(Request("/api/pair", "POST", "{\"code\":\"invalid\"}", null).Status == 403, "incorrect pairing attempt " + (i + 1));
                }
                Check(Request("/api/pair", "POST", pair, null).Status == 429, "pairing rate limit");
                Check(Request("/api/status", "GET", null, token).Status == 200, "existing session survives pairing rate limit");
            }
            Check(RemoteServer.Private(IPAddress.Parse("127.0.0.1")), "loopback allowed");
            Check(RemoteServer.Private(IPAddress.Parse("192.168.1.1")), "private network allowed");
            Check(RemoteServer.Private(IPAddress.Parse("172.16.0.1")), "private range lower boundary");
            Check(RemoteServer.Private(IPAddress.Parse("172.31.255.254")), "private range upper boundary");
            Check(!RemoteServer.Private(IPAddress.Parse("172.32.0.1")), "public boundary rejected");
            Check(!RemoteServer.Private(IPAddress.Parse("8.8.8.8")), "public IP rejected");
            Check(!RemoteServer.Private(IPAddress.IPv6Loopback), "IPv6 unsupported");
            Console.WriteLine(checks + " checks passed; no real playback or firewall changes.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
