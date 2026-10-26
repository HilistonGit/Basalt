using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Basalt.Core;

namespace Basalt.Windows
{
    public static class CoreCatalog
    {
        public static string Architecture { get { return Environment.Is64BitOperatingSystem && Directory.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cores", "windows-legacy-amd64")) ? "amd64" : "386"; } }
        public static string DirectoryPath { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cores", "windows-legacy-" + Architecture); } }
        public static void Configure(Settings settings) { settings.XrayPath = Path.Combine(DirectoryPath, "xray.exe"); settings.SingBoxPath = Path.Combine(DirectoryPath, "sing-box.exe"); }
        public static string Versions
        {
            get
            {
                string path = Path.Combine(DirectoryPath, "manifest.json");
                if (!File.Exists(path)) return L10n.T("Пакет ядер не установлен");
                try
                {
                    var manifest = Json.Read(File.ReadAllText(path));
                    if (!manifest.ContainsKey("xrayVersion") || !manifest.ContainsKey("singBoxVersion")) return L10n.T("Старый пакет ядер — обновите Basalt целиком");
                    return "Xray " + manifest["xrayVersion"] + " + sing-box " + manifest["singBoxVersion"];
                }
                catch (Exception) { return L10n.T("Не удалось прочитать версии ядер"); }
            }
        }
        public static void Verify()
        {
            string manifestPath = Path.Combine(DirectoryPath, "manifest.json");
            if (!File.Exists(manifestPath)) throw new IOException(L10n.T("В сборке отсутствует пакет ядер. Установите полный пакет Basalt с legacy-ядрами."));
            var manifest = Json.Read(File.ReadAllText(manifestPath)); var hashes = (Dictionary<string, object>)manifest["files"];
            if (!manifest.ContainsKey("xrayVersion") || !manifest.ContainsKey("singBoxVersion") || (string)manifest["xrayVersion"] != "26.10.21" || (string)manifest["singBoxVersion"] != "1.14.4")
                throw new IOException(L10n.T("Для Basalt 0.6.7 нужен полный пакет с Xray 26.10.21 и sing-box 1.14.4. Обновите каталог cores вместе с приложением."));
            foreach (string name in new[] { "xray.exe", "sing-box.exe", "wintun.dll", "geoip.dat", "geosite.dat" })
            {
                string path = Path.Combine(DirectoryPath, name); if (!File.Exists(path)) throw new IOException(L10n.T("В пакете отсутствует ") + name);
                using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create())
                    if (!String.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""), (string)hashes[name], StringComparison.OrdinalIgnoreCase)) throw new IOException(L10n.T("Контрольная сумма ") + name + L10n.T(" не совпадает с пакетом."));
            }
        }
    }
    // Only a randomly named adapter recorded BEFORE creation can be recovered.
    // Physical interface DNS/routes are never modified by this adapter.
    public sealed class TunnelSession
    {
        readonly string journal;
        public TunnelSession(string directory) { journal = Path.Combine(directory, "tun-recovery.json"); }
        public static bool IsAdministrator { get { using (var identity = WindowsIdentity.GetCurrent()) return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator); } }
        public TunnelEnvironment Prepare(Profile profile, Settings settings)
        {
            if (!IsAdministrator) throw new InvalidOperationException(L10n.T("Для TUN нужны права администратора."));
            if (Environment.OSVersion.Version.Major < 6 || (Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor < 1)) throw new NotSupportedException(L10n.T("Wintun требует Windows 7 или новее; TUN для XP недоступен."));
            Recover();
            var addresses = new List<string>();
            var resolved = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            foreach (string host in settings.UseSubscriptionRouting ? ProviderRouting.Servers(profile) : new[] { profile.Server })
            {
                if (resolved.ContainsKey(host)) continue;
                var found = new List<string>(); foreach (var ip in Dns.GetHostAddresses(host)) if (settings.IPv6 || ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) found.Add(ip.ToString());
                found.Sort(delegate(string a, string b) { return a.Contains(":").CompareTo(b.Contains(":")); });
                if (found.Count == 0) throw new IOException(L10n.T("Не удалось разрешить endpoint подписки до включения TUN."));
                resolved[host] = found[0]; foreach (var ip in found) if (!addresses.Contains(ip)) addresses.Add(ip);
            }
            if (addresses.Count == 0) throw new IOException(L10n.T("Не удалось разрешить адрес VPN-сервера до включения TUN."));
            string name = "Basalt-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces()) if (adapter.Name == name) throw new IOException(L10n.T("Имя адаптера уже занято."));
            Directory.CreateDirectory(Path.GetDirectoryName(journal));
            SettingsStore.AtomicWrite(journal, Encoding.UTF8.GetBytes(Json.Write(Json.Obj("name", name))));
            return new TunnelEnvironment { InterfaceName = name, ServerAddresses = addresses.ToArray(), ResolvedServers = resolved };
        }
        public void WaitReady(string name)
        {
            for (int i = 0; i < 100; i++)
            {
                foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces()) if (adapter.Name == name && adapter.OperationalStatus == OperationalStatus.Up) return;
                Thread.Sleep(100);
            }
            throw new TimeoutException(L10n.T("TUN-адаптер не перешёл в рабочее состояние."));
        }
        public void Recover()
        {
            if (!File.Exists(journal)) return;
            string name = (string)Json.Read(File.ReadAllText(journal))["name"];
            if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^Basalt-[a-f0-9]{12}$")) throw new IOException(L10n.T("Некорректный журнал восстановления TUN."));
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.Name != name || adapter.OperationalStatus != OperationalStatus.Up) continue;
                if (!IsAdministrator) throw new InvalidOperationException(L10n.T("Запустите Basalt от администратора для восстановления TUN после сбоя."));
                var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "netsh.exe"), "interface set interface name=\"" + name + "\" admin=disabled") { UseShellExecute = false, CreateNoWindow = true };
                using (var process = Process.Start(start)) { if (!process.WaitForExit(10000)) { process.Kill(); throw new IOException(L10n.T("Не удалось завершить восстановление TUN.")); } if (process.ExitCode != 0)
                    {
                        // Wintun can remove the adapter between enumeration and netsh.
                        bool stillUp = false;
                        for (int retry = 0; retry < 10; retry++)
                        {
                            stillUp = false;
                            foreach (var current in NetworkInterface.GetAllNetworkInterfaces()) if (current.Name == name && current.OperationalStatus == OperationalStatus.Up) stillUp = true;
                            if (!stillUp) break; Thread.Sleep(100);
                        }
                        if (stillUp) throw new IOException(L10n.T("Windows не отключила оставшийся TUN-адаптер."));
                    } }
            }
            File.Delete(journal);
        }
    }
}
