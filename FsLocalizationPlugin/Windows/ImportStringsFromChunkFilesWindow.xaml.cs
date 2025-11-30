using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Windows;
using FrostySdk.Ebx;
using FrostySdk.Interfaces;
using FrostySdk.IO;
using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace FsLocalizationPlugin.Windows
{
    public partial class ImportStringsFromChunkFilesWindow : FrostyDockableWindow, INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _binaryFilePath;
        public string BinaryFilePath
        {
            get { return _binaryFilePath; }
            set
            {
                if (_binaryFilePath != value)
                {
                    _binaryFilePath = value;
                    OnPropertyChanged(nameof(BinaryFilePath));
                    OnPropertyChanged(nameof(CanImport));
                        BinaryDragTips.Visibility = string.IsNullOrEmpty(_binaryFilePath) ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }

        private string _histogramFilePath;
        public string HistogramFilePath
        {
            get { return _histogramFilePath; }
            set
            {
                if (_histogramFilePath != value)
                {
                    _histogramFilePath = value;
                    OnPropertyChanged(nameof(HistogramFilePath));
                    OnPropertyChanged(nameof(CanImport));
                        HistogramDragTips.Visibility = string.IsNullOrEmpty(_histogramFilePath) ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }

        public bool CanImport => !string.IsNullOrEmpty(BinaryFilePath) && !string.IsNullOrEmpty(HistogramFilePath);

        public ImportStringsFromChunkFilesWindow(Window owner)
        {
            Owner = owner;

            InitializeComponent();

            Left = Owner.Left + (Owner.Width / 2.0) - (ActualWidth / 2.0);
            Top = Owner.Top + (Owner.Height / 2.0) - (ActualHeight / 2.0);

            this.DataContext = this;

            Dispatcher.UnhandledException += UnhandledException;

            LanguageComboBox.Items.Clear();
            GetLocalizedLanguages().ForEach(x => LanguageComboBox.Items.Add(x));
            LanguageComboBox.SelectedIndex = LanguageComboBox.Items.IndexOf(Config.Get<string>("Language", "English", ConfigScope.Game));
            }

        public static List<string> GetLocalizedLanguages()
        {
            HashSet<string> languages = new HashSet<string>();
            foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx("LocalizationAsset"))
            {
                // read master localization asset
                dynamic localizationAsset = App.AssetManager.GetEbx(entry).RootObject;

                // iterate through localized texts
                foreach (PointerRef pointer in localizationAsset.LocalizedTexts)
                {
                    EbxAssetEntry textEntry = App.AssetManager.GetEbxEntry(pointer.External.FileGuid);
                    if (textEntry == null)
                        continue;

                    // read localized text asset
                    dynamic localizedText = App.AssetManager.GetEbx(textEntry).RootObject;

                    string lang = localizedText.Language.ToString();
                    lang = lang.Replace("LanguageFormat_", "");

                    languages.Add(lang);
                }
            }

            if (!languages.Any())
                languages.Add("English");

            return languages.ToList();
        }

        private void UnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            FrostyExceptionBox.Show(e.Exception, "Import Strings from Chunk Files - Flammenwerfer");
            DialogResult = false;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BrowsePathButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var kind = button != null ? (button.Tag as string) : null; // "Binary" or "Histogram"

            FrostyOpenFileDialog openFileDialog = new FrostyOpenFileDialog($"Import {kind} Chunk",
                "Chunk file (*.chunk)|*.chunk|Binary file (*.bin)|*.bin|All files (*.*)|*.*",
                $"LocalizedStrings_{kind}Chunk");
            if (openFileDialog.ShowDialog())
            {
                if (kind == "Binary")
                {
                    BinaryFilePath = openFileDialog.FileName;
                }
                else if (kind == "Histogram")
                {
                    HistogramFilePath = openFileDialog.FileName;
                }
                OnPropertyChanged(nameof(CanImport));
            }
        }

        private async void ReportProgress(ILogger logger, double current, double total, double currentPart = 1, double totalParts = 1, double detail = 1, double totalDetails = 1)
        {
            if (total > 0)
            {
                // totalParts = tp
                // currentPart = p
                // total = t
                // current = c
                // totalDetails = td
                // detail = d
                // ((((p - 1) * t + (c - 1)) * td + d) / (tp * t * td)) * 100%
                await Task.Run(() => logger.Log("progress:" + (((((double)currentPart - 1) * (double)total + ((double)current - 1)) * (double)totalDetails + (double)detail) / ((double)totalDetails * (double)total * (double)totalParts)) * 100.0d));
            }
        }
        private void StartOverButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(BinaryFilePath))
            {
                BinaryFilePath = string.Empty;
                OnPropertyChanged(nameof(CanImport));
            }

            if (!string.IsNullOrWhiteSpace(HistogramFilePath))
            {
                HistogramFilePath = string.Empty;
                OnPropertyChanged(nameof(CanImport));
            }
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            // Validation
            if (!File.Exists(HistogramFilePath) && !File.Exists(BinaryFilePath))
            {
                FrostyMessageBox.Show("Histogram file and Binary Strings file not found", "Import Strings from Chunk Files - Flammenwerfer", MessageBoxButton.OK);
                return;
            }
            if (!File.Exists(HistogramFilePath))
            {
                FrostyMessageBox.Show("Histogram file not found", "Import Strings from Chunk Files - Flammenwerfer", MessageBoxButton.OK);
                return;
            }
            if (!File.Exists(BinaryFilePath))
            {
                FrostyMessageBox.Show("Binary Strings file not found", "Import Strings from Chunk Files - Flammenwerfer", MessageBoxButton.OK);
                return;
            }

            CancellationTokenSource cancelToken = new CancellationTokenSource();
            FrostyTaskWindow.Show(this, "Import Strings from Chunk Files", "Loading", (task) =>
            {
                try
                {
                    int totalParts = 3;
                    cancelToken.Token.ThrowIfCancellationRequested();

                    string origLanguage = Config.Get<string>("Language", "English", ConfigScope.Game);
                    ILocalizedStringDatabase db = LocalizedStringDatabase.Current;

                    string lang = "English";
                    bool noImportEmptyOrNullCheckBox = false;
                    bool deleteExistStringsCheckBox = false;
                    bool noOverwriteSameStringsCheckBox = false;
                    Dispatcher.Invoke(() =>
                    {
                        lang = LanguageComboBox.SelectedItem.ToString() ?? "English";
                        noImportEmptyOrNullCheckBox = NoImportEmptyOrNullCheckBox.IsChecked ?? false;
                        deleteExistStringsCheckBox = DeleteExistStringsCheckBox.IsChecked ?? false;
                        noOverwriteSameStringsCheckBox = NoOverwriteSameStringsCheckBox.IsChecked ?? false;
                    });

                    Config.Add("Language", lang, ConfigScope.Game);
                    Config.Save();
                    db.Initialize();

                    int currentDelete = 0;
                    if (deleteExistStringsCheckBox)
                    {
                        task.TaskLogger.Log("[1/3] Deleting all existing strings");
                        ReportProgress(task.TaskLogger, 0, 1, currentPart: 1, totalParts);
                        cancelToken.Token.ThrowIfCancellationRequested();
                        Thread.Sleep(1);
                        App.Logger.LogWarning("Removed strings cannot be reverted. You need to revert the whole database to get them back. This is a experimental function.");
                        int totalDelete = db.EnumerateStrings().Count();
                        foreach (uint id in db.EnumerateStrings())
                        {
                            (db as FsLocalizationStringDatabase).DeleteString(id);
                            cancelToken.Token.ThrowIfCancellationRequested();
                            ReportProgress(task.TaskLogger, ++currentDelete, totalDelete, currentPart: 1, totalParts);
                        }
                    }

                    task.TaskLogger.Log("[2/3] Reading Chunks");
                    ReportProgress(task.TaskLogger, 0, 1, currentPart: 2, totalParts);
                    cancelToken.Token.ThrowIfCancellationRequested();
                    Thread.Sleep(1);

                    Dictionary<uint, string> dictionary;
                    using (var histogram = File.OpenRead(HistogramFilePath))
                    using (var binary = File.OpenRead(BinaryFilePath))
                    {
                        dictionary = Flammen.Flammen.ReadStrings(histogram, binary);
                    }

                    task.TaskLogger.Log("[3/3] Importing Strings");
                    ReportProgress(task.TaskLogger, 0, 1, currentPart: 3, totalParts);
                    cancelToken.Token.ThrowIfCancellationRequested();
                    Thread.Sleep(1);

                    int totalCount = dictionary.Count;
                    int currentCount = 0;
                    int actualImportedCount = 0;
                    foreach (var kvp in dictionary)
                    {
                        ReportProgress(task.TaskLogger, ++currentCount, totalCount, currentPart: 2, totalParts);
                        cancelToken.Token.ThrowIfCancellationRequested();
                        if (noImportEmptyOrNullCheckBox && string.IsNullOrEmpty(kvp.Value))
                            continue;
                        if (noOverwriteSameStringsCheckBox && db.GetString(kvp.Key) == kvp.Value)
                            continue;
                        db.SetString(kvp.Key, kvp.Value);
                        actualImportedCount++;
                    }

                    // Switch back to the previously backed-up language
                    Config.Add("Language", origLanguage, ConfigScope.Game);
                    Config.Save();
                    db.Initialize();
                    if (currentDelete == 0)
                        App.Logger.Log($"Import strings from chunk files completed. Imported {actualImportedCount} strings to language {lang}");
                    else
                        App.Logger.Log($"Import strings from chunk files completed. Removed {currentDelete} strings, imported {actualImportedCount} strings to language {lang}");
                }
                catch (OperationCanceledException)
                {
                    // User canceled
                    DialogResult = false;
                    App.Logger.Log("Import strings from chunk files canceled");
                }
            }, true, (task) => cancelToken.Cancel());

            GC.Collect();
            DialogResult = true;
            Close();
        }

        private void ShowDragOverlay()
        {
            MainStackPanel.Visibility = Visibility.Hidden;
            NormalFooterGrid.Visibility = Visibility.Hidden;
            DragOverlayGrid.Visibility = Visibility.Visible;
            DragFooterGrid.Visibility = Visibility.Visible;
        }

        private void HideDragOverlay()
        {
            MainStackPanel.Visibility = Visibility.Visible;
            NormalFooterGrid.Visibility = Visibility.Visible;
            DragOverlayGrid.Visibility = Visibility.Collapsed;
            DragFooterGrid.Visibility = Visibility.Collapsed;
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            ShowDragOverlay();
        }

        private void Window_DragLeave(object sender, DragEventArgs e)
        {
            HideDragOverlay();
        }

        private void BinaryDropZone_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Count() > 1)
                    App.Logger.LogWarning("Multiple files dropped. Only the first file will be opened.");

                if (files.Count() >= 1)
                {
                    BinaryFilePath = files[0];
                    Config.Add("LocalizedStrings_BinaryChunkImportPath", Path.GetDirectoryName(files[0])); // Compatible with FrostyOpenFileDialog key
                    OnPropertyChanged(nameof(CanImport));
                }
            }
            finally
            {
                HideDragOverlay();
            }
        }

        private void HistogramDropZone_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Count() > 1)
                    App.Logger.LogWarning("Multiple files dropped. Only the first file will be opened.");

                if (files.Count() >= 1)
                {
                    HistogramFilePath = files[0];
                    Config.Add("LocalizedStrings_HistogramChunkImportPath", Path.GetDirectoryName(files[0])); // Compatible with FrostyOpenFileDialog key
                    OnPropertyChanged(nameof(CanImport));
                }
            }
            finally
            {
                HideDragOverlay();
            }
        }

        private void BackFromDragButton_Click(object sender, RoutedEventArgs e)
        {
            HideDragOverlay();
        }
    }
}
