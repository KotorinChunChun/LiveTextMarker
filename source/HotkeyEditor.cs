using System.Windows.Forms;
namespace PointerUnderline;
internal sealed record HotkeyBinding(uint Modifiers,int Key)
{
 public static HotkeyBinding ToggleDefault=>new(3,(int)Keys.U);
 public static HotkeyBinding QuitDefault=>new(3,(int)Keys.Q);
 public bool Enabled=>Modifiers!=0 || Key!=0;
}
internal sealed class HotkeyEditor : FlowLayoutPanel
{
 private readonly CheckBox ctrl=new(){Text="Ctrl",AutoSize=true};
 private readonly CheckBox alt=new(){Text="Alt",AutoSize=true};
 private readonly CheckBox shift=new(){Text="Shift",AutoSize=true};
 private readonly CheckBox win=new(){Text="Win",AutoSize=true};
 private readonly ComboBox key=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=160};
 private sealed record KeyChoice(int Code,string Name) { public override string ToString()=>Name; }
 public event EventHandler? BindingChanged;
 public HotkeyBinding Binding=>new((ctrl.Checked?2u:0)|(alt.Checked?1u:0)|(shift.Checked?4u:0)|(win.Checked?8u:0),((KeyChoice?)key.SelectedItem)?.Code??0);
 public HotkeyEditor(string label,HotkeyBinding initial) {
  AutoSize=true; Dock=DockStyle.Fill; WrapContents=false;
  Controls.Add(new Label{Text=label,AutoSize=false,Width=105,Height=28,TextAlign=System.Drawing.ContentAlignment.MiddleLeft});
  Controls.AddRange(new Control[]{ctrl,alt,shift,win,key});
  key.AccessibleName=label+"のキー";
  key.Items.Add(new KeyChoice(0,"なし"));
  foreach(var code in Enum.GetValues<Keys>().Select(k=>(int)k).Where(k=>k>0 && k<256).Distinct().OrderBy(k=>k)) key.Items.Add(new KeyChoice(code,((Keys)code).ToString()));
  ctrl.Checked=(initial.Modifiers&2)!=0; alt.Checked=(initial.Modifiers&1)!=0; shift.Checked=(initial.Modifiers&4)!=0; win.Checked=(initial.Modifiers&8)!=0;
  key.SelectedIndex=0;
  for(int i=0;i<key.Items.Count;i++) if(key.Items[i] is KeyChoice choice && choice.Code==initial.Key) key.SelectedIndex=i;
  foreach(var check in new[]{ctrl,alt,shift,win}) check.CheckedChanged+=(_,_)=>BindingChanged?.Invoke(this,EventArgs.Empty);
  key.SelectedIndexChanged+=(_,_)=>BindingChanged?.Invoke(this,EventArgs.Empty);
 }
 public bool ModifierOnlyDown {
  get {
   var binding=Binding;
   if(binding.Key!=0 || binding.Modifiers==0) return false;
   uint down=0;
   if((Native.GetAsyncKeyState(0x11)&0x8000)!=0) down|=2;
   if((Native.GetAsyncKeyState(0x12)&0x8000)!=0) down|=1;
   if((Native.GetAsyncKeyState(0x10)&0x8000)!=0) down|=4;
   if(((Native.GetAsyncKeyState(0x5b)|Native.GetAsyncKeyState(0x5c))&0x8000)!=0) down|=8;
   return down==binding.Modifiers;
  }
 }
}
