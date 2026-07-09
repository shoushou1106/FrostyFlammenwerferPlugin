using System.Windows;
using System.Windows.Media;

namespace FsLocalizationPlugin.Extensions
{
    public class ModifyMultipleStringsMenuExtension : ExtendedMenuExtension
    {
        public override string TopLevelMenuName => "Tools";

        public override string SubLevelMenuName => "Flammenwerfer";

        public override string MenuItemName => "Modify Multiple Strings";

        public override ImageSource Icon => GetIcon("pack://application:,,,/FsLocalizationPlugin;component/Images/find_replace_32dp_FFFFFF_FILL0_wght600_GRAD-25_opsz24.png");

        protected override void OnClicked()
        {
            new Windows.ModifyMultipleStringsWindow(Application.Current.MainWindow).Show();
        }
    }
}
