using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Pomodoro.Converters
{
    public static class TextBlockExtensions
    {
        public static readonly DependencyProperty MarkdownProperty =
            DependencyProperty.RegisterAttached(
                "Markdown",
                typeof(string),
                typeof(TextBlockExtensions),
                new PropertyMetadata(string.Empty, OnMarkdownChanged));

        public static string GetMarkdown(DependencyObject obj)
        {
            return (string)obj.GetValue(MarkdownProperty);
        }

        public static void SetMarkdown(DependencyObject obj, string value)
        {
            obj.SetValue(MarkdownProperty, value);
        }

        private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBlock textBlock)
            {
                textBlock.Inlines.Clear();
                string text = e.NewValue as string ?? string.Empty;

                // Simple regex parser for markdown tags: **bold**, *italic*, __underline__
                string pattern = @"(\*\*.*?\*\*|\*.*?\*|__.*?__)";
                string[] parts = Regex.Split(text, pattern);

                foreach (var part in parts)
                {
                    if (string.IsNullOrEmpty(part)) continue;

                    if (part.StartsWith("**") && part.EndsWith("**") && part.Length >= 4)
                    {
                        var bold = new Bold(new Run(part.Substring(2, part.Length - 4)));
                        textBlock.Inlines.Add(bold);
                    }
                    else if (part.StartsWith("*") && part.EndsWith("*") && part.Length >= 2)
                    {
                        var italic = new Italic(new Run(part.Substring(1, part.Length - 2)));
                        textBlock.Inlines.Add(italic);
                    }
                    else if (part.StartsWith("__") && part.EndsWith("__") && part.Length >= 4)
                    {
                        var underline = new Underline(new Run(part.Substring(2, part.Length - 4)));
                        textBlock.Inlines.Add(underline);
                    }
                    else
                    {
                        textBlock.Inlines.Add(new Run(part));
                    }
                }
            }
        }
    }
}
