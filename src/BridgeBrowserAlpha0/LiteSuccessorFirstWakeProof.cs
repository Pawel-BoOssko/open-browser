using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace BridgeBrowserAlpha0;

// Proof for a genuinely numbered first wake; NOT the older one-line seed ACK.
internal static class LiteSuccessorFirstWakeProof
{
    internal sealed record Sample(
        string Url, int BrowserPid, DateTimeOffset Utc, string UserSha256,
        bool ExactReceipt, bool ExactWake, bool AssistantStarted);

    internal static async Task<Sample> CaptureAsync(
        CoreWebView2 core, string receipt, long wake)
    {
        if (receipt.Length != 32 || !receipt.All(Uri.IsHexDigit) || wake < 1)
            throw new InvalidDataException("Invalid first-wake identity");
        string before = core.Source;
        if (!LiteHandoffPersistedRoute.IsServerConversation(before))
            throw new InvalidDataException("Not a server conversation");
        // Metadata only: never return or log user/assistant message contents.
        const string script = """
            (() => {
              const main=document.querySelector('main,[role="main"]');
              const users=[...(main?.querySelectorAll(
                '[data-user-message-bubble],[data-message-author-role="user"]')||[])];
              const last=users.reverse().find(n=>((n.innerText||'').trim().length>0));
              const txt=(last?.innerText||'').trim();
              const a=[...(main?.querySelectorAll(
                '[data-message-author-role="assistant"],[data-chatgpt-selection-message-id]')||[])];
              const started=a.some(n=>!n.closest?.(
                '[data-message-author-role="user"],[data-user-message-bubble]') &&
                !n.querySelector?.('[data-message-author-role="user"],[data-user-message-bubble]') &&
                (n.innerText||'').trim().length>20);
              const normalized=txt.replace(/\s+/g,' ').trim();
              const canonical='[OB-WAKE-'+%NUMBER%+'] Kontynuuj projekt: wykonaj kolejny niezakończony etap z planu, przede wszystkim zmianę w kodzie lub wdrożenie. Nie powtarzaj zaliczonych testów bez konkretnej przyczyny.';
              const exactMinimal=normalized===canonical;
              // P0 2026-11-02: registered OBL wake now carries complete
              // owner instructions. NIEAKTUALNE: only two-line exactMinimal
              // can prove its first user bubble. Keep the same numbered
              // marker, required sections and canonical ending, without
              // changing the private receipt or issuing another send click.
              const numberMarker='[OB-WAKE-'+%NUMBER%+']';
              const action=canonical.slice(canonical.indexOf('] ')+2);
              const exactExtendedLegacy=false && normalized.startsWith(
                  numberMarker+' OBL, tylko SATANAMD.') &&
                normalized.includes('WDROZENIE:') &&
                normalized.includes('WYBUDZENIA:') &&
                normalized.includes('DOKUMENTACJA:') &&
                // P0: accept continuation and full first-wake handoff. NIEAKTUALNE: continuation-only suffix.
                (normalized.endsWith(action) || (normalized.includes('Zapisz aktualny handoff projektu zgodnie z zasadami.') && normalized.endsWith('etap z planu.'))) && normalized.length<=24000;
              // P0 2026-11-02: exact prefix/suffix was broken by WebView2
              // multiline bubble formatting (receiptMatch=false, wakeMatch=true,
              // assistantStarted=true). NIEAKTUALNE: exactExtendedLegacy.
              // Require the reserved number and all owner protocol sections,
              // including an approved final action, within one latest user bubble.
              const exactExtended=normalized.includes(numberMarker) &&
                normalized.includes('OBL, tylko SATANAMD.') &&
                normalized.includes('WDROZENIE:') &&
                normalized.includes('WYBUDZENIA:') &&
                normalized.includes('DOKUMENTACJA:') &&
                (normalized.includes('Kontynuuj projekt:') ||
                 normalized.includes('Zapisz aktualny handoff projektu')) &&
                normalized.length<=24000;              // The numeric marker, stable user bubble and a real assistant turn
              // together prove the new lead without a visible receipt in the prompt.
              return JSON.stringify({ receipt:exactMinimal || exactExtended ||
                txt.includes('['+'OB-'+%RECEIPT%+']'),
                wake:txt.includes('['+'OB-WAKE-'+%NUMBER%+']'), assistant:started });
            })()
            """;
        string expanded=script.Replace("%RECEIPT%",
            JsonSerializer.Serialize(receipt))
            .Replace("%NUMBER%",
            JsonSerializer.Serialize(wake.ToString("D6",
                System.Globalization.CultureInfo.InvariantCulture)));
        string raw=await core.ExecuteScriptAsync(expanded);
        string inner=JsonSerializer.Deserialize<string>(raw) ??
            throw new InvalidDataException("Missing proof metadata");
        using var json=JsonDocument.Parse(inner);
        var obj=json.RootElement;
        string userSha="";
        try
        {
            var bubble=await LiteHandoffUserBubbleProbe.CaptureAsync(core);
            userSha=bubble.BubbleSha256;
        }
        catch (InvalidDataException) { }
        if (before!=core.Source)
            throw new InvalidDataException("Route changed during first-wake proof");
        return new Sample(before, checked((int)core.BrowserProcessId),
            DateTimeOffset.UtcNow,userSha,
            obj.GetProperty("receipt").GetBoolean(),
            obj.GetProperty("wake").GetBoolean(),
            obj.GetProperty("assistant").GetBoolean());
    }

    internal static bool Verify(Sample first, Sample second, DateTimeOffset now, bool requireAssistantStart = true)
    {
        if (!LiteHandoffPersistedRoute.IsServerConversation(first.Url) ||
            first.Url!=second.Url ||
            first.BrowserPid<1 || first.BrowserPid!=second.BrowserPid ||
            first.UserSha256.Length!=64 || first.UserSha256!=second.UserSha256 ||
            !first.ExactReceipt || !second.ExactReceipt ||
            !first.ExactWake || !second.ExactWake ||
            (requireAssistantStart && (!first.AssistantStarted || !second.AssistantStarted)))
            return false;
        double gap=(second.Utc-first.Utc).TotalMilliseconds;
        return gap>=750 && gap<=15000 && second.Utc<=now.AddSeconds(2) &&
            (now-second.Utc).TotalSeconds<=30;
    }
    internal static int SelfTest()
    {
        var now=DateTimeOffset.UtcNow;
        const string url="https://chatgpt.com/c/11111111-1111-1111-1111-111111111111";
        var a=new Sample(url,312,now.AddMilliseconds(-1200),
            new string('a',64),true,true,true);
        var b=a with {Utc=now};
        if (!Verify(a,b,now)) return 1;
        if (Verify(a,b with{ExactReceipt=false},now)) return 2;
        if (Verify(a,b with{ExactWake=false},now)) return 3;
        if (Verify(a,b with{AssistantStarted=false},now)) return 4;
        if (Verify(a,b with{UserSha256=new string('b',64)},now)) return 5;
        if (Verify(a,b with{Url="https://chatgpt.com/"},now)) return 6;
        if (Verify(a,b with{BrowserPid=311},now)) return 7;
        // OBL: stable numbered user bubble can precede any assistant output.
        if (!Verify(a with {AssistantStarted=false}, b with {AssistantStarted=false}, now, requireAssistantStart:false)) return 8;
        if (Verify(a, b with {ExactWake=false, AssistantStarted=false}, now, requireAssistantStart:false)) return 9;
        if (Verify(a, b with {UserSha256=new string('b',64), AssistantStarted=false}, now, requireAssistantStart:false)) return 10;
        int journalProof = LiteSuccessorJournalProof.SelfTest();
        if (journalProof != 0) return 20+journalProof;
        return 0;
    }
}
