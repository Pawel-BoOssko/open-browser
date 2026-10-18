using System;
using System.Linq;

namespace BridgeBrowserAlpha0.OpenBridgeProtocol;

public sealed class OpenBridgeEnvelopeObserver
{
    private readonly LogWriter? _log;

    public OpenBridgeEnvelopeObserver(LogWriter? log)
    {
        _log = log;
    }

    public OpenBridgeEnvelopeParseResult? Observe(string responseText)
    {
        BridgeBrowserAlpha0.PipelineRawDump.Write("05_OpenBridgeEnvelopeObserver.txt", responseText);
        if (string.IsNullOrWhiteSpace(responseText))
        {
            _log?.WriteRun("observer_trace", "observe_empty", "ok", "Response text is null or white space. Skipping parsing.");
            return null;
        }

        _log?.WriteRun("observer_trace", "observe_start", "ok", 
            $"Observe starting: length={responseText.Length}",
            new { length = responseText.Length, sample = responseText.Length > 200 ? responseText.Substring(0, 200) + "... [TRUNCATED]" : responseText });

        var result = OpenBridgeEnvelopeParser.Parse(responseText, _log);

        _log?.WriteRun("observer_trace", "observe_parser_result", "ok", 
            $"Parser result: hasEnvelope={result.HasEnvelope}, errorCode={result.Error}, errorMessage={result.ErrorMessage ?? "none"}",
            new { hasEnvelope = result.HasEnvelope, errorCode = result.Error.ToString(), errorMessage = result.ErrorMessage });

        if (!result.HasEnvelope)
        {
            return result;
        }

        if (result.Error != OpenBridgeEnvelopeParseError.NONE)
        {
            _log?.WriteRun("openbridge", "openbridge_envelope_parse_error", "error", "Failed to parse OpenBridge envelope", new
            {
                errorCode = result.Error.ToString(),
                message = result.ErrorMessage
            });
            return result;
        }

        var env = result.Envelope;
        if (env == null)
        {
            _log?.WriteRun("observer_trace", "observe_envelope_null", "warning", "Envelope is null despite HasEnvelope=True and Error=NONE!");
            return result;
        }

        _log?.WriteRun("openbridge", "openbridge_envelope_detected", "ok", "OpenBridge envelope detected", new
        {
            version = env.Version,
            command = env.Command,
            hasPayload = !string.IsNullOrEmpty(env.Payload),
            unknownFields = env.UnknownFields,
            warningCount = env.UnknownFields.Count,
            payloadLength = env.Payload?.Length ?? 0
        });

        return result;
    }
}
