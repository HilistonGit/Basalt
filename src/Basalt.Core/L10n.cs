using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
namespace Basalt.Core {
 public static class L10n {
  static readonly Dictionary<string,object> EnglishStrings=Load();
  public static bool English {get;private set;}
  static L10n(){SetLanguage(null);}
  static Dictionary<string,object> Load(){using(var stream=typeof(L10n).Assembly.GetManifestResourceStream("Basalt.en.json"))using(var reader=new StreamReader(stream))return Json.Read(reader.ReadToEnd());}
  public static string Resolve(string language,CultureInfo system){return language=="en"||language=="ru"?language:system.TwoLetterISOLanguageName=="ru"?"ru":"en";}
  public static void SetLanguage(string language){English=Resolve(language,CultureInfo.CurrentUICulture)=="en";}
  public static string T(string russian){object value;return English&&EnglishStrings.TryGetValue(russian,out value)?(string)value:russian;}
 }
}
