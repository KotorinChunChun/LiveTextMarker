using System.Windows.Forms;
namespace LiveTextMarker;
internal static class Program
{
 [STAThread] static void Main(string[] args)
 {
  Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
  if(args.Contains("--worker")) { var t=new Thread(Worker.Run); t.SetApartmentState(ApartmentState.MTA); t.Start(); t.Join(); return; }
  using var mutex=new Mutex(true,"Local\\LiveTextMarker-v1",out var first);
  if(!first) { if(!args.Contains("--startup")) KotorinToys.Integration.ManagementEndpoint.ShowExisting("live-text-marker"); return; }
  Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
  using var controller = new Controller();
  controller.StartManagement();
  if(args.Contains("--startup"))
  {
   using var context = new ApplicationContext();
   controller.FormClosed += (_,_) => context.ExitThread();
   Application.Run(context);
  }
  else Application.Run(controller);
 }
}
