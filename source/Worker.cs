using System.Diagnostics;
using System.Text.Json;
namespace PointerUnderline;
internal sealed class Worker : IDisposable
{
 private Process? process;
 private bool ocrInitialized;
 private void Start()
 {
  if(process is { HasExited:false }) return;
  Dispose(); ocrInitialized=false;
  process=Process.Start(new ProcessStartInfo(Environment.ProcessPath!,"--worker") { UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true });
  // Native libraries may emit diagnostic text; do not store screen text or diagnostics on disk.
  process!.ErrorDataReceived+=(_,_)=>{}; process.BeginErrorReadLine();
 }
 public async Task<Detection> Query(Request request,int timeout)
 {
  try {
   var cold=process is null || process.HasExited;
   Start();
   using var deadline=new CancellationTokenSource(TimeSpan.FromMilliseconds(cold || (request.Kind=="ocr"&&!ocrInitialized) ? Math.Max(timeout,5000) : timeout));
   await process!.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request).AsMemory(),deadline.Token);
   var answer=await process.StandardOutput.ReadLineAsync(deadline.Token);
   if(request.Kind=="ocr") ocrInitialized=true;
   return answer==null ? new(Error:"Detector closed unexpectedly.") : JsonSerializer.Deserialize<Detection>(answer) ?? new();
  } catch(Exception ex) { Dispose(); return new(Error:ex is TimeoutException or OperationCanceledException ? "Detector timed out; it will restart automatically." : "Detector: "+ex.GetType().Name); }
 }
 public void Dispose() { if(process!=null) { try { if(!process.HasExited) process.Kill(true); } catch {} process.Dispose(); process=null; } }
 public static void Run()
 {
  using var detector=new Detector(); string? line;
  while((line=Console.ReadLine())!=null) {
   Detection result;
   try { var req=JsonSerializer.Deserialize<Request>(line)!; result=req.Kind=="uia" ? detector.Uia(req.X,req.Y) : detector.Ocr(req); }
   catch { result=new(Error:"Invalid detector request."); }
   Console.WriteLine(JsonSerializer.Serialize(result)); Console.Out.Flush();
  }
 }
}
