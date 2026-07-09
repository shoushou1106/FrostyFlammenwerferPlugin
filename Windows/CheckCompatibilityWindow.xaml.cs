using Frosty.Core;
using FsLocalizationPlugin.ViewModels;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Shell;

namespace FsLocalizationPlugin.Windows
{
    public partial class CheckCompatibilityWindow : FlammenwerferWindowBase
    {
        private readonly CheckCompatibilityViewModel viewModel;
        private TaskbarItemInfo boundTaskbar;

        public CheckCompatibilityWindow(Window owner)
        {
            Owner = owner;

            viewModel = new CheckCompatibilityViewModel(LocalizedStringDatabase.Current as FsLocalizationStringDatabase);
            viewModel.CloseRequested += HandleCloseRequested;

            InitializeComponent();

            DataContext = viewModel;

            Loaded += OnLoaded;
            Closing += OnClosing;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;

            // Mirror the refresh progress onto this window's own taskbar icon (not the main window's:
            // FrostyTaskWindow does that, but this window can outlive a single task and stay open).
            if (TaskbarItemInfo == null)
                TaskbarItemInfo = new TaskbarItemInfo();
            boundTaskbar = TaskbarItemInfo;

            BindingOperations.SetBinding(boundTaskbar, TaskbarItemInfo.ProgressStateProperty,
                new Binding(nameof(CheckCompatibilityViewModel.TaskbarState)) { Source = viewModel });
            BindingOperations.SetBinding(boundTaskbar, TaskbarItemInfo.ProgressValueProperty,
                new Binding("Overall.Progress") { Source = viewModel, Converter = new PercentToFractionConverter() });

            // Auto-run the first check so the user doesn't have to click Refresh on open.
            viewModel.StartInitialRefresh();
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            viewModel.CancelRefresh();

            if (boundTaskbar != null)
            {
                BindingOperations.ClearBinding(boundTaskbar, TaskbarItemInfo.ProgressStateProperty);
                BindingOperations.ClearBinding(boundTaskbar, TaskbarItemInfo.ProgressValueProperty);
                boundTaskbar.ProgressState = TaskbarItemProgressState.None;
                boundTaskbar = null;
            }
        }

        /// <summary>Overall.Progress is 0-100; TaskbarItemInfo.ProgressValue wants 0.0-1.0.</summary>
        private sealed class PercentToFractionConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return value is double d ? d / 100.0 : 0.0;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotSupportedException();
            }
        }
    }
}
