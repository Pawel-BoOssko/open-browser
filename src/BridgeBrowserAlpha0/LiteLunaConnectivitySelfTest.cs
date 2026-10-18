namespace BridgeBrowserAlpha0;

/// <summary>
/// Explicit one-shot live connectivity test. Does not touch a real ChatGPT chat,
/// does not reveal Windows credential and does not retry network failures.
/// </summary>
internal static class LiteLunaConnectivitySelfTest
{
    internal static async Task<int> RunAsync()
    {
        try
        {
            bool available = LiteLunaAdvisor.HasCredentials;
            LiteEventLog.Write("luna_credentials_detected", new {
                available, sharedCredential = LiteApiCredential.IsSharedKeyAvailable(),
                model = LiteLunaAdvisor.ConfiguredModel
            });
            if (!available) return 3;
            var advisor = new LiteLunaAdvisor();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(55));
            var response = await advisor.ProposeAsync(
                "Ukończyłem pierwszy rozdział książki przygodowej. " +
                "Do napisania pozostały rozdziały drugi i trzeci. " +
                "Nie ma blokad ani pytań do użytkownika.",
                "Zadanie testowe: trzy rozdziały książki przygodowej. " +
                "Ukończony: 1. Niezrealizowane: rozdziały 2 i 3.",
                timeout.Token);
            LiteEventLog.Write("luna_live_selftest", new {
                passed = response != null,
                decision = response?.Decision ?? "unavailable",
                confidence = response?.Confidence ?? "unknown"
            });
            return response != null ? 0 : 4;
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("luna_live_selftest", new {
                passed = false, errorType = ex.GetType().Name
            });
            return 5;
        }
    }
}
