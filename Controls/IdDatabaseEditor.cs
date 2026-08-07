using Frosty.Core.Controls;
using FsLocalizationPlugin.Views;
using System.Windows;
using System.Windows.Media;

namespace FsLocalizationPlugin.Controls
{
    /// <summary>
    /// Tab shell for the ID Database editor.
    /// Hosts the real MVVM UserControl (<c>Views/IdDatabaseView</c>) via a template in <c>Themes/Generic.xaml</c>
    /// </summary>
    [TemplatePart(Name = PART_View, Type = typeof(IdDatabaseView))]
    public class IdDatabaseEditor : FrostyBaseEditor
    {
        private const string PART_View = "PART_View";

        private IdDatabaseView view;

        public override ImageSource Icon => new ImageSourceConverter().ConvertFromString("pack://application:,,,/FrostyEditor;component/Images/Database.png") as ImageSource;

        static IdDatabaseEditor()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(IdDatabaseEditor), new FrameworkPropertyMetadata(typeof(IdDatabaseEditor)));
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            view = GetTemplateChild(PART_View) as IdDatabaseView;
        }

        public override void Closed()
        {
            view?.OnEditorClosed();
        }
    }
}
