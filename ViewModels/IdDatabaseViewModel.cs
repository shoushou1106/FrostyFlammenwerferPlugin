using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Windows;
using FrostySdk.Managers;
using FsLocalizationPlugin.Helpers;
using FsLocalizationPlugin.Resources;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

#if FROSTY_107
using FrostySdk.Managers.Entries;
#endif

namespace FsLocalizationPlugin.ViewModels
{
    /// <summary>Backs the ID Database editor tab: browse, scan, add, import, comment, and fix string IDs.</summary>
    public sealed class IdDatabaseViewModel : ViewModelBase
    {
        private static readonly Random Rng = new Random();

        /// <summary>Where an ID belongs. A game string goes to the shared cache, everything else to the project.</summary>
        private enum IdTarget
        {
            Cached,
            Project,
        }

        /// <summary>One list row. Mutable so detail edits show without rebuilding the list.</summary>
        public sealed class IdRow : ViewModelBase
        {
            private string id;
            private string comment;

            public IdRow(uint hash, string id, string comment)
            {
                Hash = hash;
                HashHex = hash.ToString("X8");
                this.id = id ?? string.Empty;
                this.comment = comment ?? string.Empty;
            }

            public uint Hash { get; }
            public string HashHex { get; }

            public string Id
            {
                get => id;
                set => SetProperty(ref id, value ?? string.Empty);
            }

            public string Comment
            {
                get => comment;
                set => SetProperty(ref comment, value ?? string.Empty);
            }
        }

        private readonly List<IdRow> allRows = new List<IdRow>();
        private readonly List<IdRow> rows = new List<IdRow>();
        private string filterText = string.Empty;
        private string activeFilter = string.Empty;
        private bool isFiltering;
        private bool isProjectView;
        private IdRow selectedRow;
        private string detailId = string.Empty;
        private string detailComment = string.Empty;
        private string addIdHashText = string.Empty;
        private string addIdIdText = string.Empty;
        private bool syncingAddIdFields;
        private List<AssetEntry> selectedReferences = new List<AssetEntry>();
        private string referencesHeader = "References: ";
        private bool hasRefSelection;
        private string referenceSource = string.Empty;
        private string countText = string.Empty;
        private readonly DispatcherTimer addIdRefresh;
        private FileSystemWatcher watcher;
        private System.Timers.Timer watcherDebounce;
        private bool closed;

        public IdDatabaseViewModel(FsLocalizationStringDatabase database)
        {
            Database = database;
            RowsView = new ListCollectionView(rows);

            // Typing an ID recomputes several status properties; defer that off the keystroke.
            addIdRefresh = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(200) };
            addIdRefresh.Tick += (s, e) => { addIdRefresh.Stop(); RaiseAddIdState(); };

            ScanCommand = new RelayCommand(_ => Scan());
            RefreshCommand = new RelayCommand(_ => IdDatabase.Instance.Reload());
            ImportCommand = new RelayCommand(_ => Import());
            OpenFolderCommand = new RelayCommand(_ => OpenCachesFolder());
            AddIdGenerateHashCommand = new RelayCommand(_ => AddIdGenerateHash());
            AddIdConfirmCommand = new RelayCommand(_ => AddIdConfirm(), _ => CanAddIdConfirm);
            RemoveIdCommand = new RelayCommand(_ => RemoveId(), _ => CanRemoveId);
            ConfirmDetailCommand = new RelayCommand(_ => ConfirmDetail(), _ => CanConfirmDetail);
            ExportProjectCommand = new RelayCommand(_ => ExportProject());
            ImportProjectCommand = new RelayCommand(_ => ImportProject());
            // Reserved: accelerated ID computing (GPU).
            ComputeCommand = new RelayCommand(_ => { }, _ => false);
        }

        private FsLocalizationStringDatabase Database { get; }

        public ICollectionView RowsView { get; }

        /// <summary>What the user typed. The list refilters when Enter is pressed.</summary>
        public string FilterText
        {
            get => filterText;
            set => SetProperty(ref filterText, value ?? string.Empty);
        }

        /// <summary>The filter the list currently shows. Drives the match highlight.</summary>
        public string ActiveFilter
        {
            get => activeFilter;
            private set => SetProperty(ref activeFilter, value);
        }

        /// <summary>Drives the filter progress bar and the list cover.</summary>
        public bool IsFiltering
        {
            get => isFiltering;
            private set => SetProperty(ref isFiltering, value);
        }

        public bool IsCachedView
        {
            get => !isProjectView;
            set
            {
                if (value == isProjectView)
                    SwitchView(!value);
            }
        }

        public bool IsProjectView
        {
            get => isProjectView;
            set
            {
                if (value != isProjectView)
                    SwitchView(value);
            }
        }

        /// <summary>The per-game option. Hides the view switch, leaving only the cached database.</summary>
        public bool IsProjectDatabaseEnabled => ProjectIdDatabase.Enabled;

        /// <summary>
        /// The toolbar counter: how much of the game is named in the cached view, how many entries
        /// the project holds in the project view.
        /// </summary>
        public string CountText
        {
            get => countText;
            private set => SetProperty(ref countText, value);
        }

        public IdRow SelectedRow
        {
            get => selectedRow;
            set
            {
                if (SetProperty(ref selectedRow, value))
                {
                    detailId = value?.Id ?? string.Empty;
                    detailComment = value?.Comment ?? string.Empty;
                    UpdateSelectedReferences();
                    OnPropertiesChanged(nameof(HasSelection), nameof(StringPreview), nameof(IsSelectedStringModified),
                        nameof(DetailId), nameof(DetailComment), nameof(DetailIsDirty), nameof(DetailStatus));
                }
            }
        }

        public bool HasSelection => selectedRow != null;

        /// <summary>Current-language value of the selected string, or why there is none.</summary>
        public string StringPreview
        {
            get
            {
                if (selectedRow == null)
                    return string.Empty;
                if (Database.TryGetString(selectedRow.Hash, out string value))
                    return SingleLine(value);
                return Database.IsStringRemoved(selectedRow.Hash) ? "String is Removed" : "No String Exists";
            }
        }

        /// <summary>Whether the selected string is a modification, not the unmodified original. Drives the bold "*" marker.</summary>
        public bool IsSelectedStringModified => selectedRow != null && Database.isStringEdited(selectedRow.Hash);

        /// <summary>Flattens line breaks so a multi-line string cannot stretch the preview row.</summary>
        private static string SingleLine(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return value.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
        }

        #region -- Detail editor (ID + comment, one Confirm) --
        public string DetailId
        {
            get => detailId;
            set
            {
                if (SetProperty(ref detailId, value ?? string.Empty))
                    OnPropertiesChanged(nameof(DetailIsDirty), nameof(DetailStatus));
            }
        }

        public string DetailComment
        {
            get => detailComment;
            set
            {
                if (SetProperty(ref detailComment, value ?? string.Empty))
                    OnPropertiesChanged(nameof(DetailIsDirty), nameof(DetailStatus));
            }
        }

        /// <summary>True when the editor differs from the selected row. Shows the Confirm button.</summary>
        public bool DetailIsDirty => selectedRow != null
            && (!string.Equals(DetailId, selectedRow.Id, StringComparison.Ordinal)
                || (isProjectView && !string.Equals(DetailComment, selectedRow.Comment, StringComparison.Ordinal)));

        public bool CanConfirmDetail => DetailIsDirty
            && (DetailId.Length == 0
                || string.Equals(DetailId, selectedRow.Id, StringComparison.Ordinal)
                || LocalizationHelper.HashStringId(DetailId) == selectedRow.Hash);

        /// <summary>Why Confirm is disabled while there are unsaved edits, else empty.</summary>
        public string DetailStatus
        {
            get
            {
                if (selectedRow == null || !DetailIsDirty)
                    return string.Empty;
                if (DetailId.Length > 0 && LocalizationHelper.HashStringId(DetailId) != selectedRow.Hash)
                    return $"This ID hashes to {LocalizationHelper.HashStringId(DetailId):X8}, but this string is {selectedRow.HashHex}. An ID can only be attached to its own hash.";
                return string.Empty;
            }
        }

        private void ConfirmDetail()
        {
            IdRow row = selectedRow;
            if (row == null)
                return;

            if (!string.Equals(DetailId, row.Id, StringComparison.Ordinal))
            {
                if (DetailId.Length > 0 && LocalizationHelper.HashStringId(DetailId) != row.Hash)
                {
                    FrostyMessageBox.Show($"This ID hashes to {LocalizationHelper.HashStringId(DetailId):X8}, not {row.HashHex}. An ID can only be attached to its own hash.",
                        "ID Database - Flammenwerfer", MessageBoxButton.OK);
                    return;
                }

                if (isProjectView)
                {
                    if (DetailId.Length == 0)
                        ProjectIdDatabase.RemoveIdText(row.Hash);
                    else
                        ProjectIdDatabase.SetIdText(DetailId);
                }
                else
                {
                    IdDatabase.Instance.EnsureLoaded();
                    IdDatabase.Instance.RemoveId(row.Hash);
                    if (DetailId.Length > 0)
                        IdDatabase.Instance.AddId(DetailId);
                    IdDatabase.Instance.Save();
                }

                row.Id = DetailId;
                App.Logger.Log("Flame named! ID for string {0} set to {1}", row.HashHex, DetailId.Length > 0 ? DetailId : "(none)");
            }

            if (isProjectView && !string.Equals(DetailComment, row.Comment, StringComparison.Ordinal))
            {
                ProjectIdDatabase.SetComment(row.Hash, DetailComment);
                row.Comment = DetailComment;
            }

            OnPropertiesChanged(nameof(DetailIsDirty), nameof(DetailStatus));
        }

        /// <summary>
        /// A project row only exists in the store once something was stored for it; rows that are
        /// merely "an added string with no entry yet" have nothing to remove. Cached rows always
        /// come from the database, so they always do.
        /// </summary>
        public bool CanRemoveId => selectedRow != null
            && (!isProjectView || ProjectIdDatabase.Contains(selectedRow.Hash));

        /// <summary>
        /// Forgets the selected row: its ID, comment and references go together. Clearing just the
        /// ID text is what the detail editor's Confirm already does, so this is the stronger action.
        /// </summary>
        private void RemoveId()
        {
            IdRow row = selectedRow;
            if (row == null)
                return;

            string label = row.Id.Length > 0 ? row.Id : row.HashHex;
            if (isProjectView)
            {
                if (!ProjectIdDatabase.Contains(row.Hash))
                    return;

                ProjectIdDatabase.RemoveEntry(row.Hash);
                // The project store raises no Changed event, so rebuild here. The row survives if
                // the string itself is still in the project, which is correct: it just has no entry.
                BuildRows();
            }
            else
            {
                IdDatabase.Instance.EnsureLoaded();
                if (!IdDatabase.Instance.RemoveEntry(row.Hash))
                    return;

                IdDatabase.Instance.Save();
            }

            App.Logger.Log("Flame doused! {0} removed from the {1} database", label, isProjectView ? "project" : "cached");
        }

        #endregion

        #region -- References --
        public List<AssetEntry> SelectedReferences
        {
            get => selectedReferences;
            private set => SetProperty(ref selectedReferences, value);
        }

        public string ReferencesHeader
        {
            get => referencesHeader;
            private set => SetProperty(ref referencesHeader, value);
        }

        /// <summary>Set by the view when the reference list selection changes. Gates the Remove actions.</summary>
        public bool HasRefSelection
        {
            get => hasRefSelection;
            set => SetProperty(ref hasRefSelection, value);
        }

        /// <summary>
        /// Where the selected reference came from, for the cached view's context menu. A scanned
        /// reference disappears on the next scan; a manual one does not, so the difference matters.
        /// </summary>
        public string ReferenceSource
        {
            get => referenceSource;
            private set => SetProperty(ref referenceSource, value);
        }

        /// <summary>Called by the view when the reference list selection changes.</summary>
        public void SetSelectedReference(AssetEntry entry)
        {
            HasRefSelection = entry != null;

            string source = selectedRow != null && entry != null
                ? IdDatabase.Instance.GetReferenceSource(selectedRow.Hash, entry.Name)
                : null;

            ReferenceSource = source == IdReference.ManualSource ? "Source: Added by Hand"
                : source == IdReference.ScanSource ? "Source: Game File Scan"
                : "Source: Unknown";
        }

        private void UpdateSelectedReferences()
        {
            List<AssetEntry> entries = new List<AssetEntry>();
            int total = 0;
            if (selectedRow != null)
            {
                IReadOnlyList<string> paths = isProjectView
                    ? ProjectIdDatabase.GetReferences(selectedRow.Hash)
                    : IdDatabase.Instance.GetReferences(selectedRow.Hash);
                total = paths.Count;
                foreach (string path in paths)
                {
                    EbxAssetEntry entry = App.AssetManager.GetEbxEntry(path);
                    if (entry != null)
                        entries.Add(entry);
                }
            }
            SelectedReferences = entries;
            ReferencesHeader = total > 0 ? $"{total} References: " : "References: ";
        }

        /// <summary>
        /// Adds a reference to the selected row by hand. Called by the view's asset picker.
        /// In the cached database these are marked manual, so a later scan never drops them.
        /// </summary>
        public void AddReference(EbxAssetEntry entry)
        {
            if (selectedRow == null || entry == null)
                return;

            if (isProjectView)
            {
                ProjectIdDatabase.AddReference(selectedRow.Hash, entry.Name);
            }
            else
            {
                IdDatabase.Instance.EnsureLoaded();
                if (IdDatabase.Instance.AddReference(selectedRow.Hash, entry.Name))
                    IdDatabase.Instance.Save();
            }
            UpdateSelectedReferences();
        }

        /// <summary>Removes a reference from the selected row.</summary>
        public void RemoveReference(EbxAssetEntry entry)
        {
            if (selectedRow == null || entry == null)
                return;

            if (isProjectView)
            {
                ProjectIdDatabase.RemoveReference(selectedRow.Hash, entry.Name);
            }
            else
            {
                IdDatabase.Instance.EnsureLoaded();
                if (IdDatabase.Instance.RemoveReference(selectedRow.Hash, entry.Name))
                    IdDatabase.Instance.Save();
            }
            UpdateSelectedReferences();
        }

        #endregion

        #region -- Add ID popup --
        /// <summary>The 8-digit hex hash. Editing it looks the ID up in the ID database.</summary>
        public string AddIdHashText
        {
            get => addIdHashText;
            set
            {
                if (!SetProperty(ref addIdHashText, value ?? string.Empty))
                    return;

                if (!syncingAddIdFields)
                {
                    syncingAddIdFields = true;
                    if (LocalizationHelper.TryParseHexHash(addIdHashText, out uint hash) && IdIndex.TryGet(hash, out string knownId))
                        AddIdIdText = knownId;
                    else
                        AddIdIdText = string.Empty;
                    syncingAddIdFields = false;
                }
                ScheduleAddIdRefresh();
            }
        }

        /// <summary>The string ID text. Editing it fills the hash field.</summary>
        public string AddIdIdText
        {
            get => addIdIdText;
            set
            {
                if (!SetProperty(ref addIdIdText, value ?? string.Empty))
                    return;

                if (!syncingAddIdFields)
                {
                    syncingAddIdFields = true;
                    AddIdHashText = addIdIdText.Length > 0 ? LocalizationHelper.HashStringId(addIdIdText).ToString("X8") : string.Empty;
                    syncingAddIdFields = false;
                }
                ScheduleAddIdRefresh();
            }
        }

        private uint? AddIdParsedHash => LocalizationHelper.TryParseHexHash(AddIdHashText, out uint hash) ? hash : (uint?)null;

        public bool AddIdHasStringValue => AddIdParsedHash is uint hash && Database.TryGetString(hash, out _);

        public string AddIdStringValue => AddIdParsedHash is uint hash && Database.TryGetString(hash, out string value) ? SingleLine(value) : string.Empty;

        public bool AddIdIsModified => AddIdParsedHash is uint hash && Database.isStringEdited(hash);

        /// <summary>Whether there is a reason the string value cannot be shown.</summary>
        public bool AddIdHasStatus => AddIdStatusMessage.Length > 0;

        public string AddIdStatusMessage
        {
            get
            {
                if (!AddIdParsedHash.HasValue)
                    return "Invalid Hash";
                if (AddIdParsedHash is uint hash && Database.IsStringRemoved(hash))
                    return "String is Removed";
                if (!AddIdHasStringValue)
                    return "No String Exists";
                return string.Empty;
            }
        }

        /// <summary>Explains what Confirm will do: which database, or why nothing.</summary>
        public string AddIdTargetMessage
        {
            get
            {
                if (!(AddIdParsedHash is uint hash))
                    return string.Empty;

                if (AddIdIdText.Length > 0 && LocalizationHelper.HashStringId(AddIdIdText) != hash)
                    return $"This ID hashes to {LocalizationHelper.HashStringId(AddIdIdText):X8}, not the entered hash.";

                if (AddIdIdText.Length == 0)
                {
                    if (!ProjectIdDatabase.Enabled)
                        return ProjectDatabaseOffMessage;
                    return ProjectIdDatabase.Contains(hash)
                        ? "This hash is already in the project database."
                        : "Registers this hash in the project database, so it can carry a comment.";
                }

                bool known = IdIndex.TryGet(hash, out string existing) && string.Equals(existing, AddIdIdText, StringComparison.Ordinal);
                if (ClassifyHash(hash) == IdTarget.Cached)
                    return known ? "This ID is already in the cached database." : "This ID matches a game string: it will be added to the cached database.";
                if (!ProjectIdDatabase.Enabled)
                    return ProjectDatabaseOffMessage;
                return known ? "This ID is already in the project database." : "This ID will be added to the project database.";
            }
        }

        public bool CanAddIdConfirm
        {
            get
            {
                if (!(AddIdParsedHash is uint hash))
                    return false;

                // A bare hash: register it so it can carry a comment. Queried live, since a
                // string can be added elsewhere (Modify String, Loc Studio) while this tab is open.
                if (AddIdIdText.Length == 0)
                    return ProjectIdDatabase.Enabled && !ProjectIdDatabase.Contains(hash);

                // An ID: it must hash to the entered hash, and not already be the same known ID.
                if (LocalizationHelper.HashStringId(AddIdIdText) != hash)
                    return false;
                // Nothing but the project store could hold this one, and it is switched off.
                if (!ProjectIdDatabase.Enabled && ClassifyHash(hash) == IdTarget.Project)
                    return false;
                return !(IdIndex.TryGet(hash, out string existing) && string.Equals(existing, AddIdIdText, StringComparison.Ordinal));
            }
        }

        private const string ProjectDatabaseOffMessage = "The project ID database is off. Turn it on in Tools > Options > Flammenwerfer Options.";

        private void ScheduleAddIdRefresh()
        {
            addIdRefresh.Stop();
            addIdRefresh.Start();
        }

        private void RaiseAddIdState()
        {
            OnPropertiesChanged(nameof(AddIdHasStringValue), nameof(AddIdStringValue), nameof(AddIdIsModified),
                nameof(AddIdHasStatus), nameof(AddIdStatusMessage), nameof(AddIdTargetMessage));
        }

        /// <summary>Picks a random hash no existing string, removal, or known ID uses. For comment-only entries.</summary>
        private void AddIdGenerateHash()
        {
            byte[] buffer = new byte[4];
            uint hash;
            do
            {
                Rng.NextBytes(buffer);
                hash = BitConverter.ToUInt32(buffer, 0);
            }
            while (hash == 0 || hash == 0xFFFFFFFF
                || Database.TryGetString(hash, out string _)
                || Database.IsStringRemoved(hash)
                || IdIndex.TryGet(hash, out string _));

            syncingAddIdFields = true;
            AddIdHashText = hash.ToString("X8");
            AddIdIdText = string.Empty;
            syncingAddIdFields = false;
            RaiseAddIdState();
        }

        private void AddIdConfirm()
        {
            if (!(AddIdParsedHash is uint hash))
                return;

            if (AddIdIdText.Length == 0)
            {
                // A bare hash: register it with the project so it can carry a comment.
                ProjectIdDatabase.RegisterHash(hash);
                App.Logger.Log("Hash {0} registered in the project ID database", hash.ToString("X8"));
                BuildRows();
            }
            else
            {
                IdTarget target = StoreId(AddIdIdText);
                App.Logger.Log("Flame named! ID {0} added to the {1} database", AddIdIdText, target == IdTarget.Cached ? "cached" : "project");
                if (target == IdTarget.Cached)
                    IdDatabase.Instance.Save();
                else
                    BuildRows();
            }

            // The popup stays open so several IDs can be added in a row.
            syncingAddIdFields = true;
            AddIdHashText = string.Empty;
            AddIdIdText = string.Empty;
            syncingAddIdFields = false;
            RaiseAddIdState();
        }

        #endregion

        #region -- Commands and lifecycle --
        public RelayCommand ScanCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand ImportCommand { get; }
        public RelayCommand OpenFolderCommand { get; }
        public RelayCommand AddIdGenerateHashCommand { get; }
        public RelayCommand AddIdConfirmCommand { get; }
        public RelayCommand RemoveIdCommand { get; }
        public RelayCommand ConfirmDetailCommand { get; }
        public RelayCommand ComputeCommand { get; }
        public RelayCommand ExportProjectCommand { get; }
        public RelayCommand ImportProjectCommand { get; }

        /// <summary>Called once when the view loads. Lazy: nothing touches the files before this.</summary>
        public void OnFirstLoad()
        {
            IdDatabase.Instance.EnsureLoaded();
            IdDatabase.Instance.Changed += OnDatabaseChanged;
            SetupWatcher();
            BuildRows();
        }

        /// <summary>
        /// Called when the tab closes. Drops every reference something outside this tab holds
        /// (the database event, the dispatcher timer, the file watcher), then releases the rows.
        /// Without this the whole tab, including tens of thousands of rows, would stay alive.
        /// </summary>
        public void OnClosed()
        {
            closed = true;
            addIdRefresh.Stop();
            IdDatabase.Instance.Changed -= OnDatabaseChanged;
            watcherDebounce?.Dispose();
            watcherDebounce = null;
            watcher?.Dispose();
            watcher = null;

            SelectedRow = null;
            allRows.Clear();
            allRows.TrimExcess();
            rows.Clear();
            rows.TrimExcess();
            SelectedReferences = new List<AssetEntry>();
        }

        /// <summary>Filters the list. Called when the user presses Enter.</summary>
        public async void ApplyFilterNow()
        {
            if (IsFiltering || string.Equals(ActiveFilter, FilterText, StringComparison.Ordinal))
                return;

            string filter = FilterText;
            IsFiltering = true;
            try
            {
                // Off the UI thread so the progress bar and cover actually animate.
                List<IdRow> source = new List<IdRow>(allRows);
                List<IdRow> result = await Task.Run(() => ApplyFilter(source, filter));

                if (closed)
                    return;

                ActiveFilter = filter;
                ShowRows(result);
            }
            finally
            {
                IsFiltering = false;
            }
        }

        private static List<IdRow> ApplyFilter(List<IdRow> source, string filter)
        {
            if (filter.Length == 0)
                return source;

            List<IdRow> result = new List<IdRow>();
            foreach (IdRow row in source)
            {
                if (row.HashHex.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                    || row.Id.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                    || row.Comment.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result.Add(row);
                }
            }
            return result;
        }

        private void ShowRows(List<IdRow> source)
        {
            // A rebuild replaces the row objects, so hold the selection by hash: saving a reference
            // or an ID rebuilds the list, and losing the selected string every time is maddening.
            uint? previous = selectedRow?.Hash;

            rows.Clear();
            rows.AddRange(source);
            RowsView.Refresh();
            SelectedRow = previous.HasValue ? source.Find(r => r.Hash == previous.Value) : null;
        }

        private void SwitchView(bool projectView)
        {
            isProjectView = projectView;
            OnPropertiesChanged(nameof(IsProjectView), nameof(IsCachedView));
            BuildRows();
        }

        private void OnDatabaseChanged()
        {
            // Changed can fire from a background save (scan) or the watcher timer.
            Application.Current?.Dispatcher?.BeginInvoke((Action)(() =>
            {
                if (!closed)
                    BuildRows();
            }));
        }

        /// <summary>Refreshes the cached view when the shared file is edited outside Frosty.</summary>
        private void SetupWatcher()
        {
            try
            {
                string fullPath = Path.GetFullPath(IdDatabase.Instance.DatabaseFilePath);
                string directory = Path.GetDirectoryName(fullPath);
                if (directory == null || !Directory.Exists(directory))
                    return;

                watcherDebounce = new System.Timers.Timer(500) { AutoReset = false };
                watcherDebounce.Elapsed += (s, e) => IdDatabase.Instance.Reload();

                watcher = new FileSystemWatcher(directory, Path.GetFileName(fullPath));
                watcher.Changed += OnIdsFileTouched;
                watcher.Created += OnIdsFileTouched;
                watcher.Deleted += OnIdsFileTouched;
                watcher.Renamed += (s, e) => OnIdsFileTouched(s, null);
                watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                // Live update is a nicety. The Refresh button covers it.
                DebugLogHelper.Log("IdDatabaseViewModel.SetupWatcher", "Watcher unavailable: {0}", ex.Message);
            }
        }

        private void OnIdsFileTouched(object sender, FileSystemEventArgs e)
        {
            watcherDebounce?.Stop();
            watcherDebounce?.Start();
        }

        #endregion

        #region -- Rows --
        private void BuildRows()
        {
            allRows.Clear();

            // The option lives outside this tab and can be toggled while it is open.
            OnPropertyChanged(nameof(IsProjectDatabaseEnabled));
            if (isProjectView && !ProjectIdDatabase.Enabled)
            {
                isProjectView = false;
                OnPropertiesChanged(nameof(IsProjectView), nameof(IsCachedView));
            }

            if (isProjectView)
            {
                BuildProjectRows();
                CountText = allRows.Count == 1 ? "1 string" : $"{allRows.Count} strings";
            }
            else
            {
                BuildCachedRows();
            }

            allRows.Sort((a, b) => a.Hash.CompareTo(b.Hash));
            ShowRows(ApplyFilter(allRows, ActiveFilter));
        }

        private void BuildCachedRows()
        {
            IdDatabase db = IdDatabase.Instance;
            HashSet<uint> named = new HashSet<uint>();
            foreach (KeyValuePair<uint, string> kvp in db.EnumerateIds())
            {
                named.Add(kvp.Key);
                allRows.Add(new IdRow(kvp.Key, kvp.Value, string.Empty));
            }
            foreach (uint hash in db.EnumerateRefOnlyHashes())
                allRows.Add(new IdRow(hash, string.Empty, string.Empty));

            // Counted against the game's own strings: IDs of strings the creator added live in the
            // project database, so counting those here would make the coverage look worse than it is.
            int total = 0;
            int resolved = 0;
            foreach (uint hash in Database.EnumerateStrings())
            {
                if (!Database.TryGetOriginalString(hash, out string _))
                    continue;

                total++;
                if (named.Contains(hash))
                    resolved++;
            }
            CountText = $"{resolved} of {total} strings resolved";
        }

        private void BuildProjectRows()
        {
            // Every stored entry, plus every added string that has no entry yet,
            // so creators see what still needs an ID.
            HashSet<uint> listed = new HashSet<uint>();
            foreach (KeyValuePair<uint, ProjectIdEntry> kvp in ProjectIdDatabase.EnumerateEntries())
            {
                listed.Add(kvp.Key);
                allRows.Add(new IdRow(kvp.Key, kvp.Value.Id, kvp.Value.Comment));
            }

            // The same hash can be added in several languages, so dedupe.
            foreach (uint hash in EnumerateAddedStringHashes())
            {
                if (listed.Add(hash))
                    allRows.Add(new IdRow(hash, string.Empty, string.Empty));
            }
        }

        /// <summary>Hashes of user-added strings (not in the game chunks) across every modified language.</summary>
        private IEnumerable<uint> EnumerateAddedStringHashes()
        {
            foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx(type: "UITextDatabase", modifiedOnly: true))
            {
                if (entry.IsAdded)
                    continue;
                if (!(entry.ModifiedEntry?.DataObject is ModifiedFsLocalizationAsset diff))
                    continue;

                foreach (uint hash in diff.strings.Keys)
                {
                    if (!Database.TryGetOriginalString(hash, out string _))
                        yield return hash;
                }
            }
        }

        #endregion

        #region -- ID routing --
        /// <summary>A game string's ID belongs to the shared cache; any other ID saves with the project.</summary>
        private IdTarget ClassifyHash(uint hash)
        {
            return Database.TryGetOriginalString(hash, out string _) ? IdTarget.Cached : IdTarget.Project;
        }

        /// <summary>Stores an ID into the right database. Returns its target.</summary>
        private IdTarget StoreId(string id)
        {
            uint hash = LocalizationHelper.HashStringId(id);
            IdTarget target = ClassifyHash(hash);

            if (target == IdTarget.Cached)
            {
                IdDatabase.Instance.EnsureLoaded();
                IdDatabase.Instance.RemoveId(hash);
                IdDatabase.Instance.AddId(id);
            }
            else
            {
                ProjectIdDatabase.SetIdText(id);
            }
            return target;
        }

        #endregion

        #region -- Toolbar actions --
        private void Scan()
        {
            // The scan reads the pristine game to build the shared cache. Any modification would
            // pollute it (changed or added strings), so require a clean project.
            if (App.AssetManager.GetModifiedCount() != 0)
            {
                FrostyMessageBox.Show("Please create a new project and make sure nothing is modified before scanning.",
                    "ID Database - Flammenwerfer", MessageBoxButton.OK);
                return;
            }

            CancellationTokenSource cancelToken = new CancellationTokenSource();
            IdScanner.ScanResult result = null;

            FrostyTaskWindow.Show("Scanning Game Files for String IDs", "Loading", task =>
            {
                result = IdScanner.Scan(Database, IdDatabase.Instance, task, cancelToken.Token);
            }, showCancelButton: true, cancelCallback: task => cancelToken.Cancel());

            if (result == null)
                return;

            if (result.Cancelled)
                App.Logger.Log("Scan interrupted. Kept {0} new ID(s) found so far", result.IdsFound);
            else
                App.Logger.Log("Blazing trail! Scanned {0} asset(s) and {1} swf(s): {2} new ID(s), {3} reference(s)",
                    result.AssetsScanned, result.SwfScanned, result.IdsFound, result.RefsFound);
            // IdDatabase.Save inside the scan raises Changed, which rebuilds the rows.
        }

        /// <summary>Merges a shared ID database file into the cached database. Only game-string hashes apply.</summary>
        private void Import()
        {
            FrostyOpenFileDialog dialog = new FrostyOpenFileDialog("Import ID Database",
                "ID database (*.json;*.txt)|*.json;*.txt|All files (*.*)|*.*", "FlammenwerferIdDatabase");
            if (!dialog.ShowDialog())
                return;

            try
            {
                // Anything that is not a string of this game does not belong in the shared database.
                IdDatabase.Instance.ImportFile(dialog.FileName, hash => Database.TryGetOriginalString(hash, out string _));
            }
            catch (Exception ex)
            {
                FrostyMessageBox.Show($"Import failed: {ex.Message}", "ID Database - Flammenwerfer", MessageBoxButton.OK);
            }
        }

        /// <summary>Writes the project ID database, comments and references included, to a shareable file.</summary>
        private void ExportProject()
        {
            string json = ProjectIdDatabase.ExportJson();
            if (json.Length == 0)
            {
                FrostyMessageBox.Show("There is nothing in the project ID database yet.",
                    "ID Database - Flammenwerfer", MessageBoxButton.OK);
                return;
            }

            FrostySaveFileDialog dialog = new FrostySaveFileDialog("Export Project ID Database",
                "JSON file (*.json)|*.json", "FlammenwerferProjectIdDatabase", "ProjectIdDatabase.json");
            if (!dialog.ShowDialog())
                return;

            try
            {
                File.WriteAllText(dialog.FileName, json);
            }
            catch (Exception ex)
            {
                FrostyMessageBox.Show($"Export failed: {ex.Message}", "ID Database - Flammenwerfer", MessageBoxButton.OK);
                return;
            }

            App.Logger.Log("Exported the project ID database to {0}", dialog.FileName);
        }

        /// <summary>Merges an exported project ID file. What the project already holds is kept.</summary>
        private void ImportProject()
        {
            FrostyOpenFileDialog dialog = new FrostyOpenFileDialog("Import Project ID Database",
                "JSON file (*.json)|*.json|All files (*.*)|*.*", "FlammenwerferProjectIdDatabase");
            if (!dialog.ShowDialog())
                return;

            try
            {
                ProjectIdDatabase.ImportJson(File.ReadAllText(dialog.FileName));
            }
            catch (Exception ex)
            {
                FrostyMessageBox.Show($"Import failed: {ex.Message}", "ID Database - Flammenwerfer", MessageBoxButton.OK);
                return;
            }

            // The project store raises no Changed event.
            BuildRows();
        }

        private void OpenCachesFolder()
        {
            try
            {
                string fullPath = Path.GetFullPath(IdDatabase.Instance.DatabaseFilePath);
                if (File.Exists(fullPath))
                    Process.Start("explorer.exe", $"/select,\"{fullPath}\"");
                else
                    Process.Start("explorer.exe", $"\"{Path.GetDirectoryName(fullPath)}\"");
            }
            catch (Exception ex)
            {
                App.Logger.LogError("Could not open the Caches folder: {0}", ex.Message);
            }
        }

        #endregion
    }
}
