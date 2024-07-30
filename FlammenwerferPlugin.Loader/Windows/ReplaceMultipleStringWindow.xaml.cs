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
        private uint HashStringId(string stringId)
        {
            uint result = 0xFFFFFFFF;
            for (int i = 0; i < stringId.Length; i++)
                result = stringId[i] + 33 * result;
            return result;
        }
        
        private void addButton_Click(object sender, RoutedEventArgs e)
        {
            List<uint> TotalStrings = db.EnumerateStrings().Distinct().ToList();
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
    }
}
