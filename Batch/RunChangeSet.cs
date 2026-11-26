using System;
using System.Collections.Generic;
using KeePassLib;
using KeePassLib.Delegates;

namespace KeeFetch.Batch
{
    /// <summary>One entry whose custom icon a run replaced.</summary>
    internal sealed class AppliedIconChange
    {
        public AppliedIconChange(PwEntry entry, PwUuid previousIconUuid, PwUuid newIconUuid,
            PwEntry historyBackup)
        {
            if (entry == null)
                throw new ArgumentNullException("entry");

            Entry = entry;
            PreviousIconUuid = previousIconUuid ?? PwUuid.Zero;
            NewIconUuid = newIconUuid ?? PwUuid.Zero;
            HistoryBackup = historyBackup;
        }

        public PwEntry Entry { get; private set; }
        public PwUuid PreviousIconUuid { get; private set; }
        public PwUuid NewIconUuid { get; private set; }

        /// <summary>The history item the run created before the change, or null when history is disabled.</summary>
        public PwEntry HistoryBackup { get; private set; }
    }

    /// <summary>
    /// Everything a run changed in the database, so the run can be undone
    /// from the completion dialog.
    /// </summary>
    internal sealed class RunChangeSet
    {
        private readonly List<AppliedIconChange> changes = new List<AppliedIconChange>();
        private readonly List<PwUuid> addedIconUuids = new List<PwUuid>();

        public static readonly RunChangeSet Empty = new RunChangeSet();

        /// <summary>The database the changes were made in, or null when nothing changed.</summary>
        public PwDatabase Database { get; internal set; }

        public IList<AppliedIconChange> Changes
        {
            get { return changes.AsReadOnly(); }
        }

        public IList<PwUuid> AddedIconUuids
        {
            get { return addedIconUuids.AsReadOnly(); }
        }

        public bool HasChanges
        {
            get { return changes.Count > 0 || addedIconUuids.Count > 0; }
        }

        internal void RecordChange(AppliedIconChange change)
        {
            if (change == null)
                throw new ArgumentNullException("change");
            if (object.ReferenceEquals(this, Empty))
                throw new InvalidOperationException("The empty change set is read-only.");

            changes.Add(change);
        }

        internal void RecordAddedIcon(PwUuid iconUuid)
        {
            if (iconUuid == null)
                throw new ArgumentNullException("iconUuid");
            if (object.ReferenceEquals(this, Empty))
                throw new InvalidOperationException("The empty change set is read-only.");

            addedIconUuids.Add(iconUuid);
        }

        /// <summary>
        /// Restores each changed entry's previous icon, drops the history item
        /// the run created, and removes icons the run added that nothing uses
        /// any more. Entries whose icon was changed again since the run are
        /// left alone. Returns the number of entries restored.
        /// </summary>
        public int Undo(PwDatabase db)
        {
            if (db == null)
                throw new ArgumentNullException("db");

            int restored = 0;
            for (int i = changes.Count - 1; i >= 0; i--)
            {
                AppliedIconChange change = changes[i];
                PwEntry entry = change.Entry;
                if (!entry.CustomIconUuid.Equals(change.NewIconUuid))
                    continue;

                entry.CustomIconUuid = change.PreviousIconUuid;
                if (change.HistoryBackup != null)
                    entry.History.Remove(change.HistoryBackup);

                // A fresh modification time keeps the restored icon winning
                // when this database is later synchronized with a copy that
                // already has the downloaded icon.
                entry.Touch(true, false);
                restored++;
            }

            var unused = new List<PwUuid>();
            foreach (PwUuid iconUuid in addedIconUuids)
            {
                if (db.GetCustomIconIndex(iconUuid) >= 0 && !IsCustomIconInUse(db, iconUuid))
                    unused.Add(iconUuid);
            }

            if (unused.Count > 0)
            {
                uint firstNewRecord = db.DeletedObjects.UCount;
                db.DeleteCustomIcons(unused);
                RemoveDeletionRecords(db, unused, firstNewRecord);
            }

            changes.Clear();
            addedIconUuids.Clear();
            return restored;
        }

        /// <summary>
        /// DeleteCustomIcons records each icon as deleted, so the next
        /// synchronization removes it from other copies too. Icon ids are
        /// content hashes: a later run that downloads the same image recreates
        /// the same id, and that record would strip it again at the next
        /// synchronization. These icons were added by the run being undone, so
        /// no record is kept. At worst an unused icon returns from a copy that
        /// was synchronized before the undo.
        /// </summary>
        private static void RemoveDeletionRecords(PwDatabase db, List<PwUuid> iconUuids,
            uint firstNewRecord)
        {
            for (uint i = db.DeletedObjects.UCount; i > firstNewRecord; i--)
            {
                if (iconUuids.Contains(db.DeletedObjects.GetAt(i - 1).Uuid))
                    db.DeletedObjects.RemoveAt(i - 1);
            }
        }

        internal static bool IsCustomIconInUse(PwDatabase db, PwUuid iconUuid)
        {
            if (db == null || db.RootGroup == null)
                return false;

            bool inUse = false;
            GroupHandler groupHandler = delegate(PwGroup group)
            {
                if (group.CustomIconUuid.Equals(iconUuid))
                    inUse = true;
                return !inUse;
            };
            EntryHandler entryHandler = delegate(PwEntry entry)
            {
                if (entry.CustomIconUuid.Equals(iconUuid))
                {
                    inUse = true;
                    return false;
                }

                foreach (PwEntry historyEntry in entry.History)
                {
                    if (historyEntry.CustomIconUuid.Equals(iconUuid))
                    {
                        inUse = true;
                        return false;
                    }
                }

                return true;
            };

            if (db.RootGroup.CustomIconUuid.Equals(iconUuid))
                return true;

            db.RootGroup.TraverseTree(TraversalMethod.PreOrder, groupHandler, entryHandler);
            return inUse;
        }
    }
}
