using Frosty.Controls;
using Frosty.Core;
using System.Windows;
using System.Windows.Media;

namespace FsLocalizationPlugin.Extensions
{
    public class CheckCompatibilityMenuExtension : MenuExtension
    {
        public override string TopLevelMenuName => "Tools";

        public override string SubLevelMenuName => "Flammenwerfer";

        public override string MenuItemName => "Check Compatibility";
        public override ImageSource Icon => new ImageSourceConverter().ConvertFromString("pack://application:,,,/FsLocalizationPlugin;component/Images/Tick_White.png") as ImageSource;

        public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
        {
            //Windows.ReplaceMultipleStringWindow window = new Windows.ReplaceMultipleStringWindow(Application.Current.MainWindow);
            //window.ShowDialog();
            FrostyMessageBox.Show("Not finished", "Flammenwerfer", MessageBoxButton.OK);
        });
    }
}
