using BridgeBrowserAlpha0.OpenBridgeProtocol;

namespace BridgeBrowserAlpha0.OpenBridgeHost;

public class OpenBridgeRuntimeApproval
{
    private readonly string _workingDirectory;
    private readonly LogWriter? _log;
    private readonly object _gate = new();

    public HostCommandRequest? PendingCommand { get; private set; }
    public HostCommandResult? LastResult { get; private set; }
    public bool HasPending => PendingCommand != null;

    public OpenBridgeRuntimeApproval(string workingDirectory, LogWriter? log = null)
    {
        _workingDirectory = workingDirectory;
        _log = log;
    }

    public bool TrySetPending(OpenBridgeEnvelopeParseResult parseResult, out string? error)
    {
        lock (_gate)
        {
            if (HasPending)
            {
                error = "A command is already pending approval. Reject or approve it first.";
                _log?.WriteRun("runtime_approval", "pending_ignored", "error",
                    "Pending command already exists — new candidate ignored",
                    new { command = parseResult.Envelope?.Command });
                return false;
            }

            if (!OpenBridgeHostCommandMapper.TryMap(
                    parseResult.Envelope!, _workingDirectory, 720_000, 50_000,
                    out var request, out var mapError))
            {
                error = mapError ?? "Failed to map envelope to command request.";
                _log?.WriteRun("runtime_approval", "map_failed", "error",
                    "Failed to map envelope to CC request", new { error = mapError });
                return false;
            }

            PendingCommand = request!;
            error = null;
            _log?.WriteRun("runtime_approval", "pending_created", "ok",
                "Pending CC command awaiting approval",
                new { command = request!.Command, promptLength = request.Prompt?.Length ?? 0 });
            return true;
        }
    }

    public async Task<HostCommandResult> ApproveAsync()
    {
        HostCommandRequest request;
        lock (_gate)
        {
            if (!HasPending || PendingCommand == null)
            {
                return new HostCommandResult
                {
                    Status = HostExecutionStatus.Error,
                    ErrorCode = HostErrorCodes.ExecutorError,
                    Message = "No pending command to approve."
                };
            }
            request = PendingCommand;
            PendingCommand = null;
        }

        _log?.WriteRun("runtime_approval", "approval_accepted", "ok",
            "Operator approved CC command — executing DryRun only",
            new { command = request.Command, promptLength = request.Prompt?.Length ?? 0 });

        _log?.WriteRun("runtime_approval", "host_execution_started", "ok",
            "Starting DryRun execution via Host",
            new { command = request.Command, mode = "DryRun", timeoutMs = 720_000 });

        var executor = new ClaudeCode.ClaudeCodeExecutor(new ClaudeCode.ClaudeCodeExecutorOptions
        {
            Mode = ClaudeCode.ClaudeCodeExecutorMode.DryRun,
            DefaultTimeoutMs = 720_000,
            DefaultMaxOutputChars = 50_000
        });

        var host = new OpenBridgeHost(_workingDirectory, executor);
        var result = await host.ExecuteAsync(request);

        lock (_gate)
        {
            LastResult = result;
        }

        var logStatus = result.Status == HostExecutionStatus.Ok ? "ok" : "error";
        _log?.WriteRun("runtime_approval", "host_execution_finished", logStatus,
            result.Message ?? "",
            new { result.Status, result.OperationId, result.DurationMs, result.ExitCode, result.ErrorCode });

        return result;
    }

    public void Reject()
    {
        HostCommandRequest? discarded;
        lock (_gate)
        {
            discarded = PendingCommand;
            PendingCommand = null;
            LastResult = null;
        }

        if (discarded == null) return;

        _log?.WriteRun("runtime_approval", "approval_rejected", "ok",
            "Operator rejected CC command",
            new { command = discarded.Command, promptLength = discarded.Prompt?.Length ?? 0 });
    }

    public string PendingSummary()
    {
        lock (_gate)
        {
            if (PendingCommand == null) return "No pending command.";
            var prompt = PendingCommand.Prompt ?? "";
            var promptLen = prompt.Length;
            var preview = promptLen > 1000 ? prompt[..1000] + "..." : prompt;
            return $"Command: CC | Prompt ({promptLen} chars): {preview} | Timeout: 720000ms | Mode: DryRun";
        }
    }

    public string PendingCommandDetails()
    {
        lock (_gate)
        {
            if (PendingCommand == null) return "";
            return $"Command: CC\nVersion: 001\nTimeout: 720000ms\nMaxOutput: 50000 chars\nMode: DryRun\n" +
                   $"WorkingDir: {PendingCommand.WorkingDirectory}\n" +
                   $"PromptLength: {PendingCommand.Prompt?.Length ?? 0} chars";
        }
    }

    public string ResultSummary()
    {
        lock (_gate)
        {
            if (LastResult == null) return "";
            return $"OperationId: {LastResult.OperationId}  Status: {LastResult.Status}  " +
                   $"Duration: {LastResult.DurationMs}ms  ExitCode: {LastResult.ExitCode}  " +
                   $"Message: {LastResult.Message}";
        }
    }
}
