using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace LiveTextMarker;
internal sealed partial class Controller
{
    private void ApplyHotkeys()
    {
        Native.UnregisterHotKey(Handle, 1);
        Native.UnregisterHotKey(Handle, 2);
        toggleModifiersDown = toggleKey.ModifierOnlyDown;
        quitModifiersDown = quitKey.ModifierOnlyDown;
        bool Register(int id, HotkeyBinding binding) => binding.Key == 0 || Native.RegisterHotKey(Handle, id, binding.Modifiers | 0x4000, (uint)binding.Key);
        var toggleOk = Register(1, toggleKey.Binding);
        var quitOk = Register(2, quitKey.Binding);
        hotkeyCollision = toggleKey.Binding.Enabled && toggleKey.Binding == quitKey.Binding;
        if (hotkeyCollision)
        {
            Native.UnregisterHotKey(Handle, 1);
            Native.UnregisterHotKey(Handle, 2);
            status.Text = "開始と終了には異なるキーを設定してください。";
            AddLog(status.Text);
        }
        else if (!toggleOk || !quitOk)
        {
            status.Text = "キーを登録できません。ほかの割り当てを選んでください。";
            AddLog(status.Text);
        }
        else
            status.Text = Active ? "追従中" : ctrlOnly.Checked ? "Ctrl待機中" : "一時停止中";
    }

    private void UpdateModeControls()
    {
        var fill = displayMode.SelectedIndex == 1;
        thickness.Enabled = !fill;
        offset.Enabled = !fill;
        opacity.Enabled = fill;
    }

    private void SaveSettings()
    {
        try
        {
            new Settings(ctrlOnly.Checked, ocr.Checked, (int)thickness.Value, offset.Value, lineColor.ToArgb(), displayMode.SelectedIndex == 1, (int)opacity.Value, highlightColor.ToArgb(), toggleKey.Binding, quitKey.Binding).Save();
        }
        catch (Exception ex)
        {
            AddLog("設定保存: " + ex.GetType().Name);
        }
    }
}
