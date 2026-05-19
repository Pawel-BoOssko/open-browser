using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace BridgeBrowserAlpha0.OpenBridgeProtocol;

public static class OpenBridgeEnvelopeParser
{
    private const string ExecBegin = "@@OPENBRIDGE_EXEC_BEGIN@@";
    private const string ExecEnd = "@@OPENBRIDGE_EXEC_END@@";
    private const string RawBegin = "@@OPENBRIDGE_RAW_BEGIN@@";
    private const string RawEnd = "@@OPENBRIDGE_RAW_END@@";

    public static OpenBridgeEnvelopeParseResult Parse(string text)
    {
        var result = new OpenBridgeEnvelopeParseResult();

        if (string.IsNullOrEmpty(text))
        {
            result.HasEnvelope = false;
            return result;
        }

        int firstExecBegin = text.IndexOf(ExecBegin);
        if (firstExecBegin == -1)
        {
            result.HasEnvelope = false;
            return result;
        }

        result.HasEnvelope = true;

        int execEndIndex = text.IndexOf(ExecEnd);
        if (execEndIndex == -1)
        {
            result.Error = OpenBridgeEnvelopeParseError.EXEC_END_MISSING;
            return result;
        }

        int contentStart = firstExecBegin + ExecBegin.Length;
        string envelopeContent = text.Substring(contentStart, execEndIndex - contentStart).Trim();

        int rawBeginCount = CountSubstrings(envelopeContent, RawBegin);
        if (rawBeginCount > 1)
        {
            result.Error = OpenBridgeEnvelopeParseError.MULTIPLE_RAW_BLOCKS;
            return result;
        }

        string jsonToParse = envelopeContent;

        if (rawBeginCount == 1)
        {
            int rawBeginIndex = envelopeContent.IndexOf(RawBegin);
            int rawEndIndex = envelopeContent.IndexOf(RawEnd);

            if (rawEndIndex == -1)
            {
                result.Error = OpenBridgeEnvelopeParseError.RAW_END_MISSING;
                return result;
            }

            int rawContentStart = rawBeginIndex + RawBegin.Length;
            string rawContent = envelopeContent.Substring(rawContentStart, rawEndIndex - rawContentStart);

            string serialized = System.Text.Json.JsonSerializer.Serialize(rawContent);

            string beforeRaw = envelopeContent.Substring(0, rawBeginIndex).TrimEnd();
            string afterRaw = envelopeContent.Substring(rawEndIndex + RawEnd.Length).TrimStart();

            // Serialize adds its own quotes. Trim the adjacent quotes from the split
            // points so we don't get double quotes: "payload64":""content""
            if (beforeRaw.EndsWith('\"'))
                beforeRaw = beforeRaw[..^1];
            if (afterRaw.StartsWith('\"'))
                afterRaw = afterRaw[1..];

            jsonToParse = beforeRaw + serialized + afterRaw;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(jsonToParse);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                result.Error = OpenBridgeEnvelopeParseError.JSON_PARSE_ERROR;
                result.ErrorMessage = "Root element is not a JSON object.";
                return result;
            }

            var env = new OpenBridgeEnvelope();

            if (!root.TryGetProperty("version", out JsonElement versionProp))
            {
                result.Error = OpenBridgeEnvelopeParseError.VERSION_MISSING;
                return result;
            }

            if (versionProp.ValueKind == JsonValueKind.Number)
            {
                int versionNum = versionProp.GetInt32();
                if (versionNum == 1)
                {
                    env.Version = "001";
                }
                else
                {
                    env.Version = versionNum.ToString();
                }
            }
            else if (versionProp.ValueKind == JsonValueKind.String)
            {
                env.Version = versionProp.GetString() ?? "";
            }
            else
            {
                result.Error = OpenBridgeEnvelopeParseError.JSON_PARSE_ERROR;
                result.ErrorMessage = "Invalid version type.";
                return result;
            }

            if (!root.TryGetProperty("command", out JsonElement commandProp))
            {
                result.Error = OpenBridgeEnvelopeParseError.COMMAND_MISSING;
                return result;
            }

            env.Command = commandProp.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(env.Command))
            {
                result.Error = OpenBridgeEnvelopeParseError.COMMAND_EMPTY;
                return result;
            }

            if (root.TryGetProperty("payload", out JsonElement payloadProp))
            {
                env.Payload = payloadProp.ValueKind == JsonValueKind.String ? payloadProp.GetString() : payloadProp.GetRawText();
            }

            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Name != "version" && prop.Name != "command" && prop.Name != "payload")
                {
                    env.UnknownFields.Add(prop.Name);
                }
            }

            result.Envelope = env;
            BridgeBrowserAlpha0.PipelineRawDump.Write("06_OpenBridgeEnvelopeParser.txt", env.Payload ?? env.Command);
            return result;
        }
        catch (JsonException ex)
        {
            var lineText = "";
            var lineNum = (int)(ex.LineNumber ?? 0);
            if (!string.IsNullOrEmpty(jsonToParse))
            {
                var lines = jsonToParse.Split('\n');
                if (lineNum < lines.Length)
                    lineText = lines[lineNum].Trim();
            }

            result.Error = OpenBridgeEnvelopeParseError.JSON_PARSE_ERROR;
            result.ErrorMessage = $"Line {lineNum + 1}: {lineText}\n" +
                                  $"Position {ex.BytePositionInLine}: {ex.Message}";
            return result;
        }
    }

    public static string ErrorToUserMessage(OpenBridgeEnvelopeParseResult result)
    {
        return result.Error switch
        {
            OpenBridgeEnvelopeParseError.EXEC_END_MISSING => "[OpenBridge] Missing @@OPENBRIDGE_EXEC_END@@ marker.",
            OpenBridgeEnvelopeParseError.MULTIPLE_RAW_BLOCKS => "[OpenBridge] Multiple raw blocks in envelope.",
            OpenBridgeEnvelopeParseError.RAW_END_MISSING => "[OpenBridge] Missing @@OPENBRIDGE_RAW_END@@ marker.",
            OpenBridgeEnvelopeParseError.JSON_PARSE_ERROR => $"[OpenBridge] Invalid JSON in envelope: {result.ErrorMessage}",
            OpenBridgeEnvelopeParseError.VERSION_MISSING => "[OpenBridge] Missing version field in envelope.",
            OpenBridgeEnvelopeParseError.COMMAND_MISSING => "[OpenBridge] Missing command field in envelope.",
            OpenBridgeEnvelopeParseError.COMMAND_EMPTY => "[OpenBridge] Command field is empty.",
            _ => "[OpenBridge] Unknown envelope error."
        };
    }

    private static int CountSubstrings(string text, string sub)
    {
        int count = 0;
        int i = 0;
        while ((i = text.IndexOf(sub, i)) != -1)
        {
            count++;
            i += sub.Length;
        }
        return count;
    }
}
