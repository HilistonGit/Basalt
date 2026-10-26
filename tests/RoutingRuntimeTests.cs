using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Basalt.Core;
using Basalt.Windows;
class RoutingRuntimeTests {
 static int Port() { var s=new TcpListener(IPAddress.Loopback,0);s.Start();int p=((IPEndPoint)s.LocalEndpoint).Port;s.Stop();return p; }
 static void Read(NetworkStream s,byte[] b) { int at=0;while(at<b.Length) { int n=s.Read(b,at,b.Length-at);if(n==0)throw new Exception("EOF");at+=n; } }
 static void Direct(CoreRuntime runtime,Profile profile,Settings settings) {
  var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;
  try { runtime.Start(profile,settings); using(var client=new TcpClient("127.0.0.1",settings.ProxyPort)) { client.ReceiveTimeout=10000;var stream=client.GetStream();stream.Write(new byte[]{5,1,0},0,3);var hello=new byte[2];Read(stream,hello);if(hello[1]!=0)throw new Exception("SOCKS auth failed");byte[] request={5,1,0,1,127,0,0,1,(byte)(port>>8),(byte)port};stream.Write(request,0,request.Length);byte[] reply=new byte[10];Read(stream,reply);if(reply[1]!=0)throw new Exception("Provider direct rule failed: "+profile.ProviderFormat);
   using(var accepted=listener.AcceptTcpClient()) { accepted.GetStream().WriteByte(42);if(stream.ReadByte()!=42)throw new Exception("Wrong direct response"); }
  } Console.WriteLine("PASS actual direct bypass with unreachable proxy: "+profile.ProviderFormat+" / "+profile.Protocol); } finally { runtime.Stop();listener.Stop(); }
 }
 static int Main(string[] args) { try {
  var settings=new Settings{XrayPath=Path.GetFullPath(args[0]),SingBoxPath=Path.GetFullPath(args[1]),ProxyPort=Port(),HttpPort=Port(),BridgePort=Port(),NativeBridgePort=Port(),BypassLan=false};
  string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"routing-runtime");
  string happ=FormatFixtures.HappRoute.Replace("dummy","dummy");
  var profiles=new[]{Subscriptions.Parse(FormatFixtures.Happ.Replace("192.0.2.0/24","127.0.0.1/32")).Profiles[0],Subscriptions.Parse(FormatFixtures.HappNative).Profiles[0],Subscriptions.Parse(FormatFixtures.SingRoutes).Profiles[0],Subscriptions.Parse(FormatFixtures.ClashRoutes).Profiles[0],Subscriptions.Parse(FormatFixtures.XrayRoutes).Profiles[0]};
  using(var runtime=new CoreRuntime(folder)) foreach(var p in profiles) {
   var c=Json.Read(p.ProviderConfig);
   if(p.ProviderFormat=="happ") c["DirectIp"]=new[]{"127.0.0.1/32"};
   else if(p.ProviderFormat=="clash") c["rules"]=new[]{"IP-CIDR,127.0.0.1/32,DIRECT,no-resolve","MATCH,HY2"};
   else if(p.ProviderFormat=="sing-box") c["route"]=Json.Obj("rules",new[]{Json.Obj("ip_cidr",new[]{"127.0.0.1/32"},"outbound","direct")},"final","select");
   else { var outs=new System.Collections.Generic.List<object>();foreach(var o in ProviderRouting.List(c,"outbounds"))outs.Add(o);outs.Add(Json.Obj("tag","direct","protocol","freedom"));c["outbounds"]=outs;c["routing"]=Json.Obj("rules",new[]{Json.Obj("type","field","ip",new[]{"127.0.0.1/32"},"outboundTag","direct")}); }
   p.ProviderConfig=Json.Write(c);Direct(runtime,p,settings);
  } return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;} }
}
