using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace BridgeBrowserAlpha0;

public sealed class DiagnosticsController
{
    private readonly LogWriter _log;
    private readonly BridgeBrowserModuleManager _moduleManager;
    private readonly Action<string> _setDiagnostics;
    private readonly Func<bool> _isWebViewReady;

    public DiagnosticsController(
        LogWriter log,
        BridgeBrowserModuleManager moduleManager,
        Action<string> setDiagnostics,
        Func<bool> isWebViewReady)
    {
        _log = log;
        _moduleManager = moduleManager;
        _setDiagnostics = setDiagnostics;
        _isWebViewReady = isWebViewReady;
    }

    public async Task RefreshAsync(bool writeLog)
    {
        if (!_isWebViewReady()) return;

        try
        {
            var statusJson = await _moduleManager.GetConversationTrimmerStatusJsonAsync();
            if (writeLog)
            {
                _log.WriteRun("modules", "module_status", "ok", "Conversation trimmer status", JsonSerializer.Deserialize<object>(statusJson));
            }

            _setDiagnostics(statusJson);
        }
        catch (Exception ex)
        {
            _setDiagnostics("trimmer status error: " + ex.Message);
        }
    }
}
