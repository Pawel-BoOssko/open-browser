using System.Text;
using BridgeBrowserAlpha0.OpenBridgeProtocol;

namespace BridgeBrowserAlpha0.OpenBridgeHost;

public static class OpenBridgeHostCommandMapper
{
    public static bool TryMap(OpenBridgeEnvelope envelope, string defaultWorkingDirectory,
        int defaultTimeoutMs, int defaultMaxOutputChars, out HostCommandRequest? request, out string? error)
    {
        request = null;
        error = null;

        if (!string.Equals(envelope.Command, "CC", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(envelope.Command, "PS", StringComparison.OrdinalIgnoreCase))
        {
            error = $"Command not supported: {envelope.Command}. Supported: CC, PS.";
            return false;
        }

        var prompt = BuildPrompt(envelope);
        if (string.IsNullOrWhiteSpace(prompt))
        {
            error = "Prompt is empty. Provide a payload or payload64 in the envelope.";
            return false;
        }

        request = new HostCommandRequest
        {
            Command = envelope.Command,
            WorkingDirectory = defaultWorkingDirectory,
            Prompt = prompt,
            TimeoutMs = defaultTimeoutMs,
            MaxOutputChars = defaultMaxOutputChars
        };
        BridgeBrowserAlpha0.PipelineRawDump.Write("07_OpenBridgeHostCommandMapper.txt", prompt);
        return true;
    }

    private static string? BuildPrompt(OpenBridgeEnvelope envelope)
    {
        var hasPayload = !string.IsNullOrWhiteSpace(envelope.Payload);
        var hasPayload64 = !string.IsNullOrWhiteSpace(envelope.Payload64);

        if (hasPayload && hasPayload64)
        {
            var decoded = DecodeBase64Utf8(envelope.Payload64!);
            return string.IsNullOrEmpty(decoded)
                ? envelope.Payload
                : envelope.Payload + "\n" + decoded;
        }

        if (hasPayload) return envelope.Payload;
        if (hasPayload64) return DecodeBase64Utf8(envelope.Payload64!);

        return null;
    }

    private static string DecodeBase64Utf8(string base64)
    {
        try
        {
            var bytes = Convert.FromBase64String(base64);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return "";
        }
    }
}
