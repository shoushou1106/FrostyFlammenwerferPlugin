using Frosty.Core;
using Frosty.Core.Controls;
using FrostySdk.Attributes;
using System;
using System.Globalization;
using System.IO;

namespace FsLocalizationPlugin.Options
{
    [DisplayName("Flammenwerfer Options")]
    public class FlammenwerferOptions : OptionsExtension
    {
        // Cached so DebugLog doesn't hit Config on every call. Load()/Save() refresh it;
        // otherwise it's read lazily on first use (Load() only runs when Options is opened).
        private static bool? debugLoggingCache;

        /// <summary>Whether Debug Logging is on. Use <see cref="DebugLog"/> instead of checking this directly.</summary>
        public static bool DebugLoggingEnabled
        {
            get
            {
                if (!debugLoggingCache.HasValue)
                    debugLoggingCache = Config.Get("Flammenwerfer_DebugLogging", false, ConfigScope.Global);
                return debugLoggingCache.Value;
            }
        }

        /// <summary>Logs only when Debug Logging is enabled.
        /// Styled like: "[Debug] [<paramref name="methodName"/>] <paramref name="content"/>"  </summary>
        public static void DebugLog(string methodName, string content, params object[] args)
        {
            if (DebugLoggingEnabled)
                App.Logger.Log("[Debug] [" + methodName + "] " + content, args);
        }

        //[Category("General")]
        //[Description("Currently placeholder")]
        //[DisplayName("Language")]
        //[EbxFieldMeta(FrostySdk.IO.EbxFieldType.idontknow)]
        //public bool Language { get; set; } = false;

        [Category("Debug")]
        [Description("Records more detailed logs for debugging.")]
        [DisplayName("Debug Logging")]
        [EbxFieldMeta(FrostySdk.IO.EbxFieldType.Boolean)]
        public bool DebugLogging { get; set; }

        public override void Load()
        {
            DebugLogging = Config.Get("Flammenwerfer_DebugLogging", false, ConfigScope.Global);
            debugLoggingCache = DebugLogging;
        }

        public override void Save()
        {
            Config.Add("Flammenwerfer_DebugLogging", DebugLogging, ConfigScope.Global);
            debugLoggingCache = DebugLogging;
        }

        //public static string GetBestLocale(CultureInfo culture = null)
        //{
        //    culture = culture ?? CultureInfo.CurrentUICulture;
        //    string[] available = { "en-US", "zh-Hans-CN" };

        //    foreach (string locale in available)
        //    {
        //        if (string.Equals(locale, culture.Name, StringComparison.OrdinalIgnoreCase))
        //        {
        //            App.Logger.Log($"First run detected, current UI culture: {culture.Name}, exact available locale found: {locale}.");
        //            return locale;
        //        }
        //    }
        //    foreach (string locale in available)
        //    {
        //        int dash = locale.IndexOf('-');
        //        string firstPart = dash > 0 ? locale.Substring(0, dash) : locale;
        //        if (string.Equals(firstPart, culture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase))
        //        {
        //            App.Logger.Log($"First run detected, current UI culture: {culture.Name}, exact locale not found, best available locale: {locale}.");
        //            return locale;
        //        }
        //    }
        //    App.Logger.Log($"First run detected, current UI culture: {culture.Name}, available locale not found, fallback to en-US.");
        //    return "en-US";
        //}

    }
}
