static class FormatFixtures
{
    public static string HappRoute = "happ://routing/onadd/" + System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"Name\":\"Test\",\"GlobalProxy\":\"true\",\"DirectSites\":[\"domain:example.net\"],\"DirectIp\":[\"192.0.2.0/24\"],\"BlockSites\":[\"full:ads.example.org\"],\"DomainStrategy\":\"IPIfNonMatch\",\"RemoteDNSType\":\"DoU\",\"RemoteDNSIP\":\"1.1.1.1\"}"));
    public static string Happ { get { return HappRoute + "\n" + XhttpExtra; } }
    public static string HappNative = HappRoute + "\nhy2://test@example.org:443";
    public static string HappFake { get { return "happ://routing/onadd/" + System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"GlobalProxy\":\"true\",\"FakeDNS\":\"true\"}")) + "\n" + XhttpExtra; } }
    public static string ClashDnsRoutes { get { return ClashRoutes + "dns:\n  enable: true\n  enhanced-mode: fake-ip\n  nameserver: ['https://cloudflare-dns.com/dns-query']\n  default-nameserver: [1.1.1.1]\n  fake-ip-filter: ['+.example.net']\n  nameserver-policy: {example.org: 8.8.8.8}\n"; } }
    public static string ClashRoutes = Clash + "rules:\n  - DOMAIN-SUFFIX,example.net,DIRECT\n  - DOMAIN,ads.example.org,REJECT\n  - PROCESS-NAME,notepad.exe,DIRECT\n  - MATCH,HY2\n";
    public static string SingRoutes = SingBox.TrimEnd().TrimEnd('}') + ",\"route\":{\"rules\":[{\"domain_suffix\":[\"example.net\"],\"outbound\":\"direct\"},{\"domain\":[\"ads.example.org\"],\"action\":\"reject\"}],\"final\":\"select\"}}";
    public static string XrayRoutes = XrayHysteria.TrimEnd('}') + ",\"routing\":{\"rules\":[{\"type\":\"field\",\"domain\":[\"full:example.net\"],\"outboundTag\":\"HY2\"}]},\"dns\":{\"servers\":[\"1.1.1.1\"]}}";
    public const string XrayHysteria = "{\"outbounds\":[{\"tag\":\"HY2\",\"protocol\":\"hysteria\",\"settings\":{\"version\":2,\"address\":\"example.org\",\"port\":443},\"streamSettings\":{\"network\":\"hysteria\",\"security\":\"tls\",\"tlsSettings\":{\"alpn\":[\"h3\"]},\"hysteriaSettings\":{\"version\":2,\"auth\":\"test-auth\"}}}]}";
    public static string XhttpExtra = "vless://11111111-1111-4111-8111-111111111111@example.org:443?security=tls&type=xhttp&extra=" + System.Uri.EscapeDataString("{\"noGRPCHeader\":true,\"scMaxEachPostBytes\":1000000,\"xmux\":{\"maxConcurrency\":\"16-32\"}}");
    public const string Clash = @"defaults: &tls
  tls: true
  servername: example.org
proxies:
  - <<: *tls
    name: 'VLESS # TLS'
    type: vless
    server: example.org
    port: 443
    uuid: 11111111-1111-4111-8111-111111111111
    network: ws
    ws-opts:
      path: /ws
      headers: {Host: example.org}
  - {name: Trojan, type: trojan, server: example.org, port: 443, password: 'p:a#ss', sni: example.org}
  - {name: SS, type: ss, server: example.org, port: 8388, cipher: aes-128-gcm, password: test}
  - {name: HY2, type: hysteria2, server: example.org, port: 443, password: test, obfs: salamander, obfs-password: secret}
  - {name: VMess, type: vmess, server: example.org, port: 443, uuid: 11111111-1111-4111-8111-111111111111, tls: true, cipher: auto, alterId: 0}
  - {name: TUIC, type: tuic, server: example.org, port: 443, uuid: 11111111-1111-4111-8111-111111111111, password: test, congestion-controller: bbr}
";
    public const string SingBox = @"{""outbounds"": [
      {""type"":""selector"",""tag"":""select"",""outbounds"":[""native""]},
      {""type"":""vless"",""tag"":""native"",""server"":""example.org"",""server_port"":443,""uuid"":""11111111-1111-4111-8111-111111111111"",""tls"":{""enabled"":true},""transport"":{""type"":""grpc"",""service_name"":""vpn""}},
      {""type"":""direct"",""tag"":""direct""}
    ]}";
}
