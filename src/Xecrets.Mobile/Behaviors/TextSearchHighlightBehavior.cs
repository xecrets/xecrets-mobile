#region Copyright and GPL License

/*
 * Xecrets Ez Mobile - Copyright © 2026 Svante Seleborg, All Rights Reserved.
 *
 * This code file is part of Xecrets Ez Mobile, an application that uses the Xecrets.Net library, parts of which in turn
 * are derived from AxCrypt as licensed under GPL v3 or later. This code is not derived from AxCrypt. It is separately
 * authored and copyrighted, and licensed only as follows unless explicitly licensed otherwise.
 *
 * Xecrets Ez Mobile is free software: you can redistribute it and/or modify it under the terms of the GNU General
 * Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any
 * later version.
 *
 * No additional permission is granted beyond that license. If you incorporate this code into a larger work and
 * distribute that work to others, you are responsible for complying with the GNU General Public License version 3 or
 * later. See https://www.gnu.org/licenses/ for more information.
 *
 * Xecrets Ez Mobile is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the
 * implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more
 * details.
 *
 * You should have received a copy of the GNU General Public License along with Xecrets Ez Mobile. If not, see
 * <https://www.gnu.org/licenses/>.
 *
 * The source repository can be found at https://github.com/xecrets/xecrets-mobile please go there for more information,
 * suggestions and contributions. You may also visit https://www.axantum.com for more information about the author.
 */

#endregion Copyright and GPL License

using System;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

using Xecrets.Mobile.Abstractions;
using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Behaviors;

/// <summary>
/// Highlights the matches of a search in the text of an editor held by a <see cref="Controls.TextScrollHost"/>, and
/// scrolls the current match into view. Only the matches in the window around what is visible are highlighted, and
/// they are highlighted again when what is visible leaves the window, the text is laid out again or the result changes.
/// </summary>
internal sealed class TextSearchHighlightBehavior : Behavior<Editor>
{
    private const int MaxScrollAttempts = 3;

    // The text is scrolled to whole pixels, so it may be scrolled a fraction of a unit from where it is scrolled to.
    private const double ScrollTolerance = 1;

    /// <summary>
    /// Identifies the <see cref="Result"/> bindable property.
    /// </summary>
    public static readonly BindableProperty ResultProperty = BindableProperty.Create(
        nameof(Result),
        typeof(TextSearchResult),
        typeof(TextSearchHighlightBehavior),
        TextSearchResult.None,
        propertyChanged: static (bindable, _, _) => ((TextSearchHighlightBehavior)bindable).OnResultChanged());

    private Editor? _editor;

    // The editor is put in its host after this is attached to it, so the text view is only created by the first search.
    private ISearchableTextView? _textView;

    private TextSearchResult _highlightedResult = TextSearchResult.None;

    private TextHighlightWindow _window = TextHighlightWindow.None;

    /// <summary>
    /// The matches to highlight.
    /// </summary>
    public TextSearchResult Result
    {
        get => (TextSearchResult)GetValue(ResultProperty);
        set => SetValue(ResultProperty, value);
    }

    /// <summary>
    /// The color of the matches other than the current match.
    /// </summary>
    public Color MatchColor { get; set; } = Colors.Transparent;

    /// <summary>
    /// The color of the current match.
    /// </summary>
    public Color CurrentMatchColor { get; set; } = Colors.Transparent;

    protected override void OnAttachedTo(Editor bindable)
    {
        base.OnAttachedTo(bindable);
        _editor = bindable;
    }

    protected override void OnDetachingFrom(Editor bindable)
    {
        // There is no text view if nothing was searched for.
        _textView?.Dispose();
        _textView = null;
        _editor = null;
        base.OnDetachingFrom(bindable);
    }

    // The result only changes by searching, which is done once the editor is shown in its host.
    private async void OnResultChanged()
    {
        if (_textView is null)
        {
            Editor editor = _editor!;
            _textView = editor.Handler!.MauiContext!.Services.GetRequiredService<IPlatformServices>()
                .CreateSearchableTextView(editor);
            _textView.Scrolled += OnScrolled;
            _textView.LayoutChanged += OnLayoutChanged;
        }

        if (Result.CurrentIndex < 0)
        {
            UpdateHighlights();
            return;
        }

        int start = Result.Starts[Result.CurrentIndex];
        await ScrollIntoViewAsync(_textView, start);
        UpdateHighlights();

        // Highlighting may have the text in view laid out again, as it does on the Apple platforms to draw the
        // highlights, which may move the match.
        await ScrollIntoViewAsync(_textView, start);
    }

    private void OnScrolled(object? sender, EventArgs e) => UpdateHighlights();

    // The lines move when the text is laid out again, such as for another font size or width.
    private void OnLayoutChanged(object? sender, EventArgs e)
    {
        _window = TextHighlightWindow.None;
        UpdateHighlights();
    }

    private void UpdateHighlights()
    {
        ISearchableTextView textView = _textView!;
        if (Result.Starts.Count == 0)
        {
            if (_highlightedResult.Starts.Count > 0)
            {
                textView.HighlightMatches(Result, 0, 0, MatchColor, CurrentMatchColor);
            }

            _highlightedResult = Result;
            _window = TextHighlightWindow.None;
            return;
        }

        double visibleTop = textView.Top;
        double visibleBottom = visibleTop + textView.Height;
        double textHeight = textView.TextHeight;
        if (Result == _highlightedResult && _window.Covers(visibleTop, visibleBottom, textHeight))
        {
            return;
        }

        TextHighlightWindow window = TextHighlightWindow.Around(visibleTop, visibleBottom, textHeight);
        int startOffset = textView.GetLineStart(window.Top);
        int endOffset = window.Bottom < textHeight ? textView.GetLineStart(window.Bottom) : int.MaxValue;
        (int first, int end) = Result.GetMatchesWithin(startOffset, endOffset);
        textView.HighlightMatches(Result, first, end, MatchColor, CurrentMatchColor);
        _highlightedResult = Result;
        _window = window;
    }

    // The pages are not resized for the keyboard, which is usually shown while searching and covers the lower part of
    // the text, so the match is kept in the upper third, with a sixth of the height above it. Where the text is
    // scrolled to may then be laid out, and the positions of text that is not drawn may only be estimates, as they are
    // on the Apple platforms, so the match may move, or not be drawn, and is scrolled to again. The room above it allows
    // for it moving a few more lines as the text around it is laid out. The attempts are limited, since a match near
    // the end of the text cannot be scrolled up.
    private static async Task ScrollIntoViewAsync(ISearchableTextView textView, int start)
    {
        for (int attempt = 0; attempt < MaxScrollAttempts; attempt++)
        {
            (double top, double bottom) = textView.GetLineBounds(start);
            double room = textView.Height / 6;
            if (textView.IsDrawn(start) &&
                top - room >= textView.Top - ScrollTolerance &&
                bottom <= textView.Top + (textView.Height / 3))
            {
                return;
            }

            await textView.ScrollToLineAsync(start, room);
        }
    }
}
