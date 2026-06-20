using Frosty.Core;
using System.Windows;
using System.Windows.Media;

namespace FsLocalizationPlugin.Extensions
{
    public class AddStringMenuExtension : MenuExtension
    {
        public override string TopLevelMenuName => "Tools";

        public override string SubLevelMenuName => "Flammenwerfer";

        public override string MenuItemName => "Add String";
        public override ImageSource Icon => new ImageSourceConverter().ConvertFromString("pack://application:,,,/FrostyEditor;component/Images/Add.png") as ImageSource;

        public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
        {
            Windows.AddStringWindow window = new Windows.AddStringWindow(Application.Current.MainWindow);
            window.ShowDialog();
        });
    }
}
