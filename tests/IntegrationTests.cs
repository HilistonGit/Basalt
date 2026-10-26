using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using Basalt.Core;
using Basalt.Windows;

static class IntegrationTests
{
    static void Check(string executable, string arguments)
    {
        using (var process = Process.Start(new ProcessStartInfo(executable, arguments) { UseShellExecute = false, CreateNoWindow = true }))
        { if (!process.WaitForExit(15000)) { process.Kill(); throw new Exception("Configuration check timed out"); } if (process.ExitCode != 0) throw new Exception("Core rejected generated configuration"); }
    }
    static int Main(string[] args)
    {
        string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "integration-runtime");
        Directory.CreateDirectory(folder);
        try
        {
            var settings = new Settings { XrayPath = Path.GetFullPath(args[0]), SingBoxPath = Path.GetFullPath(args[1]), ProxyPort = 42880, BridgePort = 42881 };
            var profile = Subscriptions.ParseUri("trojan://integration-test@example.invalid:443?sni=example.invalid#Integration");
            settings.HttpPort = 42883;
            settings.TunMode = true; settings.ApplicationBypass = "example.exe"; settings.RouteMode = 2; settings.RouteEntries = "*.example.org\n10.0.0.0/8";
            foreach (var testProfile in new[] { profile, Subscriptions.ParseUri("hy2://test@example.invalid:443"), Subscriptions.ParseUri("vless://11111111-1111-4111-8111-111111111111@example.invalid:443?security=tls&type=xhttp#XHTTP") })
            {
                var plan = Configuration.Build(testProfile, settings, new TunnelEnvironment { InterfaceName = "Basalt-test", ServerAddresses = new[] { "192.0.2.1" } });
                string config = Path.Combine(folder, "tun-check.json"); File.WriteAllText(config, plan.SingBoxJson);
                Check(settings.SingBoxPath, "check -c \"" + config + "\"");
                if (plan.XrayJson != null) { var imported = Subscriptions.Parse("[" + plan.XrayJson + "]"); if (imported.Errors.Count != 0 || imported.Profiles.Count != 1) throw new Exception("Xray JSON import failed"); plan = Configuration.Build(imported.Profiles[0], settings, new TunnelEnvironment { InterfaceName = "Basalt-test", ServerAddresses = new[] { "192.0.2.1" } }); File.WriteAllText(config, plan.XrayJson); Check(settings.XrayPath, "run -test -config \"" + config + "\""); }
                File.Delete(config);
            }
            foreach (var input in new[] { FormatFixtures.Clash, FormatFixtures.SingBox, FormatFixtures.XrayHysteria, FormatFixtures.XhttpExtra, FormatFixtures.Happ, FormatFixtures.HappNative, FormatFixtures.HappFake, FormatFixtures.ClashRoutes, FormatFixtures.ClashDnsRoutes, FormatFixtures.SingRoutes, FormatFixtures.XrayRoutes })
            {
                var imported = Subscriptions.Parse(input); if (imported.Errors.Count != 0 || imported.Profiles.Count == 0) throw new Exception(String.Join("; ", imported.Errors.ToArray()));
                foreach (var item in imported.Profiles)
                {
                    var plan = Configuration.Build(item, settings, new TunnelEnvironment { InterfaceName = "Basalt-test", ServerAddresses = new[] { "192.0.2.1" } });
                    string config = Path.Combine(folder, "format-check.json"); File.WriteAllText(config, plan.SingBoxJson);
                    Check(settings.SingBoxPath, "check -c \"" + config + "\"");
                    if (plan.XrayJson != null) { File.WriteAllText(config, plan.XrayJson); Check(settings.XrayPath, "run -test -config \"" + config + "\""); }
                    File.Delete(config);
                }
            }
            settings.TunMode = false; settings.RouteMode = 0;
            using (var runtime = new CoreRuntime(folder))
            {
                runtime.Log += Console.WriteLine;
                runtime.Start(profile, settings);
                if (!runtime.Running || runtime.HasFailed()) throw new Exception("Dual-core startup failed.");
                // Verify an actual SOCKS5 handshake on the public mixed listener.
                using (var client = new TcpClient("127.0.0.1", settings.ProxyPort))
                {
                    client.ReceiveTimeout = 3000; var stream = client.GetStream(); stream.Write(new byte[] { 5, 1, 0 }, 0, 3);
                    if (stream.ReadByte() != 5 || stream.ReadByte() != 0) throw new Exception("Invalid SOCKS handshake.");
                }
                runtime.Stop();
                if (runtime.Running || File.Exists(Path.Combine(folder, "xray.json"))) throw new Exception("Runtime cleanup failed.");
                settings.ProxyPort = 42882;
                var listener = new TcpListener(IPAddress.Loopback, settings.ProxyPort); listener.Start();
                bool rejected = false; try { runtime.Start(profile, settings); } catch (SocketException) { rejected = true; } finally { listener.Stop(); }
                if (!rejected || runtime.Running) throw new Exception("Occupied port rollback failed.");
                settings.ProxyPort = 42880;
                runtime.Start(Subscriptions.ParseUri("hy2://test@example.invalid:443"), settings);
                if (!runtime.Running || runtime.HasFailed()) throw new Exception("Single-core startup failed.");
                runtime.Stop();
            }
            var store = new SettingsStore(Path.Combine(folder, "settings.dat"), new WindowsSecrets());
            settings.Profiles.Add(profile); store.Save(settings); store.Save(settings);
            if (File.ReadAllText(Path.Combine(folder, "settings.dat")).Contains("integration-test")) throw new Exception("Secret persisted in plaintext.");
            if (store.Load().Profiles[0].Secret != "integration-test") throw new Exception("DPAPI roundtrip failed.");
            File.Delete(Path.Combine(folder, "settings.dat"));
            Console.WriteLine("PASS: real core validation, dual-core startup, SOCKS handshake, shutdown, port collision rollback, Hysteria2 startup, encrypted atomic storage.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
