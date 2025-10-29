using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using FlammenwerferPlugin.Resources;
using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Windows;
using FrostySdk.Interfaces;

namespace FlammenwerferPlugin.Windows
{
    public partial class ReplaceMultipleStringWindow : FrostyDockableWindow
    {
        public string ProfileName { get; set; }

        public FsLocalizationStringDatabase db = LocalizedStringDatabase.Current as FsLocalizationStringDatabase;
        public ReplaceMultipleStringWindow()
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        
        private void addButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(CurrentValueTextBox.Text))
            {
                App.Logger.Log("Current value text cannot be empty.");
                DialogResult = false;
                Close();
                return;
            }

            List<uint> totalStrings = db.EnumerateStrings().Distinct().ToList();
            int totalCount = totalStrings.Count;
            int matchedCount = 0;

            // Determine regex options
            bool isWholeWord = isMatchWholeWord.IsChecked.GetValueOrDefault(false);
            bool isRegex = isRegularExpressions.IsChecked.GetValueOrDefault(false);
            bool isCaseSensitiveMatch = isCaseSensitive.IsChecked.GetValueOrDefault(false);
            
            RegexOptions options = isCaseSensitiveMatch ? RegexOptions.None : RegexOptions.IgnoreCase;
            string searchPattern = isRegex ? CurrentValueTextBox.Text : 
                                  (isWholeWord ? $@"\b{Regex.Escape(CurrentValueTextBox.Text)}\b" : Regex.Escape(CurrentValueTextBox.Text));

            // Setup cancellation
            CancellationTokenSource cancelToken = new CancellationTokenSource();
            
            FrostyTaskWindow.Show(this, "Replacing strings", "", (task) =>
            {
                try
                {
                    int index = 0;
                    foreach (uint stringId in totalStrings)
                    {
                        cancelToken.Token.ThrowIfCancellationRequested();
                        
                        string value = db.GetString(stringId);
                        task.TaskLogger.Log($"Processing: {value}");
                        task.TaskLogger.Log($"progress:{(index++ / (double)totalCount) * 100.0d}");

                        if (Regex.IsMatch(value, searchPattern, options))
                        {
                            string newValue = isWholeWord ? NewValueTextBox.Text : 
                                            Regex.Replace(value, searchPattern, NewValueTextBox.Text, options);
                            db.SetString(stringId, newValue);
                            matchedCount++;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    task.TaskLogger.Log("Operation cancelled by user.");
                }
            }, showCancelButton: true, cancelCallback: (task) => cancelToken.Cancel());

            App.Logger.Log($"Replaced {matchedCount} instances of \"{CurrentValueTextBox.Text}\" with \"{NewValueTextBox.Text}\".");
            DialogResult = true;
            Close();
        }
    }
}
