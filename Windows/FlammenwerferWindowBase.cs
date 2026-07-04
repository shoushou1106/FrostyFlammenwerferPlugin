using Frosty.Controls;
using Frosty.Core.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace FsLocalizationPlugin.Windows
{
    /// <summary>
    /// Common base for every Flammenwerfer dialog. Centralizes the small pieces of
    /// boilerplate that used to be pasted into each window's constructor: centering on the
    /// owner and turning an unhandled exception into a friendly error box instead of
    /// crashing the editor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="FrostyDockableWindow"/>'s static constructor overrides
    /// <c>DefaultStyleKeyProperty</c> metadata for its own type, and WPF property-metadata
    /// inheritance means a subclass that doesn't override it again keeps resolving to that
    /// same style key - so Frosty's window chrome/theme keeps applying here unchanged.
    /// </para>
    /// <para>
    /// Deliberately not <see langword="abstract"/>: every derived window's XAML root is
    /// this type (with <c>x:Class</c> redirecting the actual instantiated type to the
    /// generated partial class), and WPF's markup compiler rejects an abstract type as a
    /// named XAML root ("types without a default constructor" - MC3054) since it can't
    /// rule out the type being constructed directly, e.g. as a resource dictionary item.
    /// </para>
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

        /// <summary>
        /// Toggles the <see cref="CheckBox"/> immediately following <paramref name="sender"/>
        /// in their shared parent panel - wire this to a Label's <c>MouseLeftButtonUp</c> so
        /// clicking the option's text also checks/unchecks it, not just the small checkbox.
        /// </summary>
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
