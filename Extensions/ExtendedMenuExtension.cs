using Frosty.Core;
using System.Collections.Generic;
using System.Windows.Media;

namespace FsLocalizationPlugin.Extensions
{
    /// <summary>
    /// Common base for every clickable menu entry - caches the command/icon each entry
    /// exposes, since Frosty re-reads these properties on every menu render (the
    /// original per-extension code allocated a fresh <see cref="RelayCommand"/> and
    /// re-parsed its icon's pack URI on every single read).
    /// </summary>
    public abstract class ExtendedMenuExtension : MenuExtension
    {
        private static readonly Dictionary<string, ImageSource> IconCache = new Dictionary<string, ImageSource>();

        private RelayCommand command;

        public sealed override RelayCommand MenuItemClicked => command ?? (command = new RelayCommand(_ => OnClicked()));

        /// <summary>Invoked when the user clicks this menu item.</summary>
        protected abstract void OnClicked();

        /// <summary>
        /// Resolves an icon from a WPF pack URI (e.g. one of Frosty's own
        /// <c>pack://application:,,,/FrostyEditor;component/Images/*.png</c> icons),
        /// parsing and freezing it only once per URI no matter how many times Frosty
        /// reads <see cref="MenuExtension.Icon"/>.
        /// </summary>
        protected static ImageSource GetIcon(string packUri)
        {
            if (!IconCache.TryGetValue(packUri, out ImageSource icon))
            {
                icon = (ImageSource)new ImageSourceConverter().ConvertFromString(packUri);
                if (icon.CanFreeze && !icon.IsFrozen)
                    icon.Freeze();
                IconCache[packUri] = icon;
            }
            return icon;
        }
    }
}
