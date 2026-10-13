using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace LiveTextMarker;

internal sealed partial class Controller
{
    private void Clear()
    {
        current = null;
        currentLines = Array.Empty<Box>();
        currentScope = null;
        currentVisible = null;
        currentWindow = 0;
        HideLines();
    }

    private Box? VisibleScope(nint window, Box scope, Native.POINT point) => WindowRegion.Visible(window, scope, point.X, point.Y, overlays.Where(o => o.IsHandleCreated).Select(o => o.Handle));
    private void HideLines()
    {
        foreach (var overlay in overlays)
            overlay.HideLine();
    }

    private async void Tick(object? sender, EventArgs args)
    {
        var toggleDown = toggleKey.ModifierOnlyDown;
        var quitDown = quitKey.ModifierOnlyDown;
        if (!hotkeyCollision && quitDown && !quitModifiersDown)
        {
            ExitApplication();
            return;
        }

        if (!hotkeyCollision && toggleDown && !toggleModifiersDown)
            SetEnabled(!enabled);
        toggleModifiersDown = toggleDown;
        quitModifiersDown = quitDown;
        var active = Active;
        if (active != wasActive)
        {
            wasActive = active;
            generation++;
            Clear();
            cachedImage = null;
            lastQuery = 0;
            status.Text = active ? "追従中" : ctrlOnly.Checked ? "Ctrl待機中" : "一時停止中";
            AddLog(status.Text);
        }

        if (closing || !active || !Native.GetCursorPos(out var point))
            return;
        var now = Environment.TickCount64;
        var foreground = Native.GetForegroundWindow();
        if (foreground != lastForeground)
        {
            lastForeground = foreground;
            generation++;
            Clear();
            cachedImage = null;
        }

        if (point.X != last.X || point.Y != last.Y)
        {
            last = point;
            movedAt = now;
            cachedImage = null;
            if (current is { } b && !Geometry.NearText(b, point.X, point.Y))
                Clear();
        }

        if (current is { } shown && currentScope is { } scopeNow)
        {
            var visibleNow = VisibleScope(currentWindow, scopeNow, point);
            if (visibleNow == null)
                Clear();
            else if (visibleNow != currentVisible)
            {
                currentVisible = visibleNow;
                ShowLine(shown, point);
                cachedImage = null;
            }
        }

        if (busy || now - movedAt < 65 || now - lastQuery < 180)
            return;
        var window = Native.GetAncestor(Native.WindowFromPoint(point), 2);
        if (overlays.Any(o => o.IsHandleCreated && o.Handle == window))
        {
            Clear();
            return;
        }

        busy = true;
        lastQuery = now;
        var version = generation;
        try
        {
            var result = await worker.Query(new Request("uia", point.X, point.Y, TargetWindow: (long)window), 650);
            var uiaDetail = result.Error ?? result.UiaDetail ?? "UIA文字範囲なし";
            if (closing || !Active || version != generation)
                return;
            if (result.Scope is not { } frame || result.ScopeId == null)
            {
                Clear();
                status.Text = result.Sensitive ? "パスワード欄では認識を停止します。" : result.Error ?? "対象領域を取得できません。";
                AddLog(status.Text);
                return;
            }

            var visibleScope = VisibleScope(window, frame, point);
            if (visibleScope == null)
            {
                Clear();
                return;
            }

            if (result.Bounds == null && !result.Sensitive && ocr.Checked)
            {
                // Reject stale requests before capturing another location.
                Native.GetCursorPos(out var next);
                if (Math.Abs(next.X - point.X) > 4 || Math.Abs(next.Y - point.Y) > 4)
                    return;
                var screen = Screen.FromPoint(new Point(point.X, point.Y)).Bounds;
                int cropHeight = Math.Min(screen.Height, Math.Max(180, (int)(240 * Native.Scale(point))));
                var crop = new Rectangle(screen.Left, Math.Clamp(point.Y - cropHeight / 2, screen.Top, screen.Bottom - cropHeight), screen.Width, cropHeight);
                var limit = visibleScope.Value;
                crop = Rectangle.Intersect(crop, Rectangle.FromLTRB((int)Math.Ceiling(limit.X), (int)Math.Ceiling(limit.Y), (int)Math.Floor(limit.Right), (int)Math.Floor(limit.Bottom)));
                if (crop.Width <= 0 || crop.Height <= 0)
                {
                    Clear();
                    return;
                }

                var visible = overlays.Where(o => o.Displayed).ToArray();
                var excluded = new List<Overlay>();
                var hidden = new List<Overlay>();
                using var bitmap = new Bitmap(crop.Width, crop.Height, PixelFormat.Format24bppRgb);
                try
                {
                    foreach (var overlay in visible)
                    {
                        if (overlay.ExcludeFromCapture())
                            excluded.Add(overlay);
                        else
                        {
                            overlay.HideLine();
                            hidden.Add(overlay);
                        }
                    }

                    if (visible.Length > 0)
                        Native.DwmFlush();
                    using (var graphics = Graphics.FromImage(bitmap))
                        graphics.CopyFromScreen(crop.Location, Point.Empty, crop.Size, CopyPixelOperation.SourceCopy);
                }
                finally
                {
                    foreach (var overlay in excluded)
                        overlay.RestoreCapture();
                    if (hidden.Count > 0 && current is { } old && Active)
                    {
                        ShowLine(old, point);
                        AddLog("OCR撮影 · 互換モード（下線を一時非表示）");
                    }
                }

                using var stream = new MemoryStream();
                bitmap.Save(stream, ImageFormat.Png);
                var image = Convert.ToBase64String(stream.ToArray());
                if (image == cachedImage && crop == cachedCrop && cachedOcr != null && cachedOcr.ScopeId == result.ScopeId)
                    result = cachedOcr;
                else
                {
                    result = await worker.Query(new Request("ocr", point.X, point.Y, image, crop.X, crop.Y, (long)window, limit, result.ScopeId), 2500);
                    cachedImage = result.Error == null ? image : null;
                    cachedCrop = crop;
                    cachedOcr = result;
                }
            }

            if (closing || !Active || version != generation)
                return;
            Native.GetCursorPos(out var live);
            var liveWindow = Native.GetAncestor(Native.WindowFromPoint(live), 2);
            if (liveWindow != window || Native.GetForegroundWindow() != foreground)
            {
                Clear();
                return;
            }

            if (result.Bounds != null)
            {
                var fresh = await worker.Query(new Request("scope", live.X, live.Y, TargetWindow: (long)window), 650);
                if (closing || !Active || version != generation || fresh.Scope != frame || fresh.ScopeId != result.ScopeId)
                {
                    Clear();
                    return;
                }

                Native.GetCursorPos(out live);
                if (Native.GetAncestor(Native.WindowFromPoint(live), 2) != window || Native.GetForegroundWindow() != foreground)
                {
                    Clear();
                    return;
                }

                visibleScope = VisibleScope(window, frame, live);
                if (visibleScope == null)
                {
                    Clear();
                    return;
                }
            }

            if (DetectionGate.Accept(result.Bounds, Active, closing, version, generation, (long)window, (long)liveWindow, (long)foreground, (long)Native.GetForegroundWindow(), live.X, live.Y, point.Y, Native.Scale(live)))
            {
                var box = result.Bounds!.Value;
                current = box;
                currentScope = frame;
                currentVisible = visibleScope;
                currentWindow = window;
                currentLines = Geometry.ClipRows(result.Lines ?? new[] { box }, frame);
                ShowLine(box, live);
                status.Text = $"追従中 · {result.Source} · {currentLines.Length}行";
                AddLog($"{result.Source}{(result.Source == "OCR" ? $"（{uiaDetail}）" : "")} · {currentLines.Length}行 · 幅 {box.Width:0}px");
            }
            else
            {
                Clear();
                status.Text = result.Sensitive ? "パスワード欄では認識を停止します。" : result.Error is { } error ? error : $"追従中 · {result.Source} · 該当なし";
                AddLog(status.Text);
            }
        }
        catch (Exception ex)
        {
            Clear();
            status.Text = "認識を再試行します: " + ex.GetType().Name;
            AddLog(status.Text);
        }
        finally
        {
            busy = false;
        }
    }

    private void ShowLine(Box box, Native.POINT point)
    {
        var s = Screen.FromPoint(new Point(point.X, point.Y)).Bounds;
        var lines = currentLines.Length > 0 ? currentLines : new[]
        {
            box
        };
        while (overlays.Count < lines.Length)
            overlays.Add(new Overlay());
        for (int i = 0; i < overlays.Count; i++)
        {
            if (i >= lines.Length)
            {
                overlays[i].HideLine();
                continue;
            }

            var screen = new Box(s.X, s.Y, s.Width, s.Height);
            if (currentScope is { } frame)
                screen = screen.Intersect(frame);
            if (currentVisible is { } visible)
                screen = screen.Intersect(visible);
            var fill = displayMode.SelectedIndex == 1;
            var line = fill ? Geometry.Highlight(lines[i], screen) : Geometry.Underline(lines[i], screen, Native.Scale(point), (int)thickness.Value, (double)offset.Value);
            if (line.Width > 0 && line.Height > 0)
                overlays[i].Draw(line, fill ? highlightColor : lineColor, fill ? Geometry.HighlightAlpha((int)opacity.Value) : (byte)255);
            else
                overlays[i].HideLine();
        }
    }
}
