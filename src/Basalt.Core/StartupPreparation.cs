using System;
using System.Threading;
namespace Basalt.Core {
 public static class StartupPreparation {
  public static Settings Prepare(Settings saved,Func<Subscription,ImportResult> fetch,Action<string> report,CancellationToken cancel){
   var next=Json.Serializer().Deserialize<Settings>(Json.Write(saved));
   foreach(var subscription in next.Subscriptions){cancel.ThrowIfCancellationRequested();if(!subscription.AutoUpdate)continue;try{var result=fetch(subscription);cancel.ThrowIfCancellationRequested();Subscriptions.Merge(next,result,subscription.Id);SubscriptionSchedule.Success(subscription,DateTime.UtcNow);}catch(OperationCanceledException){throw;}catch(Exception){SubscriptionSchedule.Failure(subscription,DateTime.UtcNow);if(report!=null)report(L10n.English?"Subscription refresh failed; saved servers retained.":"Подписка не обновилась; сохранённые серверы оставлены.");}}
   if(next.Profiles.Count>0&&!next.Profiles.Exists(p=>p.Id==next.SelectedId))next.SelectedId=next.Profiles[0].Id;return next;
  }
 }
}
