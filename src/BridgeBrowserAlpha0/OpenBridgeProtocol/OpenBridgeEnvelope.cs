namespace BridgeBrowserAlpha0.OpenBridgeProtocol;

public class OpenBridgeEnvelope
{
    public string Version { get; set; } = "";
    public string Command { get; set; } = "";
    public string? Payload { get; set; }
    public System.Collections.Generic.List<string> UnknownFields { get; set; } = new();
}
