using System.Diagnostics;
using System.Text;
using BridgeBrowserAlpha0.OpenBridgeHost;
using BridgeBrowserAlpha0.OpenBridgeHost.ClaudeCode;
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
        var host = new OpenBridgeHost(allowedRoot);

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
        var processOpts = new ClaudeCodeExecutorOptions
        {
            Mode = ClaudeCodeExecutorMode.Process,
            ExecutablePath = "cmd.exe",
            ArgumentsTemplate = "/c echo OPENBRIDGE_PROCESS_TEST: {prompt}"
        };
        var processExecutor = new ClaudeCodeExecutor(processOpts);
        var processHost = new OpenBridgeHost(allowedRoot, processExecutor);

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
        var errorProcessOpts = new ClaudeCodeExecutorOptions
        {
            Mode = ClaudeCodeExecutorMode.Process,
            ExecutablePath = "cmd.exe",
            ArgumentsTemplate = "/c \"echo failing operation && exit 42\""
        };
        var errorProcessHost = new OpenBridgeHost(allowedRoot, new ClaudeCodeExecutor(errorProcessOpts));

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
        var stderrProcessOpts = new ClaudeCodeExecutorOptions
        {
            Mode = ClaudeCodeExecutorMode.Process,
            ExecutablePath = "cmd.exe",
            ArgumentsTemplate = "/c \"echo to_stderr_test >&2\""
        };
        var stderrProcessHost = new OpenBridgeHost(allowedRoot, new ClaudeCodeExecutor(stderrProcessOpts));

        var r13 = await stderrProcessHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "CC",
            WorkingDirectory = allowedRoot,
            Prompt = "stderr_test"
        });
        Assert(r13.Status == HostExecutionStatus.Ok, "Stderr capture returns ok (exit 0)");
        Assert(r13.StderrPreview != null && r13.StderrPreview.Contains("to_stderr_test"), "Stderr was captured");

        // 14. Process timeout returns timeout
        var timeoutProcessOpts = new ClaudeCodeExecutorOptions
        {
            Mode = ClaudeCodeExecutorMode.Process,
            ExecutablePath = "powershell.exe",
            ArgumentsTemplate = "-Command \"Start-Sleep -Seconds 30\""
        };
        var timeoutProcessHost = new OpenBridgeHost(allowedRoot, new ClaudeCodeExecutor(timeoutProcessOpts));

        var r14 = await timeoutProcessHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "CC",
            WorkingDirectory = allowedRoot,
            Prompt = "timeout_test",
            TimeoutMs = 500
        });
        Assert(r14.Status == HostExecutionStatus.Timeout, "Process timeout returns timeout status");
        Assert(r14.ErrorCode == HostErrorCodes.Timeout, "Error code is TIMEOUT");

        // 15. Invalid executable returns controlled error
        var badExeOpts = new ClaudeCodeExecutorOptions
        {
            Mode = ClaudeCodeExecutorMode.Process,
            ExecutablePath = "nonexistent_executable_xyz_12345"
        };
        var badExeHost = new OpenBridgeHost(allowedRoot, new ClaudeCodeExecutor(badExeOpts));

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
        var truncProcessOpts = new ClaudeCodeExecutorOptions
        {
            Mode = ClaudeCodeExecutorMode.Process,
            ExecutablePath = "cmd.exe",
            ArgumentsTemplate = "/c echo {prompt}"
        };
        var truncProcessHost = new OpenBridgeHost(allowedRoot, new ClaudeCodeExecutor(truncProcessOpts));
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
        var defaultHost = new OpenBridgeHost(allowedRoot);
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
        var rCfg1 = ClaudeCodeExecutorOptionsLoader.TryLoad(missingPath);
        Assert(rCfg1 == null, "TryLoad returns null for missing file");

        // 19. Example config can be parsed
        var examplePath = Path.GetFullPath(Path.Combine(allowedRoot, "config", "examples", "claude-code-executor.example.json"));
        if (File.Exists(examplePath))
        {
            var rCfg2 = ClaudeCodeExecutorOptionsLoader.LoadOrThrow(examplePath);
            Assert(rCfg2.Mode == ClaudeCodeExecutorMode.DryRun, "Example config mode is DryRun");
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
            var rCfg3 = ClaudeCodeExecutorOptionsLoader.LoadOrThrow(tempConfigPath);
            Assert(rCfg3.Mode == ClaudeCodeExecutorMode.Process, "Temp config mode is Process");
            Assert(rCfg3.ExecutablePath == "cmd.exe", "Temp config has executable path");
            Assert(rCfg3.ArgumentsTemplate == "/c echo CONFIG_TEST", "Temp config has arguments template");
            Assert(rCfg3.DefaultTimeoutMs == 120000, "Temp config has custom timeout");
            Assert(rCfg3.DefaultMaxOutputChars == 10000, "Temp config has custom max output chars");

            // Also verify the loaded config works with the executor (no process invoked here — only parsing)
            var configExecutor = new ClaudeCodeExecutor(rCfg3);
            var configHost = new OpenBridgeHost(allowedRoot, configExecutor);
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
                ClaudeCodeExecutorOptionsLoader.LoadOrThrow(badJsonPath);
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
            var rCfg4 = ClaudeCodeExecutorOptionsLoader.TryLoad(safeConfigPath);
            Assert(rCfg4 != null, "Safe config loads successfully");
            Assert(rCfg4!.Mode == ClaudeCodeExecutorMode.DryRun, "Safe config stays in dry-run mode");
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
            var rCfg5 = ClaudeCodeExecutorOptionsLoader.LoadOrThrow(camelConfigPath);
            Assert(rCfg5.Mode == ClaudeCodeExecutorMode.DryRun, "camelCase mode parses correctly");
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
        var defaultOpts = new ClaudeCodeExecutorOptions();
        Assert(defaultOpts.DefaultTimeoutMs == 720_000, "Default timeout is 720000ms (12 min)");

        // 25. Example config timeout is 720000
        var exampleCfgPath = Path.GetFullPath(Path.Combine(allowedRoot, "config", "examples", "claude-code-executor.example.json"));
        if (File.Exists(exampleCfgPath))
        {
            var r25 = ClaudeCodeExecutorOptionsLoader.LoadOrThrow(exampleCfgPath);
            Assert(r25.DefaultTimeoutMs == 720_000, "Example config timeout is 720000");
        }
        else
        {
            Console.WriteLine("SKIP: Example config not found");
        }

        // 26. Short timeout tests still override to small values explicitly
        var shortOpts = new ClaudeCodeExecutorOptions { DefaultTimeoutMs = 500 };
        var shortExecutor = new ClaudeCodeExecutor(shortOpts);
        var shortHost = new OpenBridgeHost(allowedRoot, shortExecutor);
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
        var env = new OpenBridgeEnvelope { Command = "CC", Payload = "Fix the auth bug" };
        var ok27 = OpenBridgeHostCommandMapper.TryMap(env, allowedRoot, 720_000, 50_000, out var req27, out var err27);
        Assert(ok27, "CC envelope with payload maps successfully");
        Assert(err27 == null, "No error for valid CC envelope");
        Assert(req27 != null && req27.Command == "CC", "Mapped command is CC");
        Assert(req27!.Prompt == "Fix the auth bug", "Mapped prompt comes from payload");
        Assert(req27.WorkingDirectory == allowedRoot, "Mapped working directory is default");
        Assert(req27.TimeoutMs == 720_000, "Mapped timeout is default");
        Assert(req27.MaxOutputChars == 50_000, "Mapped max output is default");

        // 28. CC envelope with payload64 maps to decoded prompt
        var payload64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("Implement health check"));
        var env28 = new OpenBridgeEnvelope { Command = "CC", Payload64 = payload64 };
        var ok28 = OpenBridgeHostCommandMapper.TryMap(env28, allowedRoot, 720_000, 50_000, out var req28, out var err28);
        Assert(ok28, "CC envelope with payload64 maps successfully");
        Assert(req28!.Prompt == "Implement health check", "Mapped prompt decoded from payload64");

        // 29. CC envelope with payload and payload64 combines both
        var p64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("Write a function that returns health status"));
        var env29 = new OpenBridgeEnvelope { Command = "CC", Payload = "Implement health endpoint", Payload64 = p64 };
        var ok29 = OpenBridgeHostCommandMapper.TryMap(env29, allowedRoot, 720_000, 50_000, out var req29, out var err29);
        Assert(ok29, "CC envelope with both payloads maps");
        Assert(req29!.Prompt!.Contains("Implement health endpoint"), "Prompt contains payload prefix");
        Assert(req29.Prompt.Contains("Write a function"), "Prompt contains decoded payload64");

        // 30. Unsupported command is rejected
        var env30 = new OpenBridgeEnvelope { Command = "SH", Payload = "dir" };
        var ok30 = OpenBridgeHostCommandMapper.TryMap(env30, allowedRoot, 720_000, 50_000, out var req30, out var err30);
        Assert(!ok30, "Unsupported command is rejected");
        Assert(req30 == null, "No request for unsupported command");
        Assert(err30 != null && err30.Contains("CC"), "Error mentions only CC is accepted");

        // 31. Empty/missing prompt is rejected by mapper
        var env31 = new OpenBridgeEnvelope { Command = "CC" };
        var ok31 = OpenBridgeHostCommandMapper.TryMap(env31, allowedRoot, 720_000, 50_000, out var req31, out var err31);
        Assert(!ok31, "Missing prompt is rejected by mapper");
        Assert(err31 != null && err31.Contains("empty"), "Error mentions empty prompt");

        // 32. Mapper does not execute commands — it only produces a request object
        var env32 = new OpenBridgeEnvelope { Command = "CC", Payload = "Read file" };
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
            Envelope = new OpenBridgeEnvelope { Version = "001", Command = "CC", Payload = "Fix the auth bug" }
        };
        var ok33 = approval.TrySetPending(env33, out var err33);
        Assert(ok33, "Valid CC envelope creates pending command");
        Assert(err33 == null, "No error for valid envelope");
        Assert(approval.HasPending, "HasPending is true after set");

        // 34. Second pending is rejected while first exists
        var env34 = new OpenBridgeEnvelopeParseResult
        {
            HasEnvelope = true,
            Error = OpenBridgeEnvelopeParseError.NONE,
            Envelope = new OpenBridgeEnvelope { Version = "001", Command = "CC", Payload = "Another command" }
        };
        var ok34 = approval.TrySetPending(env34, out var err34);
        Assert(!ok34, "Second candidate is rejected while first pending");
        Assert(err34 != null && err34.Contains("pending"), "Error mentions pending");

        // 35. Approve executes DryRun and returns ok
        var result35 = await approval.ApproveAsync();
        Assert(result35.Status == HostExecutionStatus.Ok, "Approve returns Ok status");
        Assert(result35.Message != null && result35.Message.Contains("Dry-run"), "Approve executes DryRun");
        Assert(!approval.HasPending, "HasPending is false after approve");

        // 36. Result is stored in LastResult
        Assert(approval.LastResult == result35, "LastResult stores execution result");

        // 37. Approve with no pending returns error
        var result37 = await approval.ApproveAsync();
        Assert(result37.Status == HostExecutionStatus.Error, "Approve with no pending returns error");
        Assert(result37.ErrorCode == HostErrorCodes.ExecutorError, "Error code is EXECUTOR_ERROR");

        // 38. Reject clears pending
        var env38 = new OpenBridgeEnvelopeParseResult
        {
            HasEnvelope = true,
            Error = OpenBridgeEnvelopeParseError.NONE,
            Envelope = new OpenBridgeEnvelope { Version = "001", Command = "CC", Payload = "Fix tests" }
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
            Envelope = new OpenBridgeEnvelope { Version = "001", Command = "CC", Payload = "Mode test" }
        };
        approval.TrySetPending(env39, out _);
        var result39 = await approval.ApproveAsync();
        Assert(result39.Message != null && result39.Message.Contains("No Claude Code process"), "DryRun confirms no real process");
        Assert(result39.Message != null && !result39.Message.Contains("Process"), "Result does not mention Process mode");

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
        Assert(err40 != null && err40.Contains("CC"), "Error mentions only CC accepted");

        // 41. PendingSummary includes key fields
        var env41 = new OpenBridgeEnvelopeParseResult
        {
            HasEnvelope = true,
            Error = OpenBridgeEnvelopeParseError.NONE,
            Envelope = new OpenBridgeEnvelope { Version = "001", Command = "CC", Payload = "Refactor auth module" }
        };
        approval.TrySetPending(env41, out _);
        var summary = approval.PendingSummary();
        Assert(summary.Contains("CC"), "Summary contains command CC");
        Assert(summary.Contains("Refactor auth module"), "Summary contains prompt");
        Assert(summary.Contains("720000"), "Summary contains timeout");
        Assert(summary.Contains("DryRun"), "Summary contains DryRun mode");
        approval.Reject();

        // 42. Empty prompt rejected by mapper
        var env42 = new OpenBridgeEnvelopeParseResult
        {
            HasEnvelope = true,
            Error = OpenBridgeEnvelopeParseError.NONE,
            Envelope = new OpenBridgeEnvelope { Version = "001", Command = "CC" }
        };
        var ok42 = approval.TrySetPending(env42, out _);
        Assert(!ok42, "Empty prompt rejected");

        // ======== Concurrency & Timeout Tests ========
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

        // Timeout fires for slow executor
        var slowExecutor = new DelayedClaudeCodeExecutor(5000);
        var timeoutHost = new OpenBridgeHost(allowedRoot, slowExecutor);
        var r20 = await timeoutHost.ExecuteAsync(new HostCommandRequest
        {
            Command = "CC",
            WorkingDirectory = allowedRoot,
            Prompt = "Timeout test",
            TimeoutMs = 200
        });
        Assert(r20.Status == HostExecutionStatus.Timeout, "Timeout returns timeout status");
        Assert(r20.ErrorCode == HostErrorCodes.Timeout, "Error code is TIMEOUT");

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

    private sealed class DelayedClaudeCodeExecutor : ClaudeCodeExecutor
    {
        private readonly int _delayMs;

        public DelayedClaudeCodeExecutor(int delayMs) : base(new ClaudeCodeExecutorOptions())
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
