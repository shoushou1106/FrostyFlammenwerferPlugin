using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;

namespace FsLocalizationPlugin.ViewModels
{
    /// <summary>
    /// Backs the Import Chunks from Files window - reads a histogram/strings-binary
    /// chunk pair exported by <see cref="ExportChunksViewModel"/> (or any compatible
    /// tool) and merges them into the currently selected language.
    /// </summary>
    public sealed class ImportChunksViewModel : LanguageAwareViewModelBase
    {
        private string binaryFilePath = string.Empty;
        private string histogramFilePath = string.Empty;
        private bool noImportEmptyOrNull;
        private bool deleteExistingStrings;
        private bool noOverwriteSameStrings;

        public ImportChunksViewModel(FsLocalizationStringDatabase database) : base(database)
        {
            BrowseCommand = new RelayCommand(kind => Browse((string)kind));
            StartOverCommand = new RelayCommand(_ => StartOver());
            ImportCommand = new RelayCommand(owner => Import((Window)owner), _ => CanImport);
            CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
        }

        /// <summary>Raised when the window should close, with the DialogResult to use.</summary>
        public event Action<bool?> CloseRequested;

        public string BinaryFilePath
        {
            get => binaryFilePath;
            set
            {
                if (SetProperty(ref binaryFilePath, value ?? string.Empty))
                    OnPropertyChanged(nameof(CanImport));
            }
        }

        public string HistogramFilePath
        {
            get => histogramFilePath;
            set
            {
                if (SetProperty(ref histogramFilePath, value ?? string.Empty))
                    OnPropertyChanged(nameof(CanImport));
            }
        }

        public bool NoImportEmptyOrNull
        {
            get => noImportEmptyOrNull;
            set => SetProperty(ref noImportEmptyOrNull, value);
        }

        public bool DeleteExistingStrings
        {
            get => deleteExistingStrings;
            set => SetProperty(ref deleteExistingStrings, value);
        }

        public bool NoOverwriteSameStrings
        {
            get => noOverwriteSameStrings;
            set => SetProperty(ref noOverwriteSameStrings, value);
        }

        public bool CanImport => !string.IsNullOrEmpty(BinaryFilePath) && !string.IsNullOrEmpty(HistogramFilePath);

        public RelayCommand BrowseCommand { get; }
        public RelayCommand StartOverCommand { get; }
        public RelayCommand ImportCommand { get; }
        public RelayCommand CancelCommand { get; }

        private void Browse(string kind)
        {
            FrostyOpenFileDialog dialog = new FrostyOpenFileDialog($"Import {kind} Chunk",
                "Chunk file (*.chunk)|*.chunk|Binary file (*.bin)|*.bin|All files (*.*)|*.*",
                $"LocalizedStrings_{kind}Chunk");

            if (!dialog.ShowDialog())
                return;

            if (kind == "Binary")
                BinaryFilePath = dialog.FileName;
            else if (kind == "Histogram")
                HistogramFilePath = dialog.FileName;
        }

        private void StartOver()
        {
            BinaryFilePath = string.Empty;
            HistogramFilePath = string.Empty;
        }

        private void Import(Window owner)
        {
            if (!File.Exists(HistogramFilePath) && !File.Exists(BinaryFilePath))
            {
                FrostyMessageBox.Show("Histogram file and Binary Strings file not found", "Import Chunks from Files - Flammenwerfer", MessageBoxButton.OK);
                return;
            }
            if (!File.Exists(HistogramFilePath))
            {
                FrostyMessageBox.Show("Histogram file not found", "Import Chunks from Files - Flammenwerfer", MessageBoxButton.OK);
                return;
            }
            if (!File.Exists(BinaryFilePath))
            {
                FrostyMessageBox.Show("Binary Strings file not found", "Import Chunks from Files - Flammenwerfer", MessageBoxButton.OK);
                return;
            }

            CancellationTokenSource cancelToken = new CancellationTokenSource();
            int deletedCount = 0;
            int importedCount = 0;
            bool cancelled = false;

            FrostyTaskWindow.Show(owner, "Import Chunks from Files", "Loading", task =>
            {
                try
                {
                    const int totalParts = 3;
                    cancelToken.Token.ThrowIfCancellationRequested();

                    if (DeleteExistingStrings)
                    {
                        task.TaskLogger.Log("[1/3] Deleting all existing strings");
                        LocalizationHelper.ReportProgress(task.TaskLogger, 0, 1, currentPart: 1, totalParts);
                        cancelToken.Token.ThrowIfCancellationRequested();
                        Thread.Sleep(1);
                        App.Logger.Log("Tip: removed strings can be restored with Revert (Modify Multiple Strings).");

                        // Snapshot first: EnumerateStrings() is a lazy iterator over the
                        // same dictionary RemoveString mutates, so removing while iterating
                        // it directly throws "collection was modified".
                        List<uint> existingStrings = Database.EnumerateStrings().ToList();
                        int totalDelete = existingStrings.Count;
                        foreach (uint id in existingStrings)
                        {
                            Database.RemoveString(id);
                            deletedCount++;
                            cancelToken.Token.ThrowIfCancellationRequested();
                            LocalizationHelper.ReportProgress(task.TaskLogger, deletedCount, totalDelete, currentPart: 1, totalParts);
                        }
                    }

                    task.TaskLogger.Log("[2/3] Reading Chunks");
                    LocalizationHelper.ReportProgress(task.TaskLogger, 0, 1, currentPart: 2, totalParts);
                    cancelToken.Token.ThrowIfCancellationRequested();
                    Thread.Sleep(1);

                    Dictionary<uint, string> dictionary;
                    using (FileStream histogram = File.OpenRead(HistogramFilePath))
                    using (FileStream binary = File.OpenRead(BinaryFilePath))
                    {
                        dictionary = Flammen.ReadStrings(histogram, binary);
                    }

                    task.TaskLogger.Log("[3/3] Importing Strings");
                    LocalizationHelper.ReportProgress(task.TaskLogger, 0, 1, currentPart: 3, totalParts);
                    cancelToken.Token.ThrowIfCancellationRequested();
                    Thread.Sleep(1);

                    int totalCount = dictionary.Count;
                    int current = 0;
                    foreach (KeyValuePair<uint, string> kvp in dictionary)
                    {
                        // Was hardcoded to currentPart: 2 (the "reading chunks" part) here -
                        // this is the "[3/3] Importing" part, so progress needs to say 3.
                        LocalizationHelper.ReportProgress(task.TaskLogger, ++current, totalCount, currentPart: 3, totalParts);
                        cancelToken.Token.ThrowIfCancellationRequested();

                        if (NoImportEmptyOrNull && string.IsNullOrEmpty(kvp.Value))
                            continue;
                        if (NoOverwriteSameStrings && Database.GetString(kvp.Key) == kvp.Value)
                            continue;

                        Database.SetString(kvp.Key, kvp.Value);
                        importedCount++;
                    }
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                }
            }, showCancelButton: true, cancelCallback: task => cancelToken.Cancel());

            // SelectedLanguage is engine-defined and safe in practice, but passed as a
            // format argument anyway for consistency with the other logging here.
            string cancelSuffix = cancelled ? " (cancelled)" : "";
            if (deletedCount == 0)
                App.Logger.Log("Import chunks from files completed. Imported {0} strings to language {1}{2}", importedCount, SelectedLanguage, cancelSuffix);
            else
                App.Logger.Log("Import chunks from files completed. Removed {0} strings, imported {1} strings to language {2}{3}", deletedCount, importedCount, SelectedLanguage, cancelSuffix);

            CloseRequested?.Invoke(!cancelled);
        }
    }
}
