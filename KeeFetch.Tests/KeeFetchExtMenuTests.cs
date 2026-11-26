using System;
using System.Linq;
using System.Windows.Forms;
using KeeFetch.Batch;
using KeeFetch.IconSelection;
using KeePass.App.Configuration;
using KeePass.Plugins;
using KeePassLib;
using KeePassLib.Keys;
using KeePassLib.Security;
using KeePassLib.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeeFetch.Tests
{
    [TestClass]
    public class KeeFetchExtMenuTests
    {
        [TestMethod]
        public void GetMenuItem_MainMenu_SubcommandsHaveIcons()
        {
            var plugin = new KeeFetchExt();

            ToolStripMenuItem menu = plugin.GetMenuItem(PluginMenuType.Main);

            Assert.IsNotNull(menu);
            ToolStripMenuItem[] commands = menu.DropDownItems
                .OfType<ToolStripMenuItem>()
                .ToArray();

            Assert.AreEqual(2, commands.Length);
            Assert.IsTrue(commands.All(command => command.Image != null));
        }

        [TestMethod]
        public void CollectEntries_LeavesOutTheRecycleBinUnlessItWasChosen()
        {
            var db = new PwDatabase();
            db.New(new IOConnectionInfo(), new CompositeKey());
            var bin = new PwGroup(true, true, "Recycle Bin", PwIcon.TrashBin);
            db.RootGroup.AddGroup(bin, true);
            db.RecycleBinUuid = bin.Uuid;
            PwEntry kept = AddEntry(db.RootGroup, "Kept");
            PwEntry deleted = AddEntry(bin, "Deleted");

            PwEntry[] fromRoot = KeeFetchExt.CollectEntries(db, db.RootGroup);
            PwEntry[] fromBin = KeeFetchExt.CollectEntries(db, bin);

            CollectionAssert.AreEqual(new[] { kept }, fromRoot);
            CollectionAssert.AreEqual(new[] { deleted }, fromBin);
        }

        [TestMethod]
        public void BulkConfirmation_NamesProfileContactsAndSkippedIcons()
        {
            var config = new Configuration(new AceCustomConfig());
            config.FetchProfileId = "everyday";
            config.AutoSave = false;
            PwEntry[] entries = { new PwEntry(true, true), new PwEntry(true, true), new PwEntry(true, true) };
            entries[0].CustomIconUuid = new PwUuid(true);

            string skipping;
            string replacing;
            Assert.IsTrue(KeeFetchExt.TryBuildBulkConfirmation(config, entries, true, "in this database", out skipping));
            Assert.IsTrue(KeeFetchExt.TryBuildBulkConfirmation(config, entries, false, "in this database", out replacing));

            StringAssert.StartsWith(skipping, "Download favicons for 2 of the 3 entries in this database?");
            StringAssert.Contains(skipping, "Profile: Balanced");
            StringAssert.Contains(skipping, "Contacts: the entry's own website, then Google and Twenty Icons");
            StringAssert.Contains(skipping, "1 entry that already has a custom icon is skipped.");
            StringAssert.StartsWith(replacing, "Download favicons for 3 entries in this database?");
            StringAssert.Contains(replacing, "1 entry already has a custom icon; KeeFetch replaces one only when it finds a new icon.");
        }

        [TestMethod]
        public void BulkConfirmation_ExplainsWhenEveryEntryWouldBeSkipped()
        {
            var config = new Configuration(new AceCustomConfig());
            var entry = new PwEntry(true, true);
            entry.CustomIconUuid = new PwUuid(true);

            string text;
            Assert.IsFalse(KeeFetchExt.TryBuildBulkConfirmation(config, new[] { entry }, true, "in 'Work'", out text));
            StringAssert.Contains(text, "already have a custom icon");
            StringAssert.Contains(text, "Settings > Downloads");
        }

        [TestMethod]
        public void CleanSingleUpdate_SkipsTheCompletionDialog()
        {
            var updated = Outcome(BatchEntryStatus.Updated);
            var missing = Outcome(BatchEntryStatus.NotFound);

            Assert.IsTrue(KeeFetchExt.IsCleanSingleUpdate(Result(false, updated)));
            Assert.IsFalse(KeeFetchExt.IsCleanSingleUpdate(Result(false, missing)));
            Assert.IsFalse(KeeFetchExt.IsCleanSingleUpdate(Result(true, updated)));
            Assert.IsFalse(KeeFetchExt.IsCleanSingleUpdate(Result(false, updated, Outcome(BatchEntryStatus.Updated))));
        }

        private static PwEntry AddEntry(PwGroup group, string title)
        {
            var entry = new PwEntry(true, true);
            entry.Strings.Set(PwDefs.TitleField, new ProtectedString(false, title));
            group.AddEntry(entry, true);
            return entry;
        }

        private static BatchRunResult Result(bool cancelled, params BatchEntryOutcome[] outcomes)
        {
            return new BatchRunResult(outcomes, cancelled, TimeSpan.FromSeconds(1), "everyday", null, null);
        }

        private static BatchEntryOutcome Outcome(BatchEntryStatus status)
        {
            return new BatchEntryOutcome(new PwEntry(true, true), "Entry", "https://example.com/", status,
                "direct-site", IconTier.SiteCanonical, false, false, 10, string.Empty);
        }
    }
}
