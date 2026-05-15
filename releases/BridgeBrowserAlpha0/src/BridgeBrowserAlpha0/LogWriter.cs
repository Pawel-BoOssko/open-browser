using System.Text.Encodings.Web;
using System.Text.Json;

namespace BridgeBrowserAlpha0;

public sealed class LogWriter : IDisposable
{
    private readonly object _gate = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    private StreamWriter? _runWriter;
    private StreamWriter? _appWriter;
    private int _seq;

    public string RunId { get; private set; } = "no-run";
    public string TabInternalId { get; } = "tab-1";
    public string? CurrentRunLogPath { get; private set; }

    public LogWriter()
    {
        AppPaths.EnsureAll();
        _appWriter = new StreamWriter(new FileStream(Path.Combine(AppPaths.Logs, "app.ndjson"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
    }

    public string StartNewRun()
    {
        lock (_gate)
        {
            _runWriter?.Dispose();
            RunId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            _seq = 0;
            CurrentRunLogPath = Path.Combine(AppPaths.Logs, $"run_{RunId}.ndjson");
            _runWriter = new StreamWriter(new FileStream(CurrentRunLogPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
            WriteLocked("app", "run_start", "ok", "New diagnostic run started", new { runLog = CurrentRunLogPath });
            return RunId;
        }
    }

    public void WriteApp(string source, string eventType, string? status = null, string? message = null, object? data = null)
    {
        lock (_gate)
        {
            WriteLine(_appWriter, MakeRecord(source, eventType, status, message, data));
        }
    }

    public void WriteRun(string source, string eventType, string? status = null, string? message = null, object? data = null)
    {
        lock (_gate)
        {
            if (_runWriter == null)
            {
                WriteLine(_appWriter, MakeRecord(source, eventType, "no_active_run", message, data));
                return;
            }
            WriteLocked(source, eventType, status, message, data);
        }
    }

    private void WriteLocked(string source, string eventType, string? status, string? message, object? data)
    {
        var rec = MakeRecord(source, eventType, status, message, data);
        WriteLine(_runWriter, rec);
        WriteLine(_appWriter, rec);
    }

    private object MakeRecord(string source, string eventType, string? status, string? message, object? data)
    {
        return new
        {
            tsUtc = DateTime.UtcNow.ToString("O"),
            seq = ++_seq,
            runId = RunId,
            tabInternalId = TabInternalId,
            source,
            eventType,
            status,
            message,
            data = Redactor.RedactObject(data)
        };
    }

    private void WriteLine(StreamWriter? writer, object record)
    {
        if (writer == null) return;
        writer.WriteLine(JsonSerializer.Serialize(record, _jsonOptions));
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _runWriter?.Dispose();
            _appWriter?.Dispose();
        }
    }
}
