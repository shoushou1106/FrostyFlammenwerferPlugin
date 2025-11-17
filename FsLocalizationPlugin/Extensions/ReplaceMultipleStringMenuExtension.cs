using Frosty.Core;
using System.Windows;
using System.Windows.Media;

namespace FsLocalizationPlugin.Extensions
{
    public class ReplaceMultipleStringMenuExtension : MenuExtension
    {
        public override string TopLevelMenuName => "Tools";

        public override string SubLevelMenuName => "Flammenwerfer";

        public override string MenuItemName => "Replace / Remove Multiple Strings (WIP)";
        public override ImageSource Icon => new ImageSourceConverter().ConvertFromString("pack://application:,,,/FrostyEditor;component/Images/ClassRef.png") as ImageSource;

        public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
        {
            Windows.ReplaceMultipleStringWindow window = new Windows.ReplaceMultipleStringWindow(Application.Current.MainWindow);
            window.ShowDialog();
        });
    }
}
