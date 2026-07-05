using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Windows;
using FsLocalizationPlugin.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;

namespace FsLocalizationPlugin.ViewModels
{
    /// <summary>Backs the Modify Multiple Strings window: finds strings by plain text or regex, then bulk-replaces, reverts, or removes them.</summary>
    public sealed class ModifyMultipleStringsViewModel : LanguageAwareViewModelBase
    {
        private enum BulkAction
        {
            Replace,
            Revert,
            Remove,
        }

        private readonly bool closeAfterAction;

        private string filterValue = string.Empty;
        private string editText = string.Empty;
        private bool caseSensitive;
        private bool matchWholeWord;
        private bool useRegex;
        private bool replaceEntireString;
        private int matchCount;
        private string patternError;

        /// <param name="closeAfterAction">Close after Replace/Revert/Remove. False stays open for another pass.</param>
        public ModifyMultipleStringsViewModel(FsLocalizationStringDatabase database, bool closeAfterAction = true) : base(database)
        {
            this.closeAfterAction = closeAfterAction;

            ReplaceCommand = new RelayCommand(owner => Process((Window)owner, BulkAction.Replace), _ => CanProcess);
            RevertCommand = new RelayCommand(owner => Process((Window)owner, BulkAction.Revert), _ => CanProcess);
            RemoveCommand = new RelayCommand(owner => Process((Window)owner, BulkAction.Remove), _ => CanProcess);
            CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));

            RecomputeMatchCount();
        }

        /// <summary>Raised to close the window, with the DialogResult.</summary>
        public event Action<bool?> CloseRequested;

        /// <summary>Text or pattern to match against.</summary>
        public string FilterValue
        {
            get => filterValue;
            set
            {
                if (SetProperty(ref filterValue, value ?? string.Empty))
                    RecomputeMatchCount();
            }
        }

        /// <summary>Replacement text for the Replace action.</summary>
        public string EditText
        {
            get => editText;
            set => SetProperty(ref editText, value ?? string.Empty);
        }

        public bool CaseSensitive
        {
            get => caseSensitive;
            set { if (SetProperty(ref caseSensitive, value)) RecomputeMatchCount(); }
        }

        public bool MatchWholeWord
        {
            get => matchWholeWord;
            set { if (SetProperty(ref matchWholeWord, value)) RecomputeMatchCount(); }
        }

        /// <summary>Use FilterValue as a raw regex. CaseSensitive/MatchWholeWord don't apply then - write them into the pattern yourself.</summary>
        public bool UseRegex
        {
            get => useRegex;
            set { if (SetProperty(ref useRegex, value)) RecomputeMatchCount(); }
        }

        /// <summary>Replace the whole string value with EditText instead of just the matched portion.</summary>
        public bool ReplaceEntireString
        {
            get => replaceEntireString;
            set => SetProperty(ref replaceEntireString, value);
        }

        public int MatchCount
        {
            get => matchCount;
            private set => SetProperty(ref matchCount, value);
        }

        /// <summary>Live match count, or the pattern error if invalid.</summary>
        public string MatchSummary => PatternError != null
            ? $"Invalid pattern: {PatternError}"
            : $"{MatchCount} string{(MatchCount == 1 ? "" : "s")} match{(MatchCount == 1 ? "es" : "")}";

        public string PatternError
        {
            get => patternError;
            private set
            {
                if (SetProperty(ref patternError, value))
                    OnPropertyChanged(nameof(CanProcess));
            }
        }

        public bool CanProcess => !string.IsNullOrEmpty(FilterValue) && PatternError == null;

        public RelayCommand ReplaceCommand { get; }
        public RelayCommand RevertCommand { get; }
        public RelayCommand RemoveCommand { get; }
        public RelayCommand CancelCommand { get; }

        protected override void OnLanguageChanged()
        {
            RecomputeMatchCount();
        }

        private bool TryBuildRegex(out Regex regex)
        {
            regex = null;
            if (string.IsNullOrEmpty(FilterValue))
            {
                PatternError = null;
                return false;
            }

            try
            {
                if (UseRegex)
                {
                    // Used as-is. Case/whole-word are plain-text-mode only.
                    regex = new Regex(FilterValue);
                }
                else
                {
                    // Escape, or a search like "1.0 (beta)" misparses as regex.
                    string pattern = Regex.Escape(FilterValue);
                    if (MatchWholeWord)
                        pattern = $@"\b{pattern}\b";

                    RegexOptions options = CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                    regex = new Regex(pattern, options);
                }
                PatternError = null;
                return true;
            }
            catch (ArgumentException ex)
            {
                PatternError = ex.Message;
                return false;
            }
        }

        private void RecomputeMatchCount()
        {
            if (!TryBuildRegex(out Regex regex))
            {
                MatchCount = 0;
                OnPropertyChanged(nameof(MatchSummary));
                return;
            }

            int count = 0;
            foreach (uint id in Database.EnumerateStrings())
            {
                if (regex.IsMatch(Database.GetString(id)))
                    count++;
            }
            MatchCount = count;
            OnPropertyChanged(nameof(MatchSummary));
        }

        private void Process(Window owner, BulkAction action)
        {
            if (!TryBuildRegex(out Regex regex))
                return;

            List<uint> targetStrings = Database.EnumerateStrings().ToList();
            int totalCount = targetStrings.Count;
            int processed = 0;
            int affected = 0;
            bool cancelled = false;
            CancellationTokenSource cancelToken = new CancellationTokenSource();

            string taskTitle;
            switch (action)
            {
                case BulkAction.Revert: taskTitle = "Reverting Strings"; break;
                case BulkAction.Remove: taskTitle = "Removing Strings"; break;
                default: taskTitle = "Replacing Strings"; break;
            }

            // Tracks every id this run touches, so a cancel can put the database back exactly how it was
            Dictionary<uint, string> priorModifiedValues = new Dictionary<uint, string>();
            List<uint> priorUnmodifiedIds = new List<uint>();
            List<uint> priorRemovedIds = new List<uint>();

            FrostyTaskWindow.Show(owner, taskTitle, "", task =>
            {
                try
                {
                    foreach (uint id in targetStrings)
                    {
                        cancelToken.Token.ThrowIfCancellationRequested();

                        string value = Database.GetString(id);
                        task.TaskLogger.Log("{0}", value);
                        LocalizationHelper.ReportProgress(task.TaskLogger, ++processed, totalCount);

                        if (!regex.IsMatch(value))
                            continue;

                        if (Database.IsStringRemoved(id))
                            priorRemovedIds.Add(id);
                        else if (Database.isStringEdited(id))
                            priorModifiedValues[id] = value;
                        else
                            priorUnmodifiedIds.Add(id);

                        switch (action)
                        {
                            case BulkAction.Replace:
                                Database.SetString(id, ReplaceEntireString ? EditText : regex.Replace(value, EditText));
                                break;
                            case BulkAction.Revert:
                                Database.RevertString(id);
                                break;
                            case BulkAction.Remove:
                                Database.RemoveString(id);
                                break;
                        }

                        affected++;
                    }
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                }
            }, showCancelButton: true, cancelCallback: task => cancelToken.Cancel());

            if (cancelled)
            {
                int touchedCount = priorModifiedValues.Count + priorUnmodifiedIds.Count + priorRemovedIds.Count;

                if (touchedCount == 0)
                {
                    App.Logger.Log("Mass cast interrupted! Nothing touched yet.");
                }
                else if (FrostyMessageBox.Show($"Temporal Ward Activated! The mass cast was interrupted, but {touchedCount} string(s) were already altered. Restore them to how they were before?", "Modify Multiple Strings - Flammenwerfer", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    FlammenwerferOptions.DebugLog("ModifyMultipleStringsViewModel.Process", "Restoring {0} prior value(s), {1} baseline revert(s), {2} re-removal(s)", priorModifiedValues.Count, priorUnmodifiedIds.Count, priorRemovedIds.Count);

                    FrostyTaskWindow.Show(owner, "Invoking Temporal Ward", "Restoring Strings", restoreTask =>
                    {
                        int restored = 0;
                        foreach (KeyValuePair<uint, string> kvp in priorModifiedValues)
                        {
                            Database.SetString(kvp.Key, kvp.Value);
                            LocalizationHelper.ReportProgress(restoreTask.TaskLogger, ++restored, touchedCount);
                        }
                        foreach (uint id in priorUnmodifiedIds)
                        {
                            Database.RevertString(id);
                            LocalizationHelper.ReportProgress(restoreTask.TaskLogger, ++restored, touchedCount);
                        }
                        foreach (uint id in priorRemovedIds)
                        {
                            Database.RemoveString(id);
                            LocalizationHelper.ReportProgress(restoreTask.TaskLogger, ++restored, touchedCount);
                        }
                    });

                    switch (action)
                    {
                        case BulkAction.Replace:
                            App.Logger.Log("Mass forge interrupted, but the Temporal Ward was activated! Restored {0} string(s) matching \"{1}\"", touchedCount, FilterValue);
                            break;
                        case BulkAction.Revert:
                            App.Logger.Log("Mass extinguish interrupted, but the Temporal Ward was activated! Restored {0} string(s) matching \"{1}\"", touchedCount, FilterValue);
                            break;
                        case BulkAction.Remove:
                            App.Logger.Log("Mass scorch interrupted, but the Temporal Ward was activated! Restored {0} string(s) matching \"{1}\"", touchedCount, FilterValue);
                            break;
                    }
                }
                else
                {
                    switch (action)
                    {
                        case BulkAction.Replace:
                            App.Logger.Log("Mass forge interrupted, and you rejected the Temporal Ward! {0} string(s) matching \"{1}\" were kept. You may want to revert the database.", touchedCount, FilterValue);
                            break;
                        case BulkAction.Revert:
                            App.Logger.Log("Mass extinguish interrupted, and you rejected the Temporal Ward! {0} string(s) matching \"{1}\" were kept. You may want to revert the database.", touchedCount, FilterValue);
                            break;
                        case BulkAction.Remove:
                            App.Logger.Log("Mass scorch interrupted, and you rejected the Temporal Ward! {0} string(s) matching \"{1}\" were kept. You may want to revert the database.", touchedCount, FilterValue);
                            break;
                    }
                }
            }
            else
            {
                switch (action)
                {
                    case BulkAction.Replace:
                        App.Logger.Log("Mass flames forged! Replaced {0} string(s) matching \"{1}\" with \"{2}\"", affected, FilterValue, EditText);
                        break;
                    case BulkAction.Revert:
                        App.Logger.Log("Mass flames extinguished! Reverted {0} string(s) matching \"{1}\"", affected, FilterValue);
                        break;
                    case BulkAction.Remove:
                        App.Logger.Log("Mass flames scorched! Removed {0} string(s) matching \"{1}\"", affected, FilterValue);
                        break;
                }
            }

            RecomputeMatchCount();
            if (closeAfterAction)
                CloseRequested?.Invoke(true);
        }
    }
}
