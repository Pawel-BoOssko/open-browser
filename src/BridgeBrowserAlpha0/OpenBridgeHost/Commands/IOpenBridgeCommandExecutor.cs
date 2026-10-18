namespace BridgeBrowserAlpha0.OpenBridgeHost.Commands;

public interface IOpenBridgeCommandExecutor
{
    Task<HostCommandResult> ExecuteAsync(HostCommandRequest request, CancellationToken ct);
}
