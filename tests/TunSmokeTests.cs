using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using Basalt.Core;
using Basalt.Windows;

// Run explicitly as administrator. Does not use or change the user's saved profiles.
static class TunSmokeTests
{
    static int Port() { var l = new TcpListener(IPAddress.Loopback, 0); l.Start(); try { return ((IPEndPoint)l.LocalEndpoint).Port; } finally { l.Stop(); } }
    static int Main(string[] args)
    {
        bool proxyOnly = args.Length == 1 && args[0] == "--proxy-only";
        string root = AppDomain.CurrentDomain.BaseDirectory;
        string folder = Path.Combine(root, "tun-smoke-runtime"); Directory.CreateDirectory(folder);
        using (var log = new StreamWriter(Path.Combine(root, proxyOnly ? "proxy-smoke-result.txt" : "tun-smoke-result.txt"), false))
        {
            log.AutoFlush = true; Process server = null; CoreRuntime runtime = null;
            try
            {
                log.WriteLine("UTC: " + DateTime.UtcNow.ToString("o") + " OS compatibility API (not winver): " + Environment.OSVersion);
                if (!proxyOnly && !TunnelSession.IsAdministrator) throw new Exception("Administrator required. No network changes performed.");
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                CoreCatalog.Verify(); var settings = new Settings { TunMode = !proxyOnly, ProxyPort = Port(), HttpPort = Port(), BridgePort = Port() }; CoreCatalog.Configure(settings);
                int serverPort = Port(), vlessPort = Port(); string uuid = Guid.NewGuid().ToString(); string secret = Guid.NewGuid().ToString("N");
                string configPath = Path.Combine(folder, "server.json");
                File.WriteAllText(configPath, Json.Write(Json.Obj("inbounds", new[] { Json.Obj("type", "shadowsocks", "listen", "127.0.0.1", "listen_port", serverPort, "method", "aes-128-gcm", "password", secret), Json.Obj("type", "vless", "listen", "127.0.0.1", "listen_port", vlessPort, "users", new[] { Json.Obj("uuid", uuid) }) }, "outbounds", new[] { Json.Obj("type", "direct", "tag", "direct") }, "route", Json.Obj("auto_detect_interface", true, "final", "direct"))));
                server = Process.Start(new ProcessStartInfo(settings.SingBoxPath, "run -c \"" + configPath + "\"") { UseShellExecute = false, CreateNoWindow = true });
                Thread.Sleep(750); if (server.HasExited) throw new Exception("Local test server failed to start");
                runtime = new CoreRuntime(Path.Combine(folder, "client")); runtime.Log += log.WriteLine;
                var profile = Subscriptions.ParseUri("ss://" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("aes-128-gcm:" + secret)) + "@127.0.0.1:" + serverPort + "#Local-smoke");
                var vless = Subscriptions.ParseUri("vless://" + uuid + "@127.0.0.1:" + vlessPort + "?security=none#Local-dual-core");
                var happNative = Json.Serializer().Deserialize<Profile>(Json.Write(profile)); happNative.ProviderFormat="happ"; happNative.ProviderConfig=Json.Write(Json.Obj("GlobalProxy","true","DirectSites",new[]{"domain:example.com"},"DirectIp",new[]{"geoip:private"},"BlockSites",new[]{"domain:blocked.example.invalid"},"RemoteDNSType","DoU","RemoteDNSIP","1.1.1.1"));
                var happXray = Json.Serializer().Deserialize<Profile>(Json.Write(vless)); happXray.ProviderFormat="happ"; happXray.ProviderConfig=happNative.ProviderConfig;
                var nativeFull = Subscriptions.Parse(Json.Write(Json.Obj("outbounds", new object[]{Configuration.Outbound(vless), Json.Obj("tag","direct","protocol","freedom")}, "dns",Json.Obj("servers",new[]{"1.1.1.1","1.0.0.1"}), "routing",Json.Obj("domainStrategy","IPIfNonMatch","rules",new[]{Json.Obj("type","field","domain",new[]{"domain:2ip.ru"},"outboundTag","direct"),Json.Obj("type","field","network","tcp,udp","outboundTag","proxy")})))).Profiles[0];
                foreach (var current in new[] { profile, vless, happNative, happXray, nativeFull })
                for (int cycle = 0; cycle < 2; cycle++)
                {
                    runtime.Start(current, settings);
                    log.WriteLine("PASS " + current.Protocol + " / " + (current.ProviderFormat ?? "local") + " runtime ready, cycle " + cycle);
                    log.WriteLine(CoreRuntime.Probe(settings.ProxyPort, null, null, !proxyOnly));
                    if (proxyOnly) { runtime.Stop(); log.WriteLine("PASS local encrypted proxy HTTPS and reconnect"); continue; }
                    using (var udp = new UdpClient())
                    {
                        udp.Client.ReceiveTimeout = 5000;
                        byte[] query = new byte[] { 0xBA, 0x5A, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 7, 101, 120, 97, 109, 112, 108, 101, 3, 99, 111, 109, 0, 0, 1, 0, 1 };
                        udp.Send(query, query.Length, new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53));
                        IPEndPoint from = null; byte[] answer = udp.Receive(ref from);
                        if (answer.Length < 12 || answer[0] != 0xBA || answer[1] != 0x5A || (answer[2] & 128) == 0 || (answer[3] & 15) != 0 || answer[7] == 0) throw new Exception("Invalid DNS response through TUN");
                    }
                    log.WriteLine("PASS UDP DNS through TUN");
                    // Inspect only the adapter owned by this runtime journal.
                    var journal = Json.Read(File.ReadAllText(Path.Combine(folder, "client", "tun-recovery.json")));
                    string name = (string)journal["name"]; NetworkInterface active = null;
                    foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces()) if (adapter.Name == name) active = adapter;
                    if (active == null || active.GetIPv4Statistics().BytesReceived == 0 || active.GetIPv4Statistics().BytesSent == 0) throw new Exception("No bidirectional TUN traffic recorded");
                    log.WriteLine("PASS bidirectional TUN traffic"); runtime.Stop();
                    foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces()) if (adapter.Name == name && adapter.OperationalStatus == OperationalStatus.Up) throw new Exception("TUN adapter remains active after stop");
                    if (File.Exists(Path.Combine(folder, "client", "tun-recovery.json"))) throw new Exception("Recovery journal remains after stop");
                    log.WriteLine("PASS adapter shutdown and journal cleanup");
                }
                log.WriteLine(proxyOnly ? "PASS proxy smoke test" : "PASS TUN smoke test: local encrypted server, HTTPS without application proxy, traffic counters, reconnect and cleanup."); return 0;
            }
            catch (Exception error) { log.WriteLine("FAIL " + error); return 1; }
            finally
            {
                if (runtime != null) try { runtime.Dispose(); log.WriteLine("Client cleanup complete"); } catch (Exception e) { log.WriteLine("CLEANUP ERROR " + e); }
                if (server != null) { try { if (!server.HasExited) { server.Kill(); server.WaitForExit(3000); } } finally { server.Dispose(); } }
                string config = Path.Combine(folder, "server.json"); if (File.Exists(config)) File.Delete(config);
            }
        }
    }
}
