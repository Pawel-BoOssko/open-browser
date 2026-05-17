using BridgeBrowserAlpha0.OpenBridgeHost.Commands;
using BridgeBrowserAlpha0.OpenBridgeHost.GeneralCommand;
using BridgeBrowserAlpha0.OpenBridgeProtocol;

namespace BridgeBrowserAlpha0.OpenBridgeHost;

public class OpenBridgeRuntimeApproval
{
    private readonly string _workingDirectory;
    private readonly string _configPath;
    private readonly LogWriter? _log;
    private readonly object _gate = new();

    public HostCommandRequest? PendingCommand { get; private set; }
    public HostCommandResult? LastResult { get; private set; }
    public bool HasPending => PendingCommand != null;

    public OpenBridgeRuntimeApproval(string workingDirectory, LogWriter? log = null, string? configPath = null)
    {
        _workingDirectory = workingDirectory;
        _configPath = configPath ?? Path.Combine(workingDirectory, "config", "local", "claude-code-executor.local.json");
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
            BridgeBrowserAlpha0.PipelineRawDump.Write("08_OpenBridgeRuntimeApproval.txt", PendingCommand.Prompt);

            var processAvail = CheckProcessAvailable();
            _log?.WriteRun("runtime_approval", "pending_created", "ok",
                "Pending CC command awaiting approval",
                new { command = request!.Command, promptLength = request.Prompt?.Length ?? 0, processAvailable = processAvail });
            return true;
        }
    }

    public bool IsProcessAvailable()
    {
        return File.Exists(_configPath);
    }

    public string? ProcessAvailableMessage()
    {
        if (File.Exists(_configPath))
        {
            try
            {
                var opts = Commands.CommandExecutorOptionsLoader.LoadOrThrow(_configPath);
                if (opts.Mode != Commands.CommandExecutorMode.Process)
                    return "Process mode unavailable: config is not in Process mode.";
                if (string.IsNullOrWhiteSpace(opts.ExecutablePath))
                    return "Process mode unavailable: executable path not configured.";
                if (HasUnsafeFlags(opts))
                    return "Process mode unavailable: config contains unsafe flags.";
                return null; // available
            }
            catch
            {
                return "Process mode unavailable: config parse error.";
            }
        }
        return "Process mode unavailable: local config missing.";
    }

    public async Task<HostCommandResult> ApproveDryRunAsync()
    {
        HostCommandRequest request;
        lock (_gate)
        {
            if (!HasPending || PendingCommand == null)
                return NoPendingResult();
            request = PendingCommand;
            PendingCommand = null;
        }

        _log?.WriteRun("runtime_approval", "approval_dryrun_accepted", "ok",
            "Operator approved command",
            new { command = request.Command, promptLength = request.Prompt?.Length ?? 0 });

        _log?.WriteRun("runtime_approval", "host_execution_started", "ok",
            "Starting execution via Host",
            new { command = request.Command });

        IOpenBridgeCommandExecutor executor = new GeneralCommand.GeneralCommandExecutor(
            "powershell.exe", "-NoProfile -Command \"{prompt}\"");

        var host = new OpenBridgeHost(executor);
        var result = await host.ExecuteAsync(request);

        lock (_gate) { LastResult = result; }

        var logStatus = result.Status == HostExecutionStatus.Ok ? "ok" : "error";
        _log?.WriteRun("runtime_approval", "host_execution_finished", logStatus,
            result.Message ?? "",
            new { result.Status, result.OperationId, result.DurationMs, result.ExitCode, result.ErrorCode, mode = "DryRun" });

        return result;
    }

    public async Task<HostCommandResult> ApproveProcessAsync()
    {
        HostCommandRequest request;
        lock (_gate)
        {
            if (!HasPending || PendingCommand == null)
                return NoPendingResult();
            request = PendingCommand;
            PendingCommand = null;
        }

        if (!File.Exists(_configPath))
        {
            _log?.WriteRun("runtime_approval", "approval_process_rejected", "error",
                "Process approval rejected: local config missing",
                new { configPath = _configPath });
            return new HostCommandResult
            {
                Status = HostExecutionStatus.Error,
                ErrorCode = HostErrorCodes.ExecutorError,
                Message = "Process mode unavailable: local config missing."
            };
        }

        Commands.CommandExecutorOptions opts;
        try
        {
            opts = Commands.CommandExecutorOptionsLoader.LoadOrThrow(_configPath);
        }
        catch (Exception ex)
        {
            _log?.WriteRun("runtime_approval", "approval_process_rejected", "error",
                "Process approval rejected: config parse error", new { error = ex.Message });
            return new HostCommandResult
            {
                Status = HostExecutionStatus.Error,
                ErrorCode = HostErrorCodes.ExecutorError,
                Message = $"Process config parse error: {ex.Message}"
            };
        }

        if (opts.Mode != Commands.CommandExecutorMode.Process)
        {
            _log?.WriteRun("runtime_approval", "approval_process_rejected", "error",
                "Process approval rejected: config not in Process mode", new { mode = opts.Mode });
            return new HostCommandResult
            {
                Status = HostExecutionStatus.Error,
                ErrorCode = HostErrorCodes.ExecutorError,
                Message = "Process mode unavailable: config is not in Process mode."
            };
        }

        if (HasUnsafeFlags(opts))
        {
            _log?.WriteRun("runtime_approval", "approval_process_rejected", "error",
                "Process approval rejected: unsafe flags in config");
            return new HostCommandResult
            {
                Status = HostExecutionStatus.Error,
                ErrorCode = HostErrorCodes.ExecutorError,
                Message = "Process mode unavailable: config contains unsafe or dangerous flags."
            };
        }

        _log?.WriteRun("runtime_approval", "approval_process_accepted", "ok",
            "Operator approved CC command — Process mode",
            new { command = request.Command, promptLength = request.Prompt?.Length ?? 0, executable = opts.ExecutablePath });

        _log?.WriteRun("runtime_approval", "host_execution_started", "ok",
            "Starting Process execution via Host",
            new { command = request.Command, mode = "Process", timeoutMs = opts.DefaultTimeoutMs });

        var executor = new Commands.CommandExecutor(opts);
        var host = new OpenBridgeHost(executor);
        var result = await host.ExecuteAsync(request);

        lock (_gate) { LastResult = result; }

        var logStatus = result.Status == HostExecutionStatus.Ok ? "ok" :
            result.Status == HostExecutionStatus.Timeout ? "timeout" : "error";
        _log?.WriteRun("runtime_approval", "host_execution_finished", logStatus,
            result.Message ?? "",
            new { result.Status, result.OperationId, result.DurationMs, result.ExitCode, result.ErrorCode, mode = "Process" });

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
            return $"Command: CC | Prompt ({promptLen} chars): {preview} | Timeout: 720000ms | Dir: {_workingDirectory}";
        }
    }

    public string PendingCommandDetails()
    {
        lock (_gate)
        {
            if (PendingCommand == null) return "";
            return $"Command: CC\nVersion: 001\nTimeout: 720000ms\nMaxOutput: 50000 chars\n" +
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

    private bool CheckProcessAvailable()
    {
        try
        {
            if (!File.Exists(_configPath)) return false;
            var opts = Commands.CommandExecutorOptionsLoader.LoadOrThrow(_configPath);
            return opts.Mode == Commands.CommandExecutorMode.Process
                   && !string.IsNullOrWhiteSpace(opts.ExecutablePath)
                   && !HasUnsafeFlags(opts);
        }
        catch
        {
            return false;
        }
    }

    private static bool HasUnsafeFlags(Commands.CommandExecutorOptions opts)
    {
        var args = opts.ArgumentsTemplate ?? "";
        var exe = opts.ExecutablePath ?? "";
        var combined = args + " " + exe;
        return combined.Contains("--dangerously-skip-permissions", StringComparison.OrdinalIgnoreCase)
               || combined.Contains("git push", StringComparison.OrdinalIgnoreCase)
               || combined.Contains("dotnet add package", StringComparison.OrdinalIgnoreCase);
    }

    private static HostCommandResult NoPendingResult()
    {
        return new HostCommandResult
        {
            Status = HostExecutionStatus.Error,
            ErrorCode = HostErrorCodes.ExecutorError,
            Message = "No pending command to approve."
        };
    }
}
