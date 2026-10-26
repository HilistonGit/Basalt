using System;
using System.Collections;
using System.Drawing;
using System.Windows.Forms;
using Basalt.Core;
namespace Basalt.Windows {
 public sealed partial class MainForm {
  static string Pretty(object value,int depth) {
   string pad=new string(' ',depth*2), next=pad+"  "; var map=value as System.Collections.Generic.Dictionary<string,object>; var list=value as IList;
   if(map!=null) { var rows=new System.Collections.Generic.List<string>(); foreach(var pair in map) rows.Add(next+Json.Write(pair.Key)+": "+Pretty(pair.Value,depth+1)); return "{\r\n"+String.Join(",\r\n",rows.ToArray())+"\r\n"+pad+"}"; }
   if(list!=null) { var rows=new System.Collections.Generic.List<string>(); foreach(var item in list) rows.Add(next+Pretty(item,depth+1)); return "[\r\n"+String.Join(",\r\n",rows.ToArray())+"\r\n"+pad+"]"; } return Json.Write(value);
  }
  void ShowProviderRoutes() {
   var p=SelectedProfile; if(p==null) { Notify(L10n.T("Сначала выберите сервер подписки."),true); return; }
   Guard(delegate { var resolved=ProviderRouting.Resolve(p,settings); using(var dialog=Dialog(L10n.T("Маршруты · ")+p.Name,560)) {
    Place(dialog,Caption(String.IsNullOrEmpty(resolved.ProviderConfig)?L10n.T("Маршруты не получены. Обновите подписку; провайдер должен передать правила."):L10n.T("Источник: ")+resolved.ProviderFormat+" · "+(settings.UseSubscriptionRouting?L10n.T("включены"):L10n.T("отключены")),10,Design.Ink),20,18,620,50);
    var field=Input(String.IsNullOrEmpty(resolved.ProviderConfig)?"{}":Pretty(Json.Read(resolved.ProviderConfig),0)); field.Multiline=true; field.ReadOnly=true; field.ScrollBars=ScrollBars.Both; field.WordWrap=false; field.Font=new Font("Consolas",10); Place(dialog,field,20,78,620,410);
    Place(dialog,Button(L10n.T("Закрыть"),"",delegate { dialog.Close(); },true),480,505,160,38); dialog.ShowDialog(this);
   }});
  }
  void EditServerJson(Profile original) {
   if(busy) return;
   Guard(delegate { using(var dialog=Dialog(L10n.T("JSON сервера · ")+original.Name,590)) {
    Place(dialog,Caption("Outbound "+original.Engine+L10n.T(". Обновление подписки заменит ручные изменения."),9,Design.Muted),20,15,620,35);
    var field=Input(Pretty(Configuration.Outbound(original),0)); field.Multiline=true; field.ScrollBars=ScrollBars.Both; field.WordWrap=false; field.Font=new Font("Consolas",10); Place(dialog,field,20,58,620,440);
    Place(dialog,Button(L10n.T("Отмена"),"",delegate { dialog.Close(); },false),300,525,150,40);
    Place(dialog,Button(L10n.T("Проверить и сохранить"),"check",delegate { Guard(delegate {
     if(runtime.Running) throw new InvalidOperationException(L10n.T("Отключитесь перед изменением конфига."));
     var outbound=Json.Read(field.Text); bool sb=outbound.ContainsKey("type"); var parsed=Subscriptions.Parse(Json.Write(Json.Obj("outbounds",new[]{outbound}))); if(parsed.Errors.Count>0 || parsed.Profiles.Count!=1) throw new FormatException(L10n.T("Нужен один поддерживаемый outbound. ")+String.Join("; ",parsed.Errors.ToArray()));
     var candidate=parsed.Profiles[0]; candidate.Id=original.Id; candidate.Name=original.Name; candidate.SubscriptionId=original.SubscriptionId; candidate.Favorite=original.Favorite;
     candidate.HappRoutingConfig=original.HappRoutingConfig; candidate.ProviderDocumentId=original.ProviderDocumentId; candidate.ProviderConfig=original.ProviderConfig; candidate.ProviderFormat=original.ProviderFormat; candidate.SourceTag=original.SourceTag; candidate.RouteAssetDirectory=original.RouteAssetDirectory; candidate.ManagedRuleSets=original.ManagedRuleSets;
     if(sb != (Configuration.Outbound(original).ContainsKey("type"))) throw new FormatException(L10n.T("Смена ядра в редакторе не поддерживается."));
     var check=Json.Serializer().Deserialize<Settings>(Json.Write(settings)); CoreCatalog.Configure(check);
     using(var validator=new CoreRuntime(System.IO.Path.Combine(dataDirectory,"editor-validation"))) validator.Validate(candidate,check);
     int index=settings.Profiles.IndexOf(original); settings.Profiles[index]=candidate; try { Save(); } catch { settings.Profiles[index]=original; throw; }
     ClearPageCache(); RefreshServers(); dialog.Close(); Notify(L10n.T("JSON сохранён и проверен ядрами."),false);
    }); },true),460,525,180,40); dialog.ShowDialog(this);
   }});
  }
 }
}
