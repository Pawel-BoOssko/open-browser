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

        if (string.Equals(request.Command, "HST_STATUS", StringComparison.OrdinalIgnoreCase))
        {
            var statusResult = ExecuteStatusCommand();
            lock (_gate) { LastResult = statusResult; }
            return statusResult;
        }

        if (string.Equals(request.Command, "PS_HELP", StringComparison.OrdinalIgnoreCase))
        {
            var helpResult = ExecutePsHelp();
            lock (_gate) { LastResult = helpResult; }
            return helpResult;
        }

        if (string.Equals(request.Command, "PY_HELP", StringComparison.OrdinalIgnoreCase))
        {
            var helpResult = ExecutePyHelp();
            lock (_gate) { LastResult = helpResult; }
            return helpResult;
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

            var toolsDir = Path.Combine(_workingDirectory, "tools");
            if (Directory.Exists(toolsDir))
            {
                var toolFolders = Directory.GetDirectories(toolsDir).Select(Path.GetFileName).ToArray();
                sb.AppendLine($"Tools: {string.Join(", ", toolFolders)}");
            }
            sb.AppendLine();

            // Recent downloads
            var downloadsDir = AppPaths.Downloads;
            if (Directory.Exists(downloadsDir))
            {
                var recentDownloads = new DirectoryInfo(downloadsDir)
                    .GetFiles()
                    .OrderByDescending(f => f.CreationTimeUtc)
                    .Take(3)
                    .Select(f => f.Name)
                    .ToArray();
                
                if (recentDownloads.Length > 0)
                {
                    sb.AppendLine("Recent downloads:");
                    foreach (var d in recentDownloads)
                    {
                        sb.AppendLine($"  {d}");
                    }
                }
                else
                {
                    sb.AppendLine("Recent downloads: (none)");
                }
            }
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
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("OpenBridge Host Commands");
            sb.AppendLine("=======================");
            sb.AppendLine();
            sb.AppendLine("How to send a command:");
            sb.AppendLine("  Include this exact block in your response. The markers must be exactly as shown:");
            sb.AppendLine();
            sb.AppendLine("  @@OPENBRIDGE_EXEC_BEGIN@@");
            sb.AppendLine("  {\"version\":\"001\",\"command\":\"HST_HELP\"}");
            sb.AppendLine("  @@OPENBRIDGE_EXEC_END@@");
            sb.AppendLine();
            sb.AppendLine("  - version is always \"001\".");
            sb.AppendLine("  - command is one of the names listed below.");
            sb.AppendLine("  - payload is the code or text to pass to the command (not needed for HST commands).");
            sb.AppendLine("  - The first valid block in your response is executed. The rest are ignored.");
            sb.AppendLine("  - If you write the markers without intending to execute — they WILL execute anyway.");
            sb.AppendLine("  - To stay in the loop: end your response with a command block. No block = loop stops.");
            sb.AppendLine();
            sb.AppendLine("HST commands (ask the system directly):");
            sb.AppendLine("  HST_HELP    — this list");
            sb.AppendLine("  HST_STATUS  — system status, working directory, conversation ID, recent downloads");
            sb.AppendLine();
            sb.AppendLine("Available executors:");
            sb.AppendLine("  PS — PowerShell  (send PS_HELP for details)");
            sb.AppendLine("  PY — Python 3    (send PY_HELP for details)");
            sb.AppendLine();
            sb.AppendLine("Example — to get PS help:");
            sb.AppendLine("  @@OPENBRIDGE_EXEC_BEGIN@@");
            sb.AppendLine("  {\"version\":\"001\",\"command\":\"PS_HELP\"}");
            sb.AppendLine("  @@OPENBRIDGE_EXEC_END@@");
            sb.AppendLine();
            sb.AppendLine("Automatic bridge features:");
            sb.AppendLine("  - Files created in the ChatGPT sandbox are auto-detected and downloaded.");
            sb.AppendLine("  - Downloads go to the Downloads directory (see HST_STATUS for recent files).");
            sb.AppendLine("  - Each command has a timeout. If exceeded, the system sends a timeout message to chat.");

            return new HostCommandResult
            {
                Status = HostExecutionStatus.Ok,
                OperationId = Guid.NewGuid().ToString("N")[..12],
                DurationMs = 0,
                StdoutPreview = sb.ToString(),
                ExitCode = 0,
                Message = "HST_HELP completed."
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
                Message = $"Help failed: {ex.Message}"
            };
        }
    }

    private HostCommandResult ExecutePsHelp()
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("PS — PowerShell Executor");
            sb.AppendLine("========================");
            sb.AppendLine();
            sb.AppendLine("Usage:");
            sb.AppendLine("  Command: PS");
            sb.AppendLine("  Payload: PowerShell code (UTF-8)");
            sb.AppendLine();
            sb.AppendLine("Limits:");
            sb.AppendLine("  Timeout: 720 seconds (12 minutes)");
            sb.AppendLine("  Max output: 50,000 characters (truncated with [... truncated ...] marker)");
            sb.AppendLine("  Working directory: the project root");
            sb.AppendLine();
            sb.AppendLine("Encoding:");
            sb.AppendLine("  Output is captured as UTF-8. Console is forced to UTF-8 (chcp 65001).");
            sb.AppendLine("  CRITICAL: Get-Content and Out-File default to the system code page");
            sb.AppendLine("  (CP1250 on Polish Windows), NOT UTF-8. Always use -Encoding UTF8:");
            sb.AppendLine("    Get-Content path -Encoding UTF8");
            sb.AppendLine("    ... | Out-File path -Encoding UTF8");
            sb.AppendLine("  Without -Encoding UTF8, Polish characters WILL be mangled.");
            sb.AppendLine();
            sb.AppendLine("Error handling:");
            sb.AppendLine("  - Non-zero exit code: output is still returned, exit code is reported.");
            sb.AppendLine("  - Timeout: system sends a timeout message to chat. Check if the command executed before retrying.");
            sb.AppendLine("  - No output: [OpenBridge] Process exited with code N. No output.");
            sb.AppendLine("  - Empty prompt: command is rejected. Provide a payload.");
            sb.AppendLine();
            sb.AppendLine("Staying in the loop:");
            sb.AppendLine("  End your response with a @@OPENBRIDGE_EXEC_BEGIN@@...@@OPENBRIDGE_EXEC_END@@ block.");
            sb.AppendLine("  Without a command block, the loop stops and no further response arrives.");
            sb.AppendLine();
            sb.AppendLine("Example:");
            sb.AppendLine("  @@OPENBRIDGE_EXEC_BEGIN@@");
            sb.AppendLine("  {\"version\":\"001\",\"command\":\"PS\",\"payload\":\"Get-ChildItem downloads\\\\\"}");
            sb.AppendLine("  @@OPENBRIDGE_EXEC_END@@");

            return new HostCommandResult
            {
                Status = HostExecutionStatus.Ok,
                OperationId = Guid.NewGuid().ToString("N")[..12],
                DurationMs = 0,
                StdoutPreview = sb.ToString(),
                ExitCode = 0,
                Message = "PS_HELP completed."
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
                Message = $"PS_HELP failed: {ex.Message}"
            };
        }
    }

    private HostCommandResult ExecutePyHelp()
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("PY — Python 3 Executor");
            sb.AppendLine("=======================");
            sb.AppendLine();
            sb.AppendLine("Usage:");
            sb.AppendLine("  Command: PY");
            sb.AppendLine("  Payload: Python 3 code (UTF-8)");
            sb.AppendLine();
            sb.AppendLine("Limits:");
            sb.AppendLine("  Timeout: 720 seconds (12 minutes)");
            sb.AppendLine("  Max output: 50,000 characters (truncated with [... truncated ...] marker)");
            sb.AppendLine("  Working directory: the project root");
            sb.AppendLine();
            sb.AppendLine("Encoding:");
            sb.AppendLine("  Output is UTF-8. Polish and all Unicode characters work correctly.");
            sb.AppendLine();
            sb.AppendLine("Error handling:");
            sb.AppendLine("  - Non-zero exit code: output and stderr are still returned.");
            sb.AppendLine("  - Timeout: system sends a timeout message to chat. Check if the command executed before retrying.");
            sb.AppendLine("  - No output: [OpenBridge] Process exited with code N. No output.");
            sb.AppendLine("  - Empty prompt: command is rejected. Provide a payload.");
            sb.AppendLine();
            sb.AppendLine("Staying in the loop:");
            sb.AppendLine("  End your response with a @@OPENBRIDGE_EXEC_BEGIN@@...@@OPENBRIDGE_EXEC_END@@ block.");
            sb.AppendLine();
            sb.AppendLine("Example:");
            sb.AppendLine("  @@OPENBRIDGE_EXEC_BEGIN@@");
            sb.AppendLine("  {\"version\":\"001\",\"command\":\"PY\",\"payload\":\"import os; print(os.getcwd())\"}");
            sb.AppendLine("  @@OPENBRIDGE_EXEC_END@@");

            return new HostCommandResult
            {
                Status = HostExecutionStatus.Ok,
                OperationId = Guid.NewGuid().ToString("N")[..12],
                DurationMs = 0,
                StdoutPreview = sb.ToString(),
                ExitCode = 0,
                Message = "PY_HELP completed."
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
                Message = $"PY_HELP failed: {ex.Message}"
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
