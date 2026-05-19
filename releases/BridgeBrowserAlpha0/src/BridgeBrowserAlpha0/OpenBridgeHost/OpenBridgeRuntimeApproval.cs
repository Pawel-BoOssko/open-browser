using BridgeBrowserAlpha0.OpenBridgeHost.GeneralCommand;
using BridgeBrowserAlpha0.OpenBridgeProtocol;

namespace BridgeBrowserAlpha0.OpenBridgeHost;

public class OpenBridgeRuntimeApproval
{
    private readonly OpenBridgeHost _host;
    private readonly string _workingDirectory;
    private readonly LogWriter? _log;
    private readonly object _gate = new();

    public HostCommandRequest? PendingCommand { get; private set; }
    public HostCommandResult? LastResult { get; private set; }
    public bool HasPending => PendingCommand != null;

    private readonly OpenBridgeHost _pythonHost;

    public OpenBridgeRuntimeApproval(OpenBridgeHost host, string workingDirectory, LogWriter? log = null)
    {
        _host = host;
        _workingDirectory = workingDirectory;
        _log = log;
        _pythonHost = new OpenBridgeHost(
            new GeneralCommand.GeneralCommandExecutor("python", "-X utf8 -c \"{prompt}\""));
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
                    "Failed to map envelope to request", new { error = mapError });
                return false;
            }

            PendingCommand = request!;
            error = null;
            BridgeBrowserAlpha0.PipelineRawDump.Write("08_OpenBridgeRuntimeApproval.txt", PendingCommand.Prompt);

            _log?.WriteRun("runtime_approval", "pending_created", "ok",
                "Pending PS command awaiting execution",
                new { command = request!.Command, promptLength = request.Prompt?.Length ?? 0 });
            return true;
        }
    }

    public async Task<HostCommandResult> ExecutePendingAsync()
    {
        HostCommandRequest request;
        lock (_gate)
        {
            if (!HasPending || PendingCommand == null)
                return NoPendingResult();
            request = PendingCommand;
            PendingCommand = null;
        }

        if (string.Equals(request.Command, "HST_HELP", StringComparison.OrdinalIgnoreCase))
        {
            var helpResult = ExecuteHelpCommand();
            lock (_gate) { LastResult = helpResult; }
            return helpResult;
        }

        if (string.Equals(request.Command, "HST_TOOLS", StringComparison.OrdinalIgnoreCase))
        {
            var toolsResult = ExecuteToolsCommand();
            lock (_gate) { LastResult = toolsResult; }
            return toolsResult;
        }

        if (string.Equals(request.Command, "HST_STATUS", StringComparison.OrdinalIgnoreCase))
        {
            var statusResult = ExecuteStatusCommand();
            lock (_gate) { LastResult = statusResult; }
            return statusResult;
        }

        if (string.Equals(request.Command, "PY", StringComparison.OrdinalIgnoreCase))
        {
            _log?.WriteRun("runtime_approval", "execution_started", "ok",
                "Starting Python execution", new { command = "PY" });
            var pyResult = await _pythonHost.ExecuteAsync(request);
            lock (_gate) { LastResult = pyResult; }
            _log?.WriteRun("runtime_approval", "execution_finished",
                pyResult.Status == HostExecutionStatus.Ok ? "ok" : "error",
                pyResult.Message ?? "",
                new { pyResult.Status, pyResult.DurationMs, pyResult.ExitCode });
            return pyResult;
        }

        _log?.WriteRun("runtime_approval", "execution_started", "ok",
            "Starting execution via Host",
            new { command = request.Command });

        var result = await _host.ExecuteAsync(request);

        lock (_gate) { LastResult = result; }

        var logStatus = result.Status == HostExecutionStatus.Ok ? "ok" : "error";
        _log?.WriteRun("runtime_approval", "execution_finished", logStatus,
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
            "Operator rejected PS command",
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
            return $"Command: PS | Prompt ({promptLen} chars): {preview} | Dir: {_workingDirectory}";
        }
    }

    public string PendingCommandDetails()
    {
        lock (_gate)
        {
            if (PendingCommand == null) return "";
            var prompt = PendingCommand.Prompt ?? "";
            return $"Command: PS\nWorkingDir: {PendingCommand.WorkingDirectory}\n" +
                   $"Prompt:\n{prompt}";
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

    private HostCommandResult ExecuteToolsCommand()
    {
        try
        {
            var toolsDir = Path.Combine(_workingDirectory, "tools");
            if (!Directory.Exists(toolsDir))
            {
                return new HostCommandResult
                {
                    Status = HostExecutionStatus.Error,
                    OperationId = Guid.NewGuid().ToString("N")[..12],
                    DurationMs = 0,
                    ErrorCode = HostErrorCodes.ExecutorError,
                    Message = "Tools directory not found."
                };
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Available tools:");
            sb.AppendLine();

            foreach (var dir in Directory.GetDirectories(toolsDir))
            {
                var name = Path.GetFileName(dir);
                var readme = Path.Combine(dir, "README.md");
                if (File.Exists(readme))
                {
                    var firstLine = File.ReadLines(readme).FirstOrDefault() ?? "";
                    sb.AppendLine($"tools/{name}/ — {firstLine.TrimStart('#', ' ')}");
                }
                else
                {
                    sb.AppendLine($"tools/{name}/");
                }

                var pyFiles = Directory.GetFiles(dir, "*.py").Select(Path.GetFileNameWithoutExtension);
                if (pyFiles.Any())
                {
                    sb.AppendLine($"  Scripts: {string.Join(", ", pyFiles)}");
                }

                var tokensPath = Path.Combine(_workingDirectory, "config", "local", name, $"{name}_tokens.json");
                var altTokensPath = Path.Combine(_workingDirectory, "config", "local", name, "linkedin_tokens.json");
                if (File.Exists(tokensPath) || File.Exists(altTokensPath))
                {
                    sb.AppendLine("  Status: configured (tokens present)");
                }

                sb.AppendLine();
            }

            return new HostCommandResult
            {
                Status = HostExecutionStatus.Ok,
                OperationId = Guid.NewGuid().ToString("N")[..12],
                DurationMs = 0,
                StdoutPreview = sb.ToString(),
                ExitCode = 0,
                Message = "HST_TOOLS completed."
            };
        }
        catch (Exception ex)
        {
            return new HostCommandResult
            {
                Status = HostExecutionStatus.Error,
                OperationId = Guid.NewGuid().ToString("N")[..12],
                DurationMs = 0,
                ErrorCode = HostErrorCodes.ExecutorError,
                Message = $"Failed to scan tools: {ex.Message}"
            };
        }
    }

    private HostCommandResult ExecuteStatusCommand()
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Open Bridge Status");
            sb.AppendLine("=================");
            sb.AppendLine();
            sb.AppendLine($"Build: {AppConstants.BuildInfo}");
            sb.AppendLine($"Time: {DateTime.Now:yyyy.MM.dd HH:mm:ss}");
            sb.AppendLine($"Started: {AppConstants.BuildStamp}");
            sb.AppendLine($"Working directory: {_workingDirectory}");
            sb.AppendLine($"Your ID: {AppConstants.ConversationId ?? "(not yet detected)"}");
            sb.AppendLine();

            // Git status
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("git", "status --short")
                {
                    WorkingDirectory = _workingDirectory,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = System.Diagnostics.Process.Start(psi);
                if (p != null)
                {
                    p.WaitForExit(3000);
                    var gitOut = p.StandardOutput.ReadToEnd().Trim();
                    sb.AppendLine("Git status:");
                    sb.AppendLine(string.IsNullOrEmpty(gitOut) ? "  Clean working tree" : gitOut);
                }
            }
            catch
            {
                sb.AppendLine("Git status: unavailable");
            }
            sb.AppendLine();

            // Tools available
            var toolsDir = Path.Combine(_workingDirectory, "tools");
            if (Directory.Exists(toolsDir))
            {
                var toolFolders = Directory.GetDirectories(toolsDir).Select(Path.GetFileName).ToArray();
                sb.AppendLine($"Tools: {string.Join(", ", toolFolders)}");
            }
            sb.AppendLine();

            // Recent downloads
            try
            {
                var downloadsDir = AppConstants.DownloadsPath;
                if (Directory.Exists(downloadsDir))
                {
                    var files = Directory.GetFiles(downloadsDir)
                        .Select(f => new FileInfo(f))
                        .OrderByDescending(f => f.LastWriteTime)
                        .Take(3)
                        .ToArray();
                    if (files.Length > 0)
                    {
                        sb.AppendLine("Recent downloads:");
                        foreach (var f in files)
                            sb.AppendLine($"  {f.Name}  ({f.Length} bytes  {f.LastWriteTime:HH:mm:ss})");
                    }
                }
            }
            catch { }
            sb.AppendLine();

            // Current command
            lock (_gate)
            {
                sb.AppendLine($"Pending command: {(HasPending ? "yes" : "none")}");
                sb.AppendLine($"Last result: {(LastResult != null ? LastResult.Status.ToString() : "none")}");
            }

            return new HostCommandResult
            {
                Status = HostExecutionStatus.Ok,
                OperationId = Guid.NewGuid().ToString("N")[..12],
                DurationMs = 0,
                StdoutPreview = sb.ToString(),
                ExitCode = 0,
                Message = "HST_STATUS completed."
            };
        }
        catch (Exception ex)
        {
            return new HostCommandResult
            {
                Status = HostExecutionStatus.Error,
                OperationId = Guid.NewGuid().ToString("N")[..12],
                DurationMs = 0,
                ErrorCode = HostErrorCodes.ExecutorError,
                Message = $"Status check failed: {ex.Message}"
            };
        }
    }

    private HostCommandResult ExecuteHelpCommand()
    {
        try
        {
            var runtimeDoc = Path.Combine(_workingDirectory, "docs", "runtime", "environment.md");
            if (File.Exists(runtimeDoc))
            {
                var content = File.ReadAllText(runtimeDoc);
                return new HostCommandResult
                {
                    Status = HostExecutionStatus.Ok,
                    OperationId = Guid.NewGuid().ToString("N")[..12],
                    DurationMs = 0,
                    StdoutPreview = content,
                    ExitCode = 0,
                    Message = "HST_HELP completed."
                };
            }
            return new HostCommandResult
            {
                Status = HostExecutionStatus.Error,
                OperationId = Guid.NewGuid().ToString("N")[..12],
                DurationMs = 0,
                ErrorCode = HostErrorCodes.ExecutorError,
                Message = "Runtime documentation not found."
            };
        }
        catch (Exception ex)
        {
            return new HostCommandResult
            {
                Status = HostExecutionStatus.Error,
                OperationId = Guid.NewGuid().ToString("N")[..12],
                DurationMs = 0,
                ErrorCode = HostErrorCodes.ExecutorError,
                Message = $"Failed to read runtime docs: {ex.Message}"
            };
        }
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
