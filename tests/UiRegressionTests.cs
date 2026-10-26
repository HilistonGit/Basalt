using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Basalt.Core;
using Basalt.Windows;

static class UiRegressionTests
{
    static bool HasText(Control control,string text){if(control.Text==text)return true;foreach(Control child in control.Controls)if(HasText(child,text))return true;return false;}
    [STAThread] static int Main()
    {
        try
        {
            var type = typeof(MainForm).Assembly.GetType("Basalt.Windows.SubscriptionBrowser", true);
            using (var list = (ListBox)Activator.CreateInstance(type, true))
            {
                list.Size = new Size(500, 400); var handle = list.Handle;
                var move = type.GetMethod("OnMouseMove", BindingFlags.Instance | BindingFlags.NonPublic);
                var down = type.GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic);
                var activate = type.GetMethod("ActivateRow", BindingFlags.Instance | BindingFlags.NonPublic);
                var measure = type.GetMethod("OnMeasureItem", BindingFlags.Instance | BindingFlags.NonPublic);
                for (int cycle = 0; cycle < 3; cycle++)
                {
                    var settings = new Settings(); if (cycle == 1) settings.Subscriptions.Add(new Subscription { Name = "Regression" });
                    type.GetMethod("Populate").Invoke(list, new object[] { settings, settings.Profiles });
                    foreach (var point in new[] { new Point(10, 10), new Point(200, 350), new Point(-1, -1), new Point(600, 500) })
                    { var e = new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0); move.Invoke(list, new object[] { e }); down.Invoke(list, new object[] { e }); }
                    foreach (int index in new[] { -1, 65535, list.Items.Count })
                    { activate.Invoke(list, new object[] { index, 0 }); using (var g = list.CreateGraphics()) measure.Invoke(list, new object[] { new MeasureItemEventArgs(g, index) }); }
                }
            }
            var country=typeof(MainForm).Assembly.GetType("Basalt.Windows.ServerCountry").GetMethod("Code"); if((string)country.Invoke(null,new object[]{"RU-3 · LTE"})!="RU" || (string)country.Invoke(null,new object[]{"Нидерланды · Amsterdam"})!="NL" || (string)country.Invoke(null,new object[]{"Unknown node"})!="") throw new Exception("Country detection regression"); var layoutType = typeof(MainForm).Assembly.GetType("Basalt.Windows.SubscriptionHeaderLayout", true);
            foreach (float dpi in new[] { 1f, 1.25f, 1.5f, 2f }) foreach (int width in new[] { 330, 450, 700 })
            {
                var bounds = new Rectangle(0, 0, (int)(width * dpi), (int)(114 * dpi));
                var layout = Activator.CreateInstance(layoutType, new object[] { bounds, dpi });
                Func<string, Rectangle> rect = delegate(string name) { return (Rectangle)layoutType.GetField(name).GetValue(layout); };
                if (rect("More").Right > rect("Delete").Left || rect("Delete").Right > bounds.Right) throw new Exception("Delete action overlap");
                if (rect("Title").Right > rect("Refresh").Left || rect("Metadata").Bottom > rect("Quota").Top || rect("Quota").Right >= rect("Expiry").Left || rect("Expiry").Right > bounds.Right || rect("More").Right > bounds.Right) throw new Exception("Subscription layout overlap at scale " + dpi);
            }
            using (var list = (ListBox)Activator.CreateInstance(type, true))
            {
                list.Size = new Size(450, 400); var handle = list.Handle; var settings = new Settings();
                settings.Subscriptions.Add(new Subscription { Id = "long", Name = new string('W', 180), UploadBytes = 1000000000, DownloadBytes = 2000000000, TotalBytes = 10000000000, ExpireUnix = 1800000000 });
                for (int i = 0; i < 60; i++) settings.Profiles.Add(new Profile { Id = "profile-" + i, Name = "Server " + i, Protocol = "trojan", SubscriptionId = "long" });
                type.GetMethod("Populate").Invoke(list, new object[] { settings, settings.Profiles });
                list.TopIndex = 15; list.SelectedIndex = 20;
                type.GetMethod("Populate").Invoke(list, new object[] { settings, settings.Profiles });
                if (list.TopIndex != 15 || list.SelectedIndex != 20) throw new Exception("Refresh lost scroll/selection");
                var draw = type.GetMethod("OnDrawItem", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (int dpi in new[] { 96, 120, 144, 192 })
                using (var bitmap = new Bitmap(900, 240))
                {
                    bitmap.SetResolution(dpi, dpi);
                    using (var graphics = Graphics.FromImage(bitmap))
                    { draw.Invoke(list, new object[] { new DrawItemEventArgs(graphics, list.Font, new Rectangle(0, 0, 900, 114 * dpi / 96), 0, DrawItemState.Default) }); }
                }
            }
            using(var form=new MainForm(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui-state-tests"),true))
            {
                var show=typeof(MainForm).GetMethod("ShowPage",BindingFlags.Instance|BindingFlags.NonPublic);
                var content=(Control)typeof(MainForm).GetField("content",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
                show.Invoke(form,new object[]{"settings"}); var original=content.Controls[0];
                show.Invoke(form,new object[]{"servers"}); show.Invoke(form,new object[]{"settings"});
                if(!Object.ReferenceEquals(original,content.Controls[0])) throw new Exception("Page was rebuilt instead of reused");
                if(form.Icon==null) throw new Exception("Missing application icon");
                var language=typeof(MainForm).GetMethod("ChangeLanguage",BindingFlags.Instance|BindingFlags.NonPublic);language.Invoke(form,new object[]{"en"});if(!HasText(form,"Settings")||!HasText(form,"Start with Windows"))throw new Exception("English UI switching failed");language.Invoke(form,new object[]{"ru"});if(!HasText(form,"Настройки"))throw new Exception("Russian UI switching failed");
                var registration=typeof(MainForm).Assembly.GetType("Basalt.Windows.StartupRegistration");string xml=(string)registration.GetMethod("TaskXml").Invoke(null,new object[]{@"D:\Apps & Tools\Basalt.exe","S-1-5-21-123"});var task=new System.Xml.XmlDocument();task.LoadXml(xml);if(!xml.Contains("HighestAvailable")||!xml.Contains("InteractiveToken")||!xml.Contains("&amp;")||!xml.Contains("--startup"))throw new Exception("Startup task definition invalid");
                typeof(MainForm).GetField("rendering",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(form,false);
                var closing=new FormClosingEventArgs(CloseReason.UserClosing,false);
                typeof(MainForm).GetMethod("HandleClosing",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,new object[]{form,closing});
                if(!closing.Cancel) throw new Exception("Disconnected window close did not hide to tray");
                typeof(MainForm).GetField("quitting",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(form,true);
                closing=new FormClosingEventArgs(CloseReason.UserClosing,false);
                typeof(MainForm).GetMethod("HandleClosing",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,new object[]{form,closing});
                if(closing.Cancel) throw new Exception("Explicit exit cancelled");
            }
            Console.WriteLine("PASS page reuse, app icon, close-to-tray without VPN, explicit exit");
            Console.WriteLine("PASS header layout at 100/125/150/200%, long labels, repaint, refresh preserves scroll and selection");
            Console.WriteLine("PASS list empty-area mouse movement/clicks, index 65535, clear/repopulate and measurement"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
