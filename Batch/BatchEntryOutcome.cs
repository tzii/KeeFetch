using KeeFetch.IconSelection;
using KeePassLib;

namespace KeeFetch.Batch
{
    internal enum BatchEntryStatus
    {
        Updated,
        Skipped,
        NotFound,
        RecoverableError,
        InvalidInput,
        Cancelled
    }

    internal sealed class BatchEntryOutcome
    {
        public BatchEntryOutcome(
            PwEntry entry,
            string title,
            string resolvedUrl,
            BatchEntryStatus status,
            string providerId,
            IconTier tier,
            bool synthetic,
            bool recoverable,
            long elapsedMilliseconds,
            string diagnostic)
        {
            Entry = entry;
            Title = title;
            ResolvedUrl = resolvedUrl;
            Status = status;
            ProviderId = providerId;
            Tier = tier;
            Synthetic = synthetic;
            Recoverable = recoverable;
            ElapsedMilliseconds = elapsedMilliseconds;
            Diagnostic = diagnostic;
        }

        public PwEntry Entry { get; private set; }
        public string Title { get; private set; }
        public string ResolvedUrl { get; private set; }
        public BatchEntryStatus Status { get; private set; }
        public string ProviderId { get; private set; }
        public IconTier Tier { get; private set; }
        public bool Synthetic { get; private set; }
        public bool Recoverable { get; private set; }
        public long ElapsedMilliseconds { get; private set; }
        public string Diagnostic { get; private set; }
    }
}
