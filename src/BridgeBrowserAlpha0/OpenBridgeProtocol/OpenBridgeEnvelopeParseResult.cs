namespace BridgeBrowserAlpha0.OpenBridgeProtocol;

public class OpenBridgeEnvelopeParseResult
{
    public bool HasEnvelope { get; set; }
    public OpenBridgeEnvelopeParseError Error { get; set; } = OpenBridgeEnvelopeParseError.NONE;
    public string? ErrorMessage { get; set; }
    public OpenBridgeEnvelope? Envelope { get; set; }
}
