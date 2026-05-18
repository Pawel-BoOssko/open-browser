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

        if (string.Equals(envelope.Command, "HST_HELP", StringComparison.OrdinalIgnoreCase)
            || string.Equals(envelope.Command, "HST_TOOLS", StringComparison.OrdinalIgnoreCase)
            || string.Equals(envelope.Command, "HST_STATUS", StringComparison.OrdinalIgnoreCase))
        {
            request = new HostCommandRequest
            {
                Command = envelope.Command.ToUpperInvariant(),
                WorkingDirectory = defaultWorkingDirectory,
                Prompt = envelope.Command.ToUpperInvariant(),
                TimeoutMs = 5000,
                MaxOutputChars = 50_000
            };
            return true;
        }

        if (!string.Equals(envelope.Command, "PS", StringComparison.OrdinalIgnoreCase))
        {
            error = $"Command not supported: {envelope.Command}. Only PS, HST_HELP, HST_TOOLS, and HST_STATUS are accepted.";
            return false;
        }

        var prompt = BuildPrompt(envelope);
        if (string.IsNullOrWhiteSpace(prompt))
        {
            error = "Prompt is empty. Provide a payload or use a RAW block in the envelope.";
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
        if (!string.IsNullOrWhiteSpace(envelope.Payload))
            return envelope.Payload;

        return null;
    }
}
