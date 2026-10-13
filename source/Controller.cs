using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace LiveTextMarker;
internal sealed partial class Controller : Form
{
    private KotorinToys.Integration.ManagementEndpoint? management;
    private readonly List<Overlay> overlays = new();
    private readonly Worker worker = new();
    private readonly System.Windows.Forms.Timer timer = new()
    {
        Interval = 20
    };
    private readonly NotifyIcon tray;
    private readonly Button toggle = new()
    {
        Text = "開始 / Start",
        AutoSize = true
    };
    private readonly CheckBox ocr = new()
    {
        Text = "画像・スライドにも対応（ローカルOCR）",
        Checked = true,
        AutoSize = true
    };
    private readonly CheckBox ctrlOnly = new()
    {
        Text = "Ctrl押下で実行",
        AutoSize = true
    };
    private readonly NumericUpDown thickness = new()
    {
        Minimum = 1,
        Maximum = 12,
        Value = 3,
        Width = 65
    };
    private readonly NumericUpDown offset = new()
    {
        Minimum = -30,
        Maximum = 30,
        Value = -2,
        DecimalPlaces = 1,
        Increment = 0.5m,
        Width = 75
    };
    private readonly ComboBox displayMode = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 100,
        AccessibleName = "表示方法"
    };
    private readonly NumericUpDown opacity = new()
    {
        Minimum = 8,
        Maximum = 30,
        Value = 16,
        Width = 65,
        AccessibleName = "濃さ"
    };
    private readonly ListBox log = new()
    {
        Dock = DockStyle.Fill,
        IntegralHeight = false,
        HorizontalScrollbar = true
    };
    private readonly ToolTip tips = new();
    private bool wasActive;
    private string lastLog = "";
    private bool Active => ActivationGate.Active(enabled, ctrlOnly.Checked, (Native.GetAsyncKeyState(0x11) & 0x8000) != 0);

    private readonly Label status = new()
    {
        Text = "待機中。開始ボタンで追従を開始します。",
        AutoSize = true,
        MaximumSize = new Size(475, 0)
    };
    private Color lineColor = Color.FromArgb(239, 45, 56);
    private Color highlightColor = Color.Gold;
    private bool enabled, busy, closing;
    private Box? current;
    private Box[] currentLines = Array.Empty<Box>();
    private Box? currentScope, currentVisible;
    private nint currentWindow;
    private Native.POINT last;
    private long movedAt, lastQuery;
    private int generation;
    private nint lastForeground;
    private string? cachedImage;
    private Detection? cachedOcr;
    private Rectangle cachedCrop;
    private readonly HotkeyEditor toggleKey, quitKey;
    private bool exitRequested, toggleModifiersDown, quitModifiersDown, hotkeyCollision;
    public Controller()
    {
        var settings = Settings.Load();
        ctrlOnly.Checked = settings.CtrlOnly;
        ocr.Checked = settings.Ocr;
        toggleKey = new HotkeyEditor("開始／一時停止", settings.ToggleKey ?? HotkeyBinding.ToggleDefault);
        quitKey = new HotkeyEditor("アプリ終了", settings.QuitKey ?? HotkeyBinding.QuitDefault);
        thickness.Value = Math.Clamp(settings.Thickness, 1, 12);
        offset.Value = Math.Clamp(settings.Offset, -30, 30);
        lineColor = Color.FromArgb(settings.Color);
        highlightColor = Color.FromArgb(settings.HighlightColor);
        opacity.Value = Math.Clamp(settings.HighlightOpacity, 8, 30);
        displayMode.Items.AddRange(new object[] { "下線", "塗り" });
        displayMode.SelectedIndex = settings.Highlight ? 1 : 0;
        Text = WindowTitle.Create("設定");
        ClientSize = new Size(680, 540);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MinimizeBox = false;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        using (var iconStream = typeof(Controller).Assembly.GetManifestResourceStream("LiveTextMarker.AppIcon")!)
        using (var appIcon = new Icon(iconStream))
            Icon = (Icon)appIcon.Clone();
        Font = new Font("Yu Gothic UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(245, 246, 248);
        ForeColor = Color.FromArgb(32, 38, 46);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(20)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 6; i++)
            layout.RowStyles.Add(new RowStyle(i == 4 ? SizeType.Percent : SizeType.AutoSize, i == 4 ? 100 : 0));
        TableLayoutPanel Section(int rows)
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = rows,
                Padding = new Padding(10),
                BackColor = Color.White,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                Margin = new Padding(0, 0, 0, 10)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < rows; i++)
                panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.Paint += (_, e) => ControlPaint.DrawBorder(e.Graphics, panel.ClientRectangle, Color.FromArgb(210, 215, 221), ButtonBorderStyle.Solid);
            return panel;
        }

        Label Field(string title) => new()
        {
            Text = title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty
        };
        var shortcuts = Section(2);
        shortcuts.Controls.Add(toggleKey, 0, 0);
        shortcuts.Controls.Add(quitKey, 0, 1);
        layout.Controls.Add(shortcuts, 0, 0);
        var checks = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 10)
        };
        checks.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var check in new[]
        {
            ocr,
            ctrlOnly
        }

        )
        {
            check.Margin = new Padding(0, 2, 0, 6);
            check.Anchor = AnchorStyles.Left;
        }

        checks.Controls.Add(ocr, 0, 0);
        checks.Controls.Add(ctrlOnly, 0, 1);
        layout.Controls.Add(checks, 0, 1);
        var appearance = Section(1);
        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 5,
            RowCount = 2,
            Margin = Padding.Empty
        };
        foreach (var width in new[]
        {
            112,
            120,
            100,
            90
        }

        )
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, width));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        fields.Controls.Add(Field("表示方法"), 0, 0);
        fields.Controls.Add(displayMode, 1, 0);
        fields.Controls.Add(Field("濃さ（%）"), 2, 0);
        fields.Controls.Add(opacity, 3, 0);
        fields.Controls.Add(Field("太さ"), 0, 1);
        fields.Controls.Add(thickness, 1, 1);
        fields.Controls.Add(Field("上下位置"), 2, 1);
        fields.Controls.Add(offset, 3, 1);
        foreach (var control in new Control[]
        {
            displayMode,
            opacity,
            thickness,
            offset
        }

        )
        {
            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(0, 3, 12, 3);
        }

        tips.SetToolTip(opacity, "塗りの不透明度。文字を透かして読むため8～30%に制限しています。");
        tips.SetToolTip(offset, "文字範囲の下端からの距離。負の値で上、正の値で下へ移動します（96 DPI基準）。");
        var color = new Button
        {
            Text = "色を選ぶ",
            Dock = DockStyle.Fill,
            Margin = new Padding(4, 2, 0, 2)
        };
        color.Click += (_, _) =>
        {
            using var d = new ColorDialog
            {
                Color = displayMode.SelectedIndex == 1 ? highlightColor : lineColor
            };
            if (d.ShowDialog(this) == DialogResult.OK)
            {
                if (displayMode.SelectedIndex == 1)
                    highlightColor = d.Color;
                else
                    lineColor = d.Color;
                Clear();
                SaveSettings();
            }
        };
        fields.Controls.Add(color, 4, 1);
        appearance.Controls.Add(fields, 0, 0);
        layout.Controls.Add(appearance, 0, 2);
        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 10)
        };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toggle.MinimumSize = new Size(108, 34);
        toggle.Margin = new Padding(0, 0, 10, 0);
        toggle.FlatStyle = FlatStyle.Flat;
        toggle.FlatAppearance.BorderSize = 0;
        toggle.BackColor = Color.FromArgb(0, 102, 204);
        toggle.ForeColor = Color.White;
        toggle.Click += (_, _) => SetEnabled(!enabled);
        actions.Controls.Add(toggle, 0, 0);
        var minimize = new Button
        {
            Text = "通知領域へ",
            AutoSize = true,
            MinimumSize = new Size(112, 32),
            Margin = new Padding(0, 0, 10, 0),
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom
        };
        minimize.Click += (_, _) => Hide();
        status.Dock = DockStyle.Fill;
        status.TextAlign = ContentAlignment.MiddleRight;
        status.MaximumSize = Size.Empty;
        status.Margin = new Padding(12, 0, 0, 0);
        actions.Controls.Add(status, 1, 0);
        layout.Controls.Add(actions, 0, 3);
        log.Margin = Padding.Empty;
        log.BackColor = Color.White;
        log.ForeColor = ForeColor;
        log.BorderStyle = BorderStyle.FixedSingle;
        layout.Controls.Add(log, 0, 4);
        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 3,
            Margin = new Padding(0, 10, 0, 0)
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var count = Field("直近10件");
        count.ForeColor = Color.FromArgb(96, 105, 117);
        bottom.Controls.Add(count, 0, 0);
        var quit = new Button
        {
            Text = "アプリを終了させる",
            AutoSize = true,
            MinimumSize = new Size(156, 32),
            Margin = Padding.Empty,
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom
        };
        quit.Click += (_, _) => ExitApplication();
        bottom.Controls.Add(minimize, 1, 0);
        bottom.Controls.Add(quit, 2, 0);
        layout.Controls.Add(bottom, 0, 5);
        Controls.Add(layout);
        var menu = new ContextMenuStrip();
        menu.Items.Add("開始 / 一時停止", null, (_, _) => SetEnabled(!enabled));
        menu.Items.Add("設定を表示", null, (_, _) =>
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        });
        menu.Items.Add("アプリを終了させる", null, (_, _) => ExitApplication());
        tray = new NotifyIcon
        {
            Icon = Icon,
            Text = "LiveTextMarker — 停止中",
            Visible = true,
            ContextMenuStrip = menu
        };
        tray.DoubleClick += (_, _) =>
        {
            Show();
            Activate();
        };
        _ = Handle;
        ApplyHotkeys();
        toggleKey.BindingChanged += (_, _) =>
        {
            ApplyHotkeys();
            SaveSettings();
        };
        quitKey.BindingChanged += (_, _) =>
        {
            ApplyHotkeys();
            SaveSettings();
        };
        ocr.CheckedChanged += (_, _) =>
        {
            generation++;
            Clear();
            cachedImage = null;
            SaveSettings();
        };
        ctrlOnly.CheckedChanged += (_, _) =>
        {
            generation++;
            Clear();
            SaveSettings();
            status.Text = Active ? "追従中" : ctrlOnly.Checked ? "Ctrl待機中" : "一時停止中";
            AddLog(ctrlOnly.Checked ? "Ctrl押下を有効化" : "Ctrl押下を無効化");
        };
        thickness.ValueChanged += (_, _) =>
        {
            Clear();
            SaveSettings();
        };
        offset.ValueChanged += (_, _) =>
        {
            Clear();
            SaveSettings();
        };
        displayMode.SelectedIndexChanged += (_, _) =>
        {
            generation++;
            Clear();
            UpdateModeControls();
            SaveSettings();
            AddLog("表示方法 · " + displayMode.Text);
        };
        opacity.ValueChanged += (_, _) =>
        {
            Clear();
            SaveSettings();
        };
        UpdateModeControls();
        timer.Tick += Tick;
        timer.Start();
    }

    private void AddLog(string message)
    {
        if (message == lastLog)
            return;
        lastLog = message;
        log.Items.Insert(0, $"{DateTime.Now:HH:mm:ss}  {message}");
        while (log.Items.Count > 10)
            log.Items.RemoveAt(log.Items.Count - 1);
    }

    private void SetEnabled(bool value)
    {
        enabled = value;
        generation++;
        cachedImage = null;
        cachedOcr = null;
        Clear();
        toggle.Text = value ? "一時停止 / Pause" : "開始 / Start";
        tray.Text = value ? "LiveTextMarker — 追従中" : "LiveTextMarker — 停止中";
        status.Text = Active ? "追従中。文字の上にポインタを移動してください。" : ctrlOnly.Checked ? "Ctrl待機中" : "一時停止中";
        AddLog(value ? "開始" : "一時停止");
        lastQuery = 0;
    }
}
