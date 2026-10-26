using Basalt.Core;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace Basalt.Windows
{
    static class Program
    {
        [STAThread] static void Main(string[] args)
        {
            if(args.Length==1&&(args[0]=="--startup-register"||args[0]=="--startup-remove")){try{StartupRegistration.Apply(args[0]=="--startup-register");}catch{Environment.ExitCode=1;}return;}
            bool startup=Array.IndexOf(args,"--startup")>=0;
            bool first;
            using (var mutex = new Mutex(true, @"Local\Basalt.Desktop", out first))
            {
                if (!first && args.Length > 0 && args[0] == "--elevated") { try { first = mutex.WaitOne(15000); } catch (AbandonedMutexException) { first = true; } }
                if (!first && startup) return;
                if (!first) { MessageBox.Show(L10n.T("Basalt уже запущен. Проверьте системный трей."), "Basalt"); return; }
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                try
                {
                    // TLS 1.2 when available; never bypass certificate validation on old Windows.
                    try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; } catch (NotSupportedException) { }
                    bool preview = args.Length >= 2 && (args[0] == "--render-preview" || args[0] == "--ui-preview");
                    string data = preview ? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1])), "preview-data") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Basalt");
                    Directory.CreateDirectory(data);
                    using (var form = new MainForm(data, preview))
                    {
                        if (preview)
                        {
                            form.RenderPreview(args[1], args.Length > 2 ? args[2] : "empty");
                            if (args[0] == "--render-preview") { form.Close(); return; }
                        }
                        if(startup) form.Shown += delegate {form.Hide();};
                        Application.Run(form);
                    }
                }
                catch (Exception e) { MessageBox.Show(L10n.T("Basalt не удалось запустить.\n") + e.Message, "Basalt", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                finally { mutex.ReleaseMutex(); }
            }
        }
    }
}
