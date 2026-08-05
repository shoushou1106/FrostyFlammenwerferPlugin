using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace FsLocalizationPlugin.Controls
{
    /// <summary>
    /// A <see cref="TextBlock"/> that highlights filter match. Supports Virtualization and OneWay binding.
    /// </summary>
    /// <remarks>
    /// Based on <see cref="Frosty.Core.Controls.FrostyHightlightTextBlock"/> <br/>
    /// <strong>
    /// Why revise?
    /// </strong>
    /// Setting <see cref="TextBlock.Inlines"/> updates the <see cref="TextBlock.Text"/>  property locally,
    /// which would destroy a <see langword="OneWay"/> Text binding,
    /// that breaks Virtualization of recycled containers like <see cref="DataGrid"/> rows. <br/>
    /// <strong>
    /// How does it fix that?
    /// </strong>
    /// Renders into <see cref="TextBlock.Inlines"/> from its own <see cref="SourceText"/> property
    /// and never touches <see cref="TextBlock.Text"/>. <br/>
    /// <strong>
    /// Why original still works?
    /// </strong>
    /// Original controls (e.g. <see cref="Frosty.Core.Controls.FrostyPropertyGrid"/>) ignores the issue
    /// and chooses to disable Virtualization,
    /// which works fine for smaller logic files,
    /// but causes lag with large files (such as world files), ID Database, and Loc Studio.
    /// </remarks>
    public class RevisedHighlightTextBlock : TextBlock
    {
        private static readonly SolidColorBrush MatchBackground = CreateFrozen(Color.FromArgb(0xff, 12, 60, 98));
        private static readonly SolidColorBrush MatchForeground = CreateFrozen(Color.FromArgb(0xff, 149, 197, 235));

        public static readonly DependencyProperty SourceTextProperty = DependencyProperty.Register(
            nameof(SourceText), typeof(string), typeof(RevisedHighlightTextBlock),
            new FrameworkPropertyMetadata(string.Empty, OnContentChanged));

        public static readonly DependencyProperty HighlightProperty = DependencyProperty.Register(
            nameof(Highlight), typeof(string), typeof(RevisedHighlightTextBlock),
            new FrameworkPropertyMetadata(string.Empty, OnContentChanged));

        /// <summary>
        /// The text to display. Use this instead of <see cref="TextBlock.Text"/>
        /// </summary>
        public string SourceText
        {
            get => (string)GetValue(SourceTextProperty);
            set => SetValue(SourceTextProperty, value);
        }

        /// <summary>
        /// The substring to highlight, if present
        /// </summary>
        public string Highlight
        {
            get => (string)GetValue(HighlightProperty);
            set => SetValue(HighlightProperty, value);
        }

        /// <summary>
        /// Creates a frozen SolidColorBrush to optimize performance and reduce memory usage.
        /// </summary>
        private static SolidColorBrush CreateFrozen(Color c)
        {
            SolidColorBrush brush = new SolidColorBrush(c);
            brush.Freeze();
            return brush;
        }

        private static void OnContentChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            ((RevisedHighlightTextBlock)source).Rebuild();
        }

        private void Rebuild()
        {
            string text = SourceText ?? string.Empty;
            string match = Highlight ?? string.Empty;

            Inlines.Clear();
            if (text.Length == 0)
                return;

            int index = match.Length > 0 ? text.IndexOf(match, StringComparison.OrdinalIgnoreCase) : -1;
            if (index < 0)
            {
                Inlines.Add(new Run(text));
                return;
            }

            if (index > 0)
                Inlines.Add(new Run(text.Substring(0, index)));

            Inlines.Add(new Run(text.Substring(index, match.Length))
            {
                Background = MatchBackground,
                Foreground = MatchForeground,
            });

            int end = index + match.Length;
            if (end < text.Length)
                Inlines.Add(new Run(text.Substring(end)));
        }
    }
}
