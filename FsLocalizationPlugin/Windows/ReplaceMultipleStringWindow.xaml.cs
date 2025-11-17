using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using FsLocalizationPlugin;
using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Windows;
using FrostySdk.Interfaces;

namespace FsLocalizationPlugin.Windows
{
    public partial class ReplaceMultipleStringWindow : FrostyDockableWindow
    {
        public string ProfileName { get; set; }

        public FsLocalizationStringDatabase db = LocalizedStringDatabase.Current as FsLocalizationStringDatabase;
        public ReplaceMultipleStringWindow(Window owner)
        {
            InitializeComponent();
            Owner = owner;
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        
        private void addButton_Click(object sender, RoutedEventArgs e)
        {
            List<uint> TotalStrings = db.EnumerateStrings().ToList();
            int totalCount = TotalStrings.Count;
            int index = 0;

            if (!String.IsNullOrEmpty(CurrentValueTextBox.Text))
            {
                RegexOptions options = isCaseSensitive.IsChecked.GetValueOrDefault(false) ? RegexOptions.None : RegexOptions.IgnoreCase;
                string pattern = isMatchWholeWord.IsChecked.GetValueOrDefault(false) ? $@"\b{CurrentValueTextBox.Text}\b" : CurrentValueTextBox.Text;
                // setup ability to cancel the process
                CancellationTokenSource cancelToken = new CancellationTokenSource();
                FrostyTaskWindow.Show(this, CurrentValueTextBox.Text, "", (task) =>
                {
                    try
                    {
                        foreach (uint stringid in TotalStrings)
                        {
                            cancelToken.Token.ThrowIfCancellationRequested();
                            string value = db.GetString(stringid);
                            task.TaskLogger.Log(value);
                            task.TaskLogger.Log("progress:" + (index++ / (double)totalCount) * 100.0d);

                            if (isMatchWholeWord.IsChecked.GetValueOrDefault(false))
                            {
                                cancelToken.Token.ThrowIfCancellationRequested();

                                if (isRegularExpressions.IsChecked.GetValueOrDefault(false))
                                {
                                    if (Regex.IsMatch(value, CurrentValueTextBox.Text))
                                    {
                                        db.SetString(stringid, NewValueTextBox.Text);
                                    }
                                }
                                else
                                {
                                    if (Regex.IsMatch(value, pattern, options))
                                    {
                                        db.SetString(stringid, NewValueTextBox.Text);
                                    }
                                }
                            }
                            else
                            {
                                cancelToken.Token.ThrowIfCancellationRequested();

                                if (isRegularExpressions.IsChecked.GetValueOrDefault(false))
                                {
                                    if (Regex.IsMatch(value, CurrentValueTextBox.Text))
                                    {
                                        db.SetString(stringid, Regex.Replace(value, CurrentValueTextBox.Text, NewValueTextBox.Text));
                                    }
                                }
                                else
                                {
                                    if (Regex.IsMatch(value, pattern, options))
                                    {
                                        db.SetString(stringid, Regex.Replace(value, pattern, NewValueTextBox.Text, options));
                                    }
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException) { }

                }, showCancelButton: true, cancelCallback: (task) => cancelToken.Cancel());

            }

            App.Logger.Log(string.Format("Replaced {0} instances of \"{1}\" with \"{2}\".", index, CurrentValueTextBox.Text, NewValueTextBox.Text));
            DialogResult = true;
            Close();
        }

        private void deleteButton_Click(object sender, RoutedEventArgs e)
        {
            List<uint> TotalStrings = db.EnumerateStrings().ToList();
            int totalCount = TotalStrings.Count;
            int index = 0;
            int totalDelete = 0;

            if (!String.IsNullOrEmpty(CurrentValueTextBox.Text))
            {
                RegexOptions options = isCaseSensitive.IsChecked.GetValueOrDefault(false) ? RegexOptions.None : RegexOptions.IgnoreCase;
                string pattern = isMatchWholeWord.IsChecked.GetValueOrDefault(false) ? $@"\b{CurrentValueTextBox.Text}\b" : CurrentValueTextBox.Text;
                // setup ability to cancel the process
                CancellationTokenSource cancelToken = new CancellationTokenSource();
                FrostyTaskWindow.Show(this, CurrentValueTextBox.Text, "", (task) =>
                {
                    try
                    {
                        foreach (uint stringid in TotalStrings)
                        {
                            cancelToken.Token.ThrowIfCancellationRequested();
                            string value = db.GetString(stringid);
                            task.TaskLogger.Log(value);
                            task.TaskLogger.Log("progress:" + (index++ / (double)totalCount) * 100.0d);

                            if (isMatchWholeWord.IsChecked.GetValueOrDefault(false))
                            {
                                cancelToken.Token.ThrowIfCancellationRequested();

                                if (isRegularExpressions.IsChecked.GetValueOrDefault(false))
                                {
                                    if (Regex.IsMatch(value, CurrentValueTextBox.Text))
                                    {
                                        db.RemoveString(stringid);
                                        totalDelete++;
                                    }
                                }
                                else
                                {
                                    if (Regex.IsMatch(value, pattern, options))
                                    {
                                        db.RemoveString(stringid);
                                        totalDelete++;
                                    }
                                }
                            }
                            else
                            {
                                cancelToken.Token.ThrowIfCancellationRequested();

                                if (isRegularExpressions.IsChecked.GetValueOrDefault(false))
                                {
                                    if (Regex.IsMatch(value, CurrentValueTextBox.Text))
                                    {
                                        db.RemoveString(stringid);
                                        totalDelete++;
                                    }
                                }
                                else
                                {
                                    if (Regex.IsMatch(value, pattern, options))
                                    {
                                        db.RemoveString(stringid);
                                        totalDelete++;
                                    }
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException) { }

                }, showCancelButton: true, cancelCallback: (task) => cancelToken.Cancel());

            }

            App.Logger.LogWarning("Deleted strings cannot be reverted. You need to revert the whole database to get them back.");
            App.Logger.Log($"Deleted {totalDelete} strings");
            DialogResult = true;
            Close();
        }
    }
}
