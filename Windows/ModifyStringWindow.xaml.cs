using Frosty.Core;
using FsLocalizationPlugin.ViewModels;
using System.Windows;

namespace FsLocalizationPlugin.Windows
{
    public partial class ModifyStringWindow : FlammenwerferWindowBase
    {
        private readonly ModifyStringViewModel viewModel;

        public ModifyStringWindow(Window owner)
        {
            Owner = owner;

            viewModel = new ModifyStringViewModel(LocalizedStringDatabase.Current as FsLocalizationStringDatabase);
            viewModel.CloseRequested += HandleCloseRequested;

            InitializeComponent();

            DataContext = viewModel;

            Closing += (s, e) => viewModel.RestoreOriginalLanguage();
        }
    }
}
