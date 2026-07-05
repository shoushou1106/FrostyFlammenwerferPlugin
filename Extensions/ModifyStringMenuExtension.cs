using System.Windows;
using System.Windows.Media;

namespace FsLocalizationPlugin.Extensions
{
    public class ModifyStringMenuExtension : ExtendedMenuExtension
    {
        public override string TopLevelMenuName => "Tools";

        public override string SubLevelMenuName => "Flammenwerfer";

        public override string MenuItemName => "Modify String";

        public override ImageSource Icon => GetIcon("pack://application:,,,/FsLocalizationPlugin;component/Images/edit_32dp_FFFFFF_FILL0_wght600_GRAD-25_opsz20.png");

        protected override void OnClicked()
        {
            // Stay open for batch editing.
            new Windows.ModifyStringWindow(Application.Current.MainWindow, closeAfterConfirm: false).ShowDialog();
        }
    }
}
