using Frosty.Core;
using FsLocalizationPlugin.ViewModels;
using System.Windows;

namespace FsLocalizationPlugin.Windows
{
    public partial class CheckCompatibilityWindow : FlammenwerferWindowBase
    {
        // private readonly CheckCompatibilityViewModel viewModel;

        /// <param name="closeAfterConfirm">Close after action.</param>
        public CheckCompatibilityWindow(Window owner)
        {
            Owner = owner;

            //viewModel = new CheckCompatibilityWindow(LocalizedStringDatabase.Current as FsLocalizationStringDatabase);
            //viewModel.CloseRequested += result =>
            //{
            //    DialogResult = result;
            //    Close();
            //};

            InitializeComponent();

            //DataContext = viewModel;

            //Closing += (s, e) => viewModel.RestoreOriginalLanguage();
        }
    }
}
