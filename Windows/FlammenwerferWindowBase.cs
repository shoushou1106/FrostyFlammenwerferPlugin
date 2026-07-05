using Frosty.Controls;
using Frosty.Core.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace FsLocalizationPlugin.Windows
{
    /// <summary>Common base for every Flammenwerfer dialog: centers on the owner, turns unhandled exceptions into an error box instead of a crash.</summary>
    /// <remarks>
    /// Not abstract: every window's XAML root is this type, and WPF's markup compiler
    /// rejects an abstract type as a named root (MC3054). DefaultStyleKey still resolves
    /// to FrostyDockableWindow via property-metadata inheritance, so the theme applies.
    /// </remarks>
    public class FlammenwerferWindowBase : FrostyDockableWindow
    {
        public FlammenwerferWindowBase()
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Dispatcher.UnhandledException += OnDispatcherUnhandledException;
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            FrostyExceptionBox.Show(e.Exception, Title);
            e.Handled = true;

            DialogResult = false;
            Close();
        }

        /// <summary>Toggles the CheckBox right after sender in the same panel. Wire to a Label's MouseLeftButtonUp so clicking its text also toggles the box.</summary>
        protected void ToggleAdjacentCheckBox(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is FrameworkElement label) || !(label.Parent is Panel panel))
                return;

            int index = panel.Children.IndexOf(label);
            if (index < 0 || index + 1 >= panel.Children.Count)
                return;

            if (panel.Children[index + 1] is CheckBox checkBox)
                checkBox.IsChecked = !(checkBox.IsChecked ?? false);
        }
    }
}
