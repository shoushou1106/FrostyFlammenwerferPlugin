using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Windows;
using FrostySdk.Ebx;
using FrostySdk.Managers;
using FsLocalizationPlugin.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shell;

#if FROSTY_107
using FrostySdk.Managers.Entries;
#endif

namespace FsLocalizationPlugin.ViewModels
{
    public enum CompatibilityTestState
    {
        NotRun,
        Loading,
        Passed,
        Warning,
        Error,
        NotApplicable,
    }

    /// <summary>A per-language bucket of detail items (hashes or characters) shown under a test.</summary>
    public sealed class CompatibilityItemGroup
    {
        public CompatibilityItemGroup(string language, IReadOnlyList<string> items)
        {
            Language = language;
            Items = items;
        }

        public string Language { get; }
        public IReadOnlyList<string> Items { get; }
    }

    /// <summary>One test row: state icon, header summary, expandable per-language detail groups, and a background progress bar.</summary>
    public sealed class CompatibilityTest : ViewModelBase
    {
        private readonly string checkingSummary;
        private CompatibilityTestState state = CompatibilityTestState.NotRun;
        private string summary;
        private IReadOnlyList<CompatibilityItemGroup> groups = Array.Empty<CompatibilityItemGroup>();
        private double progress;
        private bool isIndeterminate;
        private bool isCompleted;
        private double lastPercent = -1;

        public CompatibilityTest(string checkingSummary)
        {
            this.checkingSummary = checkingSummary;
            summary = checkingSummary;
        }

        public CompatibilityTestState State
        {
            get => state;
            private set => SetProperty(ref state, value);
        }

        public string Summary
        {
            get => summary;
            private set => SetProperty(ref summary, value);
        }

        public IReadOnlyList<CompatibilityItemGroup> Groups
        {
            get => groups;
            private set
            {
                if (SetProperty(ref groups, value))
                    OnPropertyChanged(nameof(HasItems));
            }
        }

        public bool HasItems => groups.Any(g => g.Items.Count > 0);

        public double Progress
        {
            get => progress;
            private set => SetProperty(ref progress, value);
        }

        public bool IsIndeterminate
        {
            get => isIndeterminate;
            private set => SetProperty(ref isIndeterminate, value);
        }

        /// <summary>True once the test finished. Triggers the progress bar fade-out in XAML.</summary>
        public bool IsCompleted
        {
            get => isCompleted;
            private set => SetProperty(ref isCompleted, value);
        }
        
        public void SetLoading(bool indeterminate = false)
        {
            IsCompleted = false;
            State = CompatibilityTestState.Loading;
            Summary = checkingSummary;
            Groups = Array.Empty<CompatibilityItemGroup>();
            Progress = 0;
            IsIndeterminate = indeterminate;
            lastPercent = -1;
        }

        /// <summary>Sets the bar to a 0-100 percent.</summary>
        public void SetProgress(double percent)
        {
            if (percent == lastPercent)
                return;
            lastPercent = percent;
            Progress = percent;
        }

        public void SetResult(CompatibilityTestState resultState, string resultSummary, IReadOnlyList<CompatibilityItemGroup> resultGroups = null)
        {
            IsIndeterminate = false;
            Progress = 100;
            lastPercent = 100;
            State = resultState;
            Summary = resultSummary;
            Groups = resultGroups ?? Array.Empty<CompatibilityItemGroup>();
            IsCompleted = true;
        }

        public void Reset()
        {
            IsCompleted = false;
            State = CompatibilityTestState.NotRun;
            Summary = checkingSummary;
            Groups = Array.Empty<CompatibilityItemGroup>();
            Progress = 0;
            IsIndeterminate = false;
            lastPercent = -1;
        }
    }
    
    /// <summary>Aggregates child tests into one state/progress for a group header row.</summary>
    public sealed class CompatibilityTestGroup : ViewModelBase
    {
        private readonly CompatibilityTest[] tests;

        public CompatibilityTestGroup(params CompatibilityTest[] tests)
        {
            this.tests = tests;
            foreach (CompatibilityTest test in tests)
                test.PropertyChanged += (s, e) => OnPropertiesChanged(nameof(State), nameof(Progress), nameof(IsCompleted));
        }

        public CompatibilityTestState State
        {
            get
            {
                if (tests.Any(t => t.State == CompatibilityTestState.Loading)) return CompatibilityTestState.Loading;
                if (tests.Any(t => t.State == CompatibilityTestState.Error)) return CompatibilityTestState.Error;
                if (tests.Any(t => t.State == CompatibilityTestState.Warning)) return CompatibilityTestState.Warning;
                if (tests.All(t => t.State == CompatibilityTestState.NotApplicable)) return CompatibilityTestState.NotApplicable;
                if (tests.Any(t => t.State == CompatibilityTestState.NotRun)) return CompatibilityTestState.NotRun;
                return CompatibilityTestState.Passed;
            }
        }

        public double Progress => tests.Average(t => t.Progress);

        // Never goes indeterminate: while a child test's duration is unknown, this bar just stays stuck
        // at the last computed percentage instead of switching to a marquee animation.
        public bool IsIndeterminate => false;

        public bool IsCompleted => tests.All(t => t.IsCompleted);
    }

    /// <summary>Backs the Check Compatibility window: tests the modified strings against Flammenwerfer's and vanilla FsLocalization's limits.</summary>
    public sealed class CheckCompatibilityViewModel : ViewModelBase
    {
        public const string AllLanguages = "All Languages";

        public const string GuideText = "[Warning] This mod contains features not supported by FsLocalizationPlugin. Strings in this mod will not be displayed correctly. Please install Flammenwerfer Plugin from: https://github.com/shoushou1106/FrostyFlammenwerferPlugin";

        private const int TestCount = 5;
        private const string NotApplicableSummary = "No modified strings to check";

        private string selectedLanguage = AllLanguages;
        private string greeting = "What will happen next?";
        private bool hasResults;
        private bool isRefreshing;
        private TaskbarItemProgressState taskbarState = TaskbarItemProgressState.None;
        private CancellationTokenSource refreshCts;

        // Unchanged-edit ids per language, kept only for the Fix button. Rebuilt every refresh, never persisted.
        private Dictionary<string, List<uint>> unchangedIdsByLanguage = new Dictionary<string, List<uint>>();

        public CheckCompatibilityViewModel(FsLocalizationStringDatabase database)
        {
            Database = database;

            List<string> languages = new List<string> { AllLanguages };
            languages.AddRange(LocalizationHelper.GetLocalizedLanguages());
            Languages = languages;

            UnsupportedCharsTest = new CompatibilityTest("Checking for unsupported characters");
            ApplyTest = new CompatibilityTest("Running apply test");
            UnchangedTest = new CompatibilityTest("Checking for unchanged edits");
            HistogramTest = new CompatibilityTest("Checking for characters not existing in histogram");
            ExtendedFeatureTest = new CompatibilityTest("Checking for extended feature");
            Overall = new CompatibilityTest(string.Empty);

            FlammenwerferGroup = new CompatibilityTestGroup(UnsupportedCharsTest, ApplyTest, UnchangedTest);
            FsLocGroup = new CompatibilityTestGroup(HistogramTest, ExtendedFeatureTest);

            RefreshOrCancelCommand = new RelayCommand(_ => { if (IsRefreshing) CancelRefresh(); else RefreshAsync(); });
            FixUnchangedCommand = new RelayCommand(owner => FixUnchanged(owner as Window));
            CopyGuideCommand = new RelayCommand(_ => Clipboard.SetText(GuideText));
            CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
        }

        /// <summary>Raised to close the window, with the DialogResult.</summary>
        public event Action<bool?> CloseRequested;

        private FsLocalizationStringDatabase Database { get; }

        public IReadOnlyList<string> Languages { get; }

        public string SelectedLanguage
        {
            get => selectedLanguage;
            set => SetProperty(ref selectedLanguage, value);
        }

        /// <summary>Header line above the tests, based on how much of the game's text the user has touched.</summary>
        public string Greeting
        {
            get => greeting;
            private set => SetProperty(ref greeting, value);
        }

        /// <summary>False until a refresh starts. Gates the two test group panels.</summary>
        public bool HasResults
        {
            get => hasResults;
            private set => SetProperty(ref hasResults, value);
        }

        public bool IsRefreshing
        {
            get => isRefreshing;
            private set => SetProperty(ref isRefreshing, value);
        }

        /// <summary>Mirrored onto the main window's taskbar icon, like FrostyTaskWindow does.</summary>
        public TaskbarItemProgressState TaskbarState
        {
            get => taskbarState;
            private set => SetProperty(ref taskbarState, value);
        }

        public CompatibilityTest UnsupportedCharsTest { get; }
        public CompatibilityTest ApplyTest { get; }
        public CompatibilityTest UnchangedTest { get; }
        public CompatibilityTest HistogramTest { get; }
        public CompatibilityTest ExtendedFeatureTest { get; }

        /// <summary>Drives the bottom progress bar and the taskbar. Only Progress/IsIndeterminate/IsCompleted are used.</summary>
        public CompatibilityTest Overall { get; }

        public CompatibilityTestGroup FlammenwerferGroup { get; }
        public CompatibilityTestGroup FsLocGroup { get; }

        public RelayCommand RefreshOrCancelCommand { get; }
        public RelayCommand FixUnchangedCommand { get; }
        public RelayCommand CopyGuideCommand { get; }
        public RelayCommand CancelCommand { get; }

        private IEnumerable<CompatibilityTest> AllTests
        {
            get
            {
                yield return UnsupportedCharsTest;
                yield return ApplyTest;
                yield return UnchangedTest;
                yield return HistogramTest;
                yield return ExtendedFeatureTest;
            }
        }

        /// <summary>Starts the first automatic refresh. Called once when the window loads.</summary>
        public void StartInitialRefresh()
        {
            RefreshAsync();
        }

        /// <summary>Cancels a running refresh. Call when the window closes.</summary>
        public void CancelRefresh()
        {
            refreshCts?.Cancel();
        }

        private static string BuildGreeting(int modifiedCount, int totalDecisions)
        {
            if (modifiedCount <= 0)
                return "No string modified. What will happen next?";

            double percent = totalDecisions > 0 ? modifiedCount * 100.0 / totalDecisions : 0;
            if (percent >= 100)
                return "Absolute mastery! Your dedication is legendary! All strings modified.";

            string phrase;
            if (percent >= 60)
                phrase = "Such an immense inferno! Your dedication is legendary!";
            else if (percent >= 30)
                phrase = "A sweeping firestorm. You are truly reshaping this!";
            else if (percent >= 10)
                phrase = "You are leaving a distinct mark!";
            else
                phrase = "You ignite the changes!";

            return $"{modifiedCount} of {totalDecisions} strings modified. {phrase}";
        }

        /// <summary>Finds the localized-text asset for a language and returns its chunk entries. Read live every call.</summary>
        private static bool TryGetLanguageChunks(string language, out ChunkAssetEntry histogramEntry, out ChunkAssetEntry stringChunkEntry)
        {
            histogramEntry = null;
            stringChunkEntry = null;
            string languageFormat = "LanguageFormat_" + language;

            foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx("LocalizationAsset"))
            {
                dynamic localizationAsset = App.AssetManager.GetEbx(entry).RootObject;

                foreach (PointerRef pointer in localizationAsset.LocalizedTexts)
                {
                    EbxAssetEntry textEntry = App.AssetManager.GetEbxEntry(pointer.External.FileGuid);
                    if (textEntry == null)
                        continue;

                    dynamic localizedText = App.AssetManager.GetEbx(textEntry).RootObject;
                    if (localizedText.Language.ToString() != languageFormat)
                        continue;

                    Guid histogramGuid = localizedText.HistogramChunk;
                    Guid binaryGuid = localizedText.BinaryChunk;
                    histogramEntry = App.AssetManager.GetChunkEntry(histogramGuid);
                    stringChunkEntry = App.AssetManager.GetChunkEntry(binaryGuid);
                    return histogramEntry != null && stringChunkEntry != null;
                }
            }
            return false;
        }

        private async void RefreshAsync()
        {
            if (IsRefreshing)
                return;

            IsRefreshing = true;
            HasResults = true;
            refreshCts = new CancellationTokenSource();
            CancellationToken token = refreshCts.Token;

            foreach (CompatibilityTest test in AllTests)
                test.SetLoading();
            Overall.SetLoading(indeterminate: false);
            TaskbarState = TaskbarItemProgressState.Normal;

            try
            {
                await Task.Run(() => RunTests(token), token);
            }
            catch (OperationCanceledException)
            {
                foreach (CompatibilityTest test in AllTests)
                    test.Reset();
                Overall.Reset();
                HasResults = false;
            }
            finally
            {
                IsRefreshing = false;
                TaskbarState = TaskbarItemProgressState.None;
            }
        }

        private void RunTests(CancellationToken token)
        {
            List<string> targetLanguages = SelectedLanguage == AllLanguages
                ? LocalizationHelper.GetLocalizedLanguages(modifiedOnly: true)
                : new List<string> { SelectedLanguage };

            if (targetLanguages.Count == 0)
            {
                // "All Languages" but nothing is modified anywhere.
                Greeting = BuildGreeting(0, 0);
                foreach (CompatibilityTest test in AllTests)
                    test.SetResult(CompatibilityTestState.NotApplicable, NotApplicableSummary);
                unchangedIdsByLanguage = new Dictionary<string, List<uint>>();
                Overall.SetResult(CompatibilityTestState.Passed, string.Empty);
                return;
            }

            string origLanguage = Config.Get("Language", "English", ConfigScope.Game);
            string currentlyLoaded = null;

            // Switch + re-initialize the database only when the language actually changes.
            void EnsureLanguage(string lang)
            {
                if (currentlyLoaded == lang)
                    return;
                Config.Add("Language", lang, ConfigScope.Game);
                Config.Save();
                Database.Initialize();
                currentlyLoaded = lang;
            }

            void UpdateOverall(int partIndex, double partPercent)
            {
                Overall.SetProgress(LocalizationHelper.ComputeProgress(partPercent, 100, partIndex, TestCount));
            }

            int modifiedCount = 0;
            int totalDecisions = 0;
            int totalRemoved = 0;

            try
            {
                // --- Test 1: characters above 0xFFFF (surrogate pairs, e.g. emojis).
                Thread.Sleep(1);
                List<CompatibilityItemGroup> unsupportedGroups = new List<CompatibilityItemGroup>();
                int unsupportedTotal = 0;
                for (int li = 0; li < targetLanguages.Count; li++)
                {
                    token.ThrowIfCancellationRequested();
                    EnsureLanguage(targetLanguages[li]);

                    List<uint> modIds = Database.EnumerateModifiedStrings().ToList();
                    modifiedCount += modIds.Count;
                    totalDecisions += Database.EnumerateStrings().Count();

                    HashSet<string> langChars = new HashSet<string>();
                    for (int i = 0; i < modIds.Count; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        AddSurrogates(Database.GetString(modIds[i]), langChars);

                        double pct = LocalizationHelper.ComputeProgress(i + 1, modIds.Count, li + 1, targetLanguages.Count);
                        UnsupportedCharsTest.SetProgress(pct);
                        UpdateOverall(1, pct);
                    }
                    if (langChars.Count > 0)
                    {
                        unsupportedGroups.Add(new CompatibilityItemGroup(targetLanguages[li], langChars.OrderBy(c => c, StringComparer.Ordinal).ToList()));
                        unsupportedTotal += langChars.Count;
                    }
                }
                Greeting = BuildGreeting(modifiedCount, totalDecisions);
                UnsupportedCharsTest.SetResult(
                    unsupportedTotal > 0 ? CompatibilityTestState.Error : CompatibilityTestState.Passed,
                    unsupportedTotal > 0 ? $"{unsupportedTotal} unsupported character(s)" : "No unsupported characters",
                    unsupportedGroups);
                UpdateOverall(1, 100);

                // --- Test 2 (part 2): full Flammen.WriteAll dry run.
                Thread.Sleep(1);
                ApplyTest.SetLoading(indeterminate: true);
                TaskbarState = TaskbarItemProgressState.Indeterminate;
                List<CompatibilityItemGroup> applyGroups = new List<CompatibilityItemGroup>();
                int applyErrorCount = 0;
                foreach (string lang in targetLanguages)
                {
                    token.ThrowIfCancellationRequested();
                    EnsureLanguage(lang);

                    Dictionary<uint, string> modData = new Dictionary<uint, string>();
                    foreach (uint id in Database.EnumerateModifiedStrings())
                        modData[id] = Database.GetString(id);
                    List<uint> removedIds = Database.EnumerateRemovedStrings().ToList();

                    string error = null;
                    if (!TryGetLanguageChunks(lang, out ChunkAssetEntry hist, out ChunkAssetEntry strChunk))
                    {
                        error = "Histogram or strings chunk not found";
                    }
                    else
                    {
                        try
                        {
                            Flammen.WriteAll(App.AssetManager, hist, strChunk, modData, removedIds, out byte[] _, out byte[] _);
                        }
                        catch (Exception ex)
                        {
                            error = ex.Message;
                        }
                    }
                    if (error != null)
                    {
                        applyGroups.Add(new CompatibilityItemGroup(lang, new[] { error }));
                        applyErrorCount++;
                    }
                }
                ApplyTest.SetResult(
                    applyErrorCount > 0 ? CompatibilityTestState.Error : CompatibilityTestState.Passed,
                    applyErrorCount > 0 ? "Apply test failed" : "Apply test passed",
                    applyGroups);
                TaskbarState = TaskbarItemProgressState.Normal;
                UpdateOverall(2, 100);

                // --- Test 3 (part 3): edits whose value is identical to the game's original string.
                Thread.Sleep(1);
                Dictionary<string, List<uint>> unchanged = new Dictionary<string, List<uint>>();
                List<CompatibilityItemGroup> unchangedGroups = new List<CompatibilityItemGroup>();
                int unchangedTotal = 0;
                for (int li = 0; li < targetLanguages.Count; li++)
                {
                    token.ThrowIfCancellationRequested();
                    string lang = targetLanguages[li];
                    EnsureLanguage(lang);

                    List<uint> modIds = Database.EnumerateModifiedStrings().ToList();
                    List<uint> langUnchangedIds = new List<uint>();
                    List<string> langUnchangedHashes = new List<string>();
                    for (int i = 0; i < modIds.Count; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        uint id = modIds[i];
                        if (Database.TryGetOriginalString(id, out string original) &&
                            string.Equals(original, Database.GetString(id), StringComparison.Ordinal))
                        {
                            langUnchangedIds.Add(id);
                            langUnchangedHashes.Add(id.ToString("X8"));
                        }

                        double pct = LocalizationHelper.ComputeProgress(i + 1, modIds.Count, li + 1, targetLanguages.Count);
                        UnchangedTest.SetProgress(pct);
                        UpdateOverall(3, pct);
                    }
                    if (langUnchangedIds.Count > 0)
                    {
                        unchanged[lang] = langUnchangedIds;
                        unchangedGroups.Add(new CompatibilityItemGroup(lang, langUnchangedHashes));
                        unchangedTotal += langUnchangedIds.Count;
                    }
                }
                unchangedIdsByLanguage = unchanged;
                UnchangedTest.SetResult(
                    unchangedTotal > 0 ? CompatibilityTestState.Warning : CompatibilityTestState.Passed,
                    unchangedTotal > 0 ? $"{unchangedTotal} unchanged edit(s)" : "No unchanged edits",
                    unchangedGroups);
                UpdateOverall(3, 100);

                // --- Test 4 (part 4): characters missing from the unmodified histogram.
                Thread.Sleep(1);
                List<CompatibilityItemGroup> histogramGroups = new List<CompatibilityItemGroup>();
                int histogramTotal = 0;
                for (int li = 0; li < targetLanguages.Count; li++)
                {
                    token.ThrowIfCancellationRequested();
                    string lang = targetLanguages[li];
                    EnsureLanguage(lang);

                    HashSet<ushort> histogramChars = TryGetLanguageChunks(lang, out ChunkAssetEntry hist, out ChunkAssetEntry _)
                        ? new HashSet<ushort>(Flammen.ReadHistogram(hist))
                        : new HashSet<ushort>();

                    List<uint> modIds = Database.EnumerateModifiedStrings().ToList();
                    HashSet<char> langMissing = new HashSet<char>();
                    for (int i = 0; i < modIds.Count; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        string value = Database.GetString(modIds[i]);
                        if (!string.IsNullOrEmpty(value))
                        {
                            foreach (char c in value)
                            {
                                // ASCII passes through without the histogram; surrogates are already test 1's problem.
                                if (c >= 0x80 && !char.IsSurrogate(c) && !histogramChars.Contains(c))
                                    langMissing.Add(c);
                            }
                        }

                        double pct = LocalizationHelper.ComputeProgress(i + 1, modIds.Count, li + 1, targetLanguages.Count);
                        HistogramTest.SetProgress(pct);
                        UpdateOverall(4, pct);
                    }
                    if (langMissing.Count > 0)
                    {
                        histogramGroups.Add(new CompatibilityItemGroup(lang, langMissing.OrderBy(c => c).Select(c => c.ToString()).ToList()));
                        histogramTotal += langMissing.Count;
                    }
                }
                HistogramTest.SetResult(
                    histogramTotal > 0 ? CompatibilityTestState.Warning : CompatibilityTestState.Passed,
                    histogramTotal > 0 ? $"{histogramTotal} character(s) not in histogram" : "All characters exist in histogram",
                    histogramGroups);
                UpdateOverall(4, 100);

                // --- Test 5 (part 5): Flammenwerfer-only features (string removal).
                Thread.Sleep(1);
                List<CompatibilityItemGroup> removedGroups = new List<CompatibilityItemGroup>();
                for (int li = 0; li < targetLanguages.Count; li++)
                {
                    token.ThrowIfCancellationRequested();
                    EnsureLanguage(targetLanguages[li]);

                    List<uint> removedIds = Database.EnumerateRemovedStrings().ToList();
                    List<string> langRemoved = new List<string>();
                    for (int i = 0; i < removedIds.Count; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        langRemoved.Add(removedIds[i].ToString("X8"));

                        double pct = LocalizationHelper.ComputeProgress(i + 1, removedIds.Count, li + 1, targetLanguages.Count);
                        ExtendedFeatureTest.SetProgress(pct);
                        UpdateOverall(5, pct);
                    }
                    totalRemoved += removedIds.Count;
                    if (langRemoved.Count > 0)
                        removedGroups.Add(new CompatibilityItemGroup(targetLanguages[li], langRemoved));
                }
                ExtendedFeatureTest.SetResult(
                    totalRemoved > 0 ? CompatibilityTestState.Warning : CompatibilityTestState.Passed,
                    totalRemoved > 0 ? $"{totalRemoved} string(s) removed" : "No extended features used",
                    removedGroups);
                UpdateOverall(5, 100);

                // If the selected scope has no changes at all, show N/A rather than a row of green passes.
                if (modifiedCount == 0 && totalRemoved == 0)
                {
                    foreach (CompatibilityTest test in AllTests)
                        test.SetResult(CompatibilityTestState.NotApplicable, NotApplicableSummary);
                    unchangedIdsByLanguage = new Dictionary<string, List<uint>>();
                }

                Overall.SetResult(CompatibilityTestState.Passed, string.Empty);
            }
            finally
            {
                Config.Add("Language", origLanguage, ConfigScope.Game);
                Config.Save();
                Database.Initialize();
            }
        }

        private static void AddSurrogates(string value, HashSet<string> unsupportedChars)
        {
            if (string.IsNullOrEmpty(value))
                return;
            for (int c = 0; c < value.Length; c++)
            {
                if (char.IsHighSurrogate(value[c]) && c + 1 < value.Length && char.IsLowSurrogate(value[c + 1]))
                {
                    unsupportedChars.Add(value.Substring(c, 2));
                    c++;
                }
                else if (char.IsSurrogate(value[c]))
                {
                    // Lone surrogate, malformed. Show the code point since it can't render.
                    unsupportedChars.Add($"0x{(int)value[c]:X4}");
                }
            }
        }

        private void FixUnchanged(Window owner)
        {
            if (IsRefreshing || unchangedIdsByLanguage.Count == 0)
                return;

            Dictionary<string, List<uint>> toFix = unchangedIdsByLanguage;
            int count = toFix.Sum(kvp => kvp.Value.Count);

            if (FrostyMessageBox.Show(
                    $"Revert {count} unchanged edit(s)?",
                    "Check Compatibility - Flammenwerfer", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            unchangedIdsByLanguage = new Dictionary<string, List<uint>>();

            FrostyTaskWindow.Show(owner, "Reverting Unchanged Edits", "", task =>
            {
                string origLanguage = Config.Get("Language", "English", ConfigScope.Game);
                try
                {
                    int done = 0;
                    foreach (KeyValuePair<string, List<uint>> kvp in toFix)
                    {
                        Config.Add("Language", kvp.Key, ConfigScope.Game);
                        Config.Save();
                        Database.Initialize();
                        foreach (uint id in kvp.Value)
                        {
                            Database.RevertString(id);
                            LocalizationHelper.ReportProgress(task.TaskLogger, ++done, count);
                        }
                    }
                }
                finally
                {
                    Config.Add("Language", origLanguage, ConfigScope.Game);
                    Config.Save();
                    Database.Initialize();
                }
            });

            App.Logger.Log("Illusions dispelled! Auto Fix applied, reverted {0} unchanged edit(s)", count);

            RefreshAsync();
        }
    }
}
