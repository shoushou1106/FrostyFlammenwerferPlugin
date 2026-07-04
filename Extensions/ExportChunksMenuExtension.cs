using System.Windows;
using System.Windows.Media;

namespace FsLocalizationPlugin.Extensions
{
    public class ExportChunksMenuExtension : ExtendedMenuExtension
    {
        public override string TopLevelMenuName => "Tools";

        public override string SubLevelMenuName => "Flammenwerfer";

        public override string MenuItemName => "Export Chunks to Files";

        public override ImageSource Icon => GetIcon("pack://application:,,,/FrostyEditor;component/Images/Export.png");

        protected override void OnClicked()
        {
            new Windows.ExportChunksWindow(Application.Current.MainWindow).ShowDialog();
        }
    }
}
