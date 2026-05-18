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
        if (string.IsNullOrWhiteSpace(responseText)) return null;

        var result = OpenBridgeEnvelopeParser.Parse(responseText);

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
        if (env == null) return result;

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
