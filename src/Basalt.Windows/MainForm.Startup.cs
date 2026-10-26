using System;
using System.Threading;
using Basalt.Core;
namespace Basalt.Windows {
 public sealed partial class MainForm {
  bool autoConnectPending; DateTime nextAutoConnect=DateTime.MinValue; int autoConnectFailures;
  void BeginAutoConnect(){if(rendering||busy||runtime.Running||quitting||!settings.AutoConnect)return;autoConnectPending=false;
   if(settings.TunMode&&!TunnelSession.IsAdministrator){Guard(Elevate);return;}
   cancellation=new CancellationTokenSource();var token=cancellation.Token;
   Work(delegate{return StartupPreparation.Prepare(settings,delegate(Subscription s){return FetchValidated(s,settings.DeviceId);},Log,token);},delegate(object value){cancellation.Dispose();cancellation=null;var prepared=(Settings)value;settings.Profiles=prepared.Profiles;settings.Subscriptions=prepared.Subscriptions;settings.RouteDocuments=prepared.RouteDocuments;settings.SelectedId=prepared.SelectedId;Save();ShowPage(page);if(SelectedProfile==null){RetryAutoConnect();Notify(L10n.English?"No saved servers. Retrying subscription refresh shortly.":"Нет сохранённых серверов. Обновление подписок будет повторено.",true);return;}Start(SelectedProfile,true);},L10n.English?"Startup: refreshing subscriptions before connecting…":"Запуск: обновляем подписки перед подключением…");
  }
  void RetryAutoConnect(){if(!settings.AutoConnect||quitting)return;autoConnectPending=true;nextAutoConnect=DateTime.UtcNow.AddSeconds(Math.Min(300,30*Math.Pow(2,Math.Min(autoConnectFailures++,4))));}
 }
}
