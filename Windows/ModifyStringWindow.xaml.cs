using Frosty.Core;
using FsLocalizationPlugin.ViewModels;
using System.Windows;

namespace FsLocalizationPlugin.Windows
{
    public partial class ModifyStringWindow : FlammenwerferWindowBase
    {
        private readonly ModifyStringViewModel viewModel;

        /// <param name="owner">The window to center over and block input to.</param>
        /// <param name="closeAfterConfirm">
        /// Whether Modify/Revert/Remove should close the window once they've acted. The
        /// Tools &gt; Flammenwerfer menu entry passes <see langword="false"/> for a
        /// stay-open, batch-editing experience; everywhere else uses the default.
        /// </param>
        public ModifyStringWindow(Window owner, bool closeAfterConfirm = true)
        {
            Owner = owner;

            viewModel = new ModifyStringViewModel(LocalizedStringDatabase.Current as FsLocalizationStringDatabase, closeAfterConfirm);
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
