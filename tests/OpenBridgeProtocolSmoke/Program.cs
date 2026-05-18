using System;
using BridgeBrowserAlpha0.OpenBridgeProtocol;

namespace OpenBridgeProtocolSmoke;

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
        Console.WriteLine("--- Running OpenBridgeEnvelopeParser Smoke Tests ---");

        // 1. no envelope
        var r1 = OpenBridgeEnvelopeParser.Parse("Just some text without markers");
        Assert(r1.HasEnvelope == false, "No envelope");

        // 2. valid envelope with string version and command HST_HELP
        string t2 = "@@OPENBRIDGE_EXEC_BEGIN@@\n{\"version\":\"001\", \"command\":\"HST_HELP\"}\n@@OPENBRIDGE_EXEC_END@@";
        var r2 = OpenBridgeEnvelopeParser.Parse(t2);
        Assert(r2.HasEnvelope && r2.Error == OpenBridgeEnvelopeParseError.NONE && r2.Envelope?.Version == "001" && r2.Envelope?.Command == "HST_HELP", "Valid envelope with string version");

        // 3. valid envelope with numeric version 1 normalized to "001"
        string t3 = "@@OPENBRIDGE_EXEC_BEGIN@@\n{\"version\":1, \"command\":\"FS\"}\n@@OPENBRIDGE_EXEC_END@@";
        var r3 = OpenBridgeEnvelopeParser.Parse(t3);
        Assert(r3.HasEnvelope && r3.Error == OpenBridgeEnvelopeParseError.NONE && r3.Envelope?.Version == "001", "Numeric version normalization");

        // 4. valid envelope with payload
        string t4 = "@@OPENBRIDGE_EXEC_BEGIN@@\n{\"version\":\"001\", \"command\":\"SH\", \"payload\":\"dir\"}\n@@OPENBRIDGE_EXEC_END@@";
        var r4 = OpenBridgeEnvelopeParser.Parse(t4);
        Assert(r4.Error == OpenBridgeEnvelopeParseError.NONE && r4.Envelope?.Payload == "dir", "Valid envelope with payload");

        // 5. valid envelope with RAW block — content placed in payload
        string t5 = "@@OPENBRIDGE_EXEC_BEGIN@@\n{\"version\":\"001\", \"command\":\"FS\", \"payload\": @@OPENBRIDGE_RAW_BEGIN@@HelloWorld@@OPENBRIDGE_RAW_END@@}\n@@OPENBRIDGE_EXEC_END@@";
        var r5 = OpenBridgeEnvelopeParser.Parse(t5);
        Assert(r5.Error == OpenBridgeEnvelopeParseError.NONE && r5.Envelope?.Payload == "HelloWorld", "RAW block content placed in payload");

        // 6. multiple envelopes — only first is parsed, second is ignored
        string t6 = "@@OPENBRIDGE_EXEC_BEGIN@@\n{\"version\":\"001\",\"command\":\"PS\",\"payload\":\"first\"}\n@@OPENBRIDGE_EXEC_END@@\n@@OPENBRIDGE_EXEC_BEGIN@@{\"version\":\"001\"}@@OPENBRIDGE_EXEC_END@@";
        var r6 = OpenBridgeEnvelopeParser.Parse(t6);
        Assert(r6.Error == OpenBridgeEnvelopeParseError.NONE, "Multiple envelopes: no error, first is taken");
        Assert(r6.Envelope != null && r6.Envelope.Payload == "first", "Multiple envelopes: first envelope payload parsed");

        // 7. missing EXEC END -> error
        string t7 = "@@OPENBRIDGE_EXEC_BEGIN@@\n{\"version\":\"001\"}";
        var r7 = OpenBridgeEnvelopeParser.Parse(t7);
        Assert(r7.Error == OpenBridgeEnvelopeParseError.EXEC_END_MISSING, "Missing EXEC END error");

        // 8. missing RAW END -> error
        string t8 = "@@OPENBRIDGE_EXEC_BEGIN@@\n{\"version\":\"001\", \"command\":\"FS\", \"payload\": @@OPENBRIDGE_RAW_BEGIN@@Hello}\n@@OPENBRIDGE_EXEC_END@@";
        var r8 = OpenBridgeEnvelopeParser.Parse(t8);
        Assert(r8.Error == OpenBridgeEnvelopeParseError.RAW_END_MISSING, "Missing RAW END error");

        // 9. invalid JSON -> JSON_PARSE_ERROR
        string t9 = "@@OPENBRIDGE_EXEC_BEGIN@@\n{invalid}\n@@OPENBRIDGE_EXEC_END@@";
        var r9 = OpenBridgeEnvelopeParser.Parse(t9);
        Assert(r9.Error == OpenBridgeEnvelopeParseError.JSON_PARSE_ERROR, "Invalid JSON error");

        // 10. missing command -> COMMAND_MISSING
        string t10 = "@@OPENBRIDGE_EXEC_BEGIN@@\n{\"version\":\"001\"}\n@@OPENBRIDGE_EXEC_END@@";
        var r10 = OpenBridgeEnvelopeParser.Parse(t10);
        Assert(r10.Error == OpenBridgeEnvelopeParseError.COMMAND_MISSING, "Missing command error");

        // 11. unknown field -> warning
        string t11 = "@@OPENBRIDGE_EXEC_BEGIN@@\n{\"version\":\"001\", \"command\":\"FS\", \"someField\":123}\n@@OPENBRIDGE_EXEC_END@@";
        var r11 = OpenBridgeEnvelopeParser.Parse(t11);
        Assert(r11.Error == OpenBridgeEnvelopeParseError.NONE && r11.Envelope?.UnknownFields.Count == 1 && r11.Envelope.UnknownFields[0] == "someField", "Unknown fields captured");

        // 12. Old angle-bracket marker format has no meaning
        string t12 = "<<<OPENBRIDGE:EXEC:BEGIN>>>\n{\"version\":\"001\", \"command\":\"CC\"}\n<<<OPENBRIDGE:EXEC:END>>>";
        var r12 = OpenBridgeEnvelopeParser.Parse(t12);
        Assert(!r12.HasEnvelope, "Old angle-bracket markers are ignored");

        Console.WriteLine("--- Running OpenBridgeEnvelopeObserver Smoke Tests ---");
        var observer = new OpenBridgeEnvelopeObserver(null);

        // 13. observer no envelope (plain text)
        var o1 = observer.Observe("No envelope here");
        Assert(o1?.HasEnvelope == false, "Observer: no envelope");

        // 14. observer valid envelope with new markers
        var o2 = observer.Observe("@@OPENBRIDGE_EXEC_BEGIN@@\n{\"version\":\"001\", \"command\":\"FS\"}\n@@OPENBRIDGE_EXEC_END@@");
        Assert(o2 != null && o2.HasEnvelope && o2.Error == OpenBridgeEnvelopeParseError.NONE && o2.Envelope?.Command == "FS", "Observer: valid envelope with new markers");

        // 15. observer invalid envelope
        var o3 = observer.Observe("@@OPENBRIDGE_EXEC_BEGIN@@\n{invalid}\n@@OPENBRIDGE_EXEC_END@@");
        Assert(o3 != null && o3.HasEnvelope && o3.Error == OpenBridgeEnvelopeParseError.JSON_PARSE_ERROR, "Observer: JSON error");

        // 16. observer returns passive parse result, no execution
        Assert(o2 != null && o2.GetType().Name == "OpenBridgeEnvelopeParseResult", "Observer: passive result, no execution");

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
