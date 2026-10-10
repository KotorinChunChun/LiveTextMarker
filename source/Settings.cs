using System.Text.Json;
namespace LiveTextMarker;
internal sealed record Settings(bool CtrlOnly=true, bool Ocr=true, int Thickness=3, decimal Offset=-2, int Color=-1102536, bool Highlight=false, int HighlightOpacity=16, int HighlightColor=-10496, HotkeyBinding? ToggleKey=null, HotkeyBinding? QuitKey=null)
{
 private static string FilePath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LiveTextMarker","settings.json");
 private static string LegacyFilePath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PointerUnderline","settings.json");
 public static Settings Load() {
  foreach(var path in new[]{FilePath,LegacyFilePath}) {
   try {
    var settings=JsonSerializer.Deserialize<Settings>(File.ReadAllText(path));
    if(settings==null) continue;
    if(path==LegacyFilePath) { try { settings.Save(); } catch { /* 元の設定は削除せず、そのまま使用する。 */ } }
    return settings;
   } catch { }
  }
  return new();
 }
 public void Save() {
  Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
  var temporary=FilePath+".tmp";
  File.WriteAllText(temporary,JsonSerializer.Serialize(this));
  File.Move(temporary,FilePath,true);
 }
}
