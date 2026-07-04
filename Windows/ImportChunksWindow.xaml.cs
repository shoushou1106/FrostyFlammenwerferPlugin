using Frosty.Core;
using FsLocalizationPlugin.ViewModels;
using System.IO;
using System.Windows;

namespace FsLocalizationPlugin.Windows
{
    public partial class ImportChunksWindow : FlammenwerferWindowBase
    {
        private readonly ImportChunksViewModel viewModel;

        public ImportChunksWindow(Window owner)
        {
            Owner = owner;

            viewModel = new ImportChunksViewModel(LocalizedStringDatabase.Current as FsLocalizationStringDatabase);
            viewModel.CloseRequested += result =>
            {
                DialogResult = result;
                Close();
            };

            InitializeComponent();

            DataContext = viewModel;

            Closing += (s, e) => viewModel.RestoreOriginalLanguage();
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
                if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                    return;

                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 1)
                    App.Logger.LogWarning("Multiple files dropped. Only the first file will be opened.");

                if (files.Length >= 1)
                {
                    viewModel.BinaryFilePath = files[0];
                    Config.Add("LocalizedStrings_BinaryChunkImportPath", Path.GetDirectoryName(files[0])); // Compatible with FrostyOpenFileDialog key
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
                if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                    return;

                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 1)
                    App.Logger.LogWarning("Multiple files dropped. Only the first file will be opened.");

                if (files.Length >= 1)
                {
                    viewModel.HistogramFilePath = files[0];
                    Config.Add("LocalizedStrings_HistogramChunkImportPath", Path.GetDirectoryName(files[0])); // Compatible with FrostyOpenFileDialog key
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
