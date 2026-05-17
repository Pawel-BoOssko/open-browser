using System.Diagnostics;
using BridgeBrowserAlpha0.OpenBridgeHost.ClaudeCode;

namespace BridgeBrowserAlpha0.OpenBridgeHost;

public class OpenBridgeHost
{
    private readonly IClaudeCodeExecutor _executor;
    private readonly object _operationGate = new();
    private bool _busy;

    public OpenBridgeHost(IClaudeCodeExecutor? executor = null)
    {
        _executor = executor ?? new ClaudeCode.ClaudeCodeExecutor();
    }

    public async Task<HostCommandResult> ExecuteAsync(HostCommandRequest request, CancellationToken ct = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        request.OperationId ??= Guid.NewGuid().ToString("N")[..12];

        lock (_operationGate)
        {
            if (_busy)
            {
                return new HostCommandResult
                {
                    Status = HostExecutionStatus.Error,
                    OperationId = request.OperationId,
                    DurationMs = ElapsedMs(startedAt),
                    ErrorCode = HostErrorCodes.ExecutorBusy,
                    Message = "An operation is already in progress."
                };
            }
            _busy = true;
        }

        try
        {
            return await _executor.ExecuteAsync(request, ct);
        }
        finally
        {
            lock (_operationGate) { _busy = false; }
        }
    }

    private static long ElapsedMs(long startedAt)
    {
        return (Stopwatch.GetTimestamp() - startedAt) * 1000 / Stopwatch.Frequency;
    }
}
