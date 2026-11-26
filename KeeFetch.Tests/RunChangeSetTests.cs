using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using KeeFetch.Batch;
using KeeFetch.IconSelection;
using KeePass.App.Configuration;
using KeePassLib;
using KeePassLib.Keys;
using KeePassLib.Security;
using KeePassLib.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeeFetch.Tests
{
    [TestClass]
    public class RunChangeSetTests
    {
        private static readonly byte[] IconPng = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

        [TestMethod]
        public void ApplyIconUpdate_BacksUpTheEntryAndRecordsTheChange()
        {
            PwDatabase db = NewDatabase();
            PwEntry entry = AddEntry(db, "GitHub");
            FaviconDialog dialog = NewDialog(entry, false);

            ApplyIcon(dialog, db, entry, IconPng);

            RunChangeSet changes = GetChangeSet(dialog);
            Assert.AreEqual(1u, entry.History.UCount, "The previous state must be kept in history.");
            Assert.IsTrue(entry.History.GetAt(0).CustomIconUuid.Equals(PwUuid.Zero));
            Assert.IsFalse(entry.CustomIconUuid.Equals(PwUuid.Zero));
            Assert.AreEqual(1, changes.Changes.Count);
            Assert.AreSame(entry.History.GetAt(0), changes.Changes[0].HistoryBackup);
            Assert.AreEqual(1, changes.AddedIconUuids.Count);
            Assert.AreSame(db, changes.Database);
        }

        [TestMethod]
        public void Undo_RestoresIconsDropsBackupsAndRemovesUnusedAddedIcons()
        {
            PwDatabase db = NewDatabase();
            PwEntry first = AddEntry(db, "First");
            PwEntry second = AddEntry(db, "Second");
            FaviconDialog dialog = NewDialog(first, false);

            ApplyIcon(dialog, db, first, IconPng);
            ApplyIcon(dialog, db, second, IconPng);
            RunChangeSet changes = GetChangeSet(dialog);

            int restored = changes.Undo(db);

            Assert.AreEqual(2, restored);
            Assert.IsTrue(first.CustomIconUuid.Equals(PwUuid.Zero));
            Assert.IsTrue(second.CustomIconUuid.Equals(PwUuid.Zero));
            Assert.AreEqual(0u, first.History.UCount);
            Assert.AreEqual(0u, second.History.UCount);
            Assert.AreEqual(0, db.CustomIcons.Count);
            Assert.IsFalse(changes.HasChanges);
        }

        [TestMethod]
        public void Undo_KeepsAddedIconsStillUsedAndEntriesChangedSinceTheRun()
        {
            PwDatabase db = NewDatabase();
            PwEntry changedLater = AddEntry(db, "Changed later");
            PwEntry reverted = AddEntry(db, "Reverted");
            PwEntry other = AddEntry(db, "Other");
            FaviconDialog dialog = NewDialog(changedLater, false);

            ApplyIcon(dialog, db, changedLater, IconPng);
            ApplyIcon(dialog, db, reverted, IconPng);
            PwUuid iconUuid = reverted.CustomIconUuid;
            var laterIcon = new PwUuid(true);
            changedLater.CustomIconUuid = laterIcon;
            other.CustomIconUuid = iconUuid;

            int restored = GetChangeSet(dialog).Undo(db);

            Assert.AreEqual(1, restored);
            Assert.IsTrue(changedLater.CustomIconUuid.Equals(laterIcon));
            Assert.IsTrue(reverted.CustomIconUuid.Equals(PwUuid.Zero));
            Assert.AreEqual(0, db.GetCustomIconIndex(iconUuid), "An icon another entry uses must stay.");
        }

        [TestMethod]
        public void Undo_LeavesNoDeletionRecordForTheIconsItRemoves()
        {
            PwDatabase db = NewDatabase();
            PwEntry entry = AddEntry(db, "GitHub");
            FaviconDialog dialog = NewDialog(entry, false);
            ApplyIcon(dialog, db, entry, IconPng);
            var earlierDeletion = new PwDeletedObject(new PwUuid(true), DateTime.UtcNow);
            db.DeletedObjects.Add(earlierDeletion);

            GetChangeSet(dialog).Undo(db);

            // Icon ids are content hashes, so a later run that downloads the
            // same image recreates the same id. A deletion record would make
            // the next synchronization strip that icon again.
            Assert.AreEqual(0, db.CustomIcons.Count);
            Assert.AreEqual(1u, db.DeletedObjects.UCount, "Only unrelated deletion records stay.");
            Assert.AreSame(earlierDeletion, db.DeletedObjects.GetAt(0));
        }

        [TestMethod]
        public void RedownloadedIcon_SurvivesSynchronizingWithACopyThatDeletedIt()
        {
            PwDatabase db = NewDatabase();
            PwEntry entry = AddEntry(db, "GitHub");
            FaviconDialog dialog = NewDialog(entry, false);
            ApplyIcon(dialog, db, entry, IconPng);
            PwUuid iconUuid = entry.CustomIconUuid;

            // Another copy removed the same image earlier, for example with
            // KeePass's "delete unused icons".
            PwDatabase otherCopy = NewDatabase();
            otherCopy.DeletedObjects.Add(new PwDeletedObject(iconUuid, DateTime.UtcNow.AddMinutes(-5)));

            db.MergeIn(otherCopy, PwMergeMethod.Synchronize);

            Assert.IsTrue(db.GetCustomIconIndex(iconUuid) >= 0, "The new icon must win over the older deletion.");
            Assert.IsTrue(entry.CustomIconUuid.Equals(iconUuid));
        }

        [TestMethod]
        public void ProcessEntry_SkipsExistingCustomIconsOnlyWhenTheRunAsksForIt()
        {
            PwEntry withIcon = new PwEntry(true, true);
            withIcon.CustomIconUuid = new PwUuid(true);

            BatchEntryOutcome skipping = ProcessWithoutNetwork(withIcon, true);
            BatchEntryOutcome explicitRun = ProcessWithoutNetwork(withIcon, false);

            Assert.AreEqual(BatchEntryStatus.Skipped, skipping.Status);
            // Without the skip the entry is processed; it has no URL, so it
            // stops before any network request.
            Assert.AreEqual(BatchEntryStatus.InvalidInput, explicitRun.Status);
            Assert.AreEqual("The entry has no web address.", explicitRun.Diagnostic);
        }

        [TestMethod]
        public void LegacyConstructor_FollowsTheSkipSetting()
        {
            var config = new Configuration(new AceCustomConfig());
            Assert.IsTrue(config.SkipExistingIcons, "Skipping existing icons is the default.");

            var dialog = new FaviconDialog(null, config, new PwEntry[0]);

            FieldInfo field = typeof(FaviconDialog).GetField("skipExistingCustomIcons",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            Assert.IsTrue((bool)field.GetValue(dialog));
        }

        private static BatchEntryOutcome ProcessWithoutNetwork(PwEntry entry, bool skipExisting)
        {
            var config = new Configuration(new AceCustomConfig());
            config.UseTitleField = false;
            var dialog = new FaviconDialog(null, config, new[] { entry }, skipExisting);
            Invoke(dialog, "ResetRunState");
            MethodInfo process = typeof(FaviconDialog).GetMethod("ProcessEntryAsync",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(process);
            ((Task)process.Invoke(dialog, new object[] { 0, entry, null, CancellationToken.None }))
                .GetAwaiter().GetResult();

            BatchEntryOutcome[] outcomes = (BatchEntryOutcome[])typeof(FaviconDialog)
                .GetField("outcomes", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(dialog);
            Assert.IsNotNull(outcomes[0]);
            return outcomes[0];
        }

        private static PwDatabase NewDatabase()
        {
            var db = new PwDatabase();
            db.New(new IOConnectionInfo(), new CompositeKey());
            return db;
        }

        private static PwEntry AddEntry(PwDatabase db, string title)
        {
            var entry = new PwEntry(true, true);
            entry.Strings.Set(PwDefs.TitleField, new ProtectedString(false, title));
            db.RootGroup.AddEntry(entry, true);
            return entry;
        }

        private static FaviconDialog NewDialog(PwEntry entry, bool skipExisting)
        {
            var dialog = new FaviconDialog(null, new Configuration(new AceCustomConfig()),
                new[] { entry }, skipExisting);
            Invoke(dialog, "ResetRunState");
            return dialog;
        }

        private static void ApplyIcon(FaviconDialog dialog, PwDatabase db, PwEntry entry, byte[] png)
        {
            Type pendingType = typeof(FaviconDialog).GetNestedType("PendingIconUpdate",
                BindingFlags.NonPublic);
            Assert.IsNotNull(pendingType);
            object pending = Activator.CreateInstance(pendingType,
                new object[]
                {
                    0, entry, Util.HashData(png), png, "example.com", IconTier.SiteCanonical,
                    false, "https://example.com/", "direct-site", 10L, string.Empty
                });

            MethodInfo apply = typeof(FaviconDialog).GetMethod("ApplyIconUpdate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(apply);
            apply.Invoke(dialog, new[] { db, pending });
        }

        private static RunChangeSet GetChangeSet(FaviconDialog dialog)
        {
            return (RunChangeSet)typeof(FaviconDialog)
                .GetField("changeSet", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(dialog);
        }

        private static void Invoke(FaviconDialog dialog, string methodName)
        {
            MethodInfo method = typeof(FaviconDialog).GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Missing " + methodName + ".");
            method.Invoke(dialog, null);
        }
    }
}
