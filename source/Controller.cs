using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
namespace PointerUnderline;
internal sealed class Controller : Form
{
 private readonly List<Overlay> overlays=new();
 private readonly Worker worker=new();
 private readonly System.Windows.Forms.Timer timer=new() { Interval=20 };
 private readonly NotifyIcon tray;
 private readonly Button toggle=new() { Text="開始 / Start", AutoSize=true };
 private readonly CheckBox ocr=new() { Text="画像・スライドにも対応（ローカルOCR）",Checked=true,AutoSize=true };
 private readonly NumericUpDown thickness=new() { Minimum=1,Maximum=12,Value=3,Width=65 };
 private readonly NumericUpDown offset=new() { Minimum=-4,Maximum=20,Value=3,Width=65 };
 private readonly Label status=new() { Text="待機中。開始ボタンで追従を開始します。",AutoSize=true,MaximumSize=new Size(475,0) };
 private Color lineColor=Color.FromArgb(239,45,56);
 private bool enabled,busy,closing;
 private Box? current;
 private Box[] currentLines=Array.Empty<Box>();
 private Native.POINT last;
 private long movedAt,lastQuery;
 private int generation;
 private nint lastForeground;
 private string? cachedImage;
 private Detection? cachedOcr;
 private Rectangle cachedCrop;
 private readonly bool toggleHotkey,quitHotkey;
 public Controller()
 {
  Text="Pointer Underline"; ClientSize=new Size(530,345); MinimumSize=new Size(530,380); StartPosition=FormStartPosition.CenterScreen;
  Font=new Font("Yu Gothic UI",10); AutoScaleMode=AutoScaleMode.Dpi;
  var layout=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(22),AutoScroll=true };
  layout.Controls.Add(new Label { Text="ポインタのある行に、赤い下線。",Font=new Font(Font.FontFamily,16,FontStyle.Bold),AutoSize=true });
  layout.Controls.Add(new Label { Text="文字の上で少し止めると、その行の幅に合わせて追従します。\nCtrl + Alt + U：開始 / 一時停止　　Ctrl + Alt + Q：終了",AutoSize=true,Margin=new Padding(3,12,3,12) });
  layout.Controls.Add(ocr);
  var options=new FlowLayoutPanel { AutoSize=true,WrapContents=false };
  options.Controls.Add(new Label { Text="太さ",AutoSize=true,Margin=new Padding(0,7,3,0) }); options.Controls.Add(thickness);
  options.Controls.Add(new Label { Text="下の間隔",AutoSize=true,Margin=new Padding(14,7,3,0) }); options.Controls.Add(offset);
  var color=new Button { Text="色を選ぶ",AutoSize=true }; color.Click+=(_,_)=> { using var d=new ColorDialog { Color=lineColor }; if(d.ShowDialog(this)==DialogResult.OK) { lineColor=d.Color; Clear(); } }; options.Controls.Add(color); layout.Controls.Add(options);
  var buttons=new FlowLayoutPanel { AutoSize=true,WrapContents=false }; toggle.Click+=(_,_)=>SetEnabled(!enabled); buttons.Controls.Add(toggle);
  var minimize=new Button { Text="通知領域へ",AutoSize=true }; minimize.Click+=(_,_)=>Hide(); buttons.Controls.Add(minimize);
  var quit=new Button { Text="終了",AutoSize=true }; quit.Click+=(_,_)=>Close(); buttons.Controls.Add(quit); layout.Controls.Add(buttons);
  layout.Controls.Add(status);
  layout.Controls.Add(new Label { Text="OCRは画面の横長の一部分をメモリ上で処理します。\n画像・認識テキストを保存・外部送信しません。",AutoSize=true,ForeColor=Color.DimGray,Margin=new Padding(3,12,3,3) }); Controls.Add(layout);
  var menu=new ContextMenuStrip(); menu.Items.Add("開始 / 一時停止",null,(_,_)=>SetEnabled(!enabled)); menu.Items.Add("設定を表示",null,(_,_)=>{ Show(); WindowState=FormWindowState.Normal; Activate(); }); menu.Items.Add("終了",null,(_,_)=>Close());
  tray=new NotifyIcon { Icon=SystemIcons.Information,Text="Pointer Underline — 停止中",Visible=true,ContextMenuStrip=menu }; tray.DoubleClick+=(_,_)=>{ Show(); Activate(); };
  _=Handle; toggleHotkey=Native.RegisterHotKey(Handle,1,0x4003,0x55); quitHotkey=Native.RegisterHotKey(Handle,2,0x4003,0x51);
  if(!toggleHotkey||!quitHotkey) status.Text="ショートカットが他のアプリで使用されています。ボタンまたは通知領域を使ってください。";
  ocr.CheckedChanged+=(_,_)=> { generation++; Clear(); cachedImage=null; };
  thickness.ValueChanged+=(_,_)=>Clear(); offset.ValueChanged+=(_,_)=>Clear();
  timer.Tick+=Tick; timer.Start();
 }
 private void SetEnabled(bool value) { enabled=value; generation++; cachedImage=null; cachedOcr=null; Clear(); toggle.Text=value?"一時停止 / Pause":"開始 / Start"; tray.Text=value?"Pointer Underline — 追従中":"Pointer Underline — 停止中"; status.Text=value?"追従中。文字の上にポインタを移動してください。":"一時停止中"; lastQuery=0; }
 private void Clear() { current=null; currentLines=Array.Empty<Box>(); HideLines(); }
 private void HideLines() { foreach(var overlay in overlays) overlay.Hide(); }
 protected override void WndProc(ref Message m) { if(m.Msg==0x0312) { if(m.WParam==(nint)1) SetEnabled(!enabled); else if(m.WParam==(nint)2) Close(); } base.WndProc(ref m); }
 private async void Tick(object? sender,EventArgs args)
 {
  if(closing||!enabled||!Native.GetCursorPos(out var point)) return;
  var now=Environment.TickCount64;
  var foreground=Native.GetForegroundWindow();
  if(foreground!=lastForeground) { lastForeground=foreground; generation++; Clear(); cachedImage=null; }
  if(point.X!=last.X||point.Y!=last.Y) { last=point; movedAt=now; cachedImage=null; if(current is {} b&&!b.Contains(point.X,point.Y)) Clear(); }
  if(busy||now-movedAt<65||now-lastQuery<180) return;
  var window=Native.GetAncestor(Native.WindowFromPoint(point),2);
  Native.GetWindowThreadProcessId(window,out var pid);
  if(pid==Environment.ProcessId) { Clear(); return; }
  busy=true; lastQuery=now; var version=generation;
  try {
   var result=await worker.Query(new Request("uia",point.X,point.Y),650);
   if(closing||!enabled||version!=generation) return;
   if(result.Bounds==null&&!result.Sensitive&&ocr.Checked) {
    // Reject stale requests before capturing another location.
    Native.GetCursorPos(out var next);
    if(Math.Abs(next.X-point.X)>4||Math.Abs(next.Y-point.Y)>4) return;
    var screen=Screen.FromPoint(new Point(point.X,point.Y)).Bounds;
    int cropHeight=Math.Min(screen.Height,Math.Max(180,(int)(240*Native.Scale(point))));
    var crop=new Rectangle(screen.Left,Math.Clamp(point.Y-cropHeight/2,screen.Top,screen.Bottom-cropHeight),screen.Width,cropHeight);
    var wasVisible=overlays.Any(o=>o.Visible);
    HideLines(); Native.DwmFlush();
    string image;
    try { using var bitmap=new Bitmap(crop.Width,crop.Height,PixelFormat.Format24bppRgb); using(var graphics=Graphics.FromImage(bitmap)) graphics.CopyFromScreen(crop.Location,Point.Empty,crop.Size,CopyPixelOperation.SourceCopy); using var stream=new MemoryStream(); bitmap.Save(stream,ImageFormat.Png); image=Convert.ToBase64String(stream.ToArray()); }
    finally { if(wasVisible&&current is {} old&&enabled) ShowLine(old,point); }
    if(image==cachedImage&&crop==cachedCrop&&cachedOcr!=null) result=cachedOcr;
    else { result=await worker.Query(new Request("ocr",point.X,point.Y,image,crop.X,crop.Y),2500); cachedImage=result.Error==null?image:null; cachedCrop=crop; cachedOcr=result; }
   }
   if(closing||!enabled||version!=generation) return;
   Native.GetCursorPos(out var live);
   var liveWindow=Native.GetAncestor(Native.WindowFromPoint(live),2);
   if(liveWindow!=window||Native.GetForegroundWindow()!=foreground) { Clear(); return; }
   if(DetectionGate.Accept(result.Bounds,enabled,closing,version,generation,(long)window,(long)liveWindow,(long)foreground,(long)Native.GetForegroundWindow(),live.X,live.Y,point.Y,Native.Scale(live))) { var box=result.Bounds!.Value; current=box; currentLines=result.Lines ?? new[]{box}; ShowLine(box,live); status.Text=$"追従中 · {result.Source} · {currentLines.Length}行"; }
   else { Clear(); status.Text=result.Sensitive?"パスワード欄では認識を停止します。":result.Error is {} error?error:"追従中 · ポインタ下に文字が見つかりません"; }
  } catch(Exception ex) { Clear(); status.Text="認識を再試行します: "+ex.GetType().Name; }
  finally { busy=false; }
 }
 private void ShowLine(Box box,Native.POINT point) {
  var s=Screen.FromPoint(new Point(point.X,point.Y)).Bounds;
  var lines=currentLines.Length>0?currentLines:new[]{box};
  while(overlays.Count<lines.Length) overlays.Add(new Overlay());
  for(int i=0;i<overlays.Count;i++) {
   if(i>=lines.Length) { overlays[i].Hide(); continue; }
   var line=Geometry.Underline(lines[i],new Box(s.X,s.Y,s.Width,s.Height),Native.Scale(point),(int)thickness.Value,(double)offset.Value);
   if(line.Width>0&&line.Height>0) overlays[i].Draw(line,lineColor); else overlays[i].Hide();
  }
 }
 protected override void OnFormClosing(FormClosingEventArgs e) { closing=true; enabled=false; generation++; timer.Stop(); Native.UnregisterHotKey(Handle,1); Native.UnregisterHotKey(Handle,2); worker.Dispose(); foreach(var overlay in overlays) overlay.Dispose(); tray.Visible=false; tray.Dispose(); timer.Dispose(); base.OnFormClosing(e); }
}
