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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

#if FROSTY_107
using FrostySdk.Managers.Entries;
#endif

namespace FsLocalizationPlugin.ViewModels
{
    /// <summary>Backs the ID Database editor tab. Browse, scan, add, import and fix string IDs.</summary>
    /// <remarks>The tab shows one database at a time, and every edit goes to the one on screen.</remarks>
    public sealed class IdDatabaseViewModel : ViewModelBase, IDisposable
    {
        private const string DialogTitle = "ID Database - Flammenwerfer";
        private const string ProjectDatabaseOffMessage = "The project ID database is off. Turn it on in Tools > Options > Flammenwerfer Options.";

        /// <summary>One list row. Mutable so detail edits show without rebuilding the list.</summary>
        public sealed class IdRow : ViewModelBase
        {
            private string id;

            public IdRow(uint hash, string id)
            {
                Hash = hash;
                HashHex = hash.ToString("X8", CultureInfo.InvariantCulture);
                this.id = id ?? string.Empty;
            }

            public uint Hash { get; }
            public string HashHex { get; }

            public string Id
            {
                get => id;
                set => SetProperty(ref id, value ?? string.Empty);
            }
        }

        private readonly List<IdRow> allRows = new List<IdRow>();
        private readonly List<IdRow> rows = new List<IdRow>();

        private string filterText = string.Empty;
        private string activeFilter = string.Empty;
        private bool isFiltering;
        private bool isProjectView;
        private string countText = string.Empty;

        private IdRow selectedRow;
        private string detailId = string.Empty;

        private List<AssetEntry> selectedReferences = new List<AssetEntry>();
        private string referencesHeader = "References: ";
        private bool hasRefSelection;

        private string addIdHashText = string.Empty;
        private string addIdIdText = string.Empty;
        private bool syncingAddIdFields;

        private bool closed;

        public IdDatabaseViewModel(FsLocalizationStringDatabase database)
        {
            Database = database;
            RowsView = new ListCollectionView(rows);

            ScanCommand = new RelayCommand(_ => Scan());
            RefreshCommand = new RelayCommand(_ => IdDatabase.Instance.Reload());
            ImportCommand = new RelayCommand(_ => Import());
            LocateCacheCommand = new RelayCommand(_ => LocateCacheFile());
            ExportProjectCommand = new RelayCommand(_ => ExportProject());
            AddIdConfirmCommand = new RelayCommand(_ => AddIdConfirm(), _ => CanAddIdConfirm);
            RemoveIdCommand = new RelayCommand(_ => RemoveId(), _ => CanRemoveId);
            ConfirmDetailCommand = new RelayCommand(_ => ConfirmDetail(), _ => CanConfirmDetail);
            // Reserved: accelerated ID computing (GPU).
            ComputeCommand = new RelayCommand(_ => { }, _ => false);
        }

        private FsLocalizationStringDatabase Database { get; }

        public RelayCommand ScanCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand ImportCommand { get; }
        public RelayCommand LocateCacheCommand { get; }
        public RelayCommand ExportProjectCommand { get; }
        public RelayCommand AddIdConfirmCommand { get; }
        public RelayCommand RemoveIdCommand { get; }
        public RelayCommand ConfirmDetailCommand { get; }
        public RelayCommand ComputeCommand { get; }

        #region -- Lifecycle --
        /// <summary>Called once when the view loads. Nothing touches the files before this (Lazy).</summary>
        public void OnFirstLoad()
        {
            IdDatabase.Instance.EnsureLoaded();
            IdDatabase.Instance.Changed += OnDatabaseChanged;
            BuildRows();
        }

        /// <summary>Called when the tab closes.</summary>
        /// <remarks>
        /// The event handler is the only thing holding this tab alive, since the database is a
        /// singleton and the handler closes over the view model. Dropping it lets the whole tab go,
        /// rows included. Clearing the lists by hand was measured and freed nothing extra.
        /// </remarks>
        public void Dispose()
        {
            closed = true;
            IdDatabase.Instance.Changed -= OnDatabaseChanged;
        }

        private void OnDatabaseChanged()
        {
            // Changed can fire from a background save, such as the scan.
            Application.Current?.Dispatcher?.BeginInvoke((Action)(() =>
            {
                if (!closed)
                    BuildRows();
            }));
        }

        #endregion

        #region -- Database Switch --
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

        /// <summary>The toolbar counter.</summary>
        /// <remarks>
        /// Counts how much of the game is named in the cached view,
        /// and how many entries the project holds in the project view.
        /// </remarks>
        public string CountText
        {
            get => countText;
            private set => SetProperty(ref countText, value);
        }

        private void SwitchView(bool projectView)
        {
            isProjectView = projectView;
            OnPropertiesChanged(nameof(IsProjectView), nameof(IsCachedView));
            BuildRows();
        }

        #endregion

        #region -- Rows and Filtering --
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
                BuildProjectRows();
            else
                BuildCachedRows();

            allRows.Sort((a, b) => a.Hash.CompareTo(b.Hash));
            DebugLogHelper.Log("IdDatabaseViewModel.BuildRows", "Built {0} row(s) for the {1} database", allRows.Count, isProjectView ? "project" : "cached");
            ShowRows(ApplyFilter(allRows, ActiveFilter));
        }

        /// <summary>Every hash the cached database knows, named or only referenced.</summary>
        private void BuildCachedRows()
        {
            int named = 0;
            foreach (KeyValuePair<uint, IdEntry> kvp in IdDatabase.Instance.EnumerateEntries())
            {
                if (kvp.Value.Id.Length > 0)
                    named++;
                allRows.Add(new IdRow(kvp.Key, kvp.Value.Id));
            }

            CountText = $"{named} of {Database.EnumerateOriginalStrings().Count()} strings resolved";
        }

        /// <summary>
        /// Every stored entry, plus every added string that has no entry yet,
        /// so creators see what still needs an ID.
        /// </summary>
        private void BuildProjectRows()
        {
            HashSet<uint> listed = new HashSet<uint>();
            foreach (KeyValuePair<uint, IdEntry> kvp in ProjectIdDatabase.EnumerateEntries())
            {
                listed.Add(kvp.Key);
                allRows.Add(new IdRow(kvp.Key, kvp.Value.Id));
            }

            // The same hash can be added in several languages, so dedupe.
            foreach (uint hash in EnumerateAddedStringHashes())
            {
                if (listed.Add(hash))
                    allRows.Add(new IdRow(hash, string.Empty));
            }

            CountText = allRows.Count == 1 ? "1 string" : $"{allRows.Count} strings";
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

        private static List<IdRow> ApplyFilter(List<IdRow> source, string filter)
        {
            if (filter.Length == 0)
                return source;

            List<IdRow> result = new List<IdRow>();
            foreach (IdRow row in source)
            {
                if (row.HashHex.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                    || row.Id.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result.Add(row);
                }
            }
            return result;
        }

        private void ShowRows(List<IdRow> source)
        {
            // A rebuild replaces the row objects, so the selection is held by hash instead.
            // Saving a reference or an ID rebuilds the list, and losing the selected string
            // every time would be maddening.
            uint? previous = selectedRow?.Hash;

            rows.Clear();
            rows.AddRange(source);
            RowsView.Refresh();
            SelectedRow = previous.HasValue ? source.Find(r => r.Hash == previous.Value) : null;
        }

        #endregion

        #region -- Selected String --
        public IdRow SelectedRow
        {
            get => selectedRow;
            set
            {
                if (SetProperty(ref selectedRow, value))
                {
                    detailId = value?.Id ?? string.Empty;
                    UpdateSelectedReferences();
                    OnPropertiesChanged(nameof(HasSelection), nameof(StringPreview), nameof(IsSelectedStringModified),
                        nameof(DetailId), nameof(DetailIsDirty), nameof(DetailStatus));
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

        #endregion

        #region -- Detail Editor --
        public string DetailId
        {
            get => detailId;
            set
            {
                if (SetProperty(ref detailId, value ?? string.Empty))
                    OnPropertiesChanged(nameof(DetailIsDirty), nameof(DetailStatus));
            }
        }

        /// <summary>True when the editor differs from the selected row. Shows the Confirm button.</summary>
        public bool DetailIsDirty => selectedRow != null && !string.Equals(DetailId, selectedRow.Id, StringComparison.Ordinal);

        public bool CanConfirmDetail => DetailIsDirty
            && (DetailId.Length == 0 || LocalizationHelper.HashStringId(DetailId) == selectedRow.Hash);

        /// <summary>Why Confirm is disabled while there are unsaved edits, else empty.</summary>
        public string DetailStatus => DetailIsDirty && !CanConfirmDetail
            ? $"This ID hashes to {LocalizationHelper.HashStringId(DetailId):X8}, but this string is {selectedRow.HashHex}. An ID can only be attached to its own hash."
            : string.Empty;

        private void ConfirmDetail()
        {
            IdRow row = selectedRow;
            if (row == null || !DetailIsDirty)
                return;

            if (DetailId.Length > 0 && LocalizationHelper.HashStringId(DetailId) != row.Hash)
            {
                FrostyMessageBox.Show($"This ID hashes to {LocalizationHelper.HashStringId(DetailId):X8}, not {row.HashHex}. An ID can only be attached to its own hash.",
                    DialogTitle, MessageBoxButton.OK);
                return;
            }

            StoreId(row.Hash, DetailId);
            row.Id = DetailId;
            App.Logger.Log("Flame named! ID for string {0} set to {1}", row.HashHex, DetailId.Length > 0 ? DetailId : "(none)");
            OnPropertiesChanged(nameof(DetailIsDirty), nameof(DetailStatus));
        }

        /// <summary>
        /// A project row only exists in the database once something was stored for it; rows that are
        /// merely "an added string with no entry yet" have nothing to remove. Cached rows always
        /// come from the database, so they always do.
        /// </summary>
        public bool CanRemoveId => selectedRow != null
            && (!isProjectView || ProjectIdDatabase.Contains(selectedRow.Hash));

        /// <summary>
        /// Forgets the selected row.
        /// Clearing just the ID text is what the detail editor's Confirm already does,
        /// so this is the stronger action.
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
                // The project database raises no Changed event, so rebuild here. The row survives
                // if the string itself is still in the project, which is correct. It just has no
                // entry any more.
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

        /// <summary>Writes an ID into the database the tab is showing.</summary>
        /// <remarks>
        /// Deliberately not the database the hash would route to.
        /// An edit made in a view belongs to that view.
        /// </remarks>
        private void StoreId(uint hash, string id)
        {
            if (isProjectView)
            {
                ProjectIdDatabase.SetId(hash, id);
                BuildRows();
            }
            else
            {
                IdDatabase.Instance.EnsureLoaded();
                if (IdDatabase.Instance.SetId(hash, id))
                    IdDatabase.Instance.Save();
            }
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

        /// <summary>Adds a reference to the selected row by hand. Called by the view's asset picker.</summary>
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
        /// <summary>The 8-digit hex hash. Editing it looks the ID up in both databases.</summary>
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
                    AddIdIdText = LocalizationHelper.TryParseHexHash(addIdHashText, out uint hash) && IdIndex.TryGet(hash, out string knownId)
                        ? knownId
                        : string.Empty;
                    syncingAddIdFields = false;
                }
                RaiseAddIdState();
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
                    AddIdHashText = addIdIdText.Length > 0 ? LocalizationHelper.HashStringId(addIdIdText).ToString("X8", CultureInfo.InvariantCulture) : string.Empty;
                    syncingAddIdFields = false;
                }
                RaiseAddIdState();
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
                if (!(AddIdParsedHash is uint hash))
                    return "Invalid Hash";
                if (Database.IsStringRemoved(hash))
                    return "String is Removed";
                if (!AddIdHasStringValue)
                    return "No String Exists";
                return string.Empty;
            }
        }

        /// <summary>Explains what Confirm will do, or why it will do nothing.</summary>
        public string AddIdTargetMessage
        {
            get
            {
                if (!(AddIdParsedHash is uint hash))
                    return "Type a Hash or ID to get started.";
                if (AddIdIdText.Length == 0)
                    return "Type the ID this hash was made from.";
                if (LocalizationHelper.HashStringId(AddIdIdText) != hash)
                    return $"This ID hashes to {LocalizationHelper.HashStringId(AddIdIdText):X8}, not the entered hash.";

                bool known = IdIndex.TryGet(hash, out string existing) && string.Equals(existing, AddIdIdText, StringComparison.Ordinal);
                if (IdIndex.IsGameString(hash))
                    return known ? "This ID is already in the cached database." : "This ID matches a game string. It will be added to the cached database.";
                if (!ProjectIdDatabase.Enabled)
                    return ProjectDatabaseOffMessage;
                return known ? "This ID is already in the project database." : "This ID doesn't match a game string. It will be added to the project database.";
            }
        }

        public bool CanAddIdConfirm
        {
            get
            {
                // An ID that hashes to the entered hash, and is not already the known ID for it.
                if (!(AddIdParsedHash is uint hash) || AddIdIdText.Length == 0)
                    return false;
                if (LocalizationHelper.HashStringId(AddIdIdText) != hash)
                    return false;
                // Nothing but the project database could hold this one, and it is switched off.
                if (!ProjectIdDatabase.Enabled && !IdIndex.IsGameString(hash))
                    return false;

                return !(IdIndex.TryGet(hash, out string existing) && string.Equals(existing, AddIdIdText, StringComparison.Ordinal));
            }
        }

        /// <summary>Refreshes everything the popup computes from the two text boxes.</summary>
        /// <remarks>
        /// Cheap enough to run on every keystroke.
        /// The heaviest part is one dictionary lookup for the carrier asset,
        /// and the project payload only reparses when its text actually changed.
        /// </remarks>
        private void RaiseAddIdState()
        {
            OnPropertiesChanged(nameof(AddIdHasStringValue), nameof(AddIdStringValue), nameof(AddIdIsModified),
                nameof(AddIdHasStatus), nameof(AddIdStatusMessage), nameof(AddIdTargetMessage));
        }

        private void AddIdConfirm()
        {
            if (!(AddIdParsedHash is uint hash) || AddIdIdText.Length == 0)
                return;

            // Unlike the detail editor, this one routes by the hash. That is how a creator adds an
            // ID for a game string the scan missed while looking at the project database, or the
            // other way around.
            bool game = IdIndex.IsGameString(hash);
            IdIndex.Set(hash, AddIdIdText);
            App.Logger.Log("Flame named! ID {0} added to the {1} database", AddIdIdText, game ? "cached" : "project");
            if (!game)
                BuildRows();

            // The popup stays open so several IDs can be added in a row.
            syncingAddIdFields = true;
            AddIdHashText = string.Empty;
            AddIdIdText = string.Empty;
            syncingAddIdFields = false;
            RaiseAddIdState();
        }

        #endregion

        #region -- Toolbar actions --
        /// <summary>
        /// Scans the game for IDs, into the cached database.
        /// </summary>
        /// <remarks>
        /// The scan reads the pristine game to build a database meant for sharing,
        /// so anything the project changed or added would pollute it.
        /// Both of the asset manager's counts are checked, which together are what the editor's own
        /// <c>FrostyProject.IsDirty</c> asks (<c>GetDirtyCount() != 0 || modSettings.IsDirty</c>).
        /// Modified counts every asset that carries an edit, dirty counts the ones edited since the
        /// last project save, and neither alone catches every case.
        /// </remarks>
        private void Scan()
        {
            uint modified = App.AssetManager.GetModifiedCount();
            uint dirty = App.AssetManager.GetDirtyCount();
            DebugLogHelper.Log("IdDatabaseViewModel.Scan", "Project holds {0} modified and {1} dirty asset(s)", modified, dirty);

            if (modified != 0 || dirty != 0)
            {
                FrostyMessageBox.Show("Please create a new project and make sure nothing is modified before scanning.",
                    DialogTitle, MessageBoxButton.OK);
                return;
            }

            IdScanner.ScanResult result = null;
            using (CancellationTokenSource cancelToken = new CancellationTokenSource())
            {
                FrostyTaskWindow.Show("Scanning Game Files for String IDs", "Loading", task =>
                {
                    result = IdScanner.Scan(Database, IdDatabase.Instance, task, cancelToken.Token);
                }, showCancelButton: true, cancelCallback: task => cancelToken.Cancel());
            }

            if (result == null)
                return;

            if (result.Cancelled)
                App.Logger.Log("Scan interrupted. Kept {0} new ID(s) found so far", result.IdsFound);
            else
                App.Logger.Log("Blazing trail! Scanned {0} asset(s) and {1} swf(s) in {2}s, found {3} new ID(s) and {4} reference(s)",
                    result.AssetsScanned, result.SwfScanned, result.ElapsedSeconds.ToString("F1", CultureInfo.InvariantCulture),
                    result.IdsFound, result.RefsFound);

            if (result.AssetsFailed > 0)
                App.Logger.LogWarning("{0} asset(s) could not be read and were skipped. Turn on Debug Logging in the options to see which.", result.AssetsFailed);

            // IdDatabase.Save inside the scan raises Changed, which rebuilds the rows.
        }

        /// <summary>Merges a shared ID file into both databases.</summary>
        /// <remarks>
        /// One file feeds both. A hash the game has a string for goes to the cached database,
        /// and everything else goes to the project database.
        /// </remarks>
        private void Import()
        {
            FrostyOpenFileDialog dialog = new FrostyOpenFileDialog("Import ID Database",
                "ID database (*.json;*.txt)|*.json;*.txt|All files (*.*)|*.*", "FlammenwerferIdDatabase");
            if (!dialog.ShowDialog())
                return;

            try
            {
                IdIndex.ImportFile(dialog.FileName);
            }
            catch (Exception ex)
            {
                FrostyMessageBox.Show($"Import failed: {ex.Message}", DialogTitle, MessageBoxButton.OK);
                return;
            }

            // Only the cached database raises Changed.
            BuildRows();
        }

        /// <summary>Writes the project ID database, references included, to a shareable file.</summary>
        private static void ExportProject()
        {
            string json = ProjectIdDatabase.ExportJson();
            if (json.Length == 0)
            {
                FrostyMessageBox.Show("There is nothing in the project ID database yet.", DialogTitle, MessageBoxButton.OK);
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
                FrostyMessageBox.Show($"Export failed: {ex.Message}", DialogTitle, MessageBoxButton.OK);
                return;
            }

            App.Logger.Log("Exported the project ID database to {0}", dialog.FileName);
        }

        /// <summary>Shows the cached database file in Explorer, so it can be shared as it is.</summary>
        private static void LocateCacheFile()
        {
            try
            {
                string fullPath = Path.GetFullPath(IdDatabase.FilePath);
                if (File.Exists(fullPath))
                    Process.Start("explorer.exe", $"/select,\"{fullPath}\"");
                else
                    Process.Start("explorer.exe", $"\"{Path.GetDirectoryName(fullPath)}\"");
            }
            catch (Exception ex)
            {
                App.Logger.LogError("Could not locate the cached ID database: {0}", ex.Message);
            }
        }

        #endregion
    }
}
