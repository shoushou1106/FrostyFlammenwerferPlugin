using Frosty.Core;
using System;
using System.Reflection;

namespace FsLocalizationPlugin.Helpers
{
    /// <summary>What the onboarding window should do when Frosty starts.</summary>
    public enum OnboardingReason
    {
        /// <summary>Nothing to show.</summary>
        None,

        /// <summary>Flammenwerfer has never run on this machine.</summary>
        FirstRun,

        /// <summary>A newer Flammenwerfer than the one last seen here.</summary>
        Updated,
    }

    /// <summary>The plugin's own version, and whether the user has seen this one yet.</summary>
    /// <remarks>
    /// Frosty keeps its settings in one file per host for the whole machine,
    /// <c>%LocalAppData%/Frosty/editor_config.json</c>, so two Frosty installs share it.
    /// A creator running an old Frosty beside a new one would otherwise be shown the tour again
    /// every time the older plugin started.
    /// <para>
    /// The stored value is therefore the highest version ever seen rather than the last one to run,
    /// and it only ever moves forward. An older plugin sharing the file sees a higher number, says
    /// nothing, and leaves it alone.
    /// </para>
    /// </remarks>
    public static class PluginVersion
    {
        private const string LastSeenOption = "Flammenwerfer_LastSeenVersion";

        private static Version current;

        /// <summary>The running plugin version, as major.minor.build.</summary>
        public static Version Current
        {
            get
            {
                if (current == null)
                {
                    Version raw = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
                    current = new Version(raw.Major, raw.Minor, raw.Build < 0 ? 0 : raw.Build);
                }
                return current;
            }
        }

        /// <summary>The running version as text, for titles and log lines.</summary>
        public static string CurrentText => Current.ToString(3);

        /// <summary>The highest version this machine has ever run, or null if this is the first time.</summary>
        public static Version LastSeen
        {
            get
            {
                string stored = Config.Get(LastSeenOption, string.Empty, ConfigScope.Global);
                return Version.TryParse(stored, out Version parsed) ? parsed : null;
            }
        }

        /// <summary>Decides what the user should be shown, without changing anything.</summary>
        public static OnboardingReason DecideOnboarding()
        {
            Version seen = LastSeen;
            if (seen == null)
                return OnboardingReason.FirstRun;

            return Current > seen ? OnboardingReason.Updated : OnboardingReason.None;
        }

        /// <summary>
        /// Records that this version has been seen. Never lowers the stored value, so an older
        /// plugin sharing the settings file cannot make a newer one introduce itself again.
        /// </summary>
        public static void MarkSeen()
        {
            Version seen = LastSeen;
            if (seen != null && seen >= Current)
                return;

            Config.Add(LastSeenOption, CurrentText, ConfigScope.Global);
            Config.Save();
            DebugLogHelper.Log("PluginVersion.MarkSeen", "Recorded {0}, was {1}", CurrentText, seen?.ToString(3) ?? "never run");
        }
    }
}
