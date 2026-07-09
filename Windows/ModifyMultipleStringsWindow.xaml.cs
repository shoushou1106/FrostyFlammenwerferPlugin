using Frosty.Core;
using FsLocalizationPlugin.ViewModels;
using System.Windows;

namespace FsLocalizationPlugin.Windows
{
    public partial class ModifyMultipleStringsWindow : FlammenwerferWindowBase
    {
        private readonly ModifyMultipleStringsViewModel viewModel;

        public ModifyMultipleStringsWindow(Window owner)
        {
            Owner = owner;

            viewModel = new ModifyMultipleStringsViewModel(LocalizedStringDatabase.Current as FsLocalizationStringDatabase);
            viewModel.CloseRequested += HandleCloseRequested;

            InitializeComponent();

            DataContext = viewModel;

            Closing += (s, e) => viewModel.RestoreOriginalLanguage();
        }
    }
}
