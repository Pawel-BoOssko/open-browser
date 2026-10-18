using System.Text.Json;

namespace BridgeBrowserAlpha0;

/// <summary>
/// One-shot API scenario using a summary of the owner's authorized test novel.
/// Never reads browser cookies, never submits text to a ChatGPT conversation.
/// </summary>
internal static class LiteLunaNovelScenario
{
    internal static async Task<int> RunAsync()
    {
        try
        {
            var adviser = new LiteLunaAdvisor();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(55));
            var advice = await adviser.ProposeAsync(
                "W testowej książce przygodowej ukończono siedem rozdziałów. " +
                "Ostatni nosi tytuł 'Czarny kompas'; na końcu są wątki do rozwinięcia. " +
                "Brak widocznej awarii i brak aktywnego generowania.",
                "Operator wyraził zgodę na eksperyment z książką przygodową, " +
                "w tym napisanie kolejnego nowego rozdziału bez przerabiania starszych. " +
                "Jedyny proponowany następny cel: jeden rozdział ósmy " +
                "kontynuujący fabułę i bohaterów. " +
                "Nie twórz kolejnych tomów ani nie powtarzaj wcześniejszych rozdziałów.",
                timeout.Token);
            if (advice is null || !LiteLunaAdvisor.Validate(advice))
                return 3;
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OpenBrowser", "ChatGPT-Lite", "diagnostics",
                "luna-adventure-scenario-advice.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, JsonSerializer.Serialize(new {
                    generatedUtc = DateTimeOffset.UtcNow,
                    scenario = "one_test_novel_chapter_only",
                    advice.Decision, advice.Message, advice.Reason, advice.Confidence
                }, new JsonSerializerOptions { WriteIndented = true }),
                    new System.Text.UTF8Encoding(false));
                File.Move(temp, path, true);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
            LiteEventLog.Write("luna_adventure_scenario", new {
                decision = advice.Decision, confidence = advice.Confidence
            });
            return 0;
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("luna_adventure_scenario_failed", new {
                errorType = ex.GetType().Name
            });
            return 4;
        }
    }
}
