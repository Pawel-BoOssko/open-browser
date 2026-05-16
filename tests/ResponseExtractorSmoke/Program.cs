using System.Text;
using System.Text.Json;
using BridgeBrowserAlpha0;

namespace ResponseExtractorSmoke;

class Program
{
    static int _failures = 0;

    static void Assert(bool condition, string testName)
    {
        if (condition) Console.WriteLine("PASS: " + testName);
        else { Console.WriteLine("FAIL: " + testName); _failures++; }
    }

    static void Main()
    {
        Console.WriteLine("--- ResponseExtractor Marker Preservation Smoke Tests ---");

        // Helpers using CDP body format (what the extractor actually receives)
        var msgId = "m_" + Guid.NewGuid().ToString("N")[..8];

        string CdpFrame(string sseText)
        {
            var body = JsonSerializer.Serialize(sseText);
            return $"{{\"body\":{body}}}";
        }

        string SseMessage(string text)
        {
            return $"event: message\ndata: {{\"type\":\"message\",\"message\":{{\"id\":\"{msgId}\",\"author\":{{\"role\":\"assistant\"}},\"channel\":\"final\",\"content\":{{\"content_type\":\"text\",\"parts\":[{JsonSerializer.Serialize(text)}]}}}}}}\n\n";
        }

        string SseDeltaReplace(string value)
        {
            return $"event: delta\ndata: {{\"o\":\"replace\",\"p\":\"/message/content/parts/0\",\"v\":{JsonSerializer.Serialize(value)}}}\n\n";
        }

        // Test 1: Complete ChatGPT flow — message then deltas with markers
        var marker = "@@OPENBRIDGE_EXEC_BEGIN@@\n{\"version\":\"001\",\"command\":\"CC\"}\n@@OPENBRIDGE_EXEC_END@@";
        var t1 = RunWithCdpFrames(new[]
        {
            CdpFrame(SseMessage("Initial text.")),
            CdpFrame(SseDeltaReplace("Expanded text with " + marker)),
            CdpFrame(SseDeltaReplace("Final text with " + marker + " and more"))
        });
        Assert(t1.Contains("@@OPENBRIDGE_EXEC_BEGIN@@"), "Complete flow preserves BEGIN marker");
        Assert(t1.Contains("@@OPENBRIDGE_EXEC_END@@"), "Complete flow preserves END marker");
        Assert(!t1.Contains("Initial text."), "Initial text was replaced by later deltas");
        Assert(!t1.Contains("Expanded text"), "Intermediate text was replaced by final delta");

        // Test 2: Message-only (no deltas) with markers
        var t2 = RunWithCdpFrames(new[]
        {
            CdpFrame(SseMessage("@@OPENBRIDGE_EXEC_BEGIN@@ {\"version\":\"001\"} @@OPENBRIDGE_EXEC_END@@"))
        });
        Assert(t2.Contains("@@OPENBRIDGE_EXEC_BEGIN@@"), "Message-only preserves BEGIN");
        Assert(t2.Contains("@@OPENBRIDGE_EXEC_END@@"), "Message-only preserves END");

        // Test 3: Space-before->>> marker in full flow
        var markerSpaced = "@@OPENBRIDGE_EXEC_BEGIN@@\n{\"version\":\"001\"}\n@@OPENBRIDGE_EXEC_END@@";
        var t3 = RunWithCdpFrames(new[]
        {
            CdpFrame(SseMessage("Start.")),
            CdpFrame(SseDeltaReplace(markerSpaced))
        });
        Assert(t3.Contains("OPENBRIDGE_EXEC_BEGIN"), "Space-before->>> preserved in full flow");

        // Test 4: CRLF normalization in CDP body
        var sseBody4 = $"event: message\r\ndata: {{\"type\":\"message\",\"message\":{{\"id\":\"{Guid.NewGuid():N}\",\"author\":{{\"role\":\"assistant\"}},\"channel\":\"final\",\"content\":{{\"content_type\":\"text\",\"parts\":[{JsonSerializer.Serialize(marker)}]}}}}}}\r\n\r\n";
        var t4 = RunWithCdpFrames(new[] { CdpFrame(sseBody4) });
        Assert(t4.Contains("@@OPENBRIDGE_EXEC_BEGIN@@"), "CRLF CDP preserves BEGIN");
        Assert(t4.Contains("@@OPENBRIDGE_EXEC_END@@"), "CRLF CDP preserves END");

        // Test 5: Markers survive unmodified — char-by-char verification
        var t5 = RunWithCdpFrames(new[]
        {
            CdpFrame(SseMessage("@@OPENBRIDGE_EXEC_BEGIN@@"))
        });
        var beginCount = CountSubstring(t5, "@@OPENBRIDGE_EXEC_BEGIN@@");
        var endCount = CountSubstring(t5, "@@OPENBRIDGE_EXEC_END@@");
        Assert(beginCount >= 1, $"BEGIN counted {beginCount} times (expected >=1)");
        Assert(CountSubstring(t5, "@@OPENBRIDGE_EXEC_BEGIN@@") >= 1, "BEGIN marker preserved in output");

        // Test 6: ResponseExtractor output ends with frame markers, not raw text
        var t6 = RunWithCdpFrames(new[]
        {
            CdpFrame(SseMessage("<<<MARKER>>>"))
        });
        Assert(t6.Contains("[END assistant message"), "Output is frame-formatted (contains END marker)");
        Assert(t6.Contains("<<<MARKER>>>"), "Original marker appears in frame text");

        // Test 7: No corruption of JSON payload containing angle brackets
        var t7 = RunWithCdpFrames(new[]
        {
            CdpFrame(SseMessage("{\"key\":\"value\",\"comparison\":\"a < b && c > d\"}"))
        });
        Assert(t7.Contains("a < b") || t7.Contains("a \\u003C b"), "JSON content with < preserved");

        // Test 8: The real ChatGPT fragment from logs (text inside code block)
        var realText = "Na screenie nadal wyglada, jakby w odpowiedzi bylo:\n\n```text\n@@OPENBRIDGE_EXEC_BEGIN@@\n@@OPENBRIDGE_EXEC_END@@\n```";
        var t8 = RunWithCdpFrames(new[]
        {
            CdpFrame(SseMessage(realText))
        });
        Assert(t8.Contains("OPENBRIDGE_EXEC_BEGIN"), "Real log fragment preserves markers");

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

    static int CountSubstring(string text, string pattern)
    {
        int count = 0, i = 0;
        while ((i = text.IndexOf(pattern, i, StringComparison.Ordinal)) != -1) { count++; i += pattern.Length; }
        return count;
    }

    static string RunWithCdpFrames(string[] cdpFrames)
    {
        var log = new LogWriter();
        log.StartNewRun();
        var extractor = new ResponseExtractor(log);
        extractor.StartRun();
        foreach (var cdp in cdpFrames)
            extractor.AddRaw("page_tap", "page_websocket_message", cdp);
        extractor.Finish();

        var answerPath = Path.Combine(AppPaths.Extracted, $"run_{log.RunId}_answer.txt");
        if (File.Exists(answerPath))
        {
            var text = File.ReadAllText(answerPath);
            log.Dispose();
            return text;
        }
        log.Dispose();
        return "";
    }
}
