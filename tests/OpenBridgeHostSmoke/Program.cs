using System.Diagnostics;
using BridgeBrowserAlpha0.OpenBridgeHost;

namespace OpenBridgeHostSmoke;

class Program
{
    static int _failures = 0;

    static void Assert(bool condition, string testName)
    {
        if (condition)
        {
            Console.WriteLine("PASS: " + testName);
        }
        else
        {
            Console.WriteLine("FAIL: " + testName);
            _failures++;
        }
    }

    static async Task Main()
    {
        var allowedRoot = @"D:\projects\open-browser";
        var host = new OpenBridgeHost(allowedRoot);

        Console.WriteLine("--- Running OpenBridgeHost Smoke Tests ---");

        // 1. Valid dry-run command returns ok
        var r1 = await host.ExecuteAsync(new HostCommandRequest
        {
            Command = "CC",
            WorkingDirectory = allowedRoot,
            Prompt = "Fix the null check in src/auth.py"
        });
        Assert(r1.Status == HostExecutionStatus.Ok, "Valid dry-run returns ok");
        Assert(!string.IsNullOrEmpty(r1.OperationId), "OperationId assigned");
        Assert(r1.StdoutPreview != null && r1.StdoutPreview.Contains("DRY-RUN"), "Stdout contains dry-run marker");
        Assert(r1.StdoutPreview!.Contains("Fix the null check"), "Stdout contains prompt");
        Assert(r1.StdoutPreview.Contains(allowedRoot), "Stdout contains working directory");
        Assert(r1.ExitCode == 0, "Exit code is 0");
        Assert(r1.Message != null && r1.Message.Contains("No Claude Code process"), "Message confirms no real process");

        // 2. Operation ID is assigned when missing
        var r2 = await host.ExecuteAsync(new HostCommandRequest
        {
            Command = "CC",
            WorkingDirectory = allowedRoot,
            Prompt = "Test operation id assignment"
        });
        Assert(!string.IsNullOrEmpty(r2.OperationId) && r2.OperationId.Length == 12, "OperationId auto-assigned (12 chars)");

        // 3. Operation ID is preserved when provided
        var r3 = await host.ExecuteAsync(new HostCommandRequest
        {
            OperationId = "my-op-12345",
            Command = "CC",
            WorkingDirectory = allowedRoot,
            Prompt = "Test"
        });
        Assert(r3.OperationId == "my-op-12345", "OperationId preserved when provided");

        // 4. Missing prompt returns error
        var r4 = await host.ExecuteAsync(new HostCommandRequest
        {
            Command = "CC",
            WorkingDirectory = allowedRoot,
            Prompt = ""
        });
        Assert(r4.Status == HostExecutionStatus.Error, "Empty prompt returns error");
        Assert(r4.ErrorCode == HostErrorCodes.PromptEmpty, "Error code is PROMPT_EMPTY");

        var r4b = await host.ExecuteAsync(new HostCommandRequest
        {
            Command = "CC",
            WorkingDirectory = allowedRoot,
            Prompt = null
        });
        Assert(r4b.Status == HostExecutionStatus.Error, "Null prompt returns error");
        Assert(r4b.ErrorCode == HostErrorCodes.PromptEmpty, "Null prompt error code");

        // 5. Invalid working directory returns error
        var r5 = await host.ExecuteAsync(new HostCommandRequest
        {
            Command = "CC",
            WorkingDirectory = @"D:\somewhere\else",
            Prompt = "Test"
        });
        Assert(r5.Status == HostExecutionStatus.Error, "Directory outside root returns error");
        Assert(r5.ErrorCode == HostErrorCodes.WorkingDirectoryNotAllowed, "Error code is WORKING_DIRECTORY_NOT_ALLOWED");

        var r5b = await host.ExecuteAsync(new HostCommandRequest
        {
            Command = "CC",
            WorkingDirectory = "",
            Prompt = "Test"
        });
        Assert(r5b.Status == HostExecutionStatus.Error, "Empty working directory returns error");

        // 6. Valid subdirectory under allowed root works
        var subDir = Path.Combine(allowedRoot, "releases");
        var r6 = await host.ExecuteAsync(new HostCommandRequest
        {
            Command = "CC",
            WorkingDirectory = subDir,
            Prompt = "Test subdirectory"
        });
        Assert(r6.Status == HostExecutionStatus.Ok, "Subdirectory under root is allowed");
        Assert(r6.StdoutPreview!.Contains(subDir), "Stdout contains subdirectory path");

        // 7. Command not CC returns error
        var r7 = await host.ExecuteAsync(new HostCommandRequest
        {
            Command = "SH",
            WorkingDirectory = allowedRoot,
            Prompt = "dir"
        });
        Assert(r7.Status == HostExecutionStatus.Error, "Non-CC command returns error");
        Assert(r7.ErrorCode == HostErrorCodes.CommandNotRecognized, "Error code is COMMAND_NOT_RECOGNIZED");

        // 8. Max output truncation works
        var longPrompt = new string('X', 200);
        var r8 = await host.ExecuteAsync(new HostCommandRequest
        {
            Command = "CC",
            WorkingDirectory = allowedRoot,
            Prompt = longPrompt,
            MaxOutputChars = 50
        });
        Assert(r8.Status == HostExecutionStatus.Ok, "Small max output succeeds");
        Assert(r8.StdoutFullTruncated, "Stdout was truncated");
        Assert(r8.StdoutPreview!.Length <= 50, "Stdout preview is within max chars");

        // 9. Max output not truncated when large enough
        var r9 = await host.ExecuteAsync(new HostCommandRequest
        {
            Command = "CC",
            WorkingDirectory = allowedRoot,
            Prompt = "Short prompt",
            MaxOutputChars = 100_000
        });
        Assert(r9.Status == HostExecutionStatus.Ok, "Large max output succeeds");
        Assert(!r9.StdoutFullTruncated, "Stdout was not truncated");

        // 10. Duration is positive
        var r10 = await host.ExecuteAsync(new HostCommandRequest
        {
            Command = "CC",
            WorkingDirectory = allowedRoot,
            Prompt = "Timing test"
        });
        Assert(r10.DurationMs >= 0, "Duration is non-negative");

        // 11. Concurrency: busy state rejects second operation
        Console.WriteLine("--- Testing concurrency lock ---");
        var delayedExecutor = new DelayedClaudeCodeExecutor(2000);
        var concurrentHost = new OpenBridgeHost(allowedRoot, delayedExecutor);

        var firstTask = concurrentHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "CC",
            WorkingDirectory = allowedRoot,
            Prompt = "First operation (delayed)"
        });

        await Task.Delay(50);

        var r11 = await concurrentHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "CC",
            WorkingDirectory = allowedRoot,
            Prompt = "Second operation (should be rejected)"
        });
        Assert(r11.Status == HostExecutionStatus.Error, "Concurrent operation rejected");
        Assert(r11.ErrorCode == HostErrorCodes.ExecutorBusy, "Error code is EXECUTOR_BUSY");

        var firstResult = await firstTask;
        Assert(firstResult.Status == HostExecutionStatus.Ok, "First operation completed after second was rejected");

        // 12. After first completes, next operation succeeds (lock released)
        var r12 = await concurrentHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "CC",
            WorkingDirectory = allowedRoot,
            Prompt = "After lock released"
        });
        Assert(r12.Status == HostExecutionStatus.Ok, "Operation succeeds after previous completes");

        // 13. Timeout fires for slow executor
        var slowExecutor = new DelayedClaudeCodeExecutor(5000);
        var timeoutHost = new OpenBridgeHost(allowedRoot, slowExecutor);
        var r13 = await timeoutHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "CC",
            WorkingDirectory = allowedRoot,
            Prompt = "Timeout test",
            TimeoutMs = 200
        });
        Assert(r13.Status == HostExecutionStatus.Timeout, "Timeout returns timeout status");
        Assert(r13.ErrorCode == HostErrorCodes.Timeout, "Error code is TIMEOUT");

        if (_failures > 0)
        {
            Console.WriteLine($"\nTotal Failures: {_failures}");
            Environment.Exit(1);
        }
        else
        {
            Console.WriteLine("\nAll tests PASSED.");
            Environment.Exit(0);
        }
    }

    private sealed class DelayedClaudeCodeExecutor : BridgeBrowserAlpha0.OpenBridgeHost.ClaudeCode.ClaudeCodeExecutor
    {
        private readonly int _delayMs;

        public DelayedClaudeCodeExecutor(int delayMs)
        {
            _delayMs = delayMs;
        }

        public override async Task<HostCommandResult> ExecuteAsync(HostCommandRequest request, CancellationToken ct = default)
        {
            var startedAt = Stopwatch.GetTimestamp();

            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                return ErrorResult(request, startedAt);
            }

            try
            {
                await Task.Delay(_delayMs, ct);
            }
            catch (OperationCanceledException)
            {
                return new HostCommandResult
                {
                    Status = HostExecutionStatus.Timeout,
                    OperationId = request.OperationId ?? "",
                    DurationMs = ElapsedMs(startedAt),
                    ErrorCode = HostErrorCodes.Timeout,
                    Message = "Delayed executor cancelled due to timeout."
                };
            }

            var echo = $"[DELAYED-DRY-RUN] Prompt: {request.Prompt}";
            return new HostCommandResult
            {
                Status = HostExecutionStatus.Ok,
                OperationId = request.OperationId ?? "",
                DurationMs = ElapsedMs(startedAt),
                StdoutPreview = echo,
                ExitCode = 0,
                Message = "Delayed dry-run completed."
            };
        }

        private static HostCommandResult ErrorResult(HostCommandRequest request, long startedAt)
        {
            return new HostCommandResult
            {
                Status = HostExecutionStatus.Error,
                OperationId = request.OperationId ?? "",
                DurationMs = ElapsedMs(startedAt),
                ErrorCode = HostErrorCodes.PromptEmpty,
                Message = "Prompt must not be empty."
            };
        }
    }
}
