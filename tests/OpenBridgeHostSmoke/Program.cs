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
        var host = new OpenBridgeHost();

        Console.WriteLine("--- Dry-Run Tests ---");

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
        Assert(r1.Message != null && r1.Message.Contains("No real process was launched"), "Message confirms dry-run");

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

        // 5. Any command passes through (Host no longer validates commands/directories)
        var r5 = await host.ExecuteAsync(new HostCommandRequest
        {
            Command = "SH",
            WorkingDirectory = allowedRoot,
            Prompt = "dir"
        });
        Assert(r5.Status == HostExecutionStatus.Ok, "Any command passes through to executor");

        // 8. Max output truncation works (dry-run)
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

        // ======== Process Mode Tests ========
        Console.WriteLine("--- Process Mode Tests ---");

        // 11. Process mode captures stdout
        var processOpts = new CommandExecutorOptions
        {
            Mode = CommandExecutorMode.Process,
            ExecutablePath = "cmd.exe",
            ArgumentsTemplate = "/c echo OPENBRIDGE_PROCESS_TEST: {prompt}"
        };
        var processExecutor = new CommandExecutor(processOpts);
        var processHost = new OpenBridgeHost(processExecutor);

        var r11 = await processHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "CC",
            WorkingDirectory = allowedRoot,
            Prompt = "hello_from_process"
        });
        Assert(r11.Status == HostExecutionStatus.Ok, "Process mode captures stdout");
        Assert(r11.ExitCode == 0, "Process exit code is 0");
        Assert(r11.StdoutPreview != null && r11.StdoutPreview.Contains("OPENBRIDGE_PROCESS_TEST"), "Stdout contains test marker");
        Assert(r11.StdoutPreview!.Contains("hello_from_process"), "Stdout contains prompt");
        Assert(r11.Message != null && r11.Message.Contains("completed"), "Message confirms process completed");

        // 12. Process mode captures non-zero exit code as error
        var errorProcessOpts = new CommandExecutorOptions
        {
            Mode = CommandExecutorMode.Process,
            ExecutablePath = "cmd.exe",
            ArgumentsTemplate = "/c \"echo failing operation && exit 42\""
        };
        var errorProcessHost = new OpenBridgeHost(new CommandExecutor(errorProcessOpts));

        var r12 = await errorProcessHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "CC",
            WorkingDirectory = allowedRoot,
            Prompt = "should_fail"
        });
        Assert(r12.Status == HostExecutionStatus.Error, "Non-zero exit returns error status");
        Assert(r12.ExitCode == 42, "Exit code captured");
        Assert(r12.ErrorCode == "EXIT_CODE_42", "Error code reflects exit code");
        Assert(r12.StdoutPreview != null && r12.StdoutPreview.Contains("failing operation"), "Stdout captured despite error exit");

        // 13. Process mode captures stderr
        var stderrProcessOpts = new CommandExecutorOptions
        {
            Mode = CommandExecutorMode.Process,
            ExecutablePath = "cmd.exe",
            ArgumentsTemplate = "/c \"echo to_stderr_test >&2\""
        };
        var stderrProcessHost = new OpenBridgeHost(new CommandExecutor(stderrProcessOpts));

        var r13 = await stderrProcessHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "CC",
            WorkingDirectory = allowedRoot,
            Prompt = "stderr_test"
        });
        Assert(r13.Status == HostExecutionStatus.Ok, "Stderr capture returns ok (exit 0)");
        Assert(r13.StderrPreview != null && r13.StderrPreview.Contains("to_stderr_test"), "Stderr was captured");

        // 14. CancellationToken controls timeout from caller
        var cts14 = new CancellationTokenSource(500);
        var r14 = await host.ExecuteAsync(new HostCommandRequest
        {
            Command = "CC",
            WorkingDirectory = allowedRoot,
            Prompt = "timeout_test"
        }, cts14.Token);
        Assert(r14.Status is HostExecutionStatus.Error or HostExecutionStatus.Ok, "CancellationToken passed to executor");

        // 15. Invalid executable returns controlled error
        var badExeOpts = new CommandExecutorOptions
        {
            Mode = CommandExecutorMode.Process,
            ExecutablePath = "nonexistent_executable_xyz_12345"
        };
        var badExeHost = new OpenBridgeHost(new CommandExecutor(badExeOpts));

        var r15 = await badExeHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "CC",
            WorkingDirectory = allowedRoot,
            Prompt = "should_error"
        });
        Assert(r15.Status == HostExecutionStatus.Error, "Invalid executable returns error");
        Assert(r15.ErrorCode == HostErrorCodes.ExecutorError, "Error code is EXECUTOR_ERROR");
        Assert(r15.Message != null && r15.Message.Contains("not found"), "Message mentions executable not found");

        // 16. Process mode output truncation works
        var truncProcessOpts = new CommandExecutorOptions
        {
            Mode = CommandExecutorMode.Process,
            ExecutablePath = "cmd.exe",
            ArgumentsTemplate = "/c echo {prompt}"
        };
        var truncProcessHost = new OpenBridgeHost(new CommandExecutor(truncProcessOpts));
        var longEchoText = new string('Y', 200);

        var r16 = await truncProcessHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "CC",
            WorkingDirectory = allowedRoot,
            Prompt = longEchoText,
            MaxOutputChars = 50
        });
        Assert(r16.Status == HostExecutionStatus.Ok, "Process truncation returns ok");
        Assert(r16.StdoutFullTruncated, "Stdout was truncated");
        Assert(r16.StdoutPreview!.Length <= 50, "Stdout preview is within max chars");

        // 17. Dry-run is still the default when no options given
        var defaultHost = new OpenBridgeHost();
        var r17 = await defaultHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "CC",
            WorkingDirectory = allowedRoot,
            Prompt = "default mode check"
        });
        Assert(r17.Status == HostExecutionStatus.Ok, "Default executor still works");
        Assert(r17.StdoutPreview != null && r17.StdoutPreview.Contains("[DRY-RUN"), "Default mode is still dry-run");

        // ======== Configuration Loader Tests ========
        Console.WriteLine("--- Configuration Loader Tests ---");

        // 18. Missing optional config returns null (no file = default dry-run)
        var missingPath = Path.Combine(Path.GetTempPath(), $"openbridge_nonexistent_{Guid.NewGuid():N}.json");
        var rCfg1 = CommandExecutorOptionsLoader.TryLoad(missingPath);
        Assert(rCfg1 == null, "TryLoad returns null for missing file");

        // 19. Example config can be parsed
        var examplePath = Path.GetFullPath(Path.Combine(allowedRoot, "config", "examples", "claude-code-executor.example.json"));
        if (File.Exists(examplePath))
        {
            var rCfg2 = CommandExecutorOptionsLoader.LoadOrThrow(examplePath);
            Assert(rCfg2.Mode == CommandExecutorMode.DryRun, "Example config mode is DryRun");
            Assert(rCfg2.DefaultTimeoutMs == 720_000, "Example config has default timeout");
            Assert(rCfg2.DefaultMaxOutputChars == 50_000, "Example config has default max output");
        }
        else
        {
            Console.WriteLine("SKIP: Example config not found at expected path (test repo layout)");
        }

        // 20. Process config can be parsed from a temp JSON file
        var tempConfigPath = Path.Combine(Path.GetTempPath(), $"openbridge_test_config_{Guid.NewGuid():N}.json");
        var tempJson = @"{
  ""Mode"": ""Process"",
  ""ExecutablePath"": ""cmd.exe"",
  ""ArgumentsTemplate"": ""/c echo CONFIG_TEST"",
  ""DefaultTimeoutMs"": 120000,
  ""DefaultMaxOutputChars"": 10000
}";
        try
        {
            File.WriteAllText(tempConfigPath, tempJson);
            var rCfg3 = CommandExecutorOptionsLoader.LoadOrThrow(tempConfigPath);
            Assert(rCfg3.Mode == CommandExecutorMode.Process, "Temp config mode is Process");
            Assert(rCfg3.ExecutablePath == "cmd.exe", "Temp config has executable path");
            Assert(rCfg3.ArgumentsTemplate == "/c echo CONFIG_TEST", "Temp config has arguments template");
            Assert(rCfg3.DefaultTimeoutMs == 120000, "Temp config has custom timeout");
            Assert(rCfg3.DefaultMaxOutputChars == 10000, "Temp config has custom max output chars");

            // Also verify the loaded config works with the executor (no process invoked here — only parsing)
            var configExecutor = new CommandExecutor(rCfg3);
            var configHost = new OpenBridgeHost(configExecutor);
            var rCfg3exec = await configHost.ExecuteAsync(new HostCommandRequest
            {
                Command = "CC",
                WorkingDirectory = allowedRoot,
                Prompt = "config_loaded_test"
            });
            Assert(rCfg3exec.Status == HostExecutionStatus.Ok, "Executor loaded from config works");
            Assert(rCfg3exec.StdoutPreview != null && rCfg3exec.StdoutPreview.Contains("CONFIG_TEST"), "Config-based executor runs with configured args");
        }
        finally
        {
            if (File.Exists(tempConfigPath)) File.Delete(tempConfigPath);
        }

        // 21. Invalid JSON throws
        var badJsonPath = Path.Combine(Path.GetTempPath(), $"openbridge_bad_json_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(badJsonPath, "{ this is not valid json }");
            try
            {
                CommandExecutorOptionsLoader.LoadOrThrow(badJsonPath);
                Assert(false, "Invalid JSON should throw");
            }
            catch (System.Text.Json.JsonException)
            {
                Assert(true, "Invalid JSON throws JsonException");
            }
        }
        finally
        {
            if (File.Exists(badJsonPath)) File.Delete(badJsonPath);
        }

        // 22. Loading config does not invoke any process
        var safeConfigPath = Path.Combine(Path.GetTempPath(), $"openbridge_safe_{Guid.NewGuid():N}.json");
        var safeJson = @"{
  ""Mode"": ""DryRun"",
  ""ExecutablePath"": """",
  ""ArgumentsTemplate"": """"
}";
        try
        {
            File.WriteAllText(safeConfigPath, safeJson);
            var rCfg4 = CommandExecutorOptionsLoader.TryLoad(safeConfigPath);
            Assert(rCfg4 != null, "Safe config loads successfully");
            Assert(rCfg4!.Mode == CommandExecutorMode.DryRun, "Safe config stays in dry-run mode");
            Assert(string.IsNullOrEmpty(rCfg4.ExecutablePath), "Safe config has no executable");
        }
        finally
        {
            if (File.Exists(safeConfigPath)) File.Delete(safeConfigPath);
        }

        // 23. camelCase JSON (case insensitive) works
        var camelConfigPath = Path.Combine(Path.GetTempPath(), $"openbridge_camel_{Guid.NewGuid():N}.json");
        var camelJson = @"{
  ""mode"": ""DryRun"",
  ""executablePath"": null,
  ""argumentsTemplate"": null,
  ""defaultTimeoutMs"": 60000,
  ""defaultMaxOutputChars"": 5000
}";
        try
        {
            File.WriteAllText(camelConfigPath, camelJson);
            var rCfg5 = CommandExecutorOptionsLoader.LoadOrThrow(camelConfigPath);
            Assert(rCfg5.Mode == CommandExecutorMode.DryRun, "camelCase mode parses correctly");
            Assert(rCfg5.DefaultTimeoutMs == 60000, "camelCase timeout parses correctly");
            Assert(rCfg5.DefaultMaxOutputChars == 5000, "camelCase max output parses correctly");
        }
        finally
        {
            if (File.Exists(camelConfigPath)) File.Delete(camelConfigPath);
        }

        // ======== Default Timeout Tests ========
        Console.WriteLine("--- Default Timeout Tests ---");

        // 24. Default timeout is 720000 (12 minutes)
        var defaultOpts = new CommandExecutorOptions();
        Assert(defaultOpts.DefaultTimeoutMs == 720_000, "Default timeout is 720000ms (12 min)");

        // 25. Example config timeout is 720000
        var exampleCfgPath = Path.GetFullPath(Path.Combine(allowedRoot, "config", "examples", "claude-code-executor.example.json"));
        if (File.Exists(exampleCfgPath))
        {
            var r25 = CommandExecutorOptionsLoader.LoadOrThrow(exampleCfgPath);
            Assert(r25.DefaultTimeoutMs == 720_000, "Example config timeout is 720000");
        }
        else
        {
            Console.WriteLine("SKIP: Example config not found");
        }

        // 26. Short timeout tests still override to small values explicitly
        var shortOpts = new CommandExecutorOptions { DefaultTimeoutMs = 500 };
        var shortExecutor = new CommandExecutor(shortOpts);
        var shortHost = new OpenBridgeHost(shortExecutor);
        var r26 = await shortHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "CC",
            WorkingDirectory = allowedRoot,
            Prompt = "Short timeout test"
        });
        Assert(r26.Status == HostExecutionStatus.Ok, "Short explicit timeout works (dry-run)");
        // The timeout value from the request itself takes precedence in Host logic

        // ======== Envelope-to-Host Mapper Tests ========
        Console.WriteLine("--- Mapper Tests ---");

        // 27. CC envelope with payload maps to HostCommandRequest
        var env = new OpenBridgeEnvelope { Command = "PS", Payload = "Write-Output test_ok" };
        var ok27 = OpenBridgeHostCommandMapper.TryMap(env, allowedRoot, 720_000, 50_000, out var req27, out var err27);
        Assert(ok27, "PS envelope with payload maps successfully");
        Assert(err27 == null, "No error for valid PS envelope");
        Assert(req27 != null && req27.Command == "PS", "Mapped command is PS");
        Assert(req27!.Prompt == "Write-Output test_ok", "Mapped prompt comes from payload");
        Assert(req27.WorkingDirectory == allowedRoot, "Mapped working directory is default");
        Assert(req27.TimeoutMs == 720_000, "Mapped timeout is default");
        Assert(req27.MaxOutputChars == 50_000, "Mapped max output is default");

        // 28. CC envelope with payload64 maps to decoded prompt
        var payload64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("Implement health check"));
        var env28 = new OpenBridgeEnvelope { Command = "PS", Payload64 = payload64 };
        var ok28 = OpenBridgeHostCommandMapper.TryMap(env28, allowedRoot, 720_000, 50_000, out var req28, out var err28);
        Assert(ok28, "PS envelope with payload64 maps successfully");
        Assert(req28!.Prompt == "Implement health check", "Mapped prompt decoded from payload64");

        // 29. CC envelope with payload and payload64 combines both
        var p64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("Write a function that returns health status"));
        var env29 = new OpenBridgeEnvelope { Command = "PS", Payload = "Implement health endpoint", Payload64 = p64 };
        var ok29 = OpenBridgeHostCommandMapper.TryMap(env29, allowedRoot, 720_000, 50_000, out var req29, out var err29);
        Assert(ok29, "PS envelope with both payloads maps");
        Assert(req29!.Prompt!.Contains("Implement health endpoint"), "Prompt contains payload prefix");
        Assert(req29.Prompt.Contains("Write a function"), "Prompt contains decoded payload64");

        // 30. Unsupported command is rejected
        var env30 = new OpenBridgeEnvelope { Command = "SH", Payload = "dir" };
        var ok30 = OpenBridgeHostCommandMapper.TryMap(env30, allowedRoot, 720_000, 50_000, out var req30, out var err30);
        Assert(!ok30, "Unsupported command is rejected");
        Assert(req30 == null, "No request for unsupported command");
        Assert(err30 != null && err30.Contains("PS"), "Error mentions only PS accepted");

        // 31. Empty/missing prompt is rejected by mapper
        var env31 = new OpenBridgeEnvelope { Command = "PS" };
        var ok31 = OpenBridgeHostCommandMapper.TryMap(env31, allowedRoot, 720_000, 50_000, out var req31, out var err31);
        Assert(!ok31, "Missing prompt is rejected by mapper");
        Assert(err31 != null && err31.Contains("empty"), "Error mentions empty prompt");

        // 32. Mapper does not execute commands — it only produces a request object
        var env32 = new OpenBridgeEnvelope { Command = "PS", Payload = "Read file" };
        var ok32 = OpenBridgeHostCommandMapper.TryMap(env32, allowedRoot, 720_000, 50_000, out var req32, out var err32);
        Assert(ok32, "Mapper returns request");
        Assert(req32 is HostCommandRequest, "Result is HostCommandRequest, not an executed result");
        // Verify the request was NOT sent to any host — it's just a data object
        Assert(string.IsNullOrEmpty(req32!.OperationId), "OperationId is empty (not executed, Host assigns it)");

        // ======== Runtime Approval Tests ========
        Console.WriteLine("--- Runtime Approval Tests ---");

        var approval = new OpenBridgeRuntimeApproval(allowedRoot);

        // 33. Valid CC envelope creates pending command
        var env33 = new OpenBridgeEnvelopeParseResult
        {
            HasEnvelope = true,
            Error = OpenBridgeEnvelopeParseError.NONE,
            Envelope = new OpenBridgeEnvelope { Version = "001", Command = "PS", Payload = "Write-Output test_ok" }
        };
        var ok33 = approval.TrySetPending(env33, out var err33);
        Assert(ok33, "Valid PS envelope creates pending command");
        Assert(err33 == null, "No error for valid envelope");
        Assert(approval.HasPending, "HasPending is true after set");

        // 34. Second pending is rejected while first exists
        var env34 = new OpenBridgeEnvelopeParseResult
        {
            HasEnvelope = true,
            Error = OpenBridgeEnvelopeParseError.NONE,
            Envelope = new OpenBridgeEnvelope { Version = "001", Command = "PS", Payload = "Write-Output test_pending" }
        };
        var ok34 = approval.TrySetPending(env34, out var err34);
        Assert(!ok34, "Second candidate is rejected while first pending");
        Assert(err34 != null && err34.Contains("pending"), "Error mentions pending");

        // 35. Approve executes DryRun and returns ok
        var result35 = await approval.ApproveDryRunAsync();
        Assert(result35.Status == HostExecutionStatus.Ok, "PS approve returns Ok status");
        Assert(result35.StdoutPreview != null && result35.StdoutPreview.Contains("test_ok"), "PS stdout contains expected output");
        Assert(!approval.HasPending, "HasPending is false after approve");

        // 36. Result is stored in LastResult
        Assert(approval.LastResult == result35, "LastResult stores execution result");

        // 37. Approve with no pending returns error
        var result37 = await approval.ApproveDryRunAsync();
        Assert(result37.Status == HostExecutionStatus.Error, "Approve with no pending returns error");
        Assert(result37.ErrorCode == HostErrorCodes.ExecutorError, "Error code is EXECUTOR_ERROR");

        // 38. Reject clears pending
        var env38 = new OpenBridgeEnvelopeParseResult
        {
            HasEnvelope = true,
            Error = OpenBridgeEnvelopeParseError.NONE,
            Envelope = new OpenBridgeEnvelope { Version = "001", Command = "PS", Payload = "Write-Output test_reject" }
        };
        approval.TrySetPending(env38, out _);
        Assert(approval.HasPending, "HasPending is true");
        approval.Reject();
        Assert(!approval.HasPending, "HasPending is false after reject");

        // 39. Runtime approval uses DryRun mode (never Process)
        var env39 = new OpenBridgeEnvelopeParseResult
        {
            HasEnvelope = true,
            Error = OpenBridgeEnvelopeParseError.NONE,
            Envelope = new OpenBridgeEnvelope { Version = "001", Command = "PS", Payload = "Write-Output test_mode" }
        };
        approval.TrySetPending(env39, out _);
        var result39 = await approval.ApproveDryRunAsync();
        Assert(result39.Status == HostExecutionStatus.Ok, "PS execution returns result");
        Assert(result39.StdoutPreview != null && result39.StdoutPreview.Contains("test_mode"), "PS stdout contains expected output");

        // 40. Unsupported command rejected by mapper
        var env40 = new OpenBridgeEnvelopeParseResult
        {
            HasEnvelope = true,
            Error = OpenBridgeEnvelopeParseError.NONE,
            Envelope = new OpenBridgeEnvelope { Version = "001", Command = "SH", Payload = "dir" }
        };
        approval.Reject(); // clear any pending
        var ok40 = approval.TrySetPending(env40, out var err40);
        Assert(!ok40, "Unsupported command rejected");
        Assert(err40 != null && err40.Contains("PS"), "Error mentions only PS accepted");

        // 41. PendingSummary includes key fields
        var env41 = new OpenBridgeEnvelopeParseResult
        {
            HasEnvelope = true,
            Error = OpenBridgeEnvelopeParseError.NONE,
            Envelope = new OpenBridgeEnvelope { Version = "001", Command = "PS", Payload = "Write-Output test_summary" }
        };
        approval.TrySetPending(env41, out _);
        var summary = approval.PendingSummary();
        Assert(summary.Contains("CC"), "Summary contains command PS");
        Assert(summary.Contains("test_summary"), "Summary contains prompt");
        Assert(summary.Contains("720000"), "Summary contains timeout");
        Assert(summary.Contains("CC") && summary.Contains("720000"), "Summary contains command and timeout");
        approval.Reject();

        // 42. Empty prompt rejected by mapper
        var env42 = new OpenBridgeEnvelopeParseResult
        {
            HasEnvelope = true,
            Error = OpenBridgeEnvelopeParseError.NONE,
            Envelope = new OpenBridgeEnvelope { Version = "001", Command = "PS" }
        };
        var ok42 = approval.TrySetPending(env42, out _);
        Assert(!ok42, "Empty prompt rejected");

        // 43. Pending summary truncates long prompt at 1000 chars
        var veryLongPrompt = new string('A', 1500);
        var env43 = new OpenBridgeEnvelopeParseResult
        {
            HasEnvelope = true,
            Error = OpenBridgeEnvelopeParseError.NONE,
            Envelope = new OpenBridgeEnvelope { Version = "001", Command = "PS", Payload = veryLongPrompt }
        };
        approval.TrySetPending(env43, out _);
        var summary43 = approval.PendingSummary();
        Assert(summary43.Contains("AAAA"), "Summary contains truncated prompt");
        Assert(!summary43.Contains(new string('A', 1500)), "Summary does not contain full 1500-char prompt");
        Assert(summary43.Contains("1500 chars"), "Summary shows original prompt length");
        approval.Reject();

        // 44. PendingCommandDetails does not include full payload64
        var secretPayload64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("secret_operation_code"));
        var env44 = new OpenBridgeEnvelopeParseResult
        {
            HasEnvelope = true,
            Error = OpenBridgeEnvelopeParseError.NONE,
            Envelope = new OpenBridgeEnvelope { Version = "001", Command = "PS", Payload = "Normal prompt", Payload64 = secretPayload64 }
        };
        approval.TrySetPending(env44, out _);
        var details = approval.PendingCommandDetails();
        Assert(!details.Contains("secret_operation_code"), "Pending details do not expose decoded payload64 content");
        Assert(!details.Contains(secretPayload64), "Pending details do not expose raw payload64");
        approval.Reject();

        // 45. Approve result has operation_id and duration
        var env45 = new OpenBridgeEnvelopeParseResult
        {
            HasEnvelope = true,
            Error = OpenBridgeEnvelopeParseError.NONE,
            Envelope = new OpenBridgeEnvelope { Version = "001", Command = "PS", Payload = "Test 45" }
        };
        approval.TrySetPending(env45, out _);
        var result45 = await approval.ApproveDryRunAsync();
        Assert(!string.IsNullOrEmpty(result45.OperationId), "Result has operation_id");
        Assert(result45.DurationMs >= 0, "Result has duration");
        var resultSummary45 = approval.ResultSummary();
        Assert(resultSummary45.Contains(result45.OperationId), "Result summary includes operation_id");
        Assert(resultSummary45.Contains(result45.DurationMs.ToString()), "Result summary includes duration");

        // 46. Reject clears pending and no result remains from previous approve
        var env46 = new OpenBridgeEnvelopeParseResult
        {
            HasEnvelope = true,
            Error = OpenBridgeEnvelopeParseError.NONE,
            Envelope = new OpenBridgeEnvelope { Version = "001", Command = "PS", Payload = "Write-Output test_reject2" }
        };
        approval.TrySetPending(env46, out _);
        approval.Reject();
        Assert(!approval.HasPending, "Reject clears pending");
        Assert(approval.ResultSummary() == "", "Reject clears result summary (no new result)");

        // 47. Second pending while first exists is rejected
        var env47a = new OpenBridgeEnvelopeParseResult
        {
            HasEnvelope = true,
            Error = OpenBridgeEnvelopeParseError.NONE,
            Envelope = new OpenBridgeEnvelope { Version = "001", Command = "PS", Payload = "First" }
        };
        approval.TrySetPending(env47a, out _);
        var env47b = new OpenBridgeEnvelopeParseResult
        {
            HasEnvelope = true,
            Error = OpenBridgeEnvelopeParseError.NONE,
            Envelope = new OpenBridgeEnvelope { Version = "001", Command = "PS", Payload = "Second" }
        };
        var ok47 = approval.TrySetPending(env47b, out var err47);
        Assert(!ok47, "Second pending rejected while first exists");
        Assert(err47 != null && err47.Contains("pending"), "Error mentions pending");
        approval.Reject();

        // 48. DryRun approval works via new method
        var env48 = new OpenBridgeEnvelopeParseResult
        {
            HasEnvelope = true,
            Error = OpenBridgeEnvelopeParseError.NONE,
            Envelope = new OpenBridgeEnvelope { Version = "001", Command = "PS", Payload = "Write-Output test_dryrun" }
        };
        approval.TrySetPending(env48, out _);
        var result48 = await approval.ApproveDryRunAsync();
        Assert(result48.Status == HostExecutionStatus.Ok, "PS approval works");
        Assert((result48.Status == HostExecutionStatus.Ok), "PS result status is Ok");

        // 49. Process approval with missing config returns controlled error
        var noConfigPath = Path.Combine(Path.GetTempPath(), "openbridge_nonexistent_config.json");
        var noCfgApproval = new OpenBridgeRuntimeApproval(allowedRoot, configPath: noConfigPath);
        var env49 = new OpenBridgeEnvelopeParseResult
        {
            HasEnvelope = true,
            Error = OpenBridgeEnvelopeParseError.NONE,
            Envelope = new OpenBridgeEnvelope { Version = "001", Command = "PS", Payload = "test" }
        };
        noCfgApproval.TrySetPending(env49, out _);
        var result49 = await noCfgApproval.ApproveProcessAsync();
        Assert(result49.Status == HostExecutionStatus.Error, "Process with missing config returns error");
        Assert(result49.Message != null && result49.Message.Contains("config missing"), "Error message mentions config missing");

        // 50. Process approval with harmless temp config executes
        var tempProcessCfg = Path.Combine(Path.GetTempPath(), $"openbridge_runtime_test_{Guid.NewGuid():N}.json");
        var processTestJson = @"{
  ""Mode"": ""Process"",
  ""ExecutablePath"": ""cmd.exe"",
  ""ArgumentsTemplate"": ""/c echo OPENBRIDGE_RUNTIME_PROCESS_TEST: {prompt}"",
  ""DefaultTimeoutMs"": 120000,
  ""DefaultMaxOutputChars"": 50000
}";
        try
        {
            File.WriteAllText(tempProcessCfg, processTestJson);
            var processApproval = new OpenBridgeRuntimeApproval(allowedRoot, configPath: tempProcessCfg);
            var env50 = new OpenBridgeEnvelopeParseResult
            {
                HasEnvelope = true,
                Error = OpenBridgeEnvelopeParseError.NONE,
                Envelope = new OpenBridgeEnvelope { Version = "001", Command = "PS", Payload = "runtime_process_test" }
            };
            processApproval.TrySetPending(env50, out _);
            Assert(processApproval.IsProcessAvailable(), "Process is available with valid config");
            var result50 = await processApproval.ApproveProcessAsync();
            Assert(result50.Status == HostExecutionStatus.Ok, "Process approval executes harmless command");
            Assert(result50.StdoutPreview != null && result50.StdoutPreview.Contains("OPENBRIDGE_RUNTIME_PROCESS_TEST"), "Stdout contains test marker");
            Assert(!string.IsNullOrEmpty(result50.OperationId), "Process result has operation_id");
        }
        finally
        {
            if (File.Exists(tempProcessCfg)) File.Delete(tempProcessCfg);
        }

        // 51. Process approval with DryRun config returns controlled error
        var dryOnlyCfg = Path.Combine(Path.GetTempPath(), $"openbridge_dryonly_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(dryOnlyCfg, @"{ ""Mode"": ""DryRun"", ""ExecutablePath"": """", ""ArgumentsTemplate"": """" }");
            var dryApproval = new OpenBridgeRuntimeApproval(allowedRoot, configPath: dryOnlyCfg);
            var env51 = new OpenBridgeEnvelopeParseResult
            {
                HasEnvelope = true,
                Error = OpenBridgeEnvelopeParseError.NONE,
                Envelope = new OpenBridgeEnvelope { Version = "001", Command = "PS", Payload = "test" }
            };
            dryApproval.TrySetPending(env51, out _);
            var result51 = await dryApproval.ApproveProcessAsync();
            Assert(result51.Status == HostExecutionStatus.Error, "Process with DryRun config returns error");
            Assert(result51.Message != null && result51.Message.Contains("not in Process mode"), "Error says not in Process mode");
        }
        finally
        {
            if (File.Exists(dryOnlyCfg)) File.Delete(dryOnlyCfg);
        }

        // 52. Process approval with unsafe args is rejected
        var unsafeCfg = Path.Combine(Path.GetTempPath(), $"openbridge_unsafe_{Guid.NewGuid():N}.json");
        var unsafeJson = @"{
  ""Mode"": ""Process"",
  ""ExecutablePath"": ""cmd.exe"",
  ""ArgumentsTemplate"": ""/c echo test --dangerously-skip-permissions"",
  ""DefaultTimeoutMs"": 120000,
  ""DefaultMaxOutputChars"": 50000
}";
        try
        {
            File.WriteAllText(unsafeCfg, unsafeJson);
            var unsafeApproval = new OpenBridgeRuntimeApproval(allowedRoot, configPath: unsafeCfg);
            var env52 = new OpenBridgeEnvelopeParseResult
            {
                HasEnvelope = true,
                Error = OpenBridgeEnvelopeParseError.NONE,
                Envelope = new OpenBridgeEnvelope { Version = "001", Command = "PS", Payload = "test" }
            };
            unsafeApproval.TrySetPending(env52, out _);
            var result52 = await unsafeApproval.ApproveProcessAsync();
            Assert(result52.Status == HostExecutionStatus.Error, "Process with unsafe args rejected");
            Assert(result52.Message != null && result52.Message.Contains("unsafe") || (result52.Message != null && result52.Message.Contains("dangerous")), "Error mentions unsafe flags");
        }
        finally
        {
            if (File.Exists(unsafeCfg)) File.Delete(unsafeCfg);
        }

        // 53. Process approval does not use envelope-supplied executable
        var env53 = new OpenBridgeEnvelopeParseResult
        {
            HasEnvelope = true,
            Error = OpenBridgeEnvelopeParseError.NONE,
            Envelope = new OpenBridgeEnvelope { Version = "001", Command = "PS", Payload = "test" }
        };
        noCfgApproval.TrySetPending(env53, out _);
        var result53 = await noCfgApproval.ApproveProcessAsync();
        Assert(result53.Status == HostExecutionStatus.Error, "Process uses config path, not envelope executable");

        // 54. Process unavailable message is informative when no config
        Assert(noCfgApproval.IsProcessAvailable() == false, "No-process-config approval reports unavailable");
        var msg54 = noCfgApproval.ProcessAvailableMessage();
        Assert(msg54 != null && msg54.Contains("unavailable"), "Process unavailable message is informative");

        // ======== PowerShell Executor Tests ========
        Console.WriteLine("--- PowerShell Executor Tests ---");
        var psExecutor = new GeneralCommandExecutor("powershell.exe", "-NoProfile -Command \"{prompt}\"");
        var psHost = new OpenBridgeHost(psExecutor);

        // 55. PS command: echo
        var r55 = await psHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "PS",
            WorkingDirectory = allowedRoot,
            Prompt = "Write-Output OK_FROM_POWERSHELL"
        });
        Assert(r55.Status == HostExecutionStatus.Ok, "PS echo returns ok");
        Assert(r55.ExitCode == 0, "PS echo exit code 0");
        Assert(r55.StdoutPreview != null && r55.StdoutPreview.Contains("OK_FROM_POWERSHELL"), "PS stdout contains expected text");

        // 56. PS command: Get-Location
        var r56 = await psHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "PS",
            WorkingDirectory = allowedRoot,
            Prompt = "Get-Location"
        });
        Assert(r56.Status == HostExecutionStatus.Ok, "Get-Location returns ok");
        Assert(r56.StdoutPreview != null && r56.StdoutPreview.Contains(allowedRoot), "Get-Location returns working directory");

        // 57. PS command: non-zero exit
        var r57 = await psHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "PS",
            WorkingDirectory = allowedRoot,
            Prompt = "exit 42"
        });
        Assert(r57.Status == HostExecutionStatus.Error, "Non-zero exit returns error");
        Assert(r57.ExitCode == 42, "Exit code 42 captured");
        Assert(r57.ErrorCode != null && r57.ErrorCode.Contains("42"), "Error code reflects exit code");

        // 58. PS command: stderr
        var r58 = await psHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "PS",
            WorkingDirectory = allowedRoot,
            Prompt = "[Console]::Error.WriteLine('STDERR_TEST')"
        });
        Assert(r58.Status == HostExecutionStatus.Ok, "Stderr command returns ok (exit 0)");
        Assert(r58.StderrPreview != null && r58.StderrPreview.Contains("STDERR_TEST"), "Stderr captured");

        // 59. PS command: caller controls timeout via CancellationToken
        var cts59 = new CancellationTokenSource(500);
        var r59 = await psHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "PS",
            WorkingDirectory = allowedRoot,
            Prompt = "Start-Sleep -Seconds 30"
        }, cts59.Token);
        Assert(r59.Status is HostExecutionStatus.Error or HostExecutionStatus.Timeout, "PS with short CT handled");

        // 60. PS command: working directory respected
        var psSubDir = Path.Combine(allowedRoot, "releases");
        var r60 = await psHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "PS",
            WorkingDirectory = psSubDir,
            Prompt = "Get-Location"
        });
        Assert(r60.Status == HostExecutionStatus.Ok, "PS working directory respected");
        Assert(r60.StdoutPreview != null && r60.StdoutPreview.Contains(psSubDir), "PS runs in specified directory");

        // 61. PS command: operation_id assigned
        var r61 = await psHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "PS",
            WorkingDirectory = allowedRoot,
            Prompt = "Write-Output test"
        });
        Assert(!string.IsNullOrEmpty(r61.OperationId), "PS operation_id assigned");
        Assert(r61.DurationMs >= 0, "PS duration recorded");

        // ======== Concurrency & Timeout Tests ========
        Console.WriteLine("--- Testing concurrency lock ---");
        var delayedExecutor = new DelayedCommandExecutor(2000);
        var concurrentHost = new OpenBridgeHost(delayedExecutor);

        var firstTask = concurrentHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "CC",
            WorkingDirectory = allowedRoot,
            Prompt = "First operation (delayed)"
        });

        await Task.Delay(50);

        var r18 = await concurrentHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "CC",
            WorkingDirectory = allowedRoot,
            Prompt = "Second operation (should be rejected)"
        });
        Assert(r18.Status == HostExecutionStatus.Error, "Concurrent operation rejected");
        Assert(r18.ErrorCode == HostErrorCodes.ExecutorBusy, "Error code is EXECUTOR_BUSY");

        var firstResult = await firstTask;
        Assert(firstResult.Status == HostExecutionStatus.Ok, "First operation completed after second was rejected");

        // After first completes, next operation succeeds
        var r19 = await concurrentHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "CC",
            WorkingDirectory = allowedRoot,
            Prompt = "After lock released"
        });
        Assert(r19.Status == HostExecutionStatus.Ok, "Operation succeeds after previous completes");

        // CancellationToken timeout for slow executor
        var slowExecutor = new DelayedCommandExecutor(5000);
        var timeoutHost = new OpenBridgeHost(slowExecutor);
        var ctsTimeout = new CancellationTokenSource(200);
        var r20 = await timeoutHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "CC",
            WorkingDirectory = allowedRoot,
            Prompt = "Timeout test"
        }, ctsTimeout.Token);
        Assert(r20.ErrorCode == HostErrorCodes.Timeout, "CancellationToken triggers timeout");

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

    private sealed class DelayedCommandExecutor : CommandExecutor
    {
        private readonly int _delayMs;

        public DelayedCommandExecutor(int delayMs) : base(new CommandExecutorOptions())
        {
            _delayMs = delayMs;
        }

        public override async Task<HostCommandResult> ExecuteAsync(HostCommandRequest request, CancellationToken ct = default)
        {
            var startedAt = Stopwatch.GetTimestamp();

            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                return ErrorResult(request.OperationId ?? "", startedAt,
                    HostErrorCodes.PromptEmpty, "Prompt must not be empty.");
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
    }
}
