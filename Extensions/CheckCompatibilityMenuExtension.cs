using System.Windows;
using System.Windows.Media;

namespace FsLocalizationPlugin.Extensions
{
    public class CheckCompatibilityMenuExtension : ExtendedMenuExtension
    {
        public override string TopLevelMenuName => "Tools";

        public override string SubLevelMenuName => "Flammenwerfer";

        public override string MenuItemName => "Check Compatibility";

        public override ImageSource Icon => GetIcon("pack://application:,,,/FsLocalizationPlugin;component/Images/frame_bug_32dp_FFFFFF_FILL0_wght600_GRAD-25_opsz24.png");

        protected override void OnClicked()
        {
            new Windows.CheckCompatibilityWindow(Application.Current.MainWindow).Show();
        }
    }
}
