using Frosty.Core;
using Frosty.Core.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;

namespace FsLocalizationPlugin.ViewModels
{
    /// <summary>
    /// Backs the Modify Multiple Strings window - finds strings by plain text or regex
    /// across the whole database and bulk-replaces, reverts, or removes them.
    /// </summary>
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

        /// <param name="database">The active string database.</param>
        /// <param name="closeAfterAction">
        /// Whether Replace/Revert/Remove should close the window once they've acted. Pass
        /// <see langword="false"/> for a stay-open, run-several-passes experience instead.
        /// </param>
        public ModifyMultipleStringsViewModel(FsLocalizationStringDatabase database, bool closeAfterAction = true) : base(database)
        {
            this.closeAfterAction = closeAfterAction;

            ReplaceCommand = new RelayCommand(owner => Process((Window)owner, BulkAction.Replace), _ => CanProcess);
            RevertCommand = new RelayCommand(owner => Process((Window)owner, BulkAction.Revert), _ => CanProcess);
            RemoveCommand = new RelayCommand(owner => Process((Window)owner, BulkAction.Remove), _ => CanProcess);
            CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));

            RecomputeMatchCount();
        }

        /// <summary>Raised when the window should close, with the DialogResult to use.</summary>
        public event Action<bool?> CloseRequested;

        /// <summary>The text or pattern strings are matched against.</summary>
        public string FilterValue
        {
            get => filterValue;
            set
            {
                if (SetProperty(ref filterValue, value ?? string.Empty))
                    RecomputeMatchCount();
            }
        }

        /// <summary>The replacement text for the Replace action.</summary>
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

        /// <summary>
        /// When set, FilterValue is used as a raw regular expression instead of being
        /// escaped as plain text. <see cref="CaseSensitive"/> and <see cref="MatchWholeWord"/>
        /// don't apply in this mode - write them into the pattern yourself.
        /// </summary>
        public bool UseRegex
        {
            get => useRegex;
            set { if (SetProperty(ref useRegex, value)) RecomputeMatchCount(); }
        }

        /// <summary>When set, a match replaces the whole string value with <see cref="EditText"/> instead of just the matched portion.</summary>
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

        /// <summary>A human-readable summary of the live match count, or the pattern error if the current input isn't a valid pattern.</summary>
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
                    // Case sensitivity and whole-word are plain-text-mode conveniences -
                    // a raw regex is used exactly as written, so it's on the user to add
                    // \b or (?i) themselves if they want that.
                    regex = new Regex(FilterValue);
                }
                else
                {
                    // Plain-text mode must escape the input - otherwise a search for
                    // something like "1.0 (beta)" gets misinterpreted as a regex and
                    // either throws or matches the wrong thing.
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

            FrostyTaskWindow.Show(owner, taskTitle, "", task =>
            {
                try
                {
                    foreach (uint id in targetStrings)
                    {
                        cancelToken.Token.ThrowIfCancellationRequested();

                        string value = Database.GetString(id);
                        // Pass as a format argument, not the template - value is arbitrary
                        // database content and may itself contain literal braces (Frostbite
                        // strings commonly do, e.g. "{PlayerName}"), which ILogger.Log would
                        // otherwise try to parse as a composite-format placeholder and throw.
                        task.TaskLogger.Log("{0}", value);
                        LocalizationHelper.ReportProgress(task.TaskLogger, ++processed, totalCount);

                        if (!regex.IsMatch(value))
                            continue;

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

            // FilterValue/EditText are arbitrary (user-typed patterns/replacement text) and
            // passed as format arguments rather than baked into the template, for the same
            // reason as above - a literal brace in either would otherwise crash the logger.
            string cancelSuffix = cancelled ? " (cancelled)" : "";
            switch (action)
            {
                case BulkAction.Replace:
                    App.Logger.Log("Replaced {0} instance(s) matching \"{1}\" with \"{2}\"{3}", affected, FilterValue, EditText, cancelSuffix);
                    break;
                case BulkAction.Revert:
                    App.Logger.Log("Reverted {0} string(s) matching \"{1}\" to their original value{2}", affected, FilterValue, cancelSuffix);
                    break;
                case BulkAction.Remove:
                    App.Logger.Log("Removed {0} string(s) matching \"{1}\"{2}", affected, FilterValue, cancelSuffix);
                    break;
            }

            RecomputeMatchCount();
            if (closeAfterAction)
                CloseRequested?.Invoke(true);
        }
    }
}
