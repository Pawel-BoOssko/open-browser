using System.Text.Encodings.Web;
using System.Text.Json;

namespace BridgeBrowserAlpha0;

public sealed class LogWriter : IDisposable
{
    private readonly object _gate = new();
    private readonly JsonSerializerOptions _jsonOptions;

    private StreamWriter? _runWriter;
    private StreamWriter? _appWriter;
    private int _seq;

    public string RunId { get; private set; } = "no-run";
    public string TabInternalId { get; } = "tab-1";
    public string? CurrentRunLogPath { get; private set; }

    public LogWriter()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false
        };
        
        // Domyślnie obcinamy payloady tekstowe (stringi w JSON) do 4000 znaków, ponieważ:
        // 1. Zdarzenia takie jak cdp_response_body (szczególnie SPA jak ChatGPT) wrzucają tutaj całe ogromne stany aplikacji, co generuje dziesiątki MB logów w kilka sekund.
        // 2. Pełne surowe logowanie (raw logging) może być dodane później jako tryb explicit debug, ale nie jest defaultem dla zachowania stabilności.
        _jsonOptions.Converters.Add(new TruncatingStringConverter(4000));

        AppPaths.EnsureAll();
        _appWriter = new StreamWriter(new FileStream(Path.Combine(AppPaths.Logs, "app.ndjson"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
    }

    private class TruncatingStringConverter : System.Text.Json.Serialization.JsonConverter<string>
    {
        private readonly int _maxLength;
        public TruncatingStringConverter(int maxLength) => _maxLength = maxLength;

        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetString();

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            if (value.Length > _maxLength)
            {
                writer.WriteStartObject();
                writer.WriteString("truncated_value", value.Substring(0, _maxLength) + "...");
                writer.WriteBoolean("truncated", true);
                writer.WriteNumber("originalLength", value.Length);
                writer.WriteEndObject();
            }
            else
            {
                writer.WriteStringValue(value);
            }
        }
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
