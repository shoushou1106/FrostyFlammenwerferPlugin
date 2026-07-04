using Frosty.Core;
using System.Collections.Generic;

namespace FsLocalizationPlugin.ViewModels
{
    /// <summary>
    /// Base for view models with a language picker that switches the active
    /// <see cref="FsLocalizationStringDatabase"/> language for as long as their window is
    /// open, then restores whatever language was active before the window was opened.
    /// </summary>
    public abstract class LanguageAwareViewModelBase : ViewModelBase
    {
        private readonly string originalLanguage;
        private string selectedLanguage;

        protected LanguageAwareViewModelBase(FsLocalizationStringDatabase database)
        {
            Database = database;
            originalLanguage = Config.Get("Language", "English", ConfigScope.Game);
            selectedLanguage = originalLanguage;
        }

        protected FsLocalizationStringDatabase Database { get; }

        public IReadOnlyList<string> Languages => LocalizationHelper.GetLocalizedLanguages();

        public string SelectedLanguage
        {
            get => selectedLanguage;
            set
            {
                if (!SetProperty(ref selectedLanguage, value))
                    return;

                Config.Add("Language", value, ConfigScope.Game);
                Config.Save();
                Database.Initialize();
                OnLanguageChanged();
            }
        }

        /// <summary>
        /// Called after the active language has changed and the database has been
        /// re-initialized for it, so a subclass can refresh whatever depends on it.
        /// </summary>
        protected virtual void OnLanguageChanged()
        {
        }

        /// <summary>
        /// Restores the language that was active before this view model switched it, if
        /// it did. Call this when the owning window closes - languages picked in one of
        /// these dialogs are scoped to that dialog, not a lasting change to the editor.
        /// </summary>
        public void RestoreOriginalLanguage()
        {
            if (Config.Get("Language", "English", ConfigScope.Game) == originalLanguage)
                return;

            Config.Add("Language", originalLanguage, ConfigScope.Game);
            Config.Save();
            Database.Initialize();
        }
    }
}
