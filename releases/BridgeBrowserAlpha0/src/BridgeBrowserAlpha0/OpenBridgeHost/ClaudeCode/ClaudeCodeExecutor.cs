using System.Diagnostics;

namespace BridgeBrowserAlpha0.OpenBridgeHost.ClaudeCode;

public class ClaudeCodeExecutor : IClaudeCodeExecutor
{
    private readonly ClaudeCodeExecutorOptions _options;

    public ClaudeCodeExecutor() : this(new ClaudeCodeExecutorOptions()) { }

    public ClaudeCodeExecutor(ClaudeCodeExecutorOptions options)
    {
        _options = options;
    }

    public virtual Task<HostCommandResult> ExecuteAsync(HostCommandRequest request, CancellationToken ct = default)
    {
        return _options.Mode == ClaudeCodeExecutorMode.Process
            ? ExecuteProcessAsync(request, ct)
            : ExecuteDryRunAsync(request);
    }

    private Task<HostCommandResult> ExecuteDryRunAsync(HostCommandRequest request)
    {
        var startedAt = Stopwatch.GetTimestamp();

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return Task.FromResult(ErrorResult(request.OperationId ?? "", startedAt,
                HostErrorCodes.PromptEmpty, "Prompt must not be empty."));
        }

        var workingDir = request.WorkingDirectory;
        if (!string.IsNullOrWhiteSpace(workingDir) && !Directory.Exists(workingDir))
        {
            return Task.FromResult(ErrorResult(request.OperationId ?? "", startedAt,
                HostErrorCodes.WorkingDirectoryNotAllowed, $"Working directory does not exist: {workingDir}"));
        }

        var echo = $"[DRY-RUN ClaudeCodeExecutor] Working directory: {workingDir}\nPrompt: {request.Prompt}";
        var maxChars = request.MaxOutputChars > 0 ? request.MaxOutputChars : _options.DefaultMaxOutputChars;
        var truncated = echo.Length > maxChars;
        var preview = truncated ? echo[..maxChars] : echo;

        return Task.FromResult(new HostCommandResult
        {
            Status = HostExecutionStatus.Ok,
            OperationId = request.OperationId ?? "",
            DurationMs = ElapsedMs(startedAt),
            StdoutPreview = preview,
            StderrPreview = "",
            ExitCode = 0,
            Message = "Dry-run completed. No Claude Code process was launched.",
            StdoutFullTruncated = truncated,
            StderrFullTruncated = false
        });
    }

    private async Task<HostCommandResult> ExecuteProcessAsync(HostCommandRequest request, CancellationToken ct)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var opId = request.OperationId ?? "";

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return ErrorResult(opId, startedAt, HostErrorCodes.PromptEmpty, "Prompt must not be empty.");
        }

        var workingDir = request.WorkingDirectory;
        if (!string.IsNullOrWhiteSpace(workingDir) && !Directory.Exists(workingDir))
        {
            return ErrorResult(opId, startedAt, HostErrorCodes.WorkingDirectoryNotAllowed,
                $"Working directory does not exist: {workingDir}");
        }

        if (string.IsNullOrWhiteSpace(_options.ExecutablePath))
        {
            return ErrorResult(opId, startedAt, HostErrorCodes.ExecutorError,
                "ExecutablePath is not configured. Process mode requires an executable path.");
        }

        var executablePath = _options.ExecutablePath;
        if (!File.Exists(executablePath))
        {
            var resolved = FindOnPath(executablePath);
            if (resolved == null)
            {
                return ErrorResult(opId, startedAt, HostErrorCodes.ExecutorError,
                    $"Executable not found: {executablePath}");
            }
            executablePath = resolved;
        }

        var arguments = BuildArguments(request);
        var maxChars = request.MaxOutputChars > 0 ? request.MaxOutputChars : _options.DefaultMaxOutputChars;

        var psi = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments,
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDir) ? Environment.CurrentDirectory : workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process? process;
        try
        {
            process = Process.Start(psi);
        }
        catch (Exception ex)
        {
            return ErrorResult(opId, startedAt, HostErrorCodes.ExecutorError,
                $"Failed to start process: {ex.Message}");
        }

        if (process == null)
        {
            return ErrorResult(opId, startedAt, HostErrorCodes.ExecutorError, "Failed to start process.");
        }

        using (process)
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            try
            {
                await process.WaitForExitAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return new HostCommandResult
                {
                    Status = HostExecutionStatus.Timeout,
                    OperationId = opId,
                    DurationMs = ElapsedMs(startedAt),
                    ErrorCode = HostErrorCodes.Timeout,
                    Message = $"Process timed out."
                };
            }

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            var stdoutTrunc = stdout.Length > maxChars;
            var stderrTrunc = stderr.Length > maxChars;

            var ok = process.ExitCode == 0;

            return new HostCommandResult
            {
                Status = ok ? HostExecutionStatus.Ok : HostExecutionStatus.Error,
                OperationId = opId,
                DurationMs = ElapsedMs(startedAt),
                StdoutPreview = stdoutTrunc ? stdout[..maxChars] : stdout,
                StderrPreview = stderrTrunc ? stderr[..maxChars] : stderr,
                ExitCode = process.ExitCode,
                ErrorCode = ok ? null : $"EXIT_CODE_{process.ExitCode}",
                Message = ok ? "Process completed successfully." : $"Process exited with code {process.ExitCode}.",
                StdoutFullTruncated = stdoutTrunc,
                StderrFullTruncated = stderrTrunc
            };
        }
    }

    private string BuildArguments(HostCommandRequest request)
    {
        var template = _options.ArgumentsTemplate;
        if (string.IsNullOrEmpty(template)) return request.Prompt ?? "";
        return template
            .Replace("{prompt}", request.Prompt ?? "", StringComparison.Ordinal)
            .Replace("{workingDirectory}", request.WorkingDirectory ?? "", StringComparison.Ordinal);
    }

    private static string? FindOnPath(string executable)
    {
        if (Path.IsPathFullyQualified(executable)) return null;
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv)) return null;
        var ext = Path.GetExtension(executable);
        var hasExt = !string.IsNullOrEmpty(ext);
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (hasExt)
            {
                var full = Path.Combine(dir, executable);
                if (File.Exists(full)) return full;
            }
            else
            {
                foreach (var e in new[] { ".exe", ".cmd", ".bat", ".com" })
                {
                    var full = Path.Combine(dir, executable + e);
                    if (File.Exists(full)) return full;
                }
            }
        }
        return null;
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

    protected static long ElapsedMs(long startedAt)
    {
        return (Stopwatch.GetTimestamp() - startedAt) * 1000 / Stopwatch.Frequency;
    }
}
