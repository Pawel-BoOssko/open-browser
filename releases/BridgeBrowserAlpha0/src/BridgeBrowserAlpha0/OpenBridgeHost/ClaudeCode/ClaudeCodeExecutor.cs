using System.Diagnostics;

namespace BridgeBrowserAlpha0.OpenBridgeHost.ClaudeCode;

public class ClaudeCodeExecutor
{
    public virtual Task<HostCommandResult> ExecuteAsync(HostCommandRequest request, CancellationToken ct = default)
    {
        var startedAt = Stopwatch.GetTimestamp();

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return Task.FromResult(new HostCommandResult
            {
                Status = HostExecutionStatus.Error,
                OperationId = request.OperationId ?? "",
                DurationMs = ElapsedMs(startedAt),
                ErrorCode = HostErrorCodes.PromptEmpty,
                Message = "Prompt must not be empty."
            });
        }

        var workingDir = request.WorkingDirectory;
        if (!string.IsNullOrWhiteSpace(workingDir) && !Directory.Exists(workingDir))
        {
            return Task.FromResult(new HostCommandResult
            {
                Status = HostExecutionStatus.Error,
                OperationId = request.OperationId ?? "",
                DurationMs = ElapsedMs(startedAt),
                ErrorCode = HostErrorCodes.WorkingDirectoryNotAllowed,
                Message = $"Working directory does not exist: {workingDir}"
            });
        }

        var echo = $"[DRY-RUN ClaudeCodeExecutor] Working directory: {workingDir}\nPrompt: {request.Prompt}";
        var maxChars = request.MaxOutputChars > 0 ? request.MaxOutputChars : 50_000;
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

    protected static long ElapsedMs(long startedAt)
    {
        return (Stopwatch.GetTimestamp() - startedAt) * 1000 / Stopwatch.Frequency;
    }
}
