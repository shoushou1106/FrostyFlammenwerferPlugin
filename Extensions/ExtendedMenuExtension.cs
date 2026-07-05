using Frosty.Core;
using System.Collections.Generic;
using System.Windows.Media;

namespace FsLocalizationPlugin.Extensions
{
    /// <summary>Common base for every clickable menu entry. Caches the command/icon, since Frosty re-reads these on every menu render.</summary>
    public abstract class ExtendedMenuExtension : MenuExtension
    {
        private static readonly Dictionary<string, ImageSource> IconCache = new Dictionary<string, ImageSource>();

        private RelayCommand command;

        public sealed override RelayCommand MenuItemClicked => command ?? (command = new RelayCommand(_ => OnClicked()));

        /// <summary>Invoked when the user clicks this menu item.</summary>
        protected abstract void OnClicked();

        /// <summary>Resolves and freezes an icon from a pack URI, parsing each URI only once.</summary>
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
