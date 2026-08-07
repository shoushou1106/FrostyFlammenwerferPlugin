using Frosty.Core;
using FsLocalizationPlugin.Controls;
using System.Windows.Media;

namespace FsLocalizationPlugin.Extensions
{
    public class IdDatabaseMenuExtension : ExtendedMenuExtension
    {
        public override string TopLevelMenuName => "Tools";
        public override string SubLevelMenuName => "Flammenwerfer";

        public override string MenuItemName => "ID Database";

        public override ImageSource Icon => GetIcon("pack://application:,,,/FrostyEditor;component/Images/Database.png");

        protected override void OnClicked()
        {
            App.EditorWindow.OpenEditor("ID Database", new IdDatabaseEditor());
        }
    }
}
