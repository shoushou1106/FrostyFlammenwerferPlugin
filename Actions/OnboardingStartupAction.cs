using Frosty.Core;
using FrostySdk.Interfaces;
using FsLocalizationPlugin.Helpers;
using FsLocalizationPlugin.Windows;
using System;
using System.Windows;

namespace FsLocalizationPlugin.Actions
{
    /// <summary>Introduces Flammenwerfer the first time it runs, and after an update.</summary>
    /// <remarks>
    /// Frosty runs startup actions from <c>SplashWindow</c> as <c>await Task.Run(...)</c> inside an
    /// <c>async void</c> handler, which has two consequences.
    /// <list type="bullet">
    /// <item>The body is on a worker thread, so it cannot touch a Window directly.</item>
    /// <item>Anything it throws is rethrown on the dispatcher with nobody to catch it, taking
    /// Frosty down before the editor ever appears.</item>
    /// </list>
    /// <para>
    /// The window is therefore opened through <c>Dispatcher.Invoke</c>, the same shape
    /// <see cref="Frosty.Controls.FrostyMessageBox"/> uses when it is called off the UI thread.
    /// Invoke blocks the worker until the dialog closes, so the splash stays up and Frosty finishes
    /// loading only once the tour is done. <c>BeginInvoke</c> would let the editor open behind it.
    /// </para>
    /// </remarks>
    public class OnboardingStartupAction : StartupAction
    {
        public override Action<ILogger> Action => logger =>
        {
            try
            {
                bool modManager = App.PluginManager.IsManagerType(PluginManagerType.ModManager);

                // Every bug report starts with "which version". One line, once, and every
                // screenshot of the log answers it without being asked.
                App.Logger.Log("Flammenwerfer {0} ready", PluginVersion.CurrentText);
                if (!modManager)
                    WarnIfAnotherStringPluginWon();

                OnboardingReason reason = PluginVersion.DecideOnboarding();
                DebugLogHelper.Log("OnboardingStartupAction", "Version {0}, last seen {1}, decision {2}, host {3}",
                    PluginVersion.CurrentText, PluginVersion.LastSeen?.ToString(3) ?? "never", reason,
                    modManager ? "Mod Manager" : "Editor");

                if (reason == OnboardingReason.None)
                    return;

                Application.Current?.Dispatcher?.Invoke(() => Show(reason, modManager));
            }
            catch (Exception ex)
            {
                // Never let a welcome screen stop Frosty from starting. Deliberately does NOT mark
                // the version seen: failing to work out whether to show the tour is transient, and
                // suppressing it forever would be the wrong answer to a one-off error. The window's
                // own failure path below does mark it, because that one repeats every launch.
                App.Logger.LogError("Flammenwerfer could not show its welcome screen: {0}", ex.Message);
            }
        };

        /// <summary>
        /// Reports the one installation mistake that fails silently, having two localization
        /// plugins installed at once.
        /// </summary>
        /// <remarks>
        /// <c>PluginManager</c> keeps the first <c>RegisterLocalizedStringDatabase</c> it sees and
        /// ignores the rest, so with vanilla FsLocalizationPlugin still in the folder the winner
        /// comes down to the order the dll files load. When Flammenwerfer loses, its windows are
        /// still in the Tools menu and none of them edit anything, which looks like the plugin is
        /// broken rather than shadowed. Frosty builds the database before startup actions run, so
        /// by here the answer is already known.
        /// </remarks>
        private static void WarnIfAnotherStringPluginWon()
        {
            if (LocalizedStringDatabase.Current is FsLocalizationStringDatabase)
                return;

            App.Logger.LogWarning(
                "Another localized string plugin loaded before Flammenwerfer and is being used instead, so Flammenwerfer cannot edit strings. " +
                "This usually means the plugin it replaces, FsLocalizationPlugin, is still in the Plugins folder. Remove it and restart Frosty.");
        }

        /// <summary>
        /// Opens the tour on the UI thread. Owned by whatever window exists at the time, which
        /// during startup is the splash, so it centres on screen instead.
        /// </summary>
        private static void Show(OnboardingReason reason, bool modManager)
        {
            try
            {
                OnboardingWindow window = new OnboardingWindow(reason, modManager)
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Topmost = true,
                };
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                // Marked seen so a window that cannot open does not try again every launch.
                App.Logger.LogError("Flammenwerfer could not open its welcome screen: {0}", ex.Message);
                PluginVersion.MarkSeen();
            }
        }
    }
}
