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
    public partial class ExportChunksToFilesWindow : FrostyDockableWindow, INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Represents per-language export options/bindings
        public class LanguageExportOptions : INotifyPropertyChanged
        {
            private string _language;
            private bool _exportBinary;
            private string _binaryPath;
            private bool _exportHistogram;
            private string _histogramPath;

            public string Language
            {
                get { return _language; }
                set { if (_language != value) { _language = value; OnPropertyChanged(nameof(Language)); } }
            }

            public bool ExportBinary
            {
                get { return _exportBinary; }
                set { if (_exportBinary != value) { _exportBinary = value; OnPropertyChanged(nameof(ExportBinary)); } }
            }

            public string BinaryPath
            {
                get { return _binaryPath; }
                set { if (_binaryPath != value) { _binaryPath = value; OnPropertyChanged(nameof(BinaryPath)); } }
            }

            public bool ExportHistogram
            {
                get { return _exportHistogram; }
                set { if (_exportHistogram != value) { _exportHistogram = value; OnPropertyChanged(nameof(ExportHistogram)); } }
            }

            public string HistogramPath
            {
                get { return _histogramPath; }
                set { if (_histogramPath != value) { _histogramPath = value; OnPropertyChanged(nameof(HistogramPath)); } }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public ObservableCollection<LanguageExportOptions> LanguageOptions { get; private set; }

        private LanguageExportOptions _selectedLanguageOption;
        public LanguageExportOptions SelectedLanguageOption
        {
            get { return _selectedLanguageOption; }
            set
            {
                if (_selectedLanguageOption != value)
                {
                    _selectedLanguageOption = value;
                    OnPropertyChanged(nameof(SelectedLanguageOption));
                    OnPropertyChanged(nameof(CanExport));
                }
            }
        }

        public bool CanExport
        {
            get
            {
                if (LanguageOptions == null)
                    return false;
                foreach (var opt in LanguageOptions)
                {
                    if ((opt.ExportBinary && !string.IsNullOrWhiteSpace(opt.BinaryPath)) ||
                        (opt.ExportHistogram && !string.IsNullOrWhiteSpace(opt.HistogramPath)))
                        return true;
                }
                return false;
            }
        }

        public ExportChunksToFilesWindow(Window owner)
        {
            Owner = owner;

            InitializeComponent();

            Left = Owner.Left + (Owner.Width / 2.0) - (ActualWidth / 2.0);
            Top = Owner.Top + (Owner.Height / 2.0) - (ActualHeight / 2.0);

            this.DataContext = this;

            Dispatcher.UnhandledException += UnhandledException;

            LanguageOptions = new ObservableCollection<LanguageExportOptions>();

            foreach (var lang in GetModifiedLocalizedLanguages())
            {
                if (lang == "NoModifiedLanguageFound")
                {
                    MainStackPanel.Visibility = Visibility.Collapsed;
                    NoModifiedLanguageFoundLable.Visibility = Visibility.Visible;
                    StartOverButton.IsEnabled = false;
                }

                var opt = new LanguageExportOptions
                {
                    Language = lang,
                    ExportBinary = false,
                    BinaryPath = Config.Get($"LocalizedStrings_BinaryChunkExportPath", string.Empty), // Compatible with FrostySaveFileDialog key
                    ExportHistogram = false,
                    HistogramPath = Config.Get($"LocalizedStrings_HistogramChunkExportPath", string.Empty) // Compatible with FrostySaveFileDialog key
                };

                if (!string.IsNullOrWhiteSpace(opt.BinaryPath))
                {
                    opt.BinaryPath += $"\\{lang}_BinaryStringsChunk.chunk";
                }
                if (!string.IsNullOrWhiteSpace(opt.HistogramPath))
                {
                    opt.HistogramPath += $"\\{lang}_HistogramChunk.chunk";
                }

                // Track changes to re-evaluate CanExport
                opt.PropertyChanged += (s, e) => OnPropertyChanged(nameof(CanExport));
                LanguageOptions.Add(opt);
            }

            if (LanguageOptions.Count > 0)
            {
                SelectedLanguageOption = LanguageOptions[0];
            }
        }

        private List<string> GetModifiedLocalizedLanguages()
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

                    // Only Modified
                    if (textEntry == null || !textEntry.IsModified)
                        continue;

                    // read localized text asset
                    dynamic localizedText = App.AssetManager.GetEbx(textEntry).RootObject;

                    languages.Add(localizedText.Language.ToString().Replace("LanguageFormat_", ""));
                }
            }

            if (!languages.Any())
            {
                languages.Add("NoModifiedLanguageFound");
            }

            return languages.ToList();
        }

        private void UnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            FrostyExceptionBox.Show(e.Exception, "Export Chunks to Files - Flammenwerfer");
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
            if (SelectedLanguageOption == null)
                return;

            var button = sender as Button;
            var kind = button != null ? (button.Tag as string) : null; // "Binary" or "Histogram"

            FrostySaveFileDialog saveFileDialog = new FrostySaveFileDialog($"Export {SelectedLanguageOption.Language} {kind} Chunk",
                "Chunk file (*.chunk)|*.chunk|Binary file (*.bin)|*.bin|All files (*.*)|*.*",
                $"LocalizedStrings_{kind}Chunk", $"{SelectedLanguageOption.Language}_{kind}.chunk", true);
            if (saveFileDialog.ShowDialog())
            {
                if (kind == "Binary")
                {
                    SelectedLanguageOption.BinaryPath = saveFileDialog.FileName;
                }
                else if (kind == "Histogram")
                {
                    SelectedLanguageOption.HistogramPath = saveFileDialog.FileName;
                }
                OnPropertyChanged(nameof(CanExport));
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
            if (LanguageOptions == null)
                return;

            foreach (var opt in LanguageOptions)
            {
                opt.ExportBinary = false;
                opt.BinaryPath = string.Empty;
                opt.ExportHistogram = false;
                opt.HistogramPath = string.Empty;
            }
            OnPropertyChanged(nameof(CanExport));
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            CancellationTokenSource cancelToken = new CancellationTokenSource();
            var languageLookup = LanguageOptions.ToDictionary(l => l.Language);
            FrostyTaskWindow.Show(this, "Exporting Chunks to Files", "Loading", (task) =>
            {
                try
                {
                    int totalParts = LanguageOptions.Count();
                    int currentPart = 0;
                    cancelToken.Token.ThrowIfCancellationRequested();

                    string origLanguage = Config.Get<string>("Language", "English", ConfigScope.Game);
                    ILocalizedStringDatabase db = LocalizedStringDatabase.Current;

                    foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx("LocalizationAsset"))
                    {
                        cancelToken.Token.ThrowIfCancellationRequested();

                        // read master localization asset
                        dynamic localizationAsset = App.AssetManager.GetEbx(entry).RootObject;

                        // iterate through localized texts
                        foreach (PointerRef pointer in localizationAsset.LocalizedTexts)
                        {
                            cancelToken.Token.ThrowIfCancellationRequested();

                            EbxAssetEntry textEntry = App.AssetManager.GetEbxEntry(pointer.External.FileGuid);

                            if (textEntry == null)
                                continue;

                            // read localized text asset
                            dynamic localizedText = App.AssetManager.GetEbx(textEntry).RootObject;

                            string lang = localizedText.Language.ToString().Replace("LanguageFormat_", "");
                            if (!languageLookup.TryGetValue(lang, out var langOption)) continue;
                            if (!(langOption.ExportBinary || langOption.ExportHistogram)) continue; // Skip if user didn't select either

                            task.TaskLogger.Log($"[{++currentPart}/{totalParts}] Exporting {lang}");
                            ReportProgress(task.TaskLogger, 0, 4, currentPart, totalParts);
                            cancelToken.Token.ThrowIfCancellationRequested();
                            Thread.Sleep(1);

                            Config.Add("Language", lang, ConfigScope.Game);
                            Config.Save();
                            db.Initialize();
                            Dictionary<uint, string> modifiedData = new Dictionary<uint, string>();
                            foreach (var id in db.EnumerateModifiedStrings())
                            {
                                modifiedData[id] = db.GetString(id);
                            }

                            ReportProgress(task.TaskLogger, 1, 4, currentPart, totalParts);
                            cancelToken.Token.ThrowIfCancellationRequested();

                            ChunkAssetEntry histogramEntry = App.AssetManager.GetChunkEntry(localizedText.HistogramChunk);
                            ChunkAssetEntry stringChunkEntry = App.AssetManager.GetChunkEntry(localizedText.BinaryChunk);

                            Flammen.Flammen.WriteAll(App.AssetManager, histogramEntry, stringChunkEntry, modifiedData, new List<uint>(),
                                out byte[] newHistogramData,
                                out byte[] newStringData);

                            ReportProgress(task.TaskLogger, 2, 4, currentPart, totalParts);
                            cancelToken.Token.ThrowIfCancellationRequested();

                            if (langOption.ExportBinary)
                            {
                                using (NativeWriter writer = new NativeWriter(new FileStream(langOption.BinaryPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Delete)))
                                {
                                    writer.Write(newStringData);
                                }
                                App.Logger.Log($"{lang} binary chunk ({localizedText.BinaryChunk.ToString()}), file size: {(uint)newStringData.Length}, exported to {langOption.BinaryPath}");
                            }

                            ReportProgress(task.TaskLogger, 3, 4, currentPart, totalParts);
                            cancelToken.Token.ThrowIfCancellationRequested();

                            if (langOption.ExportHistogram)
                            {
                                using (NativeWriter writer = new NativeWriter(new FileStream(langOption.HistogramPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Delete)))
                                {
                                    writer.Write(newHistogramData);
                                }
                                App.Logger.Log($"{lang} histogram chunk ({localizedText.HistogramChunk.ToString()}), file size: {(uint)newHistogramData.Length}, exported to {langOption.HistogramPath}");
                            }

                            ReportProgress(task.TaskLogger, 4, 4, currentPart, totalParts);
                            cancelToken.Token.ThrowIfCancellationRequested();
                        }
                    }

                    // Switch back to the previously backed-up language
                    Config.Add("Language", origLanguage, ConfigScope.Game);
                    Config.Save();
                    db.Initialize();

                    App.Logger.Log("Chunks export to files completed");
                }
                catch (OperationCanceledException)
                {
                    // User canceled
                    DialogResult = false;
                    App.Logger.Log("Chunks export to files canceled");
                }
            }, true, (task) => cancelToken.Cancel());

            GC.Collect();
            DialogResult = true;
            Close();
        }

    }
}
