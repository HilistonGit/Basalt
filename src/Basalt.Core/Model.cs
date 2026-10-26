using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace Basalt.Core
{
    public static class Json
    {
        public static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024, RecursionLimit = 64 }; }
        public static string Write(object value) { return Serializer().Serialize(value); }
        public static Dictionary<string, object> Read(string text) { return Serializer().Deserialize<Dictionary<string, object>>(text); }
        public static Dictionary<string, object> Obj(params object[] pairs)
        {
            var result = new Dictionary<string, object>();
            for (int i = 0; i < pairs.Length; i += 2) result.Add((string)pairs[i], pairs[i + 1]);
            return result;
        }
    }
    public sealed class Profile
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Protocol { get; set; }
        public string Server { get; set; }
        public int Port { get; set; }
        public string Secret { get; set; }
        public string SingBoxOutbound { get; set; }
        public string XrayOutbound { get; set; }
        public string ProviderConfig { get; set; }
        public string HappRoutingConfig { get; set; }
        public string ProviderDocumentId { get; set; }
        public string ProviderFormat { get; set; }
        public string SourceTag { get; set; }
        public string RouteAssetDirectory { get; set; }
        public Dictionary<string,string> ManagedRuleSets { get; set; }
        public Dictionary<string, string> Options { get; set; }
        public string SubscriptionId { get; set; }
        public bool Favorite { get; set; }
        public Profile() { Id = Guid.NewGuid().ToString("N"); Options = new Dictionary<string, string>(); }
        public string Option(string key, string fallback) { string v; return Options.TryGetValue(key, out v) ? v : fallback; }
        public string Engine { get { return !String.IsNullOrEmpty(XrayOutbound) ? "Xray + sing-box" : !String.IsNullOrEmpty(SingBoxOutbound) || Protocol == "hysteria2" || Protocol == "ss" ? "sing-box" : "Xray + sing-box"; } }
        public string Identity { get { return Protocol + "|" + Server + "|" + Port + "|" + Secret + "|" + Json.Write(Options) + "|" + XrayOutbound + "|" + SingBoxOutbound; } }
        public override string ToString() { return Name; }
    }
    public sealed class Subscription
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public string UserAgent { get; set; }
        public string HwidMode { get; set; }
        public string Updated { get; set; }
        public long? UploadBytes { get; set; }
        public long? DownloadBytes { get; set; }
        public long? TotalBytes { get; set; }
        public long? ExpireUnix { get; set; }
        public bool AutoUpdate { get; set; }
        public int UpdateIntervalHours { get; set; }
        public string LastAttemptUtc { get; set; }
        public int UpdateFailures { get; set; }
        public string LastUpdateError { get; set; }
        public int UpdateHours { get; set; }
        public string SupportUrl { get; set; }
        public Subscription() { Id = Guid.NewGuid().ToString("N"); UserAgent = "Basalt/0.6.7"; HwidMode = "auto"; AutoUpdate = true; }
        public override string ToString() { return Name; }
    }
    public sealed class Settings
    {
        public bool AutoStart { get; set; }
        public bool AutoConnect { get; set; }
        public string Language { get; set; }
        public string SubscriptionUserAgent { get; set; }
        public string SubscriptionHwidMode { get; set; }
        public bool UseSubscriptionRouting { get; set; }
        public Dictionary<string,string> RouteDocuments { get; set; }
        public void MigrateSubscriptionDefaults()
        {
            if (String.IsNullOrWhiteSpace(SubscriptionUserAgent)) SubscriptionUserAgent = Subscriptions.Count == 0 ? "Happ/1.0" : Subscriptions[0].UserAgent;
            if (String.IsNullOrWhiteSpace(SubscriptionUserAgent)) SubscriptionUserAgent = "Happ/1.0";
            if (String.IsNullOrEmpty(SubscriptionHwidMode)) SubscriptionHwidMode = Subscriptions.Count == 0 ? "auto" : Subscriptions[0].HwidMode;
            if (String.IsNullOrEmpty(SubscriptionHwidMode)) SubscriptionHwidMode = "auto";
        }
        public List<Profile> Profiles { get; set; }
        public List<Subscription> Subscriptions { get; set; }
        public string DeviceId { get; set; }
        public string SelectedId { get; set; }
        public string XrayPath { get; set; }
        public string SingBoxPath { get; set; }
        public int ProxyPort { get; set; }
        public int BridgePort { get; set; }
        public int NativeBridgePort { get; set; }
        public bool SystemProxy { get; set; }
        public int RouteMode { get; set; }
        public string RouteEntries { get; set; }
        public bool TunMode { get; set; }
        public int TunMtu { get; set; }
        public bool IPv6 { get; set; }
        public bool BypassLan { get; set; }
        public string DirectDns { get; set; }
        public string RemoteDns { get; set; }
        public int HttpPort { get; set; }
        public bool AllowLan { get; set; }
        public string ProxyUsername { get; set; }
        public string ProxyPassword { get; set; }
        public string ApplicationBypass { get; set; }
        public int PingTimeout { get; set; }
        public Settings()
        {
            UseSubscriptionRouting = true; NativeBridgePort = 2083; RouteDocuments = new Dictionary<string,string>();
            DeviceId = Guid.NewGuid().ToString("N");
            Profiles = new List<Profile>(); Subscriptions = new List<Subscription>();
            ProxyPort = 2080; BridgePort = 2081; RouteEntries = "";
            TunMtu = 1500; HttpPort = 2082; BypassLan = true; DirectDns = "1.1.1.1"; RemoteDns = "1.1.1.1"; ApplicationBypass = ""; PingTimeout = 3000;
            XrayPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cores", "xray.exe");
            SingBoxPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cores", "sing-box.exe");
        }
    }
    // Storage encryption is injected by the desktop platform; domain code has no Windows dependency.
    public interface ISecretProtector { byte[] Protect(byte[] data); byte[] Unprotect(byte[] data); }
    public sealed class SettingsStore
    {
        readonly string path; readonly ISecretProtector protector;
        public SettingsStore(string path, ISecretProtector protector) { this.path = path; this.protector = protector; }
        public Settings Load()
        {
            if (!File.Exists(path)) return new Settings();
            var s = Json.Serializer().Deserialize<Settings>(Encoding.UTF8.GetString(protector.Unprotect(File.ReadAllBytes(path))));
            if (s == null || s.Profiles == null || s.Subscriptions == null) throw new InvalidDataException(L10n.T("Повреждён файл настроек. Он не будет перезаписан."));
            return s;
        }
        public void Save(Settings settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AtomicWrite(path, protector.Protect(Encoding.UTF8.GetBytes(Json.Write(settings))));
        }
        public static void AtomicWrite(string path, byte[] data)
        {
            string temp = path + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None)) { stream.Write(data, 0, data.Length); stream.Flush(true); }
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        }
    }
}
