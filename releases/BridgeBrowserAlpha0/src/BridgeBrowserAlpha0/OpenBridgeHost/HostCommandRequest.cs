namespace BridgeBrowserAlpha0.OpenBridgeHost;

public class HostCommandRequest
{
    public string? OperationId { get; set; }
    public string Command { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public string? Prompt { get; set; }
    public int TimeoutMs { get; set; } = 300_000;
    public int MaxOutputChars { get; set; } = 50_000;
}
