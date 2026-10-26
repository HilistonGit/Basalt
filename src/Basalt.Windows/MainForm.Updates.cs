using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Basalt.Core;
namespace Basalt.Windows {
 public sealed partial class MainForm {
  void ChangeLanguage(string language){settings.Language=language;Save();L10n.SetLanguage(language);ClearPageCache();while(content.Controls.Count>0)content.Controls[0].Dispose();BuildNavigation();ShowPage("settings");tray.ContextMenuStrip.Items[0].Text=L10n.T("Открыть Basalt");tray.ContextMenuStrip.Items[1].Text=L10n.T("Подключить / отключить");tray.ContextMenuStrip.Items[2].Text=L10n.T("Выход");Invalidate(true);Notify(L10n.English?"Language changed.":"Язык изменён.",false);}
  void CheckUpdates(){if(busy)return;Work(delegate {return Updates.Check(typeof(MainForm).Assembly.GetName().Version,!Environment.Is64BitOperatingSystem);},delegate(object value){var release=(ReleaseUpdate)value;if(release==null){Notify(L10n.English?"You have the latest published version.":"Установлена последняя опубликованная версия.",false);return;}if(MessageBox.Show(this,(L10n.English?"Download Basalt ":"Скачать Basalt ")+release.Version+"?\n"+(release.Size/1024/1024)+" MB\n"+(L10n.English?"The archive will be verified with SHA-256.":"Архив будет проверен по SHA-256."),"Basalt",MessageBoxButtons.YesNo,MessageBoxIcon.Information)!=DialogResult.Yes)return;
   Work(delegate {int previous=-1;return Updates.Download(release,Path.Combine(dataDirectory,"updates"),delegate(long bytes){int percent=(int)(bytes*100/release.Size);if(percent!=previous){previous=percent;if(!IsDisposed)BeginInvoke((Action)delegate {notice.Text=(L10n.English?"Downloading update: ":"Загрузка обновления: ")+percent+"%";});}});},delegate(object result){string path=(string)result;MessageBox.Show(this,(L10n.English?"Downloaded and verified. Exit Basalt from the tray, extract the archive into a new folder, then launch Basalt.exe. Your subscriptions remain in your Windows profile.":"Обновление загружено и проверено. Завершите Basalt через трей, распакуйте архив в новую папку и запустите Basalt.exe. Подписки сохраняются в профиле Windows.")+"\n\n"+path,"Basalt",MessageBoxButtons.OK,MessageBoxIcon.Information);Process.Start(new ProcessStartInfo("explorer.exe","/select,\""+path+"\""){UseShellExecute=true});},L10n.English?"Downloading update…":"Загружаем обновление…");},L10n.English?"Checking GitHub Releases…":"Проверяем GitHub Releases…");}
 }
}
