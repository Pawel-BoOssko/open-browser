using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace BridgeBrowserAlpha0.OpenBridgeProtocol;

public static class OpenBridgeEnvelopeParser
{
    private const string ExecBegin = "<<<OPENBRIDGE:EXEC:BEGIN>>>";
    private const string ExecEnd = "<<<OPENBRIDGE:EXEC:END>>>";
    private const string RawBegin = "<<<OPENBRIDGE:RAW_PAYLOAD:BEGIN>>>";
    private const string RawEnd = "<<<OPENBRIDGE:RAW_PAYLOAD:END>>>";

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

        if (text.IndexOf(ExecBegin, firstExecBegin + ExecBegin.Length) != -1)
        {
            result.Error = OpenBridgeEnvelopeParseError.MULTIPLE_ENVELOPES;
            return result;
        }

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
            
            string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawContent), Base64FormattingOptions.None);
            
            string beforeRaw = envelopeContent.Substring(0, rawBeginIndex).Trim();
            string afterRaw = envelopeContent.Substring(rawEndIndex + RawEnd.Length).Trim();
            
            jsonToParse = beforeRaw + "\"" + base64 + "\"" + afterRaw;
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

            if (root.TryGetProperty("payload64", out JsonElement payload64Prop))
            {
                env.Payload64 = payload64Prop.GetString();
            }

            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Name != "version" && prop.Name != "command" && prop.Name != "payload" && prop.Name != "payload64")
                {
                    env.UnknownFields.Add(prop.Name);
                }
            }

            result.Envelope = env;
            return result;
        }
        catch (JsonException ex)
        {
            result.Error = OpenBridgeEnvelopeParseError.JSON_PARSE_ERROR;
            result.ErrorMessage = $"Line: {ex.LineNumber}, BytePosition: {ex.BytePositionInLine}, Path: {ex.Path}, Message: {ex.Message}";
            return result;
        }
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
