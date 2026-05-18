using System.Security.Cryptography;

namespace BridgeBrowserAlpha0;

public static class Humanizer
{
    private static readonly string[] Slot1 =
    {
        "Dobra,", "OK,", "Okej,", "No dobra,", "No to,", "Dobrze,", "Jasne,",
        "W porządku,", "Gotowe,", "Tak,", "Jest,", "Mam,", "Zrobione,",
        "Działa,", "Wróciło,", "To teraz,"
    };

    private static readonly string[] Slot2 =
    {
        "już dostałem odpowiedź", "już mam odpowiedź", "już mam wynik",
        "mam już odpowiedź", "mam już wynik", "system już odpowiedział",
        "już system odpowiedział", "odpowiedź już wróciła", "już wróciła odpowiedź",
        "wynik już wrócił", "już wrócił wynik", "mam to z powrotem",
        "już mogę to przekazać", "już mogę to pokazać", "już mogę to wkleić",
        "wykonanie już się zakończyło"
    };

    private static readonly string[] Slot3 =
    {
        "daję", "podaję", "wklejam", "wrzucam", "przesyłam", "pokazuję",
        "przekazuję", "wstawiam", "podaję poniżej", "poniżej daję",
        "wklejam poniżej", "wrzucam poniżej", "podaję bez zmian",
        "wklejam bez zmian", "przesyłam bez zmian", "zostawiam poniżej"
    };

    private static readonly string[] Slot4 =
    {
        "wynik", "output", "rezultat", "odpowiedź", "wynik działania",
        "wynik wykonania", "odpowiedź systemu", "odpowiedź zwrotną",
        "pełną odpowiedź", "treść odpowiedzi", "to, co przyszło",
        "to, co wróciło", "to, co zwrócił system", "to, co dostałem z powrotem",
        "komunikat zwrotny", "surowy wynik"
    };

    private static string? _lastPrefix;

    public static string Wrap(string content)
    {
        string prefix;
        do
        {
            var s1 = Slot1[RandomIndex(Slot1.Length)];
            var s2 = Slot2[RandomIndex(Slot2.Length)];
            var s3 = Slot3[RandomIndex(Slot3.Length)];
            var s4 = Slot4[RandomIndex(Slot4.Length)];
            prefix = $"{s1} {s2}, {s3} {s4}:";
        } while (prefix == _lastPrefix);

        _lastPrefix = prefix;
        return $"{prefix}\n\n{content}";
    }

    private static int RandomIndex(int max)
    {
        var bytes = new byte[4];
        RandomNumberGenerator.Fill(bytes);
        return (int)(BitConverter.ToUInt32(bytes) & 0x7FFFFFFF) % max;
    }
}
