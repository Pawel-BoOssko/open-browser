using System.Diagnostics;

namespace BridgeBrowserAlpha0.OpenBridgeHost;

public class OpenBridgeHost
{
    private readonly string _allowedRoot;
    private readonly ClaudeCode.IClaudeCodeExecutor _ccExecutor;
    private readonly object _operationGate = new();
    private bool _busy;

    public OpenBridgeHost(string allowedRoot, ClaudeCode.IClaudeCodeExecutor? executor = null)
    {
        _allowedRoot = Path.GetFullPath(allowedRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        _ccExecutor = executor ?? new ClaudeCode.ClaudeCodeExecutor();
    }

    public async Task<HostCommandResult> ExecuteAsync(HostCommandRequest request, CancellationToken ct = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        request.OperationId ??= Guid.NewGuid().ToString("N")[..12];

        if (!string.Equals(request.Command, "CC", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(request.Command, "PS", StringComparison.OrdinalIgnoreCase))
        {
            return ErrorResult(request.OperationId, startedAt, HostErrorCodes.CommandNotRecognized,
                $"Command not recognized: {request.Command}. Supported: CC, PS.");
        }

        var executor = string.Equals(request.Command, "PS", StringComparison.OrdinalIgnoreCase)
            ? GetPsExecutor() : _ccExecutor;

        if (!IsAllowedDirectory(request.WorkingDirectory))
        {
            return ErrorResult(request.OperationId, startedAt, HostErrorCodes.WorkingDirectoryNotAllowed,
                $"Working directory not allowed: {request.WorkingDirectory}. Must be under {_allowedRoot}.");
        }

        lock (_operationGate)
        {
            if (_busy)
            {
                return ErrorResult(request.OperationId, startedAt, HostErrorCodes.ExecutorBusy,
                    "An operation is already in progress. Try again later.");
            }
            _busy = true;
        }

        try
        {
            var timeoutMs = request.TimeoutMs > 0 ? request.TimeoutMs : 300_000;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);

            var task = executor.ExecuteAsync(request, cts.Token);
            var delay = Task.Delay(timeoutMs);
            var completed = await Task.WhenAny(task, delay);

            if (completed == delay)
            {
                cts.Cancel();
                try { await task; } catch (OperationCanceledException) { }

                return new HostCommandResult
                {
                    Status = HostExecutionStatus.Timeout,
                    OperationId = request.OperationId,
                    DurationMs = ElapsedMs(startedAt),
                    ErrorCode = HostErrorCodes.Timeout,
                    Message = $"Operation timed out after {timeoutMs}ms."
                };
            }

            return await task;
        }
        finally
        {
            lock (_operationGate) { _busy = false; }
        }
    }

    private ClaudeCode.IClaudeCodeExecutor? _psExecutor;

    private ClaudeCode.IClaudeCodeExecutor GetPsExecutor()
    {
        if (_psExecutor == null)
        {
            _psExecutor = new ClaudeCode.ClaudeCodeExecutor(new ClaudeCode.ClaudeCodeExecutorOptions
            {
                Mode = ClaudeCode.ClaudeCodeExecutorMode.Process,
                ExecutablePath = "powershell.exe",
                ArgumentsTemplate = "-NoProfile -Command \"{prompt}\"",
                DefaultTimeoutMs = 120_000,
                DefaultMaxOutputChars = 50_000
            });
        }
        return _psExecutor;
    }

    private bool IsAllowedDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(full, _allowedRoot, StringComparison.OrdinalIgnoreCase)
                   || full.StartsWith(_allowedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static HostCommandResult ErrorResult(string operationId, long startedAt, string errorCode, string message)
    {
        return new HostCommandResult
        {
            Status = HostExecutionStatus.Error,
            OperationId = operationId,
            DurationMs = ElapsedMs(startedAt),
            ErrorCode = errorCode,
            Message = message
        };
    }

    private static long ElapsedMs(long startedAt)
    {
        return (Stopwatch.GetTimestamp() - startedAt) * 1000 / Stopwatch.Frequency;
    }
}
