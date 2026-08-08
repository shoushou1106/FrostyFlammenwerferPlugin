using Frosty.Core;
using FrostySdk.Managers;
using FsLocalizationPlugin.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

#if FROSTY_107
using FrostySdk.Managers.Entries;
#endif

namespace FsLocalizationPlugin.Views
{
    /// <summary>The ID Database tab content. All logic lives in IdDatabaseViewModel.</summary>
    public partial class IdDatabaseView : UserControl
    {
        private readonly IdDatabaseViewModel viewModel;
        private bool refExplorerInitialized;

        public IdDatabaseView()
        {
            InitializeComponent();

            viewModel = new IdDatabaseViewModel(LocalizedStringDatabase.Current as FsLocalizationStringDatabase);
            DataContext = viewModel;

            // FrostyAssetListView exposes no selection event of its own; the inner ListView's bubbles up.
            RefsList.AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(RefsList_SelectionChanged));

            // A ContextMenu lives outside the visual tree, so it never inherits the DataContext.
            RefsList.AssetContextMenu.DataContext = viewModel;

            Loaded += OnLoaded;
        }

        /// <summary>Called by IdDatabaseEditor when Frosty closes the tab.</summary>
        public void OnEditorClosed()
        {
            viewModel.Dispose();

            if (refExplorerInitialized)
            {
                RefExplorer.SelectionChanged -= RefExplorer_SelectionChanged;
                RefExplorer.ItemsSource = null;
                refExplorerInitialized = false;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            viewModel.OnFirstLoad();
        }

        private void FilterBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                viewModel.ApplyFilterNow();
        }

        /// <summary>
        /// Escape closes a popup and hands focus back to the button that opened it. A Popup does not
        /// do this on its own, which leaves keyboard users stuck inside it.
        /// </summary>
        private void Popup_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
                return;

            ToggleButton owner = sender == AddIdPopup ? AddIdToggleButton : AddRefToggleButton;
            owner.IsChecked = false;
            owner.Focus();
            e.Handled = true;
        }

        private void RefsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            viewModel.HasRefSelection = RefsList.SelectedItem != null;
        }

        private void RefsList_SelectedAssetDoubleClick(object sender, RoutedEventArgs e)
        {
            if (RefsList.SelectedItem is EbxAssetEntry entry)
                App.EditorWindow.OpenAsset(entry);
        }

        private void RefsList_FindAsset_Click(object sender, RoutedEventArgs e)
        {
            if (RefsList.SelectedItem is EbxAssetEntry entry)
                App.EditorWindow.DataExplorer.SelectAsset(entry);
        }

        private void RemoveRef_Click(object sender, RoutedEventArgs e)
        {
            if (RefsList.SelectedItem is EbxAssetEntry entry)
                viewModel.RemoveReference(entry);
        }

        private void AddRefPopup_Opened(object sender, System.EventArgs e)
        {
            // Same pattern as MeshSetPlugin's FrostySkeletonControl. A FrostyDataExplorer inside a
            // popup acts as the ebx picker, and selecting an asset commits and closes the popup.
            if (refExplorerInitialized)
                return;
            refExplorerInitialized = true;

            RefExplorer.ItemsSource = App.AssetManager.EnumerateEbx();
            RefExplorer.SelectionChanged += RefExplorer_SelectionChanged;
        }

        private void RefExplorer_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (!(RefExplorer.SelectedAsset is EbxAssetEntry entry))
                return;

            viewModel.AddReference(entry);
            AddRefToggleButton.IsChecked = false;

            // Deselect so the same asset can be picked again next time the popup opens.
            RefExplorer.SelectAsset(null);
        }
    }
}
