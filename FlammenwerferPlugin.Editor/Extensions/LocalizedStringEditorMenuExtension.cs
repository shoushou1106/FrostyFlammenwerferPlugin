using FlammenwerferPlugin.Editor.Controls;
using Frosty.Core;
using System.Windows.Media;

namespace FlammenwerferPlugin.Editor.Extensions
{
    public class LocalizedStringViewerMenuExtension : MenuExtension
    {
        internal static ImageSource imageSource = new ImageSourceConverter().ConvertFromString("pack://application:,,,/FlammenwerferPlugin.Editor;component/Images/LocalizedStringEditor.png") as ImageSource;

        public override string TopLevelMenuName => "View";

        public override string MenuItemName => "Localized String Editor";

        public override ImageSource Icon => imageSource;

        public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
        {
            App.EditorWindow.OpenEditor("Localized String Editor", new LocalizedStringEditor(App.Logger));
        });
    }
}
