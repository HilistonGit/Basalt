using System;
using System.Drawing;
using System.Text.RegularExpressions;
namespace Basalt.Windows {
 static class ServerCountry {
  public static string Code(string name) {
   name=name??"";
   for(int i=0;i+3<name.Length;i++) if(Char.IsHighSurrogate(name[i])&&Char.IsLowSurrogate(name[i+1])&&Char.IsHighSurrogate(name[i+2])&&Char.IsLowSurrogate(name[i+3])) { int a=Char.ConvertToUtf32(name,i)-0x1F1E6,b=Char.ConvertToUtf32(name,i+2)-0x1F1E6; if(a>=0&&a<26&&b>=0&&b<26) return ""+(char)('A'+a)+(char)('A'+b); }
   string[] codes={"RU","NL","DE","FI","FR","SE","US","GB","JP","TR","PL","CH","CA","SG","EE","LV","LT","UA","KZ","HK"};
   string[] names={"россия|russia","нидерланд|netherlands|holland","германия|germany|frankfurt","финлянд|finland|helsinki","франция|france|paris","швеция|sweden|stockholm","сша|united states","великобрит|united kingdom|london","япония|japan","турция|turkey","польша|poland","швейцар|switzerland","канада|canada","сингапур|singapore","эстония|estonia","латвия|latvia","литва|lithuania","украина|ukraine","казахстан|kazakhstan","гонконг|hong kong"};
   for(int i=0;i<codes.Length;i++) if(Regex.IsMatch(name,@"(?i)(^|[\s\[\(·_-])"+codes[i]+@"($|[\s\]\)·_-])|"+names[i])) return codes[i]; return "";
  }
  public static void Draw(Graphics g,Rectangle r,string name) {
   string code=Code(name); if(code=="") { Design.Icon(g,"globe",r,Design.Muted); return; }
   var old=g.Save(); using(var path=new System.Drawing.Drawing2D.GraphicsPath()) { path.AddEllipse(r); g.SetClip(path); Color[] colors=null;
    if(code=="RU") colors=new[]{Color.White,Color.RoyalBlue,Color.Crimson}; if(code=="NL") colors=new[]{Color.Firebrick,Color.White,Color.RoyalBlue}; if(code=="DE") colors=new[]{Color.Black,Color.Firebrick,Color.Gold};
    if(code=="FR") { var cs=new[]{Color.RoyalBlue,Color.White,Color.Crimson}; for(int i=0;i<3;i++) using(var b=new SolidBrush(cs[i])) g.FillRectangle(b,r.X+i*r.Width/3f,r.Y,r.Width/3f+1,r.Height); g.Restore(old); return; } if(code=="FI" || code=="SE") { using(var b=new SolidBrush(code=="FI"?Color.White:Color.RoyalBlue)) g.FillRectangle(b,r); using(var b=new SolidBrush(code=="FI"?Color.RoyalBlue:Color.Gold)) { g.FillRectangle(b,r.X+r.Width*.28f,r.Y,r.Width*.18f,r.Height); g.FillRectangle(b,r.X,r.Y+r.Height*.43f,r.Width,r.Height*.18f); } g.Restore(old); return; } if(colors!=null) for(int i=0;i<3;i++) using(var b=new SolidBrush(colors[i])) g.FillRectangle(b,r.X,r.Y+i*r.Height/3f,r.Width,r.Height/3f+1); else { using(var b=new SolidBrush(Design.Mint)) g.FillRectangle(b,r); using(var f=Design.Font(7,true)) Design.Text(g,code,f,r,Design.Ink,System.Windows.Forms.TextFormatFlags.HorizontalCenter); }
   } g.Restore(old);
  }
 }
}
