using System;
using System.Collections.Generic;
using KeePassLib;

namespace KeeFetch.Batch
{
    internal sealed class BatchRunResult
    {
        public BatchRunResult(
            IEnumerable<BatchEntryOutcome> outcomes,
            bool wasCancelled,
            TimeSpan elapsed,
            string profileId,
            DiagnosticsReport diagnostics,
            RunChangeSet changes)
        {
            if (outcomes == null)
                throw new ArgumentNullException("outcomes");

            var outcomeSnapshot = new List<BatchEntryOutcome>();
            var retrySnapshot = new List<PwEntry>();

            foreach (BatchEntryOutcome outcome in outcomes)
            {
                if (outcome == null)
                    throw new ArgumentException("Batch outcomes cannot contain null items.", "outcomes");

                outcomeSnapshot.Add(outcome);
                IncrementStatusCount(outcome.Status);

                if ((outcome.Status == BatchEntryStatus.NotFound ||
                     (outcome.Status == BatchEntryStatus.RecoverableError &&
                      outcome.Recoverable)) &&
                    outcome.Entry != null &&
                    !ContainsReference(retrySnapshot, outcome.Entry))
                {
                    retrySnapshot.Add(outcome.Entry);
                }
            }

            Outcomes = outcomeSnapshot.AsReadOnly();
            RetryEntries = retrySnapshot.AsReadOnly();
            TotalCount = outcomeSnapshot.Count;
            WasCancelled = wasCancelled;
            Elapsed = elapsed;
            ProfileId = profileId;
            Diagnostics = diagnostics ?? new DiagnosticsReport(null, null, null);
            Changes = changes ?? RunChangeSet.Empty;
        }

        public IReadOnlyList<BatchEntryOutcome> Outcomes { get; private set; }
        public IReadOnlyList<PwEntry> RetryEntries { get; private set; }
        public int TotalCount { get; private set; }
        public int UpdatedCount { get; private set; }
        public int SkippedCount { get; private set; }
        public int NotFoundCount { get; private set; }
        public int RecoverableErrorCount { get; private set; }
        public int InvalidInputCount { get; private set; }
        public int CancelledCount { get; private set; }
        public bool WasCancelled { get; private set; }
        public TimeSpan Elapsed { get; private set; }
        public string ProfileId { get; private set; }
        /// <summary>Redacted diagnostics for this run, never null.</summary>
        public DiagnosticsReport Diagnostics { get; private set; }

        /// <summary>The database changes this run made, never null.</summary>
        public RunChangeSet Changes { get; private set; }

        private void IncrementStatusCount(BatchEntryStatus status)
        {
            switch (status)
            {
                case BatchEntryStatus.Updated:
                    UpdatedCount++;
                    break;
                case BatchEntryStatus.Skipped:
                    SkippedCount++;
                    break;
                case BatchEntryStatus.NotFound:
                    NotFoundCount++;
                    break;
                case BatchEntryStatus.RecoverableError:
                    RecoverableErrorCount++;
                    break;
                case BatchEntryStatus.InvalidInput:
                    InvalidInputCount++;
                    break;
                case BatchEntryStatus.Cancelled:
                    CancelledCount++;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("status");
            }
        }

        private static bool ContainsReference(IEnumerable<PwEntry> entries, PwEntry candidate)
        {
            foreach (PwEntry entry in entries)
            {
                if (object.ReferenceEquals(entry, candidate))
                    return true;
            }

            return false;
        }
    }
}
