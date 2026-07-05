using Frosty.Core;

namespace FsLocalizationPlugin.Helpers
{
    /// <summary>Gated debug logging, controlled by the "Debug Logging" option. Not an OptionsExtension - kept out of FlammenwerferOptions so it doesn't show up as a property in the Options window.</summary>
    public static class DebugLogHelper
    {
        // Cached so Log doesn't hit Config on every call. FlammenwerferOptions.Load()/Save()
        // refresh it; otherwise it's read lazily on first use.
        private static bool? enabledCache;

        public static bool Enabled
        {
            get
            {
                if (!enabledCache.HasValue)
                    enabledCache = Config.Get("Flammenwerfer_DebugLogging", false, ConfigScope.Global);
                return enabledCache.Value;
            }
            set => enabledCache = value;
        }

        /// <summary>Logs only when Debug Logging is enabled. Styled like: "[Debug] [methodName] content"</summary>
        public static void Log(string methodName, string content, params object[] args)
        {
            if (Enabled)
                App.Logger.Log("[Debug] [" + methodName + "] " + content, args);
        }
    }
}
