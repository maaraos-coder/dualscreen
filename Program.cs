using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace DualScreen;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new SplitContext());
    }
}

internal static class Native
{
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; public int Width => Right - Left; public int Height => Bottom - Top; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    public const uint SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;
    public const int SW_RESTORE = 9, SW_MINIMIZE = 6;
}

internal sealed class SplitContext : ApplicationContext
{
    private readonly Dictionary<IntPtr, bool> assignedRight = new();
    private readonly HashSet<IntPtr> excluded = new();
    private readonly System.Windows.Forms.Timer timer;
    private readonly Bar left;
    private readonly Bar right;
    private Screen screen;
    private bool enabled = true;
    private bool busy;
    private const int BarHeight = 42;

    public SplitContext()
    {
        screen = Screen.PrimaryScreen ?? Screen.AllScreens[0];
        left = new Bar(false, this);
        right = new Bar(true, this);
        excluded.Add(left.Handle);
        excluded.Add(right.Handle);
        PositionBars();
        left.Show(); right.Show();
        timer = new System.Windows.Forms.Timer { Interval = 900 };
        timer.Tick += (_, _) => Tick();
        timer.Start();
        Tick();
    }

    public void Toggle()
    {
        enabled = !enabled;
        left.SetRunning(enabled); right.SetRunning(enabled);
        if (enabled) { assignedRight.Clear(); Tick(); }
    }

    public void Stop()
    {
        timer.Stop();
        left.Close(); right.Close();
        ExitThread();
    }

    private Rectangle Working => screen.WorkingArea;
    private Rectangle LeftZone
    {
        get { var a = Working; return new Rectangle(a.Left, a.Top, a.Width / 2, Math.Max(100, a.Height - BarHeight)); }
    }
    private Rectangle RightZone
    {
        get { var a = Working; return new Rectangle(a.Left + a.Width / 2, a.Top, a.Width - a.Width / 2, Math.Max(100, a.Height - BarHeight)); }
    }

    private void PositionBars()
    {
        var a = Working;
        int mid = a.Left + a.Width / 2;
        left.Bounds = new Rectangle(a.Left, a.Bottom - BarHeight, mid - a.Left, BarHeight);
        right.Bounds = new Rectangle(mid, a.Bottom - BarHeight, a.Right - mid, BarHeight);
    }

    private List<(IntPtr Handle, string Name)> Windows()
    {
        var found = new List<(IntPtr, string)>();
        Native.EnumWindows((h, _) =>
        {
            if (excluded.Contains(h) || !Native.IsWindowVisible(h) || Native.GetWindow(h, 4) != IntPtr.Zero) return true;
            if (Native.GetWindowTextLength(h) == 0) return true;
            long style = Native.GetWindowLongPtr(h, -20).ToInt64();
            if ((style & 0x80) != 0) return true; // tool windows
            Native.GetWindowThreadProcessId(h, out uint pid);
            if (pid == (uint)Environment.ProcessId || pid == 0) return true;
            var cls = new StringBuilder(128);
            Native.GetClassName(h, cls, cls.Capacity);
            if (cls.ToString() is "Shell_TrayWnd" or "Progman" or "WorkerW" or "Shell_SecondaryTrayWnd") return true;
            var title = new StringBuilder(Math.Min(300, Native.GetWindowTextLength(h) + 1));
            Native.GetWindowText(h, title, title.Capacity);
            if (title.Length > 0) found.Add((h, title.ToString()));
            return true;
        }, IntPtr.Zero);
        return found;
    }

    private void Tick()
    {
        if (busy) return;
        busy = true;
        try
        {
            if (screen.Bounds != (Screen.PrimaryScreen ?? screen).Bounds)
            {
                screen = Screen.PrimaryScreen ?? screen;
                PositionBars();
                assignedRight.Clear();
            }
            var found = Windows();
            var handles = found.Select(f => f.Handle).ToHashSet();
            foreach (var h in assignedRight.Keys.Where(h => !handles.Contains(h)).ToArray()) assignedRight.Remove(h);
            foreach (var (h, _) in found)
            {
                if (!Native.GetWindowRect(h, out var rect)) continue;
                if (!assignedRight.ContainsKey(h))
                {
                    // Already open windows go to their current half; newly opened ones follow mouse.
                    Native.GetCursorPos(out var p);
                    bool active = h == Native.GetForegroundWindow();
                    assignedRight[h] = active ? p.X >= Working.Left + Working.Width / 2 : rect.Left + rect.Width / 2 >= Working.Left + Working.Width / 2;
                }
                if (!enabled || Native.IsIconic(h)) continue;
                var zone = assignedRight[h] ? RightZone : LeftZone;
                // Leave small dialogs, pop-ups, and fullscreen applications alone.
                if (rect.Width < 320 || rect.Height < 220) continue;
                if (rect.Width >= screen.Bounds.Width - 5 && rect.Height >= screen.Bounds.Height - 5 && !Native.IsZoomed(h)) continue;
                if (Native.IsZoomed(h)) Native.ShowWindow(h, Native.SW_RESTORE);
                int x = Math.Clamp(rect.Left, zone.Left, Math.Max(zone.Left, zone.Right - 320));
                int y = Math.Clamp(rect.Top, zone.Top, Math.Max(zone.Top, zone.Bottom - 220));
                int w = Math.Min(rect.Width, zone.Right - x);
                int height = Math.Min(rect.Height, zone.Bottom - y);
                // Restore window to usable size in its half.
                if (w < Math.Min(440, zone.Width) || height < 260 || rect.Right > zone.Right || rect.Bottom > zone.Bottom || rect.Left < zone.Left)
                {
                    x = zone.Left; y = zone.Top; w = zone.Width; height = zone.Height;
                }
                if (rect.Left != x || rect.Top != y || rect.Width != w || rect.Height != height)
                    Native.SetWindowPos(h, IntPtr.Zero, x, y, w, height, Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
            }
            left.RefreshApps(found.Where(f => assignedRight.TryGetValue(f.Handle, out bool side) && !side).ToList());
            right.RefreshApps(found.Where(f => assignedRight.TryGetValue(f.Handle, out bool side) && side).ToList());
        }
        finally { busy = false; }
    }

    public void Focus(IntPtr h)
    {
        if (!Native.IsWindow(h)) return;
        if (Native.IsIconic(h)) Native.ShowWindow(h, Native.SW_RESTORE);
        Native.SetForegroundWindow(h);
    }
}

internal sealed class Bar : Form
{
    private readonly SplitContext controller;
    private readonly FlowLayoutPanel items;
    private readonly Label tag;
    private readonly Button toggle;
    private string lastSignature = "";

    public Bar(bool isRight, SplitContext controller)
    {
        this.controller = controller;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(29, 36, 49);
        ForeColor = Color.White;
        StartPosition = FormStartPosition.Manual;
        tag = new Label { Text = isRight ? "▶ 2" : "◀ 1", Width = 52, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Left, ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
        Controls.Add(tag);
        var controlPanel = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 112, WrapContents = false, Margin = Padding.Empty, Padding = new Padding(0, 4, 3, 0) };
        toggle = MakeButton("Pausar", 64);
        toggle.Click += (_, _) => controller.Toggle();
        var exit = MakeButton("✕", 34);
        exit.Click += (_, _) => controller.Stop();
        controlPanel.Controls.Add(toggle);
        controlPanel.Controls.Add(exit);
        Controls.Add(controlPanel);
        items = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = false, Padding = new Padding(0, 4, 0, 0) };
        Controls.Add(items);
        items.BringToFront();
    }

    private static Button MakeButton(string caption, int width)
    {
        return new Button { Text = caption, Width = width, Height = 31, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(53, 65, 84), Margin = new Padding(2, 0, 2, 0), Font = new Font("Segoe UI", 8) };
    }

    public void SetRunning(bool enabled) => toggle.Text = enabled ? "Pausar" : "Activar";

    public void RefreshApps(List<(IntPtr Handle, string Name)> apps)
    {
        string signature = string.Join("|", apps.Select(a => $"{a.Handle}:{a.Name}"));
        if (signature == lastSignature) return;
        lastSignature = signature;
        items.SuspendLayout();
        while (items.Controls.Count > 0) items.Controls[0].Dispose();
        foreach (var (handle, name) in apps.Take(20))
        {
            var btn = MakeButton(name.Length > 16 ? name[..15] + "…" : name, 112);
            btn.Tag = handle;
            btn.Click += (_, _) => controller.Focus((IntPtr)btn.Tag!);
            var tooltip = new ToolTip();
            tooltip.SetToolTip(btn, name);
            btn.Disposed += (_, _) => tooltip.Dispose();
            items.Controls.Add(btn);
        }
        items.ResumeLayout();
    }
}
