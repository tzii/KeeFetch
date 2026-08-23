using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KeeFetch.Batch;
using KeeFetch.IconSelection;
using KeePassLib;
using KeePassLib.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeeFetch.Tests
{
    [TestClass]
    public class BatchRunResultTests
    {
        [TestMethod]
        public void BatchEntryStatus_HasExactContract()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    "Updated",
                    "Skipped",
                    "NotFound",
                    "RecoverableError",
                    "InvalidInput",
                    "Cancelled"
                },
                Enum.GetNames(typeof(BatchEntryStatus)));
        }

        [TestMethod]
        public void BatchEntryOutcome_StoresValuesWithPrivateSetters()
        {
            PwEntry entry = NewEntry("Example");
            var outcome = new BatchEntryOutcome(
                entry,
                "Example",
                "https://example.com/",
                BatchEntryStatus.Updated,
                "direct-site",
                IconTier.SiteCanonical,
                false,
                false,
                1234,
                "selected");

            Assert.AreSame(entry, outcome.Entry);
            Assert.AreEqual("Example", outcome.Title);
            Assert.AreEqual("https://example.com/", outcome.ResolvedUrl);
            Assert.AreEqual(BatchEntryStatus.Updated, outcome.Status);
            Assert.AreEqual("direct-site", outcome.ProviderId);
            Assert.AreEqual(IconTier.SiteCanonical, outcome.Tier);
            Assert.IsFalse(outcome.Synthetic);
            Assert.IsFalse(outcome.Recoverable);
            Assert.AreEqual(1234L, outcome.ElapsedMilliseconds);
            Assert.AreEqual("selected", outcome.Diagnostic);

            foreach (PropertyInfo property in typeof(BatchEntryOutcome).GetProperties())
            {
                MethodInfo setter = property.GetSetMethod(true);
                Assert.IsNotNull(setter, property.Name + " should have a setter.");
                Assert.IsTrue(setter.IsPrivate, property.Name + " setter should be private.");
            }
        }

        [TestMethod]
        public void BatchRunResult_CopiesOutcomesAndCalculatesStatusCounts()
        {
            var source = new List<BatchEntryOutcome>
            {
                Outcome(NewEntry("Updated"), BatchEntryStatus.Updated),
                Outcome(NewEntry("Skipped"), BatchEntryStatus.Skipped),
                Outcome(NewEntry("Missing"), BatchEntryStatus.NotFound),
                Outcome(NewEntry("Network"), BatchEntryStatus.RecoverableError),
                Outcome(NewEntry("Invalid"), BatchEntryStatus.InvalidInput),
                Outcome(NewEntry("Cancelled"), BatchEntryStatus.Cancelled),
                Outcome(NewEntry("Updated 2"), BatchEntryStatus.Updated)
            };

            var result = new BatchRunResult(
                source,
                true,
                TimeSpan.FromSeconds(3),
                "everyday",
                "diagnostics.log",
                "diagnostics.csv");

            source.Clear();

            Assert.AreEqual(7, result.Outcomes.Count);
            Assert.AreEqual(7, result.TotalCount);
            Assert.AreEqual(2, result.UpdatedCount);
            Assert.AreEqual(1, result.SkippedCount);
            Assert.AreEqual(1, result.NotFoundCount);
            Assert.AreEqual(1, result.RecoverableErrorCount);
            Assert.AreEqual(1, result.InvalidInputCount);
            Assert.AreEqual(1, result.CancelledCount);
            Assert.IsTrue(result.WasCancelled);
            Assert.AreEqual(TimeSpan.FromSeconds(3), result.Elapsed);
            Assert.AreEqual("everyday", result.ProfileId);
            Assert.AreEqual("diagnostics.log", result.DiagnosticsLogPath);
            Assert.AreEqual("diagnostics.csv", result.DiagnosticsCsvPath);
        }

        [TestMethod]
        public void RetryEntries_ContainsOnlyNotFoundAndRecoverableErrors()
        {
            BatchEntryOutcome success = Outcome(NewEntry("Success"), BatchEntryStatus.Updated);
            BatchEntryOutcome notFound = Outcome(NewEntry("Missing"), BatchEntryStatus.NotFound);
            BatchEntryOutcome networkError = Outcome(NewEntry("Network"), BatchEntryStatus.RecoverableError);
            BatchEntryOutcome invalid = Outcome(NewEntry("Invalid"), BatchEntryStatus.InvalidInput);
            BatchEntryOutcome skipped = Outcome(NewEntry("Skipped"), BatchEntryStatus.Skipped);
            BatchEntryOutcome cancelled = Outcome(NewEntry("Cancelled"), BatchEntryStatus.Cancelled);

            var result = new BatchRunResult(
                new[] { success, notFound, networkError, invalid, skipped, cancelled },
                false,
                TimeSpan.FromSeconds(3),
                "everyday",
                "log.txt",
                "rows.csv");

            CollectionAssert.AreEqual(
                new[] { notFound.Entry, networkError.Entry },
                result.RetryEntries.ToArray());
        }

        [TestMethod]
        public void RetryEntries_DeduplicatesByEntryReferenceAndPreservesFirstEligibleOrder()
        {
            PwEntry first = NewEntry("Same title");
            PwEntry second = NewEntry("Same title");

            var result = new BatchRunResult(
                new[]
                {
                    Outcome(first, BatchEntryStatus.NotFound),
                    Outcome(first, BatchEntryStatus.RecoverableError),
                    Outcome(second, BatchEntryStatus.NotFound),
                    Outcome(first, BatchEntryStatus.Updated)
                },
                false,
                TimeSpan.Zero,
                "privacy",
                null,
                null);

            Assert.AreEqual(2, result.RetryEntries.Count);
            Assert.AreSame(first, result.RetryEntries[0]);
            Assert.AreSame(second, result.RetryEntries[1]);
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
                10,
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
