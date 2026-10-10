using System.Drawing;
using System.Windows.Forms;
namespace PointerUnderline;
internal sealed class Overlay : Form
{
 public Overlay() { FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false; TopMost=true; BackColor=Color.FromArgb(239,45,56); AutoScaleMode=AutoScaleMode.None; StartPosition=FormStartPosition.Manual; }
 protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Native.SetLayeredWindowAttributes(Handle,0,255,2); }
 protected override bool ShowWithoutActivation=>true;
 protected override CreateParams CreateParams { get { var p=base.CreateParams; p.ExStyle|=0x08000000|0x00000080|0x00000020|0x00080000; return p; } }
 protected override void WndProc(ref Message m) { if(m.Msg==0x0084) { m.Result=(nint)(-1); return; } if(m.Msg==0x0021) { m.Result=(nint)3; return; } base.WndProc(ref m); }
 private Rectangle? drawnBounds;
 public bool Displayed=>IsHandleCreated && Native.IsWindowVisible(Handle);
 public void HideLine() { if(Displayed) Native.SetWindowPos(Handle,0,0,0,0,0,0x0010|0x0080|0x0001|0x0002|0x0004); }
 // Exclude only during our synchronous OCR capture. Keep screen sharing enabled otherwise.
 public bool ExcludeFromCapture()=>OperatingSystem.IsWindowsVersionAtLeast(10,0,19041) && Native.SetWindowDisplayAffinity(Handle,0x11);
 public void RestoreCapture()=>Native.SetWindowDisplayAffinity(Handle,0);
 public void Draw(Box box, Color color) {
  if(!double.IsFinite(box.X) || !double.IsFinite(box.Y) || !double.IsFinite(box.Width) || !double.IsFinite(box.Height) || box.Width<=0 || box.Height<=0) { HideLine(); return; }
  var bounds=new Rectangle((int)Math.Floor(box.X),(int)Math.Floor(box.Y),Math.Max(1,(int)Math.Ceiling(box.Width)),Math.Max(1,(int)Math.Ceiling(box.Height)));
  if(drawnBounds==bounds && BackColor==color && Displayed) return;
  BackColor=color;
  // Bounds/Show/Hide can invoke additional WinForms positioning/activation paths.
  // Move, size and show the native window atomically with SWP_NOACTIVATE.
  if(Native.SetWindowPos(Handle,(nint)(-1),bounds.X,bounds.Y,bounds.Width,bounds.Height,0x0010|0x0040|0x0200)) drawnBounds=bounds;
 }
}
