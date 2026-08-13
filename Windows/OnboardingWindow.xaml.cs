using Frosty.Core;
using FsLocalizationPlugin.Helpers;
using FsLocalizationPlugin.ViewModels;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace FsLocalizationPlugin.Windows
{
    /// <summary>The welcome and what's new tour. Opened once per version by OnboardingStartupAction.</summary>
    /// <remarks>
    /// Two things live here rather than in XAML because they cannot work from markup.
    /// <list type="bullet">
    /// <item>The page transition has to bracket the page change, not follow it. The view model asks
    /// to move, this plays the old page out, commits while nothing is visible, then plays the new
    /// page in from the direction of travel.</item>
    /// <item>Scene storyboards are started by hand. A <c>DataTemplate</c> whose <c>EventTrigger</c>
    /// listens for <c>Loaded</c> never fires when a loaded <c>ContentControl</c> swaps templates, so
    /// every scene after the first froze on its opening frame. All the scenes now live in one
    /// namescope and <c>Storyboard.Begin(this, true)</c> reaches them by name.</item>
    /// </list>
    /// </remarks>
    public partial class OnboardingWindow : FlammenwerferWindowBase
    {
        private const double Shift = 24;

        private static readonly Duration OutDuration = TimeSpan.FromMilliseconds(140);
        private static readonly Duration InDuration = TimeSpan.FromMilliseconds(300);

        private readonly OnboardingViewModel viewModel;
        private readonly Dictionary<string, FrameworkElement> scenes = new Dictionary<string, FrameworkElement>();

        private Storyboard running;

        public OnboardingWindow(OnboardingReason reason, bool modManager)
        {
            viewModel = new OnboardingViewModel(reason, modManager);
            viewModel.NavigationRequested += OnNavigationRequested;
            viewModel.PageChanged += PlayIn;
            viewModel.Finished += Close;

            InitializeComponent();

            DataContext = viewModel;
            Title = viewModel.WindowTitle;

            scenes["Logo"] = LogoBox;
            scenes["Menu"] = SceneMenuBox;
            scenes["Glyphs"] = SceneGlyphsBox;
            scenes["Ids"] = SceneIdsBox;
            scenes["Share"] = SceneShareBox;
            scenes["Apply"] = SceneApplyBox;

            Loaded += OnLoaded;
        }

        /// <summary>
        /// Whether the machine wants animation at all. Honouring the Windows setting rather than
        /// insisting on motion is what an accessible app does.
        /// </summary>
        private static bool Animate => SystemParameters.ClientAreaAnimation;

        /// <summary>
        /// Reaching the window at all counts as having seen this version, however it was dismissed.
        /// Nobody wants to be introduced to the same release twice.
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            running?.Stop(this);
            PluginVersion.MarkSeen();
            base.OnClosed(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            // A sheet closes on Escape, and pages turn with the arrow keys. Enter is already
            // Continue, since that button is IsDefault.
            switch (e.Key)
            {
                case Key.Escape:
                    Close();
                    break;

                case Key.Right:
                case Key.PageDown:
                    Run(viewModel.NextCommand);
                    break;

                case Key.Left:
                case Key.PageUp:
                    Run(viewModel.BackCommand);
                    break;

                default:
                    base.OnKeyDown(e);
                    return;
            }
            e.Handled = true;
        }

        private static void Run(RelayCommand command)
        {
            if (command.CanExecute(null))
                command.Execute(null);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            ShowScene();

            // PageHost starts invisible so the first page arrives the same way the rest do.
            if (Animate)
                PlayIn(true);
            else
                PageHost.Opacity = 1;
        }

        /// <summary>Shows the current page's illustration and restarts its animation.</summary>
        private void ShowScene()
        {
            running?.Stop(this);
            running = null;

            foreach (KeyValuePair<string, FrameworkElement> scene in scenes)
                scene.Value.Visibility = Visibility.Collapsed;

            if (!scenes.TryGetValue(viewModel.Current.SceneKey, out FrameworkElement current))
                return;

            current.Visibility = Visibility.Visible;
            if (!Animate)
                return;

            if (FindResource("Anim" + viewModel.Current.SceneKey) is Storyboard storyboard)
            {
                running = storyboard;
                storyboard.Begin(this, true);
            }
        }

        private void OnNavigationRequested(bool forward)
        {
            if (!Animate)
            {
                Commit(forward);
                return;
            }

            DoubleAnimation fade = new DoubleAnimation(0, OutDuration);
            DoubleAnimation slide = new DoubleAnimation(forward ? -Shift : Shift, OutDuration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
            };

            fade.Completed += (s, e) => Commit(forward);

            PageHost.BeginAnimation(OpacityProperty, fade);
            PageShift.BeginAnimation(TranslateTransform.XProperty, slide);
        }

        private void Commit(bool forward)
        {
            viewModel.Commit(forward);
            ShowScene();
            Title = viewModel.WindowTitle;
        }

        private void PlayIn(bool forward)
        {
            if (!Animate)
                return;

            DoubleAnimation fade = new DoubleAnimation(1, InDuration);
            DoubleAnimation slide = new DoubleAnimation(forward ? Shift : -Shift, 0, InDuration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };

            PageHost.BeginAnimation(OpacityProperty, fade);
            PageShift.BeginAnimation(TranslateTransform.XProperty, slide);
        }
    }
}
