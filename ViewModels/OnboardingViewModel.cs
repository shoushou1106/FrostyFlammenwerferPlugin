using Frosty.Core;
using FsLocalizationPlugin.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace FsLocalizationPlugin.ViewModels
{
    /// <summary>One screen of the onboarding tour.</summary>
    public sealed class OnboardingPage
    {
        public OnboardingPage(string sceneKey, string title, string body, string hint = null, params string[] bullets)
        {
            SceneKey = sceneKey;
            Title = title;
            Body = body;
            Hint = hint;
            Bullets = bullets ?? Array.Empty<string>();
        }

        /// <summary>Chooses the illustration. Matches a DataTemplate key in the window's resources.</summary>
        public string SceneKey { get; }

        public string Title { get; }

        public string Body { get; }

        /// <summary>Where to find the thing, shown as a quiet line under the body. Optional.</summary>
        public string Hint { get; }

        public IReadOnlyList<string> Bullets { get; }

        public bool HasHint => !string.IsNullOrEmpty(Hint);

        public bool HasBullets => Bullets.Count > 0;
    }

    /// <summary>One dot in the page indicator.</summary>
    public sealed class OnboardingDot : ViewModelBase
    {
        private bool isCurrent;

        public bool IsCurrent
        {
            get => isCurrent;
            set => SetProperty(ref isCurrent, value);
        }
    }

    /// <summary>
    /// Backs the onboarding window. Holds the pages, the position in them, and the wording that
    /// changes with it.
    /// </summary>
    /// <remarks>
    /// A first run shows the whole tour. An update shows one page of what changed, with the tour
    /// one click away for anyone who wants it again.
    /// </remarks>
    public sealed class OnboardingViewModel : ViewModelBase
    {
        private List<OnboardingPage> pages;
        private int index;
        private bool pendingTour;

        private readonly bool modManager;

        public OnboardingViewModel(OnboardingReason reason, bool modManager)
        {
            this.modManager = modManager;
            pages = reason == OnboardingReason.Updated ? BuildWhatsNew() : BuildTour();
            Dots = new ObservableCollection<OnboardingDot>();

            NextCommand = new RelayCommand(_ => Next(), _ => true);
            BackCommand = new RelayCommand(_ => Back(), _ => CanGoBack);
            TourCommand = new RelayCommand(_ => StartTour(), _ => IsWhatsNew);

            IsWhatsNew = reason == OnboardingReason.Updated;
            RebuildDots();
        }

        /// <summary>
        /// Raised when the user asked to move. True when moving forward.
        /// </summary>
        /// <remarks>
        /// The window answers this, plays the page out, and calls <see cref="Commit"/> when the old
        /// page is off screen. Doing it the other way round would animate the new page out.
        /// </remarks>
        public event Action<bool> NavigationRequested;

        /// <summary>Raised after the page changed, so the window can play the new one in.</summary>
        public event Action<bool> PageChanged;

        /// <summary>Raised when the window should close.</summary>
        public event Action Finished;

        public RelayCommand NextCommand { get; }
        public RelayCommand BackCommand { get; }
        public RelayCommand TourCommand { get; }

        public ObservableCollection<OnboardingDot> Dots { get; }

        /// <summary>Whether this is the short update summary rather than the full tour.</summary>
        public bool IsWhatsNew { get; private set; }

        public OnboardingPage Current => pages[index];

        public bool CanGoBack => index > 0;

        public bool IsLastPage => index == pages.Count - 1;

        /// <summary>Dots are only worth showing when there is more than one page.</summary>
        public bool ShowDots => pages.Count > 1;

        public string NextText => IsLastPage ? "Get Started" : "Continue";

        public string WindowTitle => IsWhatsNew
            ? $"What's New in Flammenwerfer {PluginVersion.CurrentText}"
            : "Welcome to Flammenwerfer";

        /// <summary>Moves to the page the last request asked for. Called by the window mid-transition.</summary>
        public void Commit(bool forward)
        {
            if (pendingTour)
            {
                pendingTour = false;
                pages = BuildTour();
                index = 0;
                IsWhatsNew = false;

                RebuildDots();
                OnPropertiesChanged(nameof(IsWhatsNew), nameof(WindowTitle));
                OnMoved(forward: true);
                return;
            }

            // Clamped rather than trusted. The window guards against overlapping transitions, but
            // this class owns the invariant that index always addresses a real page, and Current
            // throwing would take the whole editor down from a double click.
            int target = index + (forward ? 1 : -1);
            if (target < 0 || target >= pages.Count)
                return;

            index = target;
            OnMoved(forward);
        }

        private void Next()
        {
            if (IsLastPage)
                Finished?.Invoke();
            else
                NavigationRequested?.Invoke(true);
        }

        private void Back()
        {
            if (CanGoBack)
                NavigationRequested?.Invoke(false);
        }

        /// <summary>Switches the update summary over to the full tour, starting at the beginning.</summary>
        private void StartTour()
        {
            pendingTour = true;
            NavigationRequested?.Invoke(true);
        }

        private void OnMoved(bool forward)
        {
            for (int i = 0; i < Dots.Count; i++)
                Dots[i].IsCurrent = i == index;

            OnPropertiesChanged(nameof(Current), nameof(CanGoBack), nameof(IsLastPage), nameof(NextText));

            // Frosty's RelayCommand hangs CanExecuteChanged off CommandManager.RequerySuggested,
            // so a page change that came from anywhere but a click has to ask for the requery.
            CommandManager.InvalidateRequerySuggested();
            PageChanged?.Invoke(forward);
        }

        private void RebuildDots()
        {
            Dots.Clear();
            for (int i = 0; i < pages.Count; i++)
                Dots.Add(new OnboardingDot { IsCurrent = i == index });

            OnPropertiesChanged(nameof(ShowDots), nameof(Current), nameof(CanGoBack), nameof(IsLastPage), nameof(NextText));
        }

        #region -- The words --
        /// <summary>The full tour, pitched at whichever host is running.</summary>
        /// <remarks>
        /// Mod Manager and the Editor have almost nothing in common here. Someone in Mod Manager
        /// installed a translation and wants to play, and may never have opened a modding tool
        /// before, so that tour is three pages of plain words and no jargon. Someone in the Editor
        /// has already shipped a mod, so that tour can say histogram and hash and get to the point.
        /// </remarks>
        private List<OnboardingPage> BuildTour()
        {
            return modManager ? BuildPlayerTour() : BuildCreatorTour();
        }

        /// <summary>Mod Manager. Assume no modding vocabulary at all.</summary>
        private static List<OnboardingPage> BuildPlayerTour()
        {
            return new List<OnboardingPage>
            {
                new OnboardingPage(
                    "Logo",
                    "Flammenwerfer is installed",
                    "It is the part that makes translation mods work. Mods that change the words in your game need it, and now you have it.",
                    "You do not have to set anything up."),

                new OnboardingPage(
                    "Apply",
                    "Add a mod, then press Launch",
                    "Drop a translation mod into your mod list like any other mod, then start the game. Flammenwerfer rebuilds the game's text while the game loads.",
                    "It only does this while you launch. It changes nothing on your PC."),

                new OnboardingPage(
                    "Share",
                    "If something looks wrong",
                    "Boxes or blanks instead of letters usually mean the mod was built for a different version of the game. Try the mod page first, and the Frosty log at the bottom of this window will say what happened.",
                    "Ask the person who made the mod, not the game."),
            };
        }

        /// <summary>Frosty Editor. Written for someone who has made mods before.</summary>
        private static List<OnboardingPage> BuildCreatorTour()
        {
            return new List<OnboardingPage>
            {
                new OnboardingPage(
                    "Logo",
                    "Flammenwerfer is installed",
                    "A drop-in replacement for FsLocalizationPlugin. Your existing projects load, your existing mods still apply, and everything below is new on top of that.",
                    "Remove FsLocalizationPlugin from your Plugins folder. Only one of them can run."),

                new OnboardingPage(
                    "Menu",
                    "Everything is in one menu",
                    "Tools › Flammenwerfer. Single edits, regex bulk edits across every string in a language, chunk import and export, and a compatibility check before you publish.",
                    "Tools › Flammenwerfer"),

                new OnboardingPage(
                    "Glyphs",
                    "The histogram grows itself",
                    "A Frostbite game can only draw the glyphs in its histogram, which is why FsLocalizationPlugin errors out on characters the game never shipped. Flammenwerfer appends them and rewrites the shift table when your mod is built.",
                    "No setup. It happens during the merge, per language."),

                new OnboardingPage(
                    "Ids",
                    "Hashes back into IDs",
                    "Strings are keyed by hash, and a hash tells you nothing about what you are translating. The ID Database resolves them, scans the whole game to find more, and stores what it learns per game so you can share the file.",
                    "Tools › Flammenwerfer › ID Database"),

                new OnboardingPage(
                    "Share",
                    "Built to be shared",
                    "Mods stay readable by vanilla FsLocalizationPlugin, so your players are not forced to switch plugins. Check Compatibility tells you which of your strings rely on Flammenwerfer before you publish.",
                    "The full guide is on the GitHub wiki."),
            };
        }

        /// <summary>
        /// The update summary. One page, because someone who already uses the plugin wants to know
        /// what changed and get back to work.
        /// </summary>
        /// <remarks>
        /// UPDATE THIS WHEN CUTTING A RELEASE. It is what every existing user sees on first launch
        /// after they update, so it should read like release notes for a person rather than a
        /// changelog. Four bullets at most, each one a thing they can now do or no longer suffer.
        /// </remarks>
        private static List<OnboardingPage> BuildWhatsNew()
        {
            return new List<OnboardingPage>
            {
                new OnboardingPage(
                    "Logo",
                    $"Flammenwerfer {PluginVersion.CurrentText}",
                    "Here is what changed since the version you were running.",
                    null,
                    "Added strings now apply correctly in game.",
                    "The ID Database turns string numbers back into readable names.",
                    "Import and export ID files, and share them with your team.",
                    "Fewer crashes when a file or a path is not what it claimed to be."),
            };
        }

        #endregion
    }
}
