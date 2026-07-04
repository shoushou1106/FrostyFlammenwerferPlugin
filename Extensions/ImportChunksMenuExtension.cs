using System.Windows;
using System.Windows.Media;

namespace FsLocalizationPlugin.Extensions
{
    public class ImportChunksMenuExtension : ExtendedMenuExtension
    {
        public override string TopLevelMenuName => "Tools";

        public override string SubLevelMenuName => "Flammenwerfer";

        public override string MenuItemName => "Import Chunks from Files";

        public override ImageSource Icon => GetIcon("pack://application:,,,/FrostyEditor;component/Images/Import.png");

        protected override void OnClicked()
        {
            new Windows.ImportChunksWindow(Application.Current.MainWindow).ShowDialog();
        }
    }
}
