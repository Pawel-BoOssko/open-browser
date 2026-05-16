namespace BridgeBrowserAlpha0.OpenBridgeProtocol;

public enum OpenBridgeEnvelopeParseError
{
    NONE,
    MULTIPLE_ENVELOPES,
    EXEC_END_MISSING,
    MULTIPLE_RAW_BLOCKS,
    RAW_END_MISSING,
    JSON_PARSE_ERROR,
    VERSION_MISSING,
    COMMAND_MISSING,
    COMMAND_EMPTY
}
