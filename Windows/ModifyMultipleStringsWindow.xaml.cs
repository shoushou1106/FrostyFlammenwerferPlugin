using Frosty.Core;
using FsLocalizationPlugin.ViewModels;
using System.Windows;

namespace FsLocalizationPlugin.Windows
{
    public partial class ModifyMultipleStringsWindow : FlammenwerferWindowBase
    {
        private readonly ModifyMultipleStringsViewModel viewModel;

        /// <param name="owner">The window to center over and block input to.</param>
        /// <param name="closeAfterConfirm">
        /// Whether Replace/Revert/Remove should close the window once they've acted. The
        /// Tools &gt; Flammenwerfer menu entry passes <see langword="false"/> for a
        /// stay-open, run-several-passes experience instead.
        /// </param>
        public ModifyMultipleStringsWindow(Window owner, bool closeAfterConfirm = true)
        {
            Owner = owner;

            viewModel = new ModifyMultipleStringsViewModel(LocalizedStringDatabase.Current as FsLocalizationStringDatabase, closeAfterConfirm);
            viewModel.CloseRequested += result =>
            {
                DialogResult = result;
                Close();
            };

            InitializeComponent();

            DataContext = viewModel;

            Closing += (s, e) => viewModel.RestoreOriginalLanguage();
        }
    }
}
