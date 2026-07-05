using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Windows;
using FsLocalizationPlugin.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;

namespace FsLocalizationPlugin.ViewModels
{
    /// <summary>Backs the Import Chunks from Files window: reads a histogram/strings-binary chunk pair and merges it into the selected language.</summary>
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

        /// <summary>Raised to close the window, with the DialogResult.</summary>
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
                FrostyMessageBox.Show("Materials missing! Histogram file and Binary Strings file not found", "Import Chunks from Files - Flammenwerfer", MessageBoxButton.OK);
                return;
            }
            if (!File.Exists(HistogramFilePath))
            {
                FrostyMessageBox.Show("Material missing! Histogram file not found", "Import Chunks from Files - Flammenwerfer", MessageBoxButton.OK);
                return;
            }
            if (!File.Exists(BinaryFilePath))
            {
                FrostyMessageBox.Show("Material missing! Binary Strings file not found", "Import Chunks from Files - Flammenwerfer", MessageBoxButton.OK);
                return;
            }

            CancellationTokenSource cancelToken = new CancellationTokenSource();
            int deletedCount = 0;
            int importedCount = 0;
            bool cancelled = false;

            // Tracks every id this run touches, so a cancel can put the database back exactly how it was
            HashSet<uint> touchedIds = new HashSet<uint>();
            Dictionary<uint, string> priorModifiedValues = new Dictionary<uint, string>();
            List<uint> priorUnmodifiedIds = new List<uint>();
            List<uint> priorRemovedIds = new List<uint>();

            void RecordPriorState(uint id)
            {
                if (!touchedIds.Add(id))
                    return;

                if (Database.IsStringRemoved(id))
                    priorRemovedIds.Add(id);
                else if (Database.isStringEdited(id))
                    priorModifiedValues[id] = Database.GetString(id);
                else
                    priorUnmodifiedIds.Add(id);
            }

            FrostyTaskWindow.Show(owner, "Import Chunks from Files", "Loading", task =>
            {
                try
                {
                    int totalParts = DeleteExistingStrings ? 3 : 2;
                    int currentPart = 0;
                    cancelToken.Token.ThrowIfCancellationRequested();

                    if (DeleteExistingStrings)
                    {
                        currentPart++;
                        task.TaskLogger.Log("[{0}/{1}] Removing all existing strings", currentPart, totalParts);
                        LocalizationHelper.ReportProgress(task.TaskLogger, 0, 1, currentPart, totalParts);
                        cancelToken.Token.ThrowIfCancellationRequested();
                        Thread.Sleep(1);

                        // Snapshot first. Removing while iterating this directly throws "collection was modified".
                        List<uint> existingStrings = Database.EnumerateStrings().ToList();
                        int totalDelete = existingStrings.Count;
                        foreach (uint id in existingStrings)
                        {
                            RecordPriorState(id);
                            Database.RemoveString(id);
                            deletedCount++;
                            cancelToken.Token.ThrowIfCancellationRequested();
                            LocalizationHelper.ReportProgress(task.TaskLogger, deletedCount, totalDelete, currentPart, totalParts);
                        }
                    }

                    currentPart++;
                    task.TaskLogger.Log("[{0}/{1}] Reading Chunks", currentPart, totalParts);
                    LocalizationHelper.ReportProgress(task.TaskLogger, 0, 1, currentPart, totalParts);
                    cancelToken.Token.ThrowIfCancellationRequested();
                    Thread.Sleep(1);

                    Dictionary<uint, string> dictionary;
                    using (FileStream histogram = File.OpenRead(HistogramFilePath))
                    using (FileStream binary = File.OpenRead(BinaryFilePath))
                    {
                        dictionary = Flammen.ReadStrings(histogram, binary);
                    }

                    currentPart++;
                    task.TaskLogger.Log("[{0}/{1}] Importing Strings", currentPart, totalParts);
                    LocalizationHelper.ReportProgress(task.TaskLogger, 0, 1, currentPart, totalParts);
                    cancelToken.Token.ThrowIfCancellationRequested();
                    Thread.Sleep(1);

                    int totalCount = dictionary.Count;
                    int current = 0;
                    foreach (KeyValuePair<uint, string> kvp in dictionary)
                    {
                        LocalizationHelper.ReportProgress(task.TaskLogger, ++current, totalCount, currentPart, totalParts);
                        cancelToken.Token.ThrowIfCancellationRequested();

                        if (NoImportEmptyOrNull && string.IsNullOrEmpty(kvp.Value))
                            continue;
                        if (NoOverwriteSameStrings && Database.GetString(kvp.Key) == kvp.Value)
                            continue;

                        RecordPriorState(kvp.Key);
                        Database.SetString(kvp.Key, kvp.Value);
                        importedCount++;
                    }
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                }
            }, showCancelButton: true, cancelCallback: task => cancelToken.Cancel());

            if (cancelled)
            {
                if (touchedIds.Count == 0)
                {
                    App.Logger.Log("Imbuition interrupted! Import chunks from files canceled. Nothing touched yet");
                }
                else if (FrostyMessageBox.Show($"Temporal Ward Activated! The cast was interrupted, but {touchedIds.Count} string(s) were already altered. Restore them to how they were before?", "Import Chunks from Files - Flammenwerfer", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    DebugLogHelper.Log("ImportChunksViewModel.Import", "Restoring {0} prior value(s), {1} database revert(s), {2} re-removal(s)", priorModifiedValues.Count, priorUnmodifiedIds.Count, priorRemovedIds.Count);

                    FrostyTaskWindow.Show(owner, "Invoking Temporal Ward", "Restoring Strings", restoreTask =>
                    {
                        int restored = 0;
                        foreach (KeyValuePair<uint, string> kvp in priorModifiedValues)
                        {
                            Database.SetString(kvp.Key, kvp.Value);
                            LocalizationHelper.ReportProgress(restoreTask.TaskLogger, ++restored, touchedIds.Count);
                        }
                        foreach (uint id in priorUnmodifiedIds)
                        {
                            Database.RevertString(id);
                            LocalizationHelper.ReportProgress(restoreTask.TaskLogger, ++restored, touchedIds.Count);
                        }
                        foreach (uint id in priorRemovedIds)
                        {
                            Database.RemoveString(id);
                            LocalizationHelper.ReportProgress(restoreTask.TaskLogger, ++restored, touchedIds.Count);
                        }
                    });

                    App.Logger.Log("Imbuition interrupted, but the Temporal Ward was activated! Import chunks from files canceled. Restored {0} change(s) ({1} removed, {2} modified).", touchedIds.Count, deletedCount, importedCount);
                }
                else
                {
                    App.Logger.Log("Imbuition interrupted, and you rejected the Temporal Ward! Import chunks from files canceled. {0} change(s) were kept ({1} removed, {2} modified). You may want to revert the database.", touchedIds.Count, deletedCount, importedCount);
                }
            }
            else
            {
                if (deletedCount == 0)
                    App.Logger.Log("Imbuition successful! Import chunks from files completed. Imported {0} strings to language {1}", importedCount, SelectedLanguage);
                else
                    App.Logger.Log("Imbuition successful! Import chunks from files completed. Removed {0} strings, imported {1} strings to language {2}", deletedCount, importedCount, SelectedLanguage);
            }

            CloseRequested?.Invoke(!cancelled);
        }
    }
}
