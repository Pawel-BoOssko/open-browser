namespace BridgeBrowserAlpha0;

/// <summary>Taskbar and notification area recovery for the Lite window.</summary>
internal sealed partial class ChatGptLiteForm
{
    private NotifyIcon? _tray;

    private void InitializeWindowRecovery()
    {
        ShowInTaskbar = true;
        var menu = new ContextMenuStrip();
        menu.Items.Add("Pokaż Open Browser", null, (_, _) => RestoreWindow());
        menu.Items.Add("Archiwum odpowiedzi", null, (_, _) =>
        {
            RestoreWindow();
            new LiteArchiveViewer().Show(this);
        });
        menu.Items.Add("Zamknij aplikację", null, (_, _) => Close());
        _tray = new NotifyIcon
        {
            Icon = Icon,
            Text = "Open Browser Lite",
            ContextMenuStrip = menu,
            Visible = true
        };
        _tray.DoubleClick += (_, _) => RestoreWindow();
        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized)
                LiteEventLog.Write("window_minimized");
        };
        VisibleChanged += (_, _) =>
        {
            LiteEventLog.Write("window_visibility_changed", new { visible = Visible });
        };
    }

    private void RestoreWindow()
    {
        if (IsDisposed) return;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        LiteEventLog.Write("window_restored");
    }

    private void DisposeWindowRecovery()
    {
        if (_tray is null) return;
        _tray.Visible = false;
        _tray.ContextMenuStrip?.Dispose();
        _tray.Dispose();
        _tray = null;
    }
}
