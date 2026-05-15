using System;

namespace BridgeBrowserAlpha0;

public sealed class BrowserTabRuntimeState
{
    public bool IsWebViewReady { get; set; }
    public bool IsNavigating { get; set; }
    public string? LastNavigationUrl { get; set; }
    public string? LastNavigationStatus { get; set; }
    public string? LastError { get; set; }
    public DateTime? LastWebMessageAtUtc { get; set; }
    public DateTime? LastStateChangeAtUtc { get; set; }

    public void MarkStateChanged()
    {
        LastStateChangeAtUtc = DateTime.UtcNow;
    }
}
