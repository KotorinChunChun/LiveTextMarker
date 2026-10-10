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
 private readonly CheckBox ctrlOnly=new() { Text="Ctrlを押している間だけ",AutoSize=true };
 private readonly NumericUpDown thickness=new() { Minimum=1,Maximum=12,Value=3,Width=65 };
 private readonly NumericUpDown offset=new() { Minimum=-30,Maximum=30,Value=-2,DecimalPlaces=1,Increment=0.5m,Width=75 };
 private readonly ComboBox displayMode=new() { DropDownStyle=ComboBoxStyle.DropDownList,Width=100,AccessibleName="表示方法" };
 private readonly NumericUpDown opacity=new() { Minimum=8,Maximum=30,Value=16,Width=65,AccessibleName="濃さ" };
 private readonly ListBox log=new() { Dock=DockStyle.Fill, IntegralHeight=false, HorizontalScrollbar=true };
 private readonly ToolTip tips=new();
 private bool wasActive;
 private string lastLog="";
 private bool Active=>ActivationGate.Active(enabled,ctrlOnly.Checked,(Native.GetAsyncKeyState(0x11)&0x8000)!=0);
 private readonly Label status=new() { Text="待機中。開始ボタンで追従を開始します。",AutoSize=true,MaximumSize=new Size(475,0) };
 private Color lineColor=Color.FromArgb(239,45,56);
 private Color highlightColor=Color.Gold;
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
  var settings=Settings.Load(); ctrlOnly.Checked=settings.CtrlOnly; ocr.Checked=settings.Ocr;
  thickness.Value=Math.Clamp(settings.Thickness,1,12); offset.Value=Math.Clamp(settings.Offset,-30,30); lineColor=Color.FromArgb(settings.Color);
  highlightColor=Color.FromArgb(settings.HighlightColor); opacity.Value=Math.Clamp(settings.HighlightOpacity,8,30);
  displayMode.Items.AddRange(new object[]{"下線","塗り"}); displayMode.SelectedIndex=settings.Highlight?1:0;
  Text="Pointer Underline"; ClientSize=new Size(560,480); MinimumSize=new Size(550,490); StartPosition=FormStartPosition.CenterScreen;
  Font=new Font("Yu Gothic UI",10); AutoScaleMode=AutoScaleMode.Dpi;
  var layout=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=10,Padding=new Padding(16) };
  layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
  for(int i=0;i<10;i++) layout.RowStyles.Add(new RowStyle(i==8?SizeType.Percent:SizeType.AutoSize,i==8?100:0));
  layout.Controls.Add(new Label { Text="ポインタの文章を強調",Font=new Font(Font.FontFamily,16,FontStyle.Bold),AutoSize=true },0,0);
  layout.Controls.Add(new Label { Text="Ctrl + Alt + U：開始 / 一時停止　 Ctrl + Alt + Q：終了",AutoSize=true,Margin=new Padding(3,8,3,8) },0,1);
  layout.Controls.Add(ocr,0,2); layout.Controls.Add(ctrlOnly,0,3);
  var modes=new FlowLayoutPanel { AutoSize=true,WrapContents=false };
  modes.Controls.Add(new Label { Text="表示方法",AutoSize=true,Margin=new Padding(0,7,3,0) }); modes.Controls.Add(displayMode);
  modes.Controls.Add(new Label { Text="濃さ（%）",AutoSize=true,Margin=new Padding(14,7,3,0) }); modes.Controls.Add(opacity); layout.Controls.Add(modes,0,4);
  tips.SetToolTip(opacity,"塗りの不透明度。文字を透かして読むため8～30%に制限しています。");
  var options=new FlowLayoutPanel { AutoSize=true,WrapContents=false };
  options.Controls.Add(new Label { Text="太さ",AutoSize=true,Margin=new Padding(0,7,3,0) }); options.Controls.Add(thickness);
  options.Controls.Add(new Label { Text="上下位置",AutoSize=true,Margin=new Padding(14,7,3,0) }); options.Controls.Add(offset);
  tips.SetToolTip(offset,"文字範囲の下端からの距離。負の値で上、正の値で下へ移動します（96 DPI基準）。");
  var color=new Button { Text="色を選ぶ",AutoSize=true }; color.Click+=(_,_)=> { using var d=new ColorDialog { Color=displayMode.SelectedIndex==1?highlightColor:lineColor }; if(d.ShowDialog(this)==DialogResult.OK) { if(displayMode.SelectedIndex==1) highlightColor=d.Color; else lineColor=d.Color; Clear(); SaveSettings(); } }; options.Controls.Add(color); layout.Controls.Add(options,0,5);
  var buttons=new FlowLayoutPanel { AutoSize=true,WrapContents=false }; toggle.Click+=(_,_)=>SetEnabled(!enabled); buttons.Controls.Add(toggle);
  var minimize=new Button { Text="通知領域へ",AutoSize=true }; minimize.Click+=(_,_)=>Hide(); buttons.Controls.Add(minimize);
  var quit=new Button { Text="終了",AutoSize=true }; quit.Click+=(_,_)=>Close(); buttons.Controls.Add(quit); layout.Controls.Add(buttons,0,6);
  layout.Controls.Add(status,0,7); layout.Controls.Add(log,0,8);
  layout.Controls.Add(new Label { Text="直近10件 · 画像・本文はログに記録しません",AutoSize=true,ForeColor=Color.DimGray },0,9); Controls.Add(layout);
  var menu=new ContextMenuStrip(); menu.Items.Add("開始 / 一時停止",null,(_,_)=>SetEnabled(!enabled)); menu.Items.Add("設定を表示",null,(_,_)=>{ Show(); WindowState=FormWindowState.Normal; Activate(); }); menu.Items.Add("終了",null,(_,_)=>Close());
  tray=new NotifyIcon { Icon=SystemIcons.Information,Text="Pointer Underline — 停止中",Visible=true,ContextMenuStrip=menu }; tray.DoubleClick+=(_,_)=>{ Show(); Activate(); };
  _=Handle; toggleHotkey=Native.RegisterHotKey(Handle,1,0x4003,0x55); quitHotkey=Native.RegisterHotKey(Handle,2,0x4003,0x51);
  if(!toggleHotkey||!quitHotkey) status.Text="ショートカットが他のアプリで使用されています。ボタンまたは通知領域を使ってください。";
  ocr.CheckedChanged+=(_,_)=> { generation++; Clear(); cachedImage=null; SaveSettings(); };
  ctrlOnly.CheckedChanged+=(_,_)=> { generation++; Clear(); SaveSettings(); AddLog(ctrlOnly.Checked?"Ctrlモード":"常時モード"); };
  thickness.ValueChanged+=(_,_)=> { Clear(); SaveSettings(); }; offset.ValueChanged+=(_,_)=> { Clear(); SaveSettings(); };
  displayMode.SelectedIndexChanged+=(_,_)=> { generation++; Clear(); UpdateModeControls(); SaveSettings(); AddLog("表示方法 · "+displayMode.Text); };
  opacity.ValueChanged+=(_,_)=> { Clear(); SaveSettings(); };
  UpdateModeControls();
  timer.Tick+=Tick; timer.Start();
 }
 private void UpdateModeControls() { var fill=displayMode.SelectedIndex==1; thickness.Enabled=!fill; offset.Enabled=!fill; opacity.Enabled=fill; }
 private void SaveSettings() { try { new Settings(ctrlOnly.Checked,ocr.Checked,(int)thickness.Value,offset.Value,lineColor.ToArgb(),displayMode.SelectedIndex==1,(int)opacity.Value,highlightColor.ToArgb()).Save(); } catch(Exception ex) { AddLog("設定保存: "+ex.GetType().Name); } }
 private void AddLog(string message) { if(message==lastLog) return; lastLog=message; log.Items.Insert(0,$"{DateTime.Now:HH:mm:ss}  {message}"); while(log.Items.Count>10) log.Items.RemoveAt(log.Items.Count-1); }
 private void SetEnabled(bool value) { enabled=value; generation++; cachedImage=null; cachedOcr=null; Clear(); toggle.Text=value?"一時停止 / Pause":"開始 / Start"; tray.Text=value?"Pointer Underline — 追従中":"Pointer Underline — 停止中"; status.Text=value?(Active?"追従中。文字の上にポインタを移動してください。":"Ctrl待機中"):"一時停止中"; AddLog(value?"開始":"一時停止"); lastQuery=0; }
 private void Clear() { current=null; currentLines=Array.Empty<Box>(); HideLines(); }
 private void HideLines() { foreach(var overlay in overlays) overlay.HideLine(); }
 protected override void WndProc(ref Message m) { if(m.Msg==0x0312) { if(m.WParam==(nint)1) SetEnabled(!enabled); else if(m.WParam==(nint)2) Close(); } base.WndProc(ref m); }
 private async void Tick(object? sender,EventArgs args)
 {
  var active=Active;
  if(active!=wasActive) { wasActive=active; generation++; Clear(); cachedImage=null; lastQuery=0; if(enabled) { status.Text=active?"追従中":"Ctrl待機中"; AddLog(status.Text); } }
  if(closing||!active||!Native.GetCursorPos(out var point)) return;
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
   if(closing||!Active||version!=generation) return;
   if(result.Bounds==null&&!result.Sensitive&&ocr.Checked) {
    // Reject stale requests before capturing another location.
    Native.GetCursorPos(out var next);
    if(Math.Abs(next.X-point.X)>4||Math.Abs(next.Y-point.Y)>4) return;
    var screen=Screen.FromPoint(new Point(point.X,point.Y)).Bounds;
    int cropHeight=Math.Min(screen.Height,Math.Max(180,(int)(240*Native.Scale(point))));
    var crop=new Rectangle(screen.Left,Math.Clamp(point.Y-cropHeight/2,screen.Top,screen.Bottom-cropHeight),screen.Width,cropHeight);
    if(Native.DwmGetWindowAttribute(window,9,out var targetRect,16)==0) crop=Rectangle.Intersect(crop,new Rectangle(targetRect.Left,targetRect.Top,targetRect.Right-targetRect.Left,targetRect.Bottom-targetRect.Top));
    if(crop.Width<=0 || crop.Height<=0) { Clear(); return; }
    var visible=overlays.Where(o=>o.Displayed).ToArray();
    var excluded=new List<Overlay>();
    var hidden=new List<Overlay>();
    using var bitmap=new Bitmap(crop.Width,crop.Height,PixelFormat.Format24bppRgb);
    try {
     foreach(var overlay in visible) {
      if(overlay.ExcludeFromCapture()) excluded.Add(overlay);
      else { overlay.HideLine(); hidden.Add(overlay); }
     }
     if(visible.Length>0) Native.DwmFlush();
     using(var graphics=Graphics.FromImage(bitmap)) graphics.CopyFromScreen(crop.Location,Point.Empty,crop.Size,CopyPixelOperation.SourceCopy);
    }
    finally {
     foreach(var overlay in excluded) overlay.RestoreCapture();
     if(hidden.Count>0 && current is {} old && Active) { ShowLine(old,point); AddLog("OCR撮影 · 互換モード（下線を一時非表示）"); }
    }
    using var stream=new MemoryStream(); bitmap.Save(stream,ImageFormat.Png); var image=Convert.ToBase64String(stream.ToArray());
    if(image==cachedImage&&crop==cachedCrop&&cachedOcr!=null) result=cachedOcr;
    else { result=await worker.Query(new Request("ocr",point.X,point.Y,image,crop.X,crop.Y),2500); cachedImage=result.Error==null?image:null; cachedCrop=crop; cachedOcr=result; }
   }
   if(closing||!Active||version!=generation) return;
   Native.GetCursorPos(out var live);
   var liveWindow=Native.GetAncestor(Native.WindowFromPoint(live),2);
   if(liveWindow!=window||Native.GetForegroundWindow()!=foreground) { Clear(); return; }
   if(DetectionGate.Accept(result.Bounds,Active,closing,version,generation,(long)window,(long)liveWindow,(long)foreground,(long)Native.GetForegroundWindow(),live.X,live.Y,point.Y,Native.Scale(live))) { var box=result.Bounds!.Value; current=box; currentLines=result.Lines ?? new[]{box}; ShowLine(box,live); status.Text=$"追従中 · {result.Source} · {currentLines.Length}行"; AddLog($"{result.Source}{(result.Source=="OCR"?"（UIA該当なし）":"")} · {currentLines.Length}行 · 幅 {box.Width:0}px"); }
   else { Clear(); status.Text=result.Sensitive?"パスワード欄では認識を停止します。":result.Error is {} error?error:$"追従中 · {result.Source} · 該当なし"; AddLog(status.Text); }
  } catch(Exception ex) { Clear(); status.Text="認識を再試行します: "+ex.GetType().Name; AddLog(status.Text); }
  finally { busy=false; }
 }
 private void ShowLine(Box box,Native.POINT point) {
  var s=Screen.FromPoint(new Point(point.X,point.Y)).Bounds;
  var lines=currentLines.Length>0?currentLines:new[]{box};
  while(overlays.Count<lines.Length) overlays.Add(new Overlay());
  for(int i=0;i<overlays.Count;i++) {
   if(i>=lines.Length) { overlays[i].HideLine(); continue; }
   var screen=new Box(s.X,s.Y,s.Width,s.Height);
   var fill=displayMode.SelectedIndex==1;
   var line=fill?Geometry.Highlight(lines[i],screen):Geometry.Underline(lines[i],screen,Native.Scale(point),(int)thickness.Value,(double)offset.Value);
   if(line.Width>0&&line.Height>0) overlays[i].Draw(line,fill?highlightColor:lineColor,fill?Geometry.HighlightAlpha((int)opacity.Value):(byte)255); else overlays[i].HideLine();
  }
 }
 protected override void OnFormClosing(FormClosingEventArgs e) { SaveSettings(); closing=true; enabled=false; generation++; timer.Stop(); Native.UnregisterHotKey(Handle,1); Native.UnregisterHotKey(Handle,2); worker.Dispose(); foreach(var overlay in overlays) overlay.Dispose(); tray.Visible=false; tray.Dispose(); timer.Dispose(); tips.Dispose(); base.OnFormClosing(e); }
}
