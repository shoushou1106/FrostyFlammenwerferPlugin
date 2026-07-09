using Frosty.Core;
using FsLocalizationPlugin.ViewModels;
using System.Windows;

namespace FsLocalizationPlugin.Windows
{
    public partial class ExportChunksWindow : FlammenwerferWindowBase
    {
        public ExportChunksWindow(Window owner)
        {
            Owner = owner;

            ExportChunksViewModel viewModel = new ExportChunksViewModel(LocalizedStringDatabase.Current as FsLocalizationStringDatabase);
            viewModel.CloseRequested += HandleCloseRequested;

            InitializeComponent();

            DataContext = viewModel;
        }
    }
}
