using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfMath.Controls;

namespace MindForge.Controls;

/// <summary>
/// Drop-in replacement for TextBlock that renders mixed plain-text + LaTeX.
/// Supports inline  $...$  and display-mode  $$...$$  notation.
/// Falls back gracefully to styled text if a formula cannot be parsed.
/// </summary>
public class LatexContentControl : UserControl
{
    // ── Dependency property ───────────────────────────────────────────────────

    public static readonly DependencyProperty TextContentProperty =
        DependencyProperty.Register(
            nameof(TextContent),
            typeof(string),
            typeof(LatexContentControl),
            new PropertyMetadata(string.Empty, OnTextContentChanged));

    public string TextContent
    {
        get => (string)GetValue(TextContentProperty);
        set => SetValue(TextContentProperty, value);
    }

    // ── Regex patterns ────────────────────────────────────────────────────────

    // Block LaTeX: $$...$$  (must come before inline to avoid mis-parsing)
    private static readonly Regex BlockLatex =
        new(@"\$\$(.+?)\$\$", RegexOptions.Singleline | RegexOptions.Compiled);

    // Inline LaTeX: $...$  (single $ not followed by another $)
    private static readonly Regex InlineLatex =
        new(@"(?<!\$)\$(?!\$)(.+?)(?<!\$)\$(?!\$)", RegexOptions.Singleline | RegexOptions.Compiled);

    // ── Layout root ───────────────────────────────────────────────────────────

    private readonly StackPanel _root;

    public LatexContentControl()
    {
        _root      = new StackPanel { Orientation = Orientation.Vertical };
        Content    = _root;
        Background = Brushes.Transparent;
    }

    // ── Property-change callback ───────────────────────────────────────────────

    private static void OnTextContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LatexContentControl ctrl)
            ctrl.Rebuild((string?)e.NewValue ?? string.Empty);
    }

    // ── Main builder ──────────────────────────────────────────────────────────

    private void Rebuild(string text)
    {
        _root.Children.Clear();
        if (string.IsNullOrEmpty(text)) return;

        // ── Phase 1: split on display-mode $$...$$ ─────────────────────────
        var blockParts = BlockLatex.Split(text);

        // Regex.Split with one capture group alternates:  plain | captured | plain | …
        for (int i = 0; i < blockParts.Length; i++)
        {
            var part = blockParts[i];
            if (string.IsNullOrEmpty(part)) continue;

            if (i % 2 == 1)
            {
                // Odd index → captured LaTeX content (inside $$...$$)
                _root.Children.Add(BuildBlockFormula(part.Trim()));
            }
            else
            {
                // Even index → plain text (may still contain inline $...$)
                AddInlineContent(part);
            }
        }
    }

    // ── Inline parser ─────────────────────────────────────────────────────────

    private void AddInlineContent(string text)
    {
        var inlineParts = InlineLatex.Split(text);

        if (inlineParts.Length == 1)
        {
            // No inline LaTeX — emit a plain wrapped TextBlock
            if (!string.IsNullOrEmpty(text))
                _root.Children.Add(MakeTextBlock(text));
            return;
        }

        // Mix of text and inline formulas → lay out horizontally in a WrapPanel
        var wrap = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Top
        };

        for (int i = 0; i < inlineParts.Length; i++)
        {
            var part = inlineParts[i];
            if (string.IsNullOrEmpty(part)) continue;

            if (i % 2 == 1)
                wrap.Children.Add(BuildInlineFormula(part));
            else
                wrap.Children.Add(MakeInlineTextBlock(part));
        }

        _root.Children.Add(wrap);
    }

    // ── Element factories ─────────────────────────────────────────────────────

    /// <summary>Multi-line TextBlock for paragraph-level plain text.</summary>
    private TextBlock MakeTextBlock(string text)
    {
        var tb = new TextBlock
        {
            Text                  = text,
            TextWrapping          = TextWrapping.Wrap,
            FontSize              = 14,
            LineHeight            = 22,
            LineStackingStrategy  = LineStackingStrategy.BlockLineHeight
        };
        tb.SetResourceReference(ForegroundProperty, "TextBrush");
        return tb;
    }

    /// <summary>Single-line TextBlock for inline runs inside a WrapPanel.</summary>
    private TextBlock MakeInlineTextBlock(string text)
    {
        var tb = new TextBlock
        {
            Text                 = text,
            FontSize             = 14,
            VerticalAlignment    = VerticalAlignment.Center,
            Margin               = new Thickness(0, 0, 2, 0)
        };
        tb.SetResourceReference(ForegroundProperty, "TextBrush");
        return tb;
    }

    /// <summary>Display-mode formula in its own centred row.</summary>
    private UIElement BuildBlockFormula(string latex)
    {
        var fc = TryFormulaControl(latex, 20.0);
        return new Border
        {
            Child               = fc,
            HorizontalAlignment = HorizontalAlignment.Center,
            Padding             = new Thickness(0, 8, 0, 8)
        };
    }

    /// <summary>Text-mode formula sized to sit inline with text.</summary>
    private UIElement BuildInlineFormula(string latex)
    {
        var fc = TryFormulaControl(latex, 14.0);
        fc.VerticalAlignment = VerticalAlignment.Center;
        fc.Margin            = new Thickness(2, 0, 2, 0);
        return fc;
    }

    private FrameworkElement TryFormulaControl(string latex, double scale)
    {
        try
        {
            var fc = new FormulaControl
            {
                Formula           = latex,
                Scale             = scale,
                Background        = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center
            };
            // Forward the theme's text colour to the formula
            fc.SetResourceReference(ForegroundProperty, "TextBrush");
            return fc;
        }
        catch
        {
            // Graceful fallback — show the raw LaTeX in red so it's obvious
            var tb = new TextBlock
            {
                Text              = $"[{latex}]",
                FontSize          = 13,
                Foreground        = Brushes.OrangeRed,
                VerticalAlignment = VerticalAlignment.Center
            };
            return tb;
        }
    }
}
