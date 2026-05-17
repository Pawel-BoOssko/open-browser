using System.Diagnostics;
using BridgeBrowserAlpha0.OpenBridgeHost.Commands;

namespace BridgeBrowserAlpha0.OpenBridgeHost.GeneralCommand;

public class GeneralCommandExecutor : IOpenBridgeCommandExecutor
{
    private readonly string _executablePath;
    private readonly string _argumentsTemplate;

    public GeneralCommandExecutor(string executablePath, string argumentsTemplate)
    {
        _executablePath = executablePath;
        _argumentsTemplate = argumentsTemplate;
    }

    public async Task<HostCommandResult> ExecuteAsync(HostCommandRequest request, CancellationToken ct = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var opId = request.OperationId ?? "";

        var psi = new ProcessStartInfo
        {
            FileName = _executablePath,
            Arguments = _argumentsTemplate.Replace("{prompt}", request.Prompt ?? "", StringComparison.Ordinal),
            WorkingDirectory = string.IsNullOrWhiteSpace(request.WorkingDirectory)
                ? Environment.CurrentDirectory : request.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process? process;
        try { process = Process.Start(psi); }
        catch (Exception ex)
        {
            return new HostCommandResult
            {
                Status = HostExecutionStatus.Error,
                OperationId = opId,
                DurationMs = ElapsedMs(startedAt),
                ErrorCode = HostErrorCodes.ExecutorError,
                Message = $"Failed to start process: {ex.Message}"
            };
        }

        if (process == null)
        {
            return new HostCommandResult
            {
                Status = HostExecutionStatus.Error,
                OperationId = opId,
                DurationMs = ElapsedMs(startedAt),
                ErrorCode = HostErrorCodes.ExecutorError,
                Message = "Failed to start process."
            };
        }

        using (process)
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            try { await process.WaitForExitAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return new HostCommandResult
                {
                    Status = HostExecutionStatus.Timeout,
                    OperationId = opId,
                    DurationMs = ElapsedMs(startedAt),
                    ErrorCode = HostErrorCodes.Timeout,
                    Message = "Process timed out."
                };
            }

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);
            var ok = process.ExitCode == 0;

            return new HostCommandResult
            {
                Status = ok ? HostExecutionStatus.Ok : HostExecutionStatus.Error,
                OperationId = opId,
                DurationMs = ElapsedMs(startedAt),
                StdoutPreview = stdout,
                StderrPreview = stderr,
                ExitCode = process.ExitCode,
                ErrorCode = ok ? null : $"EXIT_CODE_{process.ExitCode}",
                Message = ok ? "Process completed." : $"Process exited with code {process.ExitCode}."
            };
        }
    }

    private static long ElapsedMs(long startedAt)
    {
        return (Stopwatch.GetTimestamp() - startedAt) * 1000 / Stopwatch.Frequency;
    }
}
