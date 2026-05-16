using System;
using BridgeBrowserAlpha0.OpenBridgeProtocol;

namespace OpenBridgeProtocolSmoke;

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

    static void Main()
    {
        Console.WriteLine("--- Running OpenBridgeEnvelopeParser Smoke Tests ---");

        // 1. no envelope
        var r1 = OpenBridgeEnvelopeParser.Parse("Just some text without markers");
        Assert(r1.HasEnvelope == false, "No envelope");

        // 2. valid envelope with string version "001" and command HST_HELP
        string t2 = "<<<OPENBRIDGE:EXEC:BEGIN>>>\n{\"version\":\"001\", \"command\":\"HST_HELP\"}\n<<<OPENBRIDGE:EXEC:END>>>";
        var r2 = OpenBridgeEnvelopeParser.Parse(t2);
        Assert(r2.HasEnvelope && r2.Error == OpenBridgeEnvelopeParseError.NONE && r2.Envelope?.Version == "001" && r2.Envelope?.Command == "HST_HELP", "Valid envelope with string version");

        // 3. valid envelope with numeric version 1 normalized to "001"
        string t3 = "<<<OPENBRIDGE:EXEC:BEGIN>>>\n{\"version\":1, \"command\":\"FS\"}\n<<<OPENBRIDGE:EXEC:END>>>";
        var r3 = OpenBridgeEnvelopeParser.Parse(t3);
        Assert(r3.HasEnvelope && r3.Error == OpenBridgeEnvelopeParseError.NONE && r3.Envelope?.Version == "001", "Numeric version normalization");

        // 4. valid envelope with payload
        string t4 = "<<<OPENBRIDGE:EXEC:BEGIN>>>\n{\"version\":\"001\", \"command\":\"SH\", \"payload\":\"dir\"}\n<<<OPENBRIDGE:EXEC:END>>>";
        var r4 = OpenBridgeEnvelopeParser.Parse(t4);
        Assert(r4.Error == OpenBridgeEnvelopeParseError.NONE && r4.Envelope?.Payload == "dir", "Valid envelope with payload");

        // 5. valid envelope with RAW block converted to payload64
        string t5 = "<<<OPENBRIDGE:EXEC:BEGIN>>>\n{\"version\":\"001\", \"command\":\"FS\", \"payload64\": <<<OPENBRIDGE:RAW_PAYLOAD:BEGIN>>>HelloWorld<<<OPENBRIDGE:RAW_PAYLOAD:END>>>}\n<<<OPENBRIDGE:EXEC:END>>>";
        var r5 = OpenBridgeEnvelopeParser.Parse(t5);
        Assert(r5.Error == OpenBridgeEnvelopeParseError.NONE && r5.Envelope?.Payload64 == "SGVsbG9Xb3JsZA==", "RAW block to base64 payload64");

        // 6. multiple envelopes -> error
        string t6 = "<<<OPENBRIDGE:EXEC:BEGIN>>>\n{}\n<<<OPENBRIDGE:EXEC:END>>>\n<<<OPENBRIDGE:EXEC:BEGIN>>>{}<<<OPENBRIDGE:EXEC:END>>>";
        var r6 = OpenBridgeEnvelopeParser.Parse(t6);
        Assert(r6.Error == OpenBridgeEnvelopeParseError.MULTIPLE_ENVELOPES, "Multiple envelopes error");

        // 7. missing EXEC END -> error
        string t7 = "<<<OPENBRIDGE:EXEC:BEGIN>>>\n{\"version\":\"001\"}";
        var r7 = OpenBridgeEnvelopeParser.Parse(t7);
        Assert(r7.Error == OpenBridgeEnvelopeParseError.EXEC_END_MISSING, "Missing EXEC END error");

        // 8. missing RAW END -> error
        string t8 = "<<<OPENBRIDGE:EXEC:BEGIN>>>\n{\"version\":\"001\", \"command\":\"FS\", \"payload64\": <<<OPENBRIDGE:RAW_PAYLOAD:BEGIN>>>Hello}\n<<<OPENBRIDGE:EXEC:END>>>";
        var r8 = OpenBridgeEnvelopeParser.Parse(t8);
        Assert(r8.Error == OpenBridgeEnvelopeParseError.RAW_END_MISSING, "Missing RAW END error");

        // 9. invalid JSON -> JSON_PARSE_ERROR
        string t9 = "<<<OPENBRIDGE:EXEC:BEGIN>>>\n{invalid}\n<<<OPENBRIDGE:EXEC:END>>>";
        var r9 = OpenBridgeEnvelopeParser.Parse(t9);
        Assert(r9.Error == OpenBridgeEnvelopeParseError.JSON_PARSE_ERROR, "Invalid JSON error");

        // 10. missing command -> COMMAND_MISSING
        string t10 = "<<<OPENBRIDGE:EXEC:BEGIN>>>\n{\"version\":\"001\"}\n<<<OPENBRIDGE:EXEC:END>>>";
        var r10 = OpenBridgeEnvelopeParser.Parse(t10);
        Assert(r10.Error == OpenBridgeEnvelopeParseError.COMMAND_MISSING, "Missing command error");

        // 11. unknown field -> warning
        string t11 = "<<<OPENBRIDGE:EXEC:BEGIN>>>\n{\"version\":\"001\", \"command\":\"FS\", \"someField\":123}\n<<<OPENBRIDGE:EXEC:END>>>";
        var r11 = OpenBridgeEnvelopeParser.Parse(t11);
        Assert(r11.Error == OpenBridgeEnvelopeParseError.NONE && r11.Envelope?.UnknownFields.Count == 1 && r11.Envelope.UnknownFields[0] == "someField", "Unknown fields captured");

        if (_failures > 0)
        {
            Console.WriteLine("Total Failures: " + _failures);
            Environment.Exit(1);
        }
        else
        {
            Console.WriteLine("All tests PASSED.");
            Environment.Exit(0);
        }
    }
}
