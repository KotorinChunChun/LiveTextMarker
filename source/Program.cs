using System.Windows.Forms;
namespace PointerUnderline;
internal static class Program
{
 [STAThread] static void Main(string[] args)
 {
  Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
  if(args.Contains("--worker")) { var t=new Thread(Worker.Run); t.SetApartmentState(ApartmentState.MTA); t.Start(); t.Join(); return; }
  using var mutex=new Mutex(true,"Local\\PointerUnderline-v1",out var first);
  if(!first) { MessageBox.Show("Pointer Underline はすでに起動しています。通知領域のアイコンを確認してください。"); return; }
  Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
  Application.Run(new Controller());
 }
}
