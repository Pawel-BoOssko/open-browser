namespace BridgeBrowserAlpha0;

// Creates a durable reserved state in one CAS from Prepared, not two.
// The returned permission is process-local, short-lived and consumable once.
// Neither this class nor its caller automatically sends a message.
internal static class LiteHandoffOneShotReservation
{
    internal sealed class Permit
    {
        private int _consumed;
        private readonly string _path, _receipt, _epoch, _seedSha;
        private readonly int _revision;
        private readonly LiteSuccessorComposerLease _lease;
        internal Permit(string path, LiteHandoffSuccession.State committed,
            LiteSuccessorComposerLease lease)
        {
            _path = path;
            _receipt = committed.SeedReceipt;
            _epoch = committed.OwnerEpoch;
            _seedSha = committed.SeedTextSha256;
            _revision = committed.Revision;
            _lease = lease;
        }

        internal bool ConsumeOnce(
            LiteHandoffRouteEvidence.Sample current, int rendererPid,
            long navigationEpoch, DateTimeOffset now)
        {
            if (System.Threading.Interlocked.Exchange(ref _consumed, 1) != 0)
                return false;
            _lease.VerifyStillHeld(current, rendererPid, navigationEpoch, now);
            var persisted = LiteHandoffSuccession.Read(_path);
            return persisted.Step == LiteHandoffSuccession.Phase.SeedReserved &&
                persisted.Revision == _revision &&
                persisted.OwnerEpoch == _epoch &&
                persisted.SeedReceipt == _receipt &&
                persisted.SeedTextSha256 == _seedSha &&
                persisted.SeedStartedOnBlankRoute;
        }
    }

    internal static Permit Arm(string path, int expectedRevision,
        LiteSuccessorComposerLease liveLease,
        LiteHandoffRouteEvidence.Sample first,
        LiteHandoffRouteEvidence.Sample second,
        LiteHandoffRouteEvidence.Sample current,
        int currentRendererPid, long currentNavigationEpoch,
        string receipt, string seedSha256, DateTimeOffset now)
    {
        liveLease.VerifyStillHeld(current, currentRendererPid,
            currentNavigationEpoch, now);
        // ObserveBlankComposer verifies BOTH independent WebView2 probes.
        // It is NOT written separately, and no actionable stale stage remains.
        var committed = LiteHandoffSuccession.Advance(path, expectedRevision,
            previous =>
            {
                var observed = LiteHandoffSuccession.ObserveBlankComposer(
                    previous, first, second, now);
                var reserved = LiteHandoffSuccession.ReserveSeedWithFingerprint(
                    observed, receipt, seedSha256, now);
                // Exactly one file generation / CAS.
                return reserved with { Revision = previous.Revision + 1 };
            });
        if (committed.Step != LiteHandoffSuccession.Phase.SeedReserved ||
            !committed.SeedStartedOnBlankRoute ||
            committed.NewComposerRendererPid != currentRendererPid ||
            committed.NewComposerNavigationEpoch != currentNavigationEpoch)
            throw new InvalidDataException("Persisted seed fence failed");
        return new Permit(path, committed, liveLease);
    }
}