using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.Win32;
using Basalt.Core;

namespace Basalt.Windows
{
    public sealed class WindowsSecrets : ISecretProtector
    {
        public byte[] Protect(byte[] value) { return ProtectedData.Protect(value, null, DataProtectionScope.CurrentUser); }
        public byte[] Unprotect(byte[] value) { return ProtectedData.Unprotect(value, null, DataProtectionScope.CurrentUser); }
    }
    public sealed class ProxySnapshot
    {
        public Dictionary<string, object> Values { get; set; }
        public string InstalledServer { get; set; }
    }
    public sealed class WindowsProxy
    {
        const string Key = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
        readonly string journal;
        static readonly string[] Names = { "ProxyEnable", "ProxyServer", "ProxyOverride", "AutoConfigURL" };
        [DllImport("wininet.dll", SetLastError = true)] static extern bool InternetSetOption(IntPtr handle, int option, IntPtr buffer, int length);
        public WindowsProxy(string dataPath) { journal = Path.Combine(dataPath, "proxy-recovery.json"); }
        static void Notify() { InternetSetOption(IntPtr.Zero, 39, IntPtr.Zero, 0); InternetSetOption(IntPtr.Zero, 37, IntPtr.Zero, 0); }
        public bool Recover()
        {
            if (!File.Exists(journal)) return false;
            var snapshot = Json.Serializer().Deserialize<ProxySnapshot>(File.ReadAllText(journal));
            using (var key = Registry.CurrentUser.OpenSubKey(Key, true))
            {
                // Do not overwrite proxy settings changed by the user or another application.
                if ((string)key.GetValue("ProxyServer", "") == snapshot.InstalledServer)
                {
                    foreach (string name in Names)
                    {
                        object value;
                        if (snapshot.Values.TryGetValue(name, out value) && value != null)
                            key.SetValue(name, name == "ProxyEnable" ? (object)Convert.ToInt32(value) : (object)Convert.ToString(value), name == "ProxyEnable" ? RegistryValueKind.DWord : RegistryValueKind.String);
                        else key.DeleteValue(name, false);
                    }
                    Notify();
                }
            }
            File.Delete(journal); return true;
        }
        public void Enable(int port)
        {
            Recover();
            using (var key = Registry.CurrentUser.OpenSubKey(Key, true))
            {
                // PAC/WPAD cannot safely be represented by ProxyEnable alone; refuse ambiguity.
                if (!String.IsNullOrEmpty((string)key.GetValue("AutoConfigURL", ""))) throw new InvalidOperationException(L10n.T("В Windows настроен PAC. Используйте режим «Локальный прокси» или отключите PAC вручную."));
                var snapshot = new ProxySnapshot { Values = new Dictionary<string, object>(), InstalledServer = "http=127.0.0.1:" + port + ";https=127.0.0.1:" + port };
                foreach (string name in Names) snapshot.Values[name] = key.GetValue(name);
                SettingsStore.AtomicWrite(journal, System.Text.Encoding.UTF8.GetBytes(Json.Write(snapshot)));
                key.SetValue("ProxyServer", snapshot.InstalledServer, RegistryValueKind.String);
                key.SetValue("ProxyOverride", "<local>", RegistryValueKind.String);
                key.SetValue("ProxyEnable", 1, RegistryValueKind.DWord); Notify();
            }
        }
    }
    // Closing the job kills owned cores even if Basalt crashes. No process-name-wide termination.
    sealed class ProcessJob : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)] struct BasicLimits { public long PerProcess, PerJob; public uint Flags; public UIntPtr Minimum, Maximum; public uint Active; public UIntPtr Affinity; public uint Priority, Scheduling; }
        [StructLayout(LayoutKind.Sequential)] struct IoCounters { public ulong ReadOps, WriteOps, OtherOps, ReadBytes, WriteBytes, OtherBytes; }
        [StructLayout(LayoutKind.Sequential)] struct ExtendedLimits { public BasicLimits Basic; public IoCounters Io; public UIntPtr ProcessMemory, JobMemory, PeakProcess, PeakJob; }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr CreateJobObject(IntPtr attributes, string name);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetInformationJobObject(IntPtr job, int infoClass, ref ExtendedLimits info, uint size);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        IntPtr handle;
        public ProcessJob()
        {
            handle = CreateJobObject(IntPtr.Zero, null);
            var limits = new ExtendedLimits(); limits.Basic.Flags = 0x2000;
            if (handle == IntPtr.Zero || !SetInformationJobObject(handle, 9, ref limits, (uint)Marshal.SizeOf(typeof(ExtendedLimits)))) { Dispose(); throw new InvalidOperationException(L10n.T("Не удалось создать контейнер процессов Windows.")); }
        }
        public void Add(Process process) { if (!AssignProcessToJobObject(handle, process.Handle)) throw new InvalidOperationException(L10n.T("Windows отказала в контроле процесса ядра.")); }
        public void Dispose() { if (handle != IntPtr.Zero) { CloseHandle(handle); handle = IntPtr.Zero; } }
    }
    public sealed class CoreRuntime : IDisposable
    {
        readonly List<Process> processes = new List<Process>();
        readonly string directory;
        ProcessJob job;
        readonly TunnelSession tunnelSession;
        public event Action<string> Log;
        public bool Running { get { return processes.Count > 0; } }
        public CoreRuntime(string directory) { this.directory = directory; tunnelSession = new TunnelSession(directory); }
        void Write(string text) { var handler = Log; if (handler != null) handler(text); }
        static string Quote(string path) { return "\"" + path + "\""; }
        string routeAssetDirectory;
        Process Launch(string executable, string arguments)
        {
            if (!File.Exists(executable)) throw new FileNotFoundException(L10n.T("Не найдено ядро. Укажите путь на странице «Настройки»."), executable);
            var p = new Process { StartInfo = new ProcessStartInfo(executable, arguments) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true, WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(executable)) } };
            if (!String.IsNullOrEmpty(routeAssetDirectory)) p.StartInfo.EnvironmentVariables["XRAY_LOCATION_ASSET"] = routeAssetDirectory;
            // Core output may contain remote addresses or credentials. Keep it out of general logs.
            p.OutputDataReceived += delegate { }; p.ErrorDataReceived += delegate { };
            try { p.Start(); job.Add(p); p.BeginOutputReadLine(); p.BeginErrorReadLine(); return p; }
            catch { try { if (!p.HasExited) p.Kill(); } catch (InvalidOperationException) { } p.Dispose(); throw; }
        }
        void Check(string executable, string arguments)
        {
            using (var p = Launch(executable, arguments))
            {
                if (!p.WaitForExit(15000)) { p.Kill(); throw new TimeoutException(L10n.T("Ядро не завершило проверку за 15 секунд.")); }
                if (p.ExitCode != 0) throw new InvalidOperationException(Path.GetFileName(executable) + L10n.T(" отклонил конфигурацию (код ") + p.ExitCode + L10n.T("). Проверьте версию ядра и параметры профиля."));
            }
        }
        static void EnsureFree(int port)
        {
            // Binding a temporary exclusive socket mistakes TIME_WAIT for an active listener
            // and can prevent reconnects. The real core remains the final bind authority.
            for (int attempt = 0; attempt < 10; attempt++)
            {
                bool occupied = false;
                foreach (var endpoint in System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners())
                    if (endpoint.Port == port && (endpoint.Address.Equals(IPAddress.Loopback) || endpoint.Address.Equals(IPAddress.Any) || endpoint.Address.Equals(IPAddress.IPv6Any))) occupied = true;
                if (!occupied) return;
                Thread.Sleep(100);
            }
            throw new SocketException(10048);
        }
        static bool Listening(int port)
        {
            using (var client = new TcpClient())
            {
                IAsyncResult pending = client.BeginConnect(IPAddress.Loopback, port, null, null);
                using (pending.AsyncWaitHandle) { if (!pending.AsyncWaitHandle.WaitOne(150)) return false; }
                try { client.EndConnect(pending); return true; } catch (SocketException) { return false; }
            }
        }
        void Ready(Process process, int port)
        {
            for (int i = 0; i < 50; i++)
            {
                if (process.HasExited) throw new InvalidOperationException(L10n.T("Ядро завершилось при запуске (код ") + process.ExitCode + ").");
                if (Listening(port)) return;
                Thread.Sleep(100);
            }
            throw new TimeoutException(L10n.T("Ядро не открыло локальный порт."));
        }
        public void Start(Profile profile, Settings settings)
        {
            profile = ProviderRouting.Resolve(profile,settings);
            Stop();
            try
            {
                TunnelEnvironment tunnel = settings.TunMode ? tunnelSession.Prepare(profile, settings) : null;
                routeAssetDirectory = settings.UseSubscriptionRouting ? profile.RouteAssetDirectory : null; RoutingAssets.Verify(routeAssetDirectory); if (settings.UseSubscriptionRouting) RoutingAssets.VerifyRules(profile);
                var plan = Configuration.Build(profile, settings, tunnel);
                Directory.CreateDirectory(directory); job = new ProcessJob(); EnsureFree(settings.ProxyPort); EnsureFree(settings.HttpPort);
                if (plan.XrayJson != null) EnsureFree(settings.BridgePort); if (plan.NativeBridge) EnsureFree(settings.NativeBridgePort);
                string sb = Path.Combine(directory, "sing-box.json"), xr = Path.Combine(directory, "xray.json");
                File.WriteAllText(sb, plan.SingBoxJson);
                if (plan.XrayJson != null) { File.WriteAllText(xr, plan.XrayJson); Check(settings.XrayPath, "run -test -config " + Quote(xr)); }
                Check(settings.SingBoxPath, "check -c " + Quote(sb));
                if (plan.XrayJson != null)
                {
                    var xray = Launch(settings.XrayPath, "run -config " + Quote(xr)); processes.Add(xray); Ready(xray, settings.BridgePort); Write(L10n.T("Xray: локальный мост готов."));
                }
                var singbox = Launch(settings.SingBoxPath, "run -c " + Quote(sb)); processes.Add(singbox); Ready(singbox, settings.ProxyPort);
                Ready(singbox, settings.HttpPort);
                if (tunnel != null) { tunnelSession.WaitReady(tunnel.InterfaceName); if (HasFailed()) throw new IOException(L10n.T("Ядро завершилось при включении TUN.")); Write(L10n.T("TUN-адаптер активен. Доступ в интернет проверяется отдельно.")); }
                Write(L10n.T("sing-box: локальный прокси готов. Доступ в интернет ещё не проверен."));
            }
            catch { Stop(); throw; }
        }
        public void Validate(Profile profile, Settings settings)
        {
            profile = ProviderRouting.Resolve(profile,settings);
            Stop();
            try
            {
                routeAssetDirectory = settings.UseSubscriptionRouting ? profile.RouteAssetDirectory : null; RoutingAssets.Verify(routeAssetDirectory); if (settings.UseSubscriptionRouting) RoutingAssets.VerifyRules(profile);
                var plan = Configuration.Build(profile, settings); Directory.CreateDirectory(directory); job = new ProcessJob();
                string sb = Path.Combine(directory, "sing-box.json"), xr = Path.Combine(directory, "xray.json");
                File.WriteAllText(sb, plan.SingBoxJson); Check(settings.SingBoxPath, "check -c " + Quote(sb));
                if (plan.XrayJson != null) { File.WriteAllText(xr, plan.XrayJson); Check(settings.XrayPath, "run -test -config " + Quote(xr)); }
            }
            finally { Stop(); }
        }
        public bool HasFailed() { foreach (var p in processes) if (p.HasExited) return true; return false; }
        public void Stop()
        {
            if (job != null) { job.Dispose(); job = null; }
            foreach (var p in processes) { try { if (!p.HasExited) { p.Kill(); p.WaitForExit(3000); } } catch (InvalidOperationException) { } finally { p.Dispose(); } }
            processes.Clear();
            tunnelSession.Recover();
            foreach (string name in new[] { "sing-box.json", "xray.json" }) { string path = Path.Combine(directory, name); if (File.Exists(path)) File.Delete(path); }
        }
        public void Dispose() { Stop(); }
        public static string Probe(int port) { return Probe(port, null, null, false); }
        public static string Probe(int port, string username, string password, bool tunnel)
        { return (tunnel ? L10n.T("HTTPS через TUN: ") : L10n.T("HTTPS через прокси: ")) + Measure(port, username, password, 12000, tunnel) + L10n.T(" мс. Маршрут зависит от правил."); }
        public static int Measure(int port, string username, string password, int timeout, bool tunnel)
        {
            var timer = Stopwatch.StartNew();
            var request = (HttpWebRequest)WebRequest.Create("https://www.gstatic.com/generate_204");
            if (tunnel) request.Proxy = null;
            else { var proxy = new WebProxy("http://127.0.0.1:" + port); if (!String.IsNullOrEmpty(username)) proxy.Credentials = new NetworkCredential(username, password); request.Proxy = proxy; }
            request.AllowAutoRedirect = false; request.KeepAlive = false; request.ConnectionGroupName = Guid.NewGuid().ToString("N");
            request.Timeout = timeout; request.ReadWriteTimeout = timeout;
            try { using (var response = (HttpWebResponse)request.GetResponse()) { if ((int)response.StatusCode != 204) throw new IOException(L10n.T("Контрольный адрес вернул неожиданный ответ.")); } }
            finally { request.Abort(); request.ServicePoint.CloseConnectionGroup(request.ConnectionGroupName); }
            return (int)timer.ElapsedMilliseconds;
        }
    }
}
