using Frosty.Controls;
using System.Windows;
using System.Windows.Media;

namespace FsLocalizationPlugin.Extensions
{
    public class CheckCompatibilityMenuExtension : ExtendedMenuExtension
    {
        public override string TopLevelMenuName => "Tools";

        public override string SubLevelMenuName => "Flammenwerfer";

        public override string MenuItemName => "Check Compatibility (WIP)";

        public override ImageSource Icon => GetIcon("pack://application:,,,/FsLocalizationPlugin;component/Images/Tick_White.png");

        protected override void OnClicked()
        {
            FrostyMessageBox.Show("Not finished", "Flammenwerfer", MessageBoxButton.OK);
        }
    }
}
