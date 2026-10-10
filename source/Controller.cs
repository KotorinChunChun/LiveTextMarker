using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
namespace LiveTextMarker;
internal sealed class Controller : Form
{
 private readonly List<Overlay> overlays=new();
 private readonly Worker worker=new();
 private readonly System.Windows.Forms.Timer timer=new() { Interval=20 };
 private readonly NotifyIcon tray;
 private readonly Button toggle=new() { Text="開始 / Start", AutoSize=true };
 private readonly CheckBox ocr=new() { Text="画像・スライドにも対応（ローカルOCR）",Checked=true,AutoSize=true };
 private readonly CheckBox ctrlOnly=new() { Text="Ctrl押下で実行",AutoSize=true };
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
 private Box? currentScope,currentVisible;
 private nint currentWindow;
 private Native.POINT last;
 private long movedAt,lastQuery;
 private int generation;
 private nint lastForeground;
 private string? cachedImage;
 private Detection? cachedOcr;
 private Rectangle cachedCrop;
 private readonly HotkeyEditor toggleKey,quitKey;
 private bool exitRequested,toggleModifiersDown,quitModifiersDown,hotkeyCollision;
 public Controller()
 {
  var settings=Settings.Load(); ctrlOnly.Checked=settings.CtrlOnly; ocr.Checked=settings.Ocr;
  toggleKey=new HotkeyEditor("開始／一時停止",settings.ToggleKey??HotkeyBinding.ToggleDefault);
  quitKey=new HotkeyEditor("アプリ終了",settings.QuitKey??HotkeyBinding.QuitDefault);
  thickness.Value=Math.Clamp(settings.Thickness,1,12); offset.Value=Math.Clamp(settings.Offset,-30,30); lineColor=Color.FromArgb(settings.Color);
  highlightColor=Color.FromArgb(settings.HighlightColor); opacity.Value=Math.Clamp(settings.HighlightOpacity,8,30);
  displayMode.Items.AddRange(new object[]{"下線","塗り"}); displayMode.SelectedIndex=settings.Highlight?1:0;
  var version=typeof(Controller).Assembly.GetName().Version!;
  Text=$"設定 - LiveTextMarker - ver {version.Major}.{version.Minor}.{version.Build}"; ClientSize=new Size(680,540); MinimumSize=new Size(696,579); StartPosition=FormStartPosition.CenterScreen;
  Font=new Font("Yu Gothic UI",10); AutoScaleMode=AutoScaleMode.Dpi;
  BackColor=Color.FromArgb(245,246,248); ForeColor=Color.FromArgb(32,38,46);
  var layout=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=6,Padding=new Padding(20) };
  layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
  for(int i=0;i<6;i++) layout.RowStyles.Add(new RowStyle(i==4?SizeType.Percent:SizeType.AutoSize,i==4?100:0));
  TableLayoutPanel Section(int rows) {
   var panel=new TableLayoutPanel { Dock=DockStyle.Fill,AutoSize=true,ColumnCount=1,RowCount=rows,Padding=new Padding(10),BackColor=Color.White,CellBorderStyle=TableLayoutPanelCellBorderStyle.None,Margin=new Padding(0,0,0,10) };
   panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   for(int i=0;i<rows;i++) panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
   panel.Paint+=(_,e)=>ControlPaint.DrawBorder(e.Graphics,panel.ClientRectangle,Color.FromArgb(210,215,221),ButtonBorderStyle.Solid);
   return panel;
  }
  Label Field(string title)=>new(){Text=title,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Margin=Padding.Empty};
  var shortcuts=Section(2); shortcuts.Controls.Add(toggleKey,0,0); shortcuts.Controls.Add(quitKey,0,1); layout.Controls.Add(shortcuts,0,0);
  var checks=new TableLayoutPanel { Dock=DockStyle.Fill,AutoSize=true,ColumnCount=1,RowCount=2,Margin=new Padding(0,0,0,10) };
  checks.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
  foreach(var check in new[]{ocr,ctrlOnly}) { check.Margin=new Padding(0,2,0,6); check.Anchor=AnchorStyles.Left; }
  checks.Controls.Add(ocr,0,0); checks.Controls.Add(ctrlOnly,0,1); layout.Controls.Add(checks,0,1);
  var appearance=Section(1);
  var fields=new TableLayoutPanel { Dock=DockStyle.Fill,AutoSize=true,ColumnCount=5,RowCount=2,Margin=Padding.Empty };
  foreach(var width in new[]{112,120,100,90}) fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,width));
  fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
  fields.RowStyles.Add(new RowStyle(SizeType.Absolute,34)); fields.RowStyles.Add(new RowStyle(SizeType.Absolute,34));
  fields.Controls.Add(Field("表示方法"),0,0); fields.Controls.Add(displayMode,1,0); fields.Controls.Add(Field("濃さ（%）"),2,0); fields.Controls.Add(opacity,3,0);
  fields.Controls.Add(Field("太さ"),0,1); fields.Controls.Add(thickness,1,1); fields.Controls.Add(Field("上下位置"),2,1); fields.Controls.Add(offset,3,1);
  foreach(var control in new Control[]{displayMode,opacity,thickness,offset}) { control.Dock=DockStyle.Fill; control.Margin=new Padding(0,3,12,3); }
  tips.SetToolTip(opacity,"塗りの不透明度。文字を透かして読むため8～30%に制限しています。");
  tips.SetToolTip(offset,"文字範囲の下端からの距離。負の値で上、正の値で下へ移動します（96 DPI基準）。");
  var color=new Button { Text="色を選ぶ",Dock=DockStyle.Fill,Margin=new Padding(4,2,0,2) };
  color.Click+=(_,_)=> { using var d=new ColorDialog { Color=displayMode.SelectedIndex==1?highlightColor:lineColor }; if(d.ShowDialog(this)==DialogResult.OK) { if(displayMode.SelectedIndex==1) highlightColor=d.Color; else lineColor=d.Color; Clear(); SaveSettings(); } };
  fields.Controls.Add(color,4,1); appearance.Controls.Add(fields,0,0); layout.Controls.Add(appearance,0,2);
  var actions=new TableLayoutPanel { Dock=DockStyle.Fill,AutoSize=true,ColumnCount=3,RowCount=1,Margin=new Padding(0,0,0,10) };
  actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
  toggle.MinimumSize=new Size(108,34); toggle.Margin=new Padding(0,0,10,0); toggle.FlatStyle=FlatStyle.Flat; toggle.FlatAppearance.BorderSize=0; toggle.BackColor=Color.FromArgb(0,102,204); toggle.ForeColor=Color.White;
  toggle.Click+=(_,_)=>SetEnabled(!enabled); actions.Controls.Add(toggle,0,0);
  var minimize=new Button { Text="通知領域へ",AutoSize=true,MinimumSize=new Size(112,34),Margin=Padding.Empty }; minimize.Click+=(_,_)=>Hide(); actions.Controls.Add(minimize,1,0);
  status.Dock=DockStyle.Fill; status.TextAlign=ContentAlignment.MiddleRight; status.MaximumSize=Size.Empty; status.Margin=new Padding(12,0,0,0); actions.Controls.Add(status,2,0); layout.Controls.Add(actions,0,3);
  log.Margin=Padding.Empty; log.BackColor=Color.White; log.ForeColor=ForeColor; log.BorderStyle=BorderStyle.FixedSingle; layout.Controls.Add(log,0,4);
  var bottom=new TableLayoutPanel { Dock=DockStyle.Fill,AutoSize=true,ColumnCount=2,Margin=new Padding(0,10,0,0) };
  bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
  var count=Field("直近10件"); count.ForeColor=Color.FromArgb(96,105,117); bottom.Controls.Add(count,0,0);
  var quit=new Button { Text="アプリを終了させる",AutoSize=true,MinimumSize=new Size(156,32),Margin=Padding.Empty,Anchor=AnchorStyles.Right|AnchorStyles.Bottom }; quit.Click+=(_,_)=>ExitApplication(); bottom.Controls.Add(quit,1,0);
  layout.Controls.Add(bottom,0,5); Controls.Add(layout);
  var menu=new ContextMenuStrip(); menu.Items.Add("開始 / 一時停止",null,(_,_)=>SetEnabled(!enabled)); menu.Items.Add("設定を表示",null,(_,_)=>{ Show(); WindowState=FormWindowState.Normal; Activate(); }); menu.Items.Add("アプリを終了させる",null,(_,_)=>ExitApplication());
  tray=new NotifyIcon { Icon=SystemIcons.Information,Text="LiveTextMarker — 停止中",Visible=true,ContextMenuStrip=menu }; tray.DoubleClick+=(_,_)=>{ Show(); Activate(); };
  _=Handle; ApplyHotkeys();
  toggleKey.BindingChanged+=(_,_)=> { ApplyHotkeys(); SaveSettings(); };
  quitKey.BindingChanged+=(_,_)=> { ApplyHotkeys(); SaveSettings(); };
  ocr.CheckedChanged+=(_,_)=> { generation++; Clear(); cachedImage=null; SaveSettings(); };
  ctrlOnly.CheckedChanged+=(_,_)=> { generation++; Clear(); SaveSettings(); status.Text=Active?"追従中":ctrlOnly.Checked?"Ctrl待機中":"一時停止中"; AddLog(ctrlOnly.Checked?"Ctrl押下を有効化":"Ctrl押下を無効化"); };
  thickness.ValueChanged+=(_,_)=> { Clear(); SaveSettings(); }; offset.ValueChanged+=(_,_)=> { Clear(); SaveSettings(); };
  displayMode.SelectedIndexChanged+=(_,_)=> { generation++; Clear(); UpdateModeControls(); SaveSettings(); AddLog("表示方法 · "+displayMode.Text); };
  opacity.ValueChanged+=(_,_)=> { Clear(); SaveSettings(); };
  UpdateModeControls();
  timer.Tick+=Tick; timer.Start();
 }
 private void ExitApplication() { exitRequested=true; Close(); }
 private void ApplyHotkeys() {
  Native.UnregisterHotKey(Handle,1); Native.UnregisterHotKey(Handle,2);
  toggleModifiersDown=toggleKey.ModifierOnlyDown; quitModifiersDown=quitKey.ModifierOnlyDown;
  bool Register(int id,HotkeyBinding binding)=>binding.Key==0 || Native.RegisterHotKey(Handle,id,binding.Modifiers|0x4000,(uint)binding.Key);
  var toggleOk=Register(1,toggleKey.Binding); var quitOk=Register(2,quitKey.Binding);
  hotkeyCollision=toggleKey.Binding.Enabled && toggleKey.Binding==quitKey.Binding;
  if(hotkeyCollision) {
   Native.UnregisterHotKey(Handle,1); Native.UnregisterHotKey(Handle,2);
   status.Text="開始と終了には異なるキーを設定してください。"; AddLog(status.Text);
  } else if(!toggleOk || !quitOk) {
   status.Text="キーを登録できません。ほかの割り当てを選んでください。"; AddLog(status.Text);
  } else status.Text=Active?"追従中":ctrlOnly.Checked?"Ctrl待機中":"一時停止中";
 }
 private void UpdateModeControls() { var fill=displayMode.SelectedIndex==1; thickness.Enabled=!fill; offset.Enabled=!fill; opacity.Enabled=fill; }
 private void SaveSettings() { try { new Settings(ctrlOnly.Checked,ocr.Checked,(int)thickness.Value,offset.Value,lineColor.ToArgb(),displayMode.SelectedIndex==1,(int)opacity.Value,highlightColor.ToArgb(),toggleKey.Binding,quitKey.Binding).Save(); } catch(Exception ex) { AddLog("設定保存: "+ex.GetType().Name); } }
 private void AddLog(string message) { if(message==lastLog) return; lastLog=message; log.Items.Insert(0,$"{DateTime.Now:HH:mm:ss}  {message}"); while(log.Items.Count>10) log.Items.RemoveAt(log.Items.Count-1); }
 private void SetEnabled(bool value) { enabled=value; generation++; cachedImage=null; cachedOcr=null; Clear(); toggle.Text=value?"一時停止 / Pause":"開始 / Start"; tray.Text=value?"LiveTextMarker — 追従中":"LiveTextMarker — 停止中"; status.Text=Active?"追従中。文字の上にポインタを移動してください。":ctrlOnly.Checked?"Ctrl待機中":"一時停止中"; AddLog(value?"開始":"一時停止"); lastQuery=0; }
 private void Clear() { current=null; currentLines=Array.Empty<Box>(); currentScope=null; currentVisible=null; currentWindow=0; HideLines(); }
 private Box? VisibleScope(nint window,Box scope,Native.POINT point)=>WindowRegion.Visible(window,scope,point.X,point.Y,overlays.Where(o=>o.IsHandleCreated).Select(o=>o.Handle));
 private void HideLines() { foreach(var overlay in overlays) overlay.HideLine(); }
 protected override void WndProc(ref Message m) { if(m.Msg==0x0312) { if(m.WParam==(nint)1) SetEnabled(!enabled); else if(m.WParam==(nint)2) ExitApplication(); } base.WndProc(ref m); }
 private async void Tick(object? sender,EventArgs args)
 {
  var toggleDown=toggleKey.ModifierOnlyDown; var quitDown=quitKey.ModifierOnlyDown;
  if(!hotkeyCollision && quitDown&&!quitModifiersDown) { ExitApplication(); return; }
  if(!hotkeyCollision && toggleDown&&!toggleModifiersDown) SetEnabled(!enabled);
  toggleModifiersDown=toggleDown; quitModifiersDown=quitDown;
  var active=Active;
  if(active!=wasActive) { wasActive=active; generation++; Clear(); cachedImage=null; lastQuery=0; status.Text=active?"追従中":ctrlOnly.Checked?"Ctrl待機中":"一時停止中"; AddLog(status.Text); }
  if(closing||!active||!Native.GetCursorPos(out var point)) return;
  var now=Environment.TickCount64;
  var foreground=Native.GetForegroundWindow();
  if(foreground!=lastForeground) { lastForeground=foreground; generation++; Clear(); cachedImage=null; }
  if(point.X!=last.X||point.Y!=last.Y) { last=point; movedAt=now; cachedImage=null; if(current is {} b&&!b.Contains(point.X,point.Y)) Clear(); }
  if(current is {} shown && currentScope is {} scopeNow) {
   var visibleNow=VisibleScope(currentWindow,scopeNow,point);
   if(visibleNow==null) Clear();
   else if(visibleNow!=currentVisible) { currentVisible=visibleNow; ShowLine(shown,point); cachedImage=null; }
  }
  if(busy||now-movedAt<65||now-lastQuery<180) return;
  var window=Native.GetAncestor(Native.WindowFromPoint(point),2);
  Native.GetWindowThreadProcessId(window,out var pid);
  if(pid==Environment.ProcessId) { Clear(); return; }
  busy=true; lastQuery=now; var version=generation;
  try {
   var result=await worker.Query(new Request("uia",point.X,point.Y,TargetWindow:(long)window),650);
   if(closing||!Active||version!=generation) return;
   if(result.Scope is not {} frame || result.ScopeId==null) { Clear(); status.Text=result.Sensitive?"パスワード欄では認識を停止します。":result.Error ?? "対象領域を取得できません。"; AddLog(status.Text); return; }
   var visibleScope=VisibleScope(window,frame,point);
   if(visibleScope==null) { Clear(); return; }
   if(result.Bounds==null&&!result.Sensitive&&ocr.Checked) {
    // Reject stale requests before capturing another location.
    Native.GetCursorPos(out var next);
    if(Math.Abs(next.X-point.X)>4||Math.Abs(next.Y-point.Y)>4) return;
    var screen=Screen.FromPoint(new Point(point.X,point.Y)).Bounds;
    int cropHeight=Math.Min(screen.Height,Math.Max(180,(int)(240*Native.Scale(point))));
    var crop=new Rectangle(screen.Left,Math.Clamp(point.Y-cropHeight/2,screen.Top,screen.Bottom-cropHeight),screen.Width,cropHeight);
    var limit=visibleScope.Value;
    crop=Rectangle.Intersect(crop,Rectangle.FromLTRB((int)Math.Ceiling(limit.X),(int)Math.Ceiling(limit.Y),(int)Math.Floor(limit.Right),(int)Math.Floor(limit.Bottom)));
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
    if(image==cachedImage&&crop==cachedCrop&&cachedOcr!=null&&cachedOcr.ScopeId==result.ScopeId) result=cachedOcr;
    else { result=await worker.Query(new Request("ocr",point.X,point.Y,image,crop.X,crop.Y,(long)window,limit,result.ScopeId),2500); cachedImage=result.Error==null?image:null; cachedCrop=crop; cachedOcr=result; }
   }
   if(closing||!Active||version!=generation) return;
   Native.GetCursorPos(out var live);
   var liveWindow=Native.GetAncestor(Native.WindowFromPoint(live),2);
   if(liveWindow!=window||Native.GetForegroundWindow()!=foreground) { Clear(); return; }
   if(result.Bounds!=null) {
    var fresh=await worker.Query(new Request("scope",live.X,live.Y,TargetWindow:(long)window),650);
    if(closing||!Active||version!=generation || fresh.Scope!=frame || fresh.ScopeId!=result.ScopeId) { Clear(); return; }
    Native.GetCursorPos(out live);
    if(Native.GetAncestor(Native.WindowFromPoint(live),2)!=window || Native.GetForegroundWindow()!=foreground) { Clear(); return; }
    visibleScope=VisibleScope(window,frame,live);
    if(visibleScope==null) { Clear(); return; }
   }
   if(DetectionGate.Accept(result.Bounds,Active,closing,version,generation,(long)window,(long)liveWindow,(long)foreground,(long)Native.GetForegroundWindow(),live.X,live.Y,point.Y,Native.Scale(live))) { var box=result.Bounds!.Value; current=box; currentScope=frame; currentVisible=visibleScope; currentWindow=window; currentLines=Geometry.ClipRows(result.Lines ?? new[]{box},frame); ShowLine(box,live); status.Text=$"追従中 · {result.Source} · {currentLines.Length}行"; AddLog($"{result.Source}{(result.Source=="OCR"?"（UIA該当なし）":"")} · {currentLines.Length}行 · 幅 {box.Width:0}px"); }
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
   if(currentScope is {} frame) screen=screen.Intersect(frame);
   if(currentVisible is {} visible) screen=screen.Intersect(visible);
   var fill=displayMode.SelectedIndex==1;
   var line=fill?Geometry.Highlight(lines[i],screen):Geometry.Underline(lines[i],screen,Native.Scale(point),(int)thickness.Value,(double)offset.Value);
   if(line.Width>0&&line.Height>0) overlays[i].Draw(line,fill?highlightColor:lineColor,fill?Geometry.HighlightAlpha((int)opacity.Value):(byte)255); else overlays[i].HideLine();
  }
 }
 protected override void OnFormClosing(FormClosingEventArgs e) { if(!exitRequested && e.CloseReason==CloseReason.UserClosing) { e.Cancel=true; SaveSettings(); Hide(); return; } SaveSettings(); closing=true; enabled=false; generation++; timer.Stop(); Native.UnregisterHotKey(Handle,1); Native.UnregisterHotKey(Handle,2); worker.Dispose(); foreach(var overlay in overlays) overlay.Dispose(); tray.Visible=false; tray.Dispose(); timer.Dispose(); tips.Dispose(); base.OnFormClosing(e); }
}
