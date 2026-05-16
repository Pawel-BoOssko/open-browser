namespace BridgeBrowserAlpha0.OpenBridgeHost;

public class HostCommandResult
{
    public HostExecutionStatus Status { get; set; }
    public string OperationId { get; set; } = "";
    public long DurationMs { get; set; }
    public string? StdoutPreview { get; set; }
    public string? StderrPreview { get; set; }
    public int? ExitCode { get; set; }
    public string? ErrorCode { get; set; }
    public string? Message { get; set; }
    public bool StdoutFullTruncated { get; set; }
    public bool StderrFullTruncated { get; set; }
}
