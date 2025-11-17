using Frosty.Core;
using System.Windows;
using System.Windows.Media;

namespace FsLocalizationPlugin.Extensions
{
    public class ExportChunksToFilesMenuExtension : MenuExtension
    {
        public override string TopLevelMenuName => "Tools";

        public override string SubLevelMenuName => "Flammenwerfer";

        public override string MenuItemName => "Export Chunks to Files";
        public override ImageSource Icon => new ImageSourceConverter().ConvertFromString("pack://application:,,,/FrostyEditor;component/Images/Export.png") as ImageSource;

        public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
        {
            Windows.ExportChunksToFilesWindow window = new Windows.ExportChunksToFilesWindow(Application.Current.MainWindow);
            window.ShowDialog();
        });
    }
}
