using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Windows;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;

#if FROSTY_107
using FrostySdk.Managers.Entries;
#endif

namespace FsLocalizationPlugin.ViewModels
{
    /// <summary>Backs the Export Chunks to Files window: re-encodes a language's chunks and writes them to files, without a full mod build.</summary>
    public sealed class ExportChunksViewModel : ViewModelBase
    {
        private LanguageExportOptions selectedLanguageOption;

        public ExportChunksViewModel(FsLocalizationStringDatabase database)
        {
            Database = database;

            List<string> modifiedLanguages = LocalizationHelper.GetLocalizedLanguages(modifiedOnly: true);
            HasModifiedLanguages = modifiedLanguages.Count > 0;

            LanguageOptions = new ObservableCollection<LanguageExportOptions>();
            foreach (string lang in modifiedLanguages)
            {
                LanguageExportOptions opt = new LanguageExportOptions(lang)
                {
                    // Keys match FrostySaveFileDialog's remembered-path keys.
                    BinaryPath = BuildDefaultPath(Config.Get("LocalizedStrings_BinaryChunkExportPath", string.Empty), lang, "BinaryStringsChunk"),
                    HistogramPath = BuildDefaultPath(Config.Get("LocalizedStrings_HistogramChunkExportPath", string.Empty), lang, "HistogramChunk"),
                };
                opt.PropertyChanged += (s, e) => OnPropertyChanged(nameof(CanExport));
                LanguageOptions.Add(opt);
            }

            if (LanguageOptions.Count > 0)
                SelectedLanguageOption = LanguageOptions[0];

            BrowseCommand = new RelayCommand(kind => Browse((string)kind));
            StartOverCommand = new RelayCommand(_ => StartOver(), _ => HasModifiedLanguages);
            ExportCommand = new RelayCommand(owner => Export((Window)owner), _ => CanExport);
            CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
        }

        /// <summary>Raised to close the window, with the DialogResult.</summary>
        public event Action<bool?> CloseRequested;

        private FsLocalizationStringDatabase Database { get; }

        /// <summary>Whether any language has modifications to export. False shows a placeholder instead of the form.</summary>
        public bool HasModifiedLanguages { get; }

        public ObservableCollection<LanguageExportOptions> LanguageOptions { get; }

        public LanguageExportOptions SelectedLanguageOption
        {
            get => selectedLanguageOption;
            set => SetProperty(ref selectedLanguageOption, value);
        }

        /// <summary>Whether at least one language has something selected to export.</summary>
        public bool CanExport => LanguageOptions.Any(opt => opt.HasSelection);

        public RelayCommand BrowseCommand { get; }
        public RelayCommand StartOverCommand { get; }
        public RelayCommand ExportCommand { get; }
        public RelayCommand CancelCommand { get; }

        private static string BuildDefaultPath(string basePath, string language, string suffix)
        {
            return string.IsNullOrWhiteSpace(basePath) ? string.Empty : $"{basePath}\\{language}_{suffix}.chunk";
        }

        private void Browse(string kind)
        {
            if (SelectedLanguageOption == null)
                return;

            FrostySaveFileDialog dialog = new FrostySaveFileDialog($"Export {SelectedLanguageOption.Language} {kind} Chunk",
                "Chunk file (*.chunk)|*.chunk|Binary file (*.bin)|*.bin|All files (*.*)|*.*",
                $"LocalizedStrings_{kind}Chunk", $"{SelectedLanguageOption.Language}_{kind}.chunk", true);

            if (!dialog.ShowDialog())
                return;

            if (kind == "Binary")
                SelectedLanguageOption.BinaryPath = dialog.FileName;
            else if (kind == "Histogram")
                SelectedLanguageOption.HistogramPath = dialog.FileName;

            OnPropertyChanged(nameof(CanExport));
        }

        private void StartOver()
        {
            foreach (LanguageExportOptions opt in LanguageOptions)
            {
                opt.ExportBinary = false;
                opt.BinaryPath = string.Empty;
                opt.ExportHistogram = false;
                opt.HistogramPath = string.Empty;
            }
            OnPropertyChanged(nameof(CanExport));
        }

        private void Export(Window owner)
        {
            CancellationTokenSource cancelToken = new CancellationTokenSource();
            Dictionary<string, LanguageExportOptions> languageLookup = LanguageOptions.ToDictionary(l => l.Language);
            bool cancelled = false;

            FrostyTaskWindow.Show(owner, "Exporting Chunks to Files", "Loading", task =>
            {
                string origLanguage = Config.Get("Language", "English", ConfigScope.Game);
                try
                {
                    int totalParts = LanguageOptions.Count;
                    int currentPart = 0;
                    cancelToken.Token.ThrowIfCancellationRequested();

                    foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx("LocalizationAsset"))
                    {
                        cancelToken.Token.ThrowIfCancellationRequested();

                        dynamic localizationAsset = App.AssetManager.GetEbx(entry).RootObject;

                        foreach (PointerRef pointer in localizationAsset.LocalizedTexts)
                        {
                            cancelToken.Token.ThrowIfCancellationRequested();

                            EbxAssetEntry textEntry = App.AssetManager.GetEbxEntry(pointer.External.FileGuid);
                            if (textEntry == null)
                                continue;

                            dynamic localizedText = App.AssetManager.GetEbx(textEntry).RootObject;

                            string lang = localizedText.Language.ToString().Replace("LanguageFormat_", "");
                            if (!languageLookup.TryGetValue(lang, out LanguageExportOptions langOption))
                                continue;
                            if (!(langOption.ExportBinary || langOption.ExportHistogram))
                                continue;

                            task.TaskLogger.Log("[{0}/{1}] Exporting {2}", ++currentPart, totalParts, lang);
                            LocalizationHelper.ReportProgress(task.TaskLogger, 0, 4, currentPart, totalParts);
                            cancelToken.Token.ThrowIfCancellationRequested();
                            Thread.Sleep(1);

                            Config.Add("Language", lang, ConfigScope.Game);
                            Config.Save();
                            Database.Initialize();
                            Dictionary<uint, string> modifiedData = new Dictionary<uint, string>();
                            foreach (uint id in Database.EnumerateModifiedStrings())
                                modifiedData[id] = Database.GetString(id);

                            LocalizationHelper.ReportProgress(task.TaskLogger, 1, 4, currentPart, totalParts);
                            cancelToken.Token.ThrowIfCancellationRequested();

                            ChunkAssetEntry histogramEntry = App.AssetManager.GetChunkEntry(localizedText.HistogramChunk);
                            ChunkAssetEntry stringChunkEntry = App.AssetManager.GetChunkEntry(localizedText.BinaryChunk);

                            Flammen.WriteAll(App.AssetManager, histogramEntry, stringChunkEntry, modifiedData, Array.Empty<uint>(),
                                out byte[] newHistogramData,
                                out byte[] newStringData);

                            LocalizationHelper.ReportProgress(task.TaskLogger, 2, 4, currentPart, totalParts);
                            cancelToken.Token.ThrowIfCancellationRequested();

                            if (langOption.ExportBinary)
                            {
                                using (NativeWriter writer = new NativeWriter(new FileStream(langOption.BinaryPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Delete)))
                                {
                                    writer.Write(newStringData);
                                }
                                App.Logger.Log("{0} binary chunk ({1}), file size: {2}, exported to {3}", lang, localizedText.BinaryChunk, (uint)newStringData.Length, langOption.BinaryPath);
                            }

                            LocalizationHelper.ReportProgress(task.TaskLogger, 3, 4, currentPart, totalParts);
                            cancelToken.Token.ThrowIfCancellationRequested();

                            if (langOption.ExportHistogram)
                            {
                                using (NativeWriter writer = new NativeWriter(new FileStream(langOption.HistogramPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Delete)))
                                {
                                    writer.Write(newHistogramData);
                                }
                                App.Logger.Log("{0} histogram chunk ({1}), file size: {2}, exported to {3}", lang, localizedText.HistogramChunk, (uint)newHistogramData.Length, langOption.HistogramPath);
                            }

                            LocalizationHelper.ReportProgress(task.TaskLogger, 4, 4, currentPart, totalParts);
                            cancelToken.Token.ThrowIfCancellationRequested();
                        }
                    }

                    App.Logger.Log("Chunks export to files completed");
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                    App.Logger.Log("Chunks export to files canceled");
                }
                finally
                {
                    // Restore the original language whether this finished, cancelled, or threw.
                    Config.Add("Language", origLanguage, ConfigScope.Game);
                    Config.Save();
                    Database.Initialize();
                }
            }, showCancelButton: true, cancelCallback: task => cancelToken.Cancel());

            // Show() blocks until the callback returns, so cancelled is safe to read here.
            CloseRequested?.Invoke(!cancelled);
        }
    }

    /// <summary>Per-language export choices: whether to export the binary/histogram chunk, and where to write each.</summary>
    public sealed class LanguageExportOptions : ViewModelBase
    {
        private bool exportBinary;
        private string binaryPath = string.Empty;
        private bool exportHistogram;
        private string histogramPath = string.Empty;

        public LanguageExportOptions(string language)
        {
            Language = language;
        }

        public string Language { get; }

        public bool ExportBinary
        {
            get => exportBinary;
            set => SetProperty(ref exportBinary, value);
        }

        public string BinaryPath
        {
            get => binaryPath;
            set => SetProperty(ref binaryPath, value ?? string.Empty);
        }

        public bool ExportHistogram
        {
            get => exportHistogram;
            set => SetProperty(ref exportHistogram, value);
        }

        public string HistogramPath
        {
            get => histogramPath;
            set => SetProperty(ref histogramPath, value ?? string.Empty);
        }

        /// <summary>Whether this language has anything selected to export.</summary>
        public bool HasSelection =>
            (ExportBinary && !string.IsNullOrWhiteSpace(BinaryPath)) ||
            (ExportHistogram && !string.IsNullOrWhiteSpace(HistogramPath));
    }
}
