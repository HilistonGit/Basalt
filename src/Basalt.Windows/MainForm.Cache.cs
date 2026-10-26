using System;
using System.Collections.Generic;
using System.Windows.Forms;
namespace Basalt.Windows
{
    public sealed partial class MainForm
    {
        sealed class CachedPage { public Control[] Controls; public Action Restore; }
        readonly Dictionary<string, CachedPage> pages = new Dictionary<string, CachedPage>();
        void CachePage()
        {
            if (content.Controls.Count == 0) return;
            var controls = new Control[content.Controls.Count]; content.Controls.CopyTo(controls, 0);
            var a=servers; var b=search; var c=sorting; var d=resultCount; var e=emptyState;
            var f=connectionState; var g=connectionDetail; var h=currentServer; var i=engineLabel; var j=connect; var k=power; var l=uptime;
            pages[page] = new CachedPage { Controls=controls, Restore=delegate { servers=a; search=b; sorting=c; resultCount=d; emptyState=e; connectionState=f; connectionDetail=g; currentServer=h; engineLabel=i; connect=j; power=k; uptime=l; } };
        }
        bool RestorePage(string name)
        {
            CachedPage cached; if (!pages.TryGetValue(name, out cached)) return false;
            pages.Remove(name); content.Controls.AddRange(cached.Controls); cached.Restore(); return true;
        }
        void ClearPageCache()
        {
            foreach (var cached in pages.Values) foreach (var control in cached.Controls)
            { if (control.Contains(logs)) logs.Parent.Controls.Remove(logs); control.Dispose(); }
            pages.Clear();
        }
    }
}
