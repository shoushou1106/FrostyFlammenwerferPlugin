using Frosty.Core;
using System.Collections.Generic;

namespace FsLocalizationPlugin.ViewModels
{
    /// <summary>Base for view models with a language picker: switches the active database language while the window is open, then restores it on close.</summary>
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

        /// <summary>Called after the language changes and the database re-initializes for it.</summary>
        protected virtual void OnLanguageChanged()
        {
        }

        /// <summary>Restores the language active before this view model switched it. Call on window close.</summary>
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
