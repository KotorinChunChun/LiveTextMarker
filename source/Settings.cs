using System.Text.Json;
namespace PointerUnderline;
internal sealed record Settings(bool CtrlOnly=false, bool Ocr=true, int Thickness=3, decimal Offset=-2, int Color=-1102536)
{
 private static string FilePath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PointerUnderline","settings.json");
 public static Settings Load() {
  try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new(); }
  catch { return new(); }
 }
 public void Save() {
  Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
  var temporary=FilePath+".tmp";
  File.WriteAllText(temporary,JsonSerializer.Serialize(this));
  File.Move(temporary,FilePath,true);
 }
}
