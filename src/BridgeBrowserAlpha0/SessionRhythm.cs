using System;
using System.Security.Cryptography;

namespace BridgeBrowserAlpha0;

public class SessionRhythm
{
    private readonly DateTime _sessionStartedUtc;
    private int _cycleCount;
    private DateTime? _lastPauseEndedUtc;
    private readonly int _firstPauseMinCycles;
    private readonly int _firstPauseMinMinutes;

    public SessionRhythm()
    {
        _sessionStartedUtc = DateTime.UtcNow;
        _firstPauseMinCycles = RandomInt(5, 11);   // 5–10 cycles before first pause
        _firstPauseMinMinutes = RandomInt(10, 16); // 10–15 minutes before first pause
    }

    public void RecordCycle()
    {
        _cycleCount++;
    }

    public void RecordPauseEnded()
    {
        _lastPauseEndedUtc = DateTime.UtcNow;
    }

    public int MaybeGetPauseMs()
    {
        if (_cycleCount < _firstPauseMinCycles)
            return 0;

        var sessionMinutes = (DateTime.UtcNow - _sessionStartedUtc).TotalMinutes;
        if (sessionMinutes < _firstPauseMinMinutes)
            return 0;

        if (_lastPauseEndedUtc != null)
        {
            var sinceLastPause = (DateTime.UtcNow - _lastPauseEndedUtc.Value).TotalMinutes;
            if (sinceLastPause < 15)
                return 0;
        }

        double probability = 0.15;
        if (sessionMinutes > 120) probability = 0.35;
        else if (sessionMinutes > 60) probability = 0.25;

        var roll = RandomDouble();
        if (roll >= probability)
            return 0;

        var durationRoll = RandomInt(0, 100);
        int minutes;
        if (durationRoll < 60)
            minutes = RandomInt(2, 6);      // 2–5 minutes, 60%
        else if (durationRoll < 90)
            minutes = RandomInt(5, 16);     // 5–15 minutes, 30%
        else
            minutes = RandomInt(20, 31);    // 20–30 minutes, 10%

        return minutes * 60_000;
    }

    private static int RandomInt(int minInclusive, int maxExclusive)
    {
        return RandomNumberGenerator.GetInt32(minInclusive, maxExclusive);
    }

    private static double RandomDouble()
    {
        var bytes = new byte[4];
        RandomNumberGenerator.Fill(bytes);
        return (BitConverter.ToUInt32(bytes) & 0x7FFFFFFF) / (double)int.MaxValue;
    }
}
