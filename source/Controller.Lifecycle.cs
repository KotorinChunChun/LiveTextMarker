using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace LiveTextMarker;
internal sealed partial class Controller
{
    internal void StartManagement()
    {
        management ??= new("live-text-marker", this, () =>
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }, ExitApplication);
    }

    private void ExitApplication()
    {
        management?.Dispose();
        exitRequested = true;
        Close();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312)
        {
            if (m.WParam == (nint)1)
                SetEnabled(!enabled);
            else if (m.WParam == (nint)2)
                ExitApplication();
        }

        base.WndProc(ref m);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!exitRequested && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            SaveSettings();
            Hide();
            return;
        }

        SaveSettings();
        closing = true;
        enabled = false;
        generation++;
        timer.Stop();
        Native.UnregisterHotKey(Handle, 1);
        Native.UnregisterHotKey(Handle, 2);
        worker.Dispose();
        foreach (var overlay in overlays)
            overlay.Dispose();
        tray.Visible = false;
        tray.Dispose();
        timer.Dispose();
        tips.Dispose();
        base.OnFormClosing(e);
    }
}
