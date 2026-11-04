namespace BridgeBrowserAlpha0;

// One modeless projects workspace per browser window, with navigation shared
// with the existing WebView2. Repeated clicks focus, not duplicate, the UI.
internal sealed partial class ChatGptLiteForm
{
    private LiteProjectDashboard? _projectWorkspace;

    private void ShowProjectWorkspace()
    {
        if (_projectWorkspace is { IsDisposed: false })
        {
            _projectWorkspace.RefreshFromOwner();
            if (_projectWorkspace.WindowState == FormWindowState.Minimized)
                _projectWorkspace.WindowState = FormWindowState.Normal;
            _projectWorkspace.Activate();
            _projectWorkspace.BringToFront();
            return;
        }

        var dashboard = new LiteProjectDashboard(
            url => NavigateToProjectChatAsync(url),
            () => (_browser.CoreWebView2?.Source,
                   _browser.CoreWebView2?.DocumentTitle),
            id => RequestSelectedProjectHandoff(id),
            id => TakeOverSelectedProjectChat(id),
            id => ArchiveOlderChatsOfProjectAsync(id),
            (projectId, chatId, title) => RenameNativeProjectChatAsync(projectId, chatId, title),
            (projectId, chatId) => ReadNativeProjectChatTitleAsync(projectId, chatId));
        _projectWorkspace = dashboard;
        dashboard.FormClosed += (_, _) =>
        {
            if (ReferenceEquals(_projectWorkspace, dashboard))
                _projectWorkspace = null;
        };
        dashboard.Show(this);
        LiteEventLog.Write("project_dashboard_opened_single_instance");
    }
}
