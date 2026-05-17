namespace BridgeBrowserAlpha0.OpenBridgeHost.Commands;

public class CommandExecutorOptions
{
    public CommandExecutorMode Mode { get; set; } = CommandExecutorMode.DryRun;
    public string? ExecutablePath { get; set; }
    public string? ArgumentsTemplate { get; set; }
    public int DefaultTimeoutMs { get; set; } = 720_000;
    public int DefaultMaxOutputChars { get; set; } = 50_000;
}
