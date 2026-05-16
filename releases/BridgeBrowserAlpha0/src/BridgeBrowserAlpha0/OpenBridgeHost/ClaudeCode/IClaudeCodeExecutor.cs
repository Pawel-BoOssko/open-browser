namespace BridgeBrowserAlpha0.OpenBridgeHost.ClaudeCode;

public interface IClaudeCodeExecutor
{
    Task<HostCommandResult> ExecuteAsync(HostCommandRequest request, CancellationToken ct);
}
