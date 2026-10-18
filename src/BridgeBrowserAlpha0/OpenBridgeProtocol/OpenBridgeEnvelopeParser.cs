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

    public static OpenBridgeEnvelopeParseResult Parse(string text, LogWriter? log = null)
    {
        var result = new OpenBridgeEnvelopeParseResult();

        if (string.IsNullOrEmpty(text))
        {
            log?.WriteRun("parser_trace", "parse_empty_input", "ok", "Input text is null or empty");
            result.HasEnvelope = false;
            return result;
        }

        log?.WriteRun("parser_trace", "parse_start", "ok",
            $"Parser starting: inputLength={text.Length}",
            new { inputLength = text.Length, sample = text.Length > 500 ? text.Substring(0, 500) + "... [TRUNCATED]" : text });

        int firstExecBegin = text.IndexOf(ExecBegin);
        if (firstExecBegin == -1)
        {
            log?.WriteRun("parser_trace", "parse_no_exec_begin", "ok",
                $"No @@OPENBRIDGE_EXEC_BEGIN@@ marker found in input of length {text.Length}");
            result.HasEnvelope = false;
            return result;
        }

        log?.WriteRun("parser_trace", "parse_exec_begin_found", "ok",
            $"@@OPENBRIDGE_EXEC_BEGIN@@ found at position {firstExecBegin}",
            new { firstExecBegin, inputLength = text.Length });

        result.HasEnvelope = true;

        int execEndIndex = text.IndexOf(ExecEnd);
        if (execEndIndex == -1)
        {
            log?.WriteRun("parser_trace", "parse_exec_end_missing", "error",
                $"@@OPENBRIDGE_EXEC_END@@ not found. beginPos={firstExecBegin}",
                new { firstExecBegin });
            result.Error = OpenBridgeEnvelopeParseError.EXEC_END_MISSING;
            return result;
        }

        log?.WriteRun("parser_trace", "parse_exec_end_found", "ok",
            $"@@OPENBRIDGE_EXEC_END@@ found at position {execEndIndex}",
            new { firstExecBegin, execEndIndex, contentLength = execEndIndex - firstExecBegin - ExecBegin.Length });

        int contentStart = firstExecBegin + ExecBegin.Length;
        string envelopeContent = text.Substring(contentStart, execEndIndex - contentStart).Trim();

        log?.WriteRun("parser_trace", "parse_envelope_content", "ok",
            $"Envelope content extracted: length={envelopeContent.Length}",
            new { length = envelopeContent.Length, content = envelopeContent.Length > 1000 ? envelopeContent.Substring(0, 1000) + "... [TRUNCATED]" : envelopeContent });

        int rawBeginCount = CountSubstrings(envelopeContent, RawBegin);
        if (rawBeginCount > 1)
        {
            log?.WriteRun("parser_trace", "parse_multiple_raw_blocks", "error", $"Multiple RAW blocks found: count={rawBeginCount}");
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
                log?.WriteRun("parser_trace", "parse_raw_end_missing", "error", "RAW_END marker missing");
                result.Error = OpenBridgeEnvelopeParseError.RAW_END_MISSING;
                return result;
            }

            int rawContentStart = rawBeginIndex + RawBegin.Length;
            string rawContent = envelopeContent.Substring(rawContentStart, rawEndIndex - rawContentStart);

            string serialized = System.Text.Json.JsonSerializer.Serialize(rawContent);

            string beforeRaw = envelopeContent.Substring(0, rawBeginIndex).TrimEnd();
            string afterRaw = envelopeContent.Substring(rawEndIndex + RawEnd.Length).TrimStart();

            // Serialize adds its own quotes. Trim the adjacent quotes from the split
            // points so we don't get double quotes around the encoded content
            if (beforeRaw.EndsWith('\"'))
                beforeRaw = beforeRaw[..^1];
            if (afterRaw.StartsWith('\"'))
                afterRaw = afterRaw[1..];

            jsonToParse = beforeRaw + serialized + afterRaw;

            log?.WriteRun("parser_trace", "parse_raw_block_processed", "ok",
                $"RAW block processed: rawContentLength={rawContent.Length}, finalJsonLength={jsonToParse.Length}",
                new { rawContentLength = rawContent.Length, finalJsonLength = jsonToParse.Length });
        }

        log?.WriteRun("parser_trace", "parse_json_input", "ok",
            $"JSON to parse: length={jsonToParse.Length}",
            new { length = jsonToParse.Length, json = jsonToParse.Length > 2000 ? jsonToParse.Substring(0, 2000) + "... [TRUNCATED]" : jsonToParse });

        try
        {
            using JsonDocument doc = JsonDocument.Parse(jsonToParse);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                log?.WriteRun("parser_trace", "parse_not_object", "error", $"Root is not object: {root.ValueKind}");
                result.Error = OpenBridgeEnvelopeParseError.JSON_PARSE_ERROR;
                result.ErrorMessage = "Root element is not a JSON object.";
                return result;
            }

            var env = new OpenBridgeEnvelope();

            if (!root.TryGetProperty("version", out JsonElement versionProp))
            {
                log?.WriteRun("parser_trace", "parse_version_missing", "error", "version field missing");
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
                log?.WriteRun("parser_trace", "parse_version_invalid_type", "error", $"Invalid version type: {versionProp.ValueKind}");
                result.Error = OpenBridgeEnvelopeParseError.JSON_PARSE_ERROR;
                result.ErrorMessage = "Invalid version type.";
                return result;
            }

            if (!root.TryGetProperty("command", out JsonElement commandProp))
            {
                log?.WriteRun("parser_trace", "parse_command_missing", "error", "command field missing");
                result.Error = OpenBridgeEnvelopeParseError.COMMAND_MISSING;
                return result;
            }

            env.Command = commandProp.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(env.Command))
            {
                log?.WriteRun("parser_trace", "parse_command_empty", "error", "command field is empty");
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

            log?.WriteRun("parser_trace", "parse_success", "ok",
                $"Envelope parsed: version={env.Version}, command={env.Command}, payloadLength={env.Payload?.Length ?? 0}, unknownFields={env.UnknownFields.Count}",
                new { version = env.Version, command = env.Command, payloadLength = env.Payload?.Length ?? 0,
                       payloadSample = (env.Payload?.Length ?? 0) > 500 ? env.Payload!.Substring(0, 500) + "... [TRUNCATED]" : env.Payload,
                       unknownFields = env.UnknownFields });

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

            log?.WriteRun("parser_trace", "parse_json_exception", "error",
                $"JSON parse exception: line={lineNum + 1}, position={ex.BytePositionInLine}, message={ex.Message}",
                new { lineNum = lineNum + 1, position = ex.BytePositionInLine, message = ex.Message, lineText });

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
