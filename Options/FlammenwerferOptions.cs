using Frosty.Core;
using FrostySdk.Attributes;
using FsLocalizationPlugin.Helpers;
using FsLocalizationPlugin.Resources;

namespace FsLocalizationPlugin.Options
{
    /// <summary>The plugin's page in Tools > Options.</summary>
    /// <remarks>
    /// Frosty only constructs this and calls <see cref="Load"/> when the Options window opens
    /// and <see cref="Save"/> when it is accepted.
    /// Neither runs at startup, so anything that has to be right before
    /// the user visits Options reads <see cref="Config"/> for itself and treats these as a push.
    /// See <see cref="ProjectIdDatabase.Enabled"/> and <see cref="DebugLogHelper.Enabled"/>.
    /// <para>
    /// Keys are shared with those readers, so they live here as constants rather than as literals
    /// repeated across four call sites.
    /// </para>
    /// </remarks>
    [DisplayName("Flammenwerfer Options")]
    public class FlammenwerferOptions : OptionsExtension
    {
        //[Category("General")]
        //[Description("Currently placeholder")]
        //[DisplayName("Language")]
        //[EbxFieldMeta(FrostySdk.IO.EbxFieldType.idontknow)]
        //public bool Language { get; set; } = false;

        [Category("Editor")]
        [Description("Collects and manages strings added to a project. Saved in a added EBX Asset in your project, stays alive even when Flammenwerfer is not installed, and never exported to a mod file. You have to manually revert Flammenwerfer\\ProjectIdDatabase after disabling this.")]
        [DisplayName("Project ID Database")]
        [EbxFieldMeta(FrostySdk.IO.EbxFieldType.Boolean)]

        public bool ProjectIdDatabaseEnabled { get; set; } = true;

        [Category("Debug")]
        [Description("Records more detailed logs for debugging.")]
        [DisplayName("Debug Logging")]
        [EbxFieldMeta(FrostySdk.IO.EbxFieldType.Boolean)]
        public bool DebugLogging { get; set; }

        /// <summary>Per game, because a creator may want the extra asset in one project and not another.</summary>
        internal const string ProjectIdDatabaseKey = "Flammenwerfer_ProjectIdDatabaseEnabled";

        /// <summary>Global, because it is about how the tool talks rather than about a game.</summary>
        internal const string DebugLoggingKey = "Flammenwerfer_DebugLogging";

        public override void Load()
        {
            ProjectIdDatabaseEnabled = Config.Get(ProjectIdDatabaseKey, true, ConfigScope.Game);
            ProjectIdDatabase.Enabled = ProjectIdDatabaseEnabled;

            DebugLogging = Config.Get(DebugLoggingKey, false, ConfigScope.Global);
            DebugLogHelper.Enabled = DebugLogging;
        }

        public override void Save()
        {
            Config.Add(ProjectIdDatabaseKey, ProjectIdDatabaseEnabled, ConfigScope.Game);
            ProjectIdDatabase.Enabled = ProjectIdDatabaseEnabled;

            Config.Add(DebugLoggingKey, DebugLogging, ConfigScope.Global);
            DebugLogHelper.Enabled = DebugLogging;
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
