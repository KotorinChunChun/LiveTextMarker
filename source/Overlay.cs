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
 public void Draw(Box box, Color color) { if(!double.IsFinite(box.X) || !double.IsFinite(box.Y) || !double.IsFinite(box.Width) || !double.IsFinite(box.Height) || box.Width<=0 || box.Height<=0) { Hide(); return; } BackColor=color; Bounds=new Rectangle((int)Math.Floor(box.X),(int)Math.Floor(box.Y),Math.Max(1,(int)Math.Ceiling(box.Width)),Math.Max(1,(int)Math.Ceiling(box.Height))); if(!Visible) Show(); Native.SetWindowPos(Handle,(nint)(-1),Left,Top,Width,Height,0x0010|0x0040); }
}
