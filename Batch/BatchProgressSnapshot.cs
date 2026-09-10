namespace KeeFetch.Batch
{
    internal sealed class BatchProgressSnapshot
    {
        public BatchProgressSnapshot(
            int totalCount,
            int processedCount,
            int updatedCount,
            int skippedCount,
            int notFoundCount,
            int errorCount,
            bool cancellationRequested,
            bool isComplete,
            string detail)
        {
            TotalCount = totalCount;
            ProcessedCount = processedCount;
            UpdatedCount = updatedCount;
            SkippedCount = skippedCount;
            NotFoundCount = notFoundCount;
            ErrorCount = errorCount;
            CancellationRequested = cancellationRequested;
            IsComplete = isComplete;
            Detail = detail;
        }

        public int TotalCount { get; private set; }
        public int ProcessedCount { get; private set; }
        public int UpdatedCount { get; private set; }
        public int SkippedCount { get; private set; }
        public int NotFoundCount { get; private set; }
        public int ErrorCount { get; private set; }
        public bool CancellationRequested { get; private set; }
        public bool IsComplete { get; private set; }
        public string Detail { get; private set; }
    }
}
