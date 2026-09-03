using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using KeeFetch.Batch;
using KeeFetch.IconSelection;
using KeePassLib;
using KeePassLib.Security;
using KeePass.App.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeeFetch.Tests
{
    [TestClass]
    public class FaviconDialogResultTests
    {
        [TestMethod]
        public void BuildProgressText_UsesStableRunningWording()
        {
            var snapshot = new BatchProgressSnapshot(
                10, 4, 2, 1, 1, 0, false, false, null);

            Assert.AreEqual(
                "Processed 4 of 10 — updated 2, skipped 1, not found 1, errors 0",
                FaviconDialog.BuildProgressText(snapshot));
        }

        [TestMethod]
        public void BuildProgressText_DistinguishesCancellingAndCancelled()
        {
            var cancelling = new BatchProgressSnapshot(
                10, 4, 2, 1, 1, 0, true, false, "Waiting for active requests");
            var cancelled = new BatchProgressSnapshot(
                10, 10, 2, 1, 1, 0, true, true, null);

            Assert.AreEqual(
                "Cancelling… Processed 4 of 10 — updated 2, skipped 1, not found 1, errors 0" +
                Environment.NewLine + "Waiting for active requests",
                FaviconDialog.BuildProgressText(cancelling));
            Assert.AreEqual(
                "Cancelled after 10 of 10 — updated 2, skipped 1, not found 1, errors 0",
                FaviconDialog.BuildProgressText(cancelled));
        }

        [TestMethod]
        public void BuildBatchRunResult_PreservesInputOrderCountsAndDiagnosticPaths()
        {
            PwEntry first = NewEntry("First");
            PwEntry second = NewEntry("Second");
            PwEntry third = NewEntry("Third");
            var outcomes = new[]
            {
                Outcome(first, BatchEntryStatus.Updated),
                Outcome(second, BatchEntryStatus.NotFound),
                Outcome(third, BatchEntryStatus.RecoverableError)
            };

            BatchRunResult result = FaviconDialog.BuildBatchRunResult(
                new[] { first, second, third },
                outcomes,
                false,
                TimeSpan.FromSeconds(7),
                "everyday",
                "C:\\logs\\diagnostics.log",
                "C:\\logs\\diagnostics.csv");

            CollectionAssert.AreEqual(
                new[] { first, second, third },
                result.Outcomes.Select(outcome => outcome.Entry).ToArray());
            Assert.AreEqual(1, result.UpdatedCount);
            Assert.AreEqual(1, result.NotFoundCount);
            Assert.AreEqual(1, result.RecoverableErrorCount);
            Assert.AreEqual(TimeSpan.FromSeconds(7), result.Elapsed);
            Assert.AreEqual("everyday", result.ProfileId);
            Assert.AreEqual("C:\\logs\\diagnostics.log", result.DiagnosticsLogPath);
            Assert.AreEqual("C:\\logs\\diagnostics.csv", result.DiagnosticsCsvPath);
        }

        [TestMethod]
        public void BuildBatchRunResult_FillsUnfinishedEntriesAsCancelledInInputOrder()
        {
            PwEntry first = NewEntry("First");
            PwEntry second = NewEntry("Second");

            BatchRunResult result = FaviconDialog.BuildBatchRunResult(
                new[] { first, second },
                new[] { Outcome(first, BatchEntryStatus.Updated), null },
                true,
                TimeSpan.Zero,
                "privacy",
                null,
                null);

            Assert.AreEqual(2, result.Outcomes.Count);
            Assert.AreSame(second, result.Outcomes[1].Entry);
            Assert.AreEqual(BatchEntryStatus.Cancelled, result.Outcomes[1].Status);
            Assert.AreEqual("Cancelled before processing completed.",
                result.Outcomes[1].Diagnostic);
        }

        [TestMethod]
        public void RunAsync_ReturnsStructuredResultAndOwnsNoCompletionMessage()
        {
            MethodInfo run = typeof(FaviconDialog).GetMethod("RunAsync");
            Assert.IsNotNull(run);
            Assert.AreEqual(typeof(Task<BatchRunResult>), run.ReturnType);
            Assert.IsNull(typeof(FaviconDialog).GetMethod(
                "ShowCompletionMessage", BindingFlags.Instance | BindingFlags.NonPublic));
        }

        [TestMethod]
        public void RunAsync_EmptyInputReturnsAndStoresAnEmptyStructuredResult()
        {
            var config = new Configuration(new AceCustomConfig());
            var dialog = new FaviconDialog(null, config, new PwEntry[0]);

            BatchRunResult result = dialog.RunAsync().GetAwaiter().GetResult();

            Assert.AreSame(result, dialog.Result);
            Assert.AreEqual(0, result.TotalCount);
            Assert.AreEqual(config.FetchProfileId, result.ProfileId);
            Assert.IsFalse(result.WasCancelled);
        }

        private static BatchEntryOutcome Outcome(PwEntry entry, BatchEntryStatus status)
        {
            return new BatchEntryOutcome(
                entry,
                entry.Strings.ReadSafe(PwDefs.TitleField),
                "https://example.com/",
                status,
                "direct-site",
                IconTier.SiteCanonical,
                false,
                status == BatchEntryStatus.RecoverableError,
                25,
                status.ToString());
        }

        private static PwEntry NewEntry(string title)
        {
            var entry = new PwEntry(true, true);
            entry.Strings.Set(PwDefs.TitleField, new ProtectedString(false, title));
            return entry;
        }
    }
}
