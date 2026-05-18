using System.Diagnostics;
using System.Text;
using BridgeBrowserAlpha0.OpenBridgeHost;
using BridgeBrowserAlpha0.OpenBridgeHost.Commands;
using BridgeBrowserAlpha0.OpenBridgeHost.GeneralCommand;
using BridgeBrowserAlpha0.OpenBridgeProtocol;

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
        var psExecutor = new GeneralCommandExecutor("powershell.exe", "-NoProfile -Command \"{prompt}\"");
        var host = new OpenBridgeHost(psExecutor);

        // ======== Host Tests ========
        Console.WriteLine("--- Host Tests ---");

        var r1 = await host.ExecuteAsync(new HostCommandRequest
        {
            Command = "PS",
            WorkingDirectory = allowedRoot,
            Prompt = "Write-Output OK_FROM_HOST_TEST"
        });
        Assert(r1.Status == HostExecutionStatus.Ok, "Valid PS returns ok");
        Assert(!string.IsNullOrEmpty(r1.OperationId), "OperationId assigned");
        Assert(r1.StdoutPreview != null && r1.StdoutPreview.Contains("OK_FROM_HOST_TEST"), "Stdout contains expected output");
        Assert(r1.ExitCode == 0, "Exit code is 0");

        var r2 = await host.ExecuteAsync(new HostCommandRequest
        {
            Command = "PS",
            WorkingDirectory = allowedRoot,
            Prompt = "Write-Output test_opid"
        });
        Assert(!string.IsNullOrEmpty(r2.OperationId) && r2.OperationId.Length == 12, "OperationId auto-assigned (12 chars)");

        var r3 = await host.ExecuteAsync(new HostCommandRequest
        {
            OperationId = "my-op-12345",
            Command = "PS",
            WorkingDirectory = allowedRoot,
            Prompt = "Write-Output test"
        });
        Assert(r3.OperationId == "my-op-12345", "OperationId preserved when provided");

        var r4 = await host.ExecuteAsync(new HostCommandRequest
        {
            Command = "PS",
            WorkingDirectory = allowedRoot,
            Prompt = ""
        });
        Assert(r4.Status == HostExecutionStatus.Error, "Empty prompt returns error");
        Assert(r4.ErrorCode == HostErrorCodes.PromptEmpty, "Error code is PROMPT_EMPTY");

        var r4b = await host.ExecuteAsync(new HostCommandRequest
        {
            Command = "PS",
            WorkingDirectory = allowedRoot,
            Prompt = null
        });
        Assert(r4b.Status == HostExecutionStatus.Error, "Null prompt returns error");
        Assert(r4b.ErrorCode == HostErrorCodes.PromptEmpty, "Null prompt error code");

        var r5 = await host.ExecuteAsync(new HostCommandRequest
        {
            Command = "PS",
            WorkingDirectory = allowedRoot,
            Prompt = "Get-Location"
        });
        Assert(r5.Status == HostExecutionStatus.Ok, "Any prompt passes through to executor");

        var r6 = await host.ExecuteAsync(new HostCommandRequest
        {
            Command = "PS",
            WorkingDirectory = allowedRoot,
            Prompt = "Write-Output timing_test"
        });
        Assert(r6.DurationMs >= 0, "Duration is non-negative");

        // ======== PowerShell Executor Tests ========
        Console.WriteLine("--- PowerShell Executor Tests ---");

        var r7 = await host.ExecuteAsync(new HostCommandRequest
        {
            Command = "PS",
            WorkingDirectory = allowedRoot,
            Prompt = "Write-Output OK_FROM_POWERSHELL"
        });
        Assert(r7.Status == HostExecutionStatus.Ok, "PS echo returns ok");
        Assert(r7.ExitCode == 0, "PS echo exit code 0");
        Assert(r7.StdoutPreview != null && r7.StdoutPreview.Contains("OK_FROM_POWERSHELL"), "PS stdout contains expected text");

        var r8 = await host.ExecuteAsync(new HostCommandRequest
        {
            Command = "PS",
            WorkingDirectory = allowedRoot,
            Prompt = "Get-Location"
        });
        Assert(r8.Status == HostExecutionStatus.Ok, "Get-Location returns ok");
        Assert(r8.StdoutPreview != null && r8.StdoutPreview.Contains(allowedRoot), "Get-Location returns working directory");

        var r9 = await host.ExecuteAsync(new HostCommandRequest
        {
            Command = "PS",
            WorkingDirectory = allowedRoot,
            Prompt = "exit 42"
        });
        Assert(r9.Status == HostExecutionStatus.Error, "Non-zero exit returns error");
        Assert(r9.ExitCode == 42, "Exit code 42 captured");
        Assert(r9.ErrorCode != null && r9.ErrorCode.Contains("42"), "Error code reflects exit code");

        var r10 = await host.ExecuteAsync(new HostCommandRequest
        {
            Command = "PS",
            WorkingDirectory = allowedRoot,
            Prompt = "[Console]::Error.WriteLine('STDERR_TEST')"
        });
        Assert(r10.Status == HostExecutionStatus.Ok, "Stderr command returns ok (exit 0)");
        Assert(r10.StderrPreview != null && r10.StderrPreview.Contains("STDERR_TEST"), "Stderr captured");

        var psSubDir = Path.Combine(allowedRoot, "releases");
        var r11 = await host.ExecuteAsync(new HostCommandRequest
        {
            Command = "PS",
            WorkingDirectory = psSubDir,
            Prompt = "Get-Location"
        });
        Assert(r11.Status == HostExecutionStatus.Ok, "PS working directory respected");
        Assert(r11.StdoutPreview != null && r11.StdoutPreview.Contains(psSubDir), "PS runs in specified directory");

        var r12 = await host.ExecuteAsync(new HostCommandRequest
        {
            Command = "PS",
            WorkingDirectory = allowedRoot,
            Prompt = "Write-Output test"
        });
        Assert(!string.IsNullOrEmpty(r12.OperationId), "PS operation_id assigned");
        Assert(r12.DurationMs >= 0, "PS duration recorded");

        var cts = new CancellationTokenSource(500);
        var r13 = await host.ExecuteAsync(new HostCommandRequest
        {
            Command = "PS",
            WorkingDirectory = allowedRoot,
            Prompt = "Start-Sleep -Seconds 30"
        }, cts.Token);
        Assert(r13.Status is HostExecutionStatus.Error or HostExecutionStatus.Timeout, "PS with short CT handled");

        // ======== GeneralCommandExecutor (any exe) Tests ========
        Console.WriteLine("--- GeneralCommandExecutor Tests ---");
        var cmdExecutor = new GeneralCommandExecutor("cmd.exe", "/c echo CMD_TEST: {prompt}");
        var cmdHost = new OpenBridgeHost(cmdExecutor);

        var r14 = await cmdHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "PS",
            WorkingDirectory = allowedRoot,
            Prompt = "hello_from_cmd"
        });
        Assert(r14.Status == HostExecutionStatus.Ok, "Cmd returns ok");
        Assert(r14.StdoutPreview != null && r14.StdoutPreview.Contains("CMD_TEST"), "Stdout contains test marker");
        Assert(r14.StdoutPreview!.Contains("hello_from_cmd"), "Stdout contains prompt");

        var cmdErr = new GeneralCommandExecutor("cmd.exe", "/c \"echo failing && exit 42\"");
        var cmdErrHost = new OpenBridgeHost(cmdErr);
        var r15 = await cmdErrHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "PS",
            WorkingDirectory = allowedRoot,
            Prompt = "should_fail"
        });
        Assert(r15.Status == HostExecutionStatus.Error, "Non-zero exit returns error");
        Assert(r15.ExitCode == 42, "Exit code captured");
        Assert(r15.ErrorCode == "EXIT_CODE_42", "Error code reflects exit code");

        var cmdStderr = new GeneralCommandExecutor("cmd.exe", "/c \"echo to_stderr_test >&2\"");
        var cmdStderrHost = new OpenBridgeHost(cmdStderr);
        var r16 = await cmdStderrHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "PS",
            WorkingDirectory = allowedRoot,
            Prompt = "stderr_test"
        });
        Assert(r16.Status == HostExecutionStatus.Ok, "Stderr capture returns ok (exit 0)");
        Assert(r16.StderrPreview != null && r16.StderrPreview.Contains("to_stderr_test"), "Stderr was captured");

        var badExe = new GeneralCommandExecutor("nonexistent_executable_xyz_12345", "");
        var badHost = new OpenBridgeHost(badExe);
        var r17 = await badHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "PS",
            WorkingDirectory = allowedRoot,
            Prompt = "should_error"
        });
        Assert(r17.Status == HostExecutionStatus.Error, "Invalid executable returns error");
        Assert(r17.ErrorCode == HostErrorCodes.ExecutorError, "Error code is EXECUTOR_ERROR");

        // ======== Concurrency Tests ========
        Console.WriteLine("--- Concurrency Lock Tests ---");
        var delayedExecutor = new DelayedCommandExecutor(2000);
        var concurrentHost = new OpenBridgeHost(delayedExecutor);

        var firstTask = concurrentHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "PS",
            WorkingDirectory = allowedRoot,
            Prompt = "First operation (delayed)"
        });

        await Task.Delay(50);

        var r18 = await concurrentHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "PS",
            WorkingDirectory = allowedRoot,
            Prompt = "Second operation (should be rejected)"
        });
        Assert(r18.Status == HostExecutionStatus.Error, "Concurrent operation rejected");
        Assert(r18.ErrorCode == HostErrorCodes.ExecutorBusy, "Error code is EXECUTOR_BUSY");

        var firstResult = await firstTask;
        Assert(firstResult.Status == HostExecutionStatus.Ok, "First operation completed after second was rejected");

        var r19 = await concurrentHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "PS",
            WorkingDirectory = allowedRoot,
            Prompt = "After lock released"
        });
        Assert(r19.Status == HostExecutionStatus.Ok, "Operation succeeds after previous completes");

        var slowExecutor = new DelayedCommandExecutor(5000);
        var timeoutHost = new OpenBridgeHost(slowExecutor);
        var ctsTimeout = new CancellationTokenSource(200);
        var r20 = await timeoutHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "PS",
            WorkingDirectory = allowedRoot,
            Prompt = "Timeout test"
        }, ctsTimeout.Token);
        Assert(r20.ErrorCode == HostErrorCodes.Timeout, "CancellationToken triggers timeout");

        // ======== Envelope-to-Host Mapper Tests ========
        Console.WriteLine("--- Mapper Tests ---");

        var env = new OpenBridgeEnvelope { Command = "PS", Payload = "Write-Output test_ok" };
        var ok21 = OpenBridgeHostCommandMapper.TryMap(env, allowedRoot, 720_000, 50_000, out var req21, out var err21);
        Assert(ok21, "PS envelope with payload maps successfully");
        Assert(err21 == null, "No error for valid PS envelope");
        Assert(req21 != null && req21.Command == "PS", "Mapped command is PS");
        Assert(req21!.Prompt == "Write-Output test_ok", "Mapped prompt comes from payload");
        Assert(req21.WorkingDirectory == allowedRoot, "Mapped working directory is default");

        // 22. PS envelope with payload maps
        var env22 = new OpenBridgeEnvelope { Command = "PS", Payload = "Write-Output test_simple" };
        var ok22 = OpenBridgeHostCommandMapper.TryMap(env22, allowedRoot, 720_000, 50_000, out var req22, out var err22);
        Assert(ok22, "PS envelope with payload maps");
        Assert(req22!.Prompt == "Write-Output test_simple", "Mapped prompt matches payload");

        // 23. PS envelope with multi-line payload maps
        var env23 = new OpenBridgeEnvelope { Command = "PS", Payload = "Write-Output line1\nWrite-Output line2" };
        var ok23 = OpenBridgeHostCommandMapper.TryMap(env23, allowedRoot, 720_000, 50_000, out var req23, out var err23);
        Assert(ok23, "PS envelope with multi-line payload maps");
        Assert(req23!.Prompt!.Contains("line1"), "Prompt contains first line");
        Assert(req23.Prompt.Contains("line2"), "Prompt contains second line");

        var env24 = new OpenBridgeEnvelope { Command = "SH", Payload = "dir" };
        var ok24 = OpenBridgeHostCommandMapper.TryMap(env24, allowedRoot, 720_000, 50_000, out var req24, out var err24);
        Assert(!ok24, "Unsupported command is rejected");
        Assert(req24 == null, "No request for unsupported command");
        Assert(err24 != null && err24.Contains("PS"), "Error mentions only PS accepted");

        var env25 = new OpenBridgeEnvelope { Command = "PS" };
        var ok25 = OpenBridgeHostCommandMapper.TryMap(env25, allowedRoot, 720_000, 50_000, out var req25, out var err25);
        Assert(!ok25, "Missing prompt is rejected");
        Assert(err25 != null && err25.Contains("empty"), "Error mentions empty prompt");

        // Mapper produces request object, does not execute
        var env26 = new OpenBridgeEnvelope { Command = "PS", Payload = "Read file" };
        var ok26 = OpenBridgeHostCommandMapper.TryMap(env26, allowedRoot, 720_000, 50_000, out var req26, out var err26);
        Assert(ok26, "Mapper returns request");
        Assert(req26 is HostCommandRequest, "Result is HostCommandRequest, not executed");
        Assert(string.IsNullOrEmpty(req26!.OperationId), "OperationId empty (Host assigns it)");

        // ======== Runtime Approval Tests ========
        Console.WriteLine("--- Runtime Approval Tests ---");

        var approval = new OpenBridgeRuntimeApproval(host, allowedRoot);

        var env27 = new OpenBridgeEnvelopeParseResult
        {
            HasEnvelope = true,
            Error = OpenBridgeEnvelopeParseError.NONE,
            Envelope = new OpenBridgeEnvelope { Version = "001", Command = "PS", Payload = "Write-Output test_ok" }
        };
        var ok27 = approval.TrySetPending(env27, out var err27);
        Assert(ok27, "Valid PS envelope creates pending command");
        Assert(err27 == null, "No error for valid envelope");
        Assert(approval.HasPending, "HasPending is true after set");

        var env28 = new OpenBridgeEnvelopeParseResult
        {
            HasEnvelope = true,
            Error = OpenBridgeEnvelopeParseError.NONE,
            Envelope = new OpenBridgeEnvelope { Version = "001", Command = "PS", Payload = "Write-Output test_pending" }
        };
        var ok28 = approval.TrySetPending(env28, out var err28);
        Assert(!ok28, "Second candidate is rejected while first pending");
        Assert(err28 != null && err28.Contains("pending"), "Error mentions pending");

        var result29 = await approval.ExecutePendingAsync();
        Assert(result29.Status == HostExecutionStatus.Ok, "PS approve returns Ok status");
        Assert(result29.StdoutPreview != null && result29.StdoutPreview.Contains("test_ok"), "PS stdout contains expected output");
        Assert(!approval.HasPending, "HasPending is false after approve");

        Assert(approval.LastResult == result29, "LastResult stores execution result");

        var result30 = await approval.ExecutePendingAsync();
        Assert(result30.Status == HostExecutionStatus.Error, "Approve with no pending returns error");
        Assert(result30.ErrorCode == HostErrorCodes.ExecutorError, "Error code is EXECUTOR_ERROR");

        var env31 = new OpenBridgeEnvelopeParseResult
        {
            HasEnvelope = true,
            Error = OpenBridgeEnvelopeParseError.NONE,
            Envelope = new OpenBridgeEnvelope { Version = "001", Command = "PS", Payload = "Write-Output test_reject" }
        };
        approval.TrySetPending(env31, out _);
        Assert(approval.HasPending, "HasPending is true");
        approval.Reject();
        Assert(!approval.HasPending, "HasPending is false after reject");

        var env32 = new OpenBridgeEnvelopeParseResult
        {
            HasEnvelope = true,
            Error = OpenBridgeEnvelopeParseError.NONE,
            Envelope = new OpenBridgeEnvelope { Version = "001", Command = "SH", Payload = "dir" }
        };
        approval.Reject();
        var ok32 = approval.TrySetPending(env32, out var err32);
        Assert(!ok32, "Unsupported command rejected");
        Assert(err32 != null && err32.Contains("PS"), "Error mentions only PS accepted");

        var env33 = new OpenBridgeEnvelopeParseResult
        {
            HasEnvelope = true,
            Error = OpenBridgeEnvelopeParseError.NONE,
            Envelope = new OpenBridgeEnvelope { Version = "001", Command = "PS", Payload = "Write-Output test_summary" }
        };
        approval.TrySetPending(env33, out _);
        var summary = approval.PendingSummary();
        Assert(summary.Contains("PS"), "Summary contains command PS");
        Assert(summary.Contains("test_summary"), "Summary contains prompt");
        approval.Reject();

        var env34 = new OpenBridgeEnvelopeParseResult
        {
            HasEnvelope = true,
            Error = OpenBridgeEnvelopeParseError.NONE,
            Envelope = new OpenBridgeEnvelope { Version = "001", Command = "PS" }
        };
        var ok34 = approval.TrySetPending(env34, out _);
        Assert(!ok34, "Empty prompt rejected");

        var veryLongPrompt = new string('A', 1500);
        var env35 = new OpenBridgeEnvelopeParseResult
        {
            HasEnvelope = true,
            Error = OpenBridgeEnvelopeParseError.NONE,
            Envelope = new OpenBridgeEnvelope { Version = "001", Command = "PS", Payload = veryLongPrompt }
        };
        approval.TrySetPending(env35, out _);
        var summary35 = approval.PendingSummary();
        Assert(summary35.Contains("AAAA"), "Summary contains truncated prompt");
        Assert(!summary35.Contains(new string('A', 1500)), "Summary does not contain full prompt");
        Assert(summary35.Contains("1500 chars"), "Summary shows original prompt length");
        approval.Reject();

        var env36 = new OpenBridgeEnvelopeParseResult
        {
            HasEnvelope = true,
            Error = OpenBridgeEnvelopeParseError.NONE,
            Envelope = new OpenBridgeEnvelope { Version = "001", Command = "PS", Payload = "Write-Output test_result" }
        };
        approval.TrySetPending(env36, out _);
        var result36 = await approval.ExecutePendingAsync();
        Assert(!string.IsNullOrEmpty(result36.OperationId), "Result has operation_id");
        Assert(result36.DurationMs >= 0, "Result has duration");
        var resultSummary36 = approval.ResultSummary();
        Assert(resultSummary36.Contains(result36.OperationId), "Result summary includes operation_id");

        var env37 = new OpenBridgeEnvelopeParseResult
        {
            HasEnvelope = true,
            Error = OpenBridgeEnvelopeParseError.NONE,
            Envelope = new OpenBridgeEnvelope { Version = "001", Command = "PS", Payload = "Write-Output test_reject2" }
        };
        approval.TrySetPending(env37, out _);
        approval.Reject();
        Assert(!approval.HasPending, "Reject clears pending");
        Assert(approval.ResultSummary() == "", "Reject clears result summary");

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

    private sealed class DelayedCommandExecutor : IOpenBridgeCommandExecutor
    {
        private readonly int _delayMs;

        public DelayedCommandExecutor(int delayMs)
        {
            _delayMs = delayMs;
        }

        public async Task<HostCommandResult> ExecuteAsync(HostCommandRequest request, CancellationToken ct = default)
        {
            var startedAt = Stopwatch.GetTimestamp();

            if (string.IsNullOrWhiteSpace(request.Prompt))
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

            var echo = $"[DELAYED] Prompt: {request.Prompt}";
            return new HostCommandResult
            {
                Status = HostExecutionStatus.Ok,
                OperationId = request.OperationId ?? "",
                DurationMs = ElapsedMs(startedAt),
                StdoutPreview = echo,
                ExitCode = 0,
                Message = "Delayed execution completed."
            };
        }

        private static long ElapsedMs(long startedAt)
        {
            return (Stopwatch.GetTimestamp() - startedAt) * 1000 / Stopwatch.Frequency;
        }
    }
}
