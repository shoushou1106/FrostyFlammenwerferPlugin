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

        /// <summary>Whether this window was opened with ShowDialog (vs Show).</summary>
        protected bool IsShownAsDialog { get; private set; }

        public new bool? ShowDialog()
        {
            // Tracked because DialogResult may only be set on dialog-shown windows,
            // and close-after-action only applies to dialogs (Show windows stay open).
            IsShownAsDialog = true;
            return base.ShowDialog();
        }

        /// <summary>
        /// Wire a view model's CloseRequested to this. ShowDialog windows close after a
        /// confirmed action (result true); Show windows stay open for batch editing.
        /// A false result (explicit cancel/exit) always closes.
        /// </summary>
        protected void HandleCloseRequested(bool? result)
        {
            if (result != false && !IsShownAsDialog)
                return;

            if (IsShownAsDialog)
                DialogResult = result;
            Close();
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            FrostyExceptionBox.Show(e.Exception, Title);
            e.Handled = true;

            if (IsShownAsDialog)
                DialogResult = false;
            Close();
        }
    }
}
