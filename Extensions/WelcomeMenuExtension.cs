using FsLocalizationPlugin.Helpers;
using FsLocalizationPlugin.Windows;
using System.Windows;
using System.Windows.Media;

namespace FsLocalizationPlugin.Extensions
{
    /// <summary>Reopens the welcome tour, which otherwise only ever appears once.</summary>
    /// <remarks>
    /// Onboarding that cannot be replayed is onboarding you have to get right first time. This is
    /// also where someone lands who clicked through it too fast on install day.
    /// </remarks>
    public class WelcomeMenuExtension : ExtendedMenuExtension
    {
        public override string TopLevelMenuName => "Tools";
        public override string SubLevelMenuName => "Flammenwerfer";

        public override string MenuItemName => "Welcome Tour";

        public override ImageSource Icon => GetIcon("pack://application:,,,/FsLocalizationPlugin;component/Images/handyman_32dp_FFFFFF_FILL0_wght600_GRAD-25_opsz24.png");

        protected override void OnClicked()
        {
            new OnboardingWindow(OnboardingReason.FirstRun, modManager: false)
            {
                Owner = Application.Current.MainWindow,
            }.ShowDialog();
        }
    }
}
