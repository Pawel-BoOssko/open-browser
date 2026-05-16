namespace BridgeBrowserAlpha0.OpenBridgeHost.ClaudeCode;

public class ClaudeCodeExecutorOptions
{
    public ClaudeCodeExecutorMode Mode { get; set; } = ClaudeCodeExecutorMode.DryRun;
    public string? ExecutablePath { get; set; }
    public string? ArgumentsTemplate { get; set; }
    public int DefaultTimeoutMs { get; set; } = 300_000;
    public int DefaultMaxOutputChars { get; set; } = 50_000;
}
