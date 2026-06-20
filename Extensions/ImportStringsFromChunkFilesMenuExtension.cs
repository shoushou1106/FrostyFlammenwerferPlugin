using Frosty.Core;
using System.Windows;
using System.Windows.Media;

namespace FsLocalizationPlugin.Extensions
{
    public class ImportStringsFromChunkFilesMenuExtension : MenuExtension
    {
        public override string TopLevelMenuName => "Tools";

        public override string SubLevelMenuName => "Flammenwerfer";

        public override string MenuItemName => "Import Strings from Chunk Files";
        public override ImageSource Icon => new ImageSourceConverter().ConvertFromString("pack://application:,,,/FrostyEditor;component/Images/Import.png") as ImageSource;

        public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
        {
            Windows.ImportStringsFromChunkFilesWindow window = new Windows.ImportStringsFromChunkFilesWindow(Application.Current.MainWindow);
            window.ShowDialog();
        });
    }
}
