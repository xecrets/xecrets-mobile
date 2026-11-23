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
/// Highlights the matches of a search in the text of an editor, and scrolls the current match into view in the scroll
/// view that is the parent of the editor. Only the matches in the window around what is visible are highlighted, and
/// they are highlighted again when what is visible leaves the window, the editor is resized or the result changes.
/// </summary>
internal sealed class TextSearchHighlightBehavior : Behavior<Editor>
{
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

    // The editor is put in the scroll view after this is attached to it, so it is only known by the first search.
    private ScrollView? _scrollView;

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

    private IPlatformServices PlatformServices =>
        _editor!.Handler!.MauiContext!.Services.GetRequiredService<IPlatformServices>();

    protected override void OnAttachedTo(Editor bindable)
    {
        base.OnAttachedTo(bindable);
        _editor = bindable;
        bindable.SizeChanged += OnEditorSizeChanged;
    }

    protected override void OnDetachingFrom(Editor bindable)
    {
        bindable.SizeChanged -= OnEditorSizeChanged;
        if (_scrollView is not null)
        {
            _scrollView.Scrolled -= OnScrolled;
            _scrollView = null;
        }

        _editor = null;
        base.OnDetachingFrom(bindable);
    }

    // The result only changes by searching, which is done once the editor is shown in the scroll view.
    private async void OnResultChanged()
    {
        Editor editor = _editor!;
        if (_scrollView is null)
        {
            _scrollView = (ScrollView)editor.Parent;
            _scrollView.Scrolled += OnScrolled;
        }

        if (Result.CurrentIndex >= 0)
        {
            (double top, double bottom) = PlatformServices.GetTextLineBounds(editor, Result.Starts[Result.CurrentIndex]);
            await ScrollIntoViewAsync(_scrollView, editor.Y + top, editor.Y + bottom);
        }

        UpdateHighlights();
    }

    private void OnScrolled(object? sender, ScrolledEventArgs e) => UpdateHighlights();

    // The lines move when the editor is resized, such as for another font size or width.
    private void OnEditorSizeChanged(object? sender, EventArgs e)
    {
        _window = TextHighlightWindow.None;
        UpdateHighlights();
    }

    private void UpdateHighlights()
    {
        Editor editor = _editor!;
        if (Result.Starts.Count == 0)
        {
            if (_highlightedResult.Starts.Count > 0)
            {
                PlatformServices.HighlightTextMatches(editor, Result, 0, 0, MatchColor, CurrentMatchColor);
            }

            _highlightedResult = Result;
            _window = TextHighlightWindow.None;
            return;
        }

        ScrollView scrollView = _scrollView!;
        double visibleTop = scrollView.ScrollY - editor.Y;
        double visibleBottom = visibleTop + scrollView.Height;
        if (Result == _highlightedResult && _window.Covers(visibleTop, visibleBottom, editor.Height))
        {
            return;
        }

        TextHighlightWindow window = TextHighlightWindow.Around(visibleTop, visibleBottom, editor.Height);
        int startOffset = PlatformServices.GetTextLineStart(editor, window.Top);
        int endOffset = window.Bottom < editor.Height
            ? PlatformServices.GetTextLineStart(editor, window.Bottom)
            : int.MaxValue;
        (int first, int end) = Result.GetMatchesWithin(startOffset, endOffset);
        PlatformServices.HighlightTextMatches(editor, Result, first, end, MatchColor, CurrentMatchColor);
        _highlightedResult = Result;
        _window = window;
    }

    // The pages are not resized for the keyboard, which is usually shown while searching and covers the lower part of
    // the scroll view, so the match is kept in the upper third, a line from its top.
    private static Task ScrollIntoViewAsync(ScrollView scrollView, double top, double bottom)
    {
        double margin = bottom - top;
        bool isInView = top - margin >= scrollView.ScrollY && bottom <= scrollView.ScrollY + (scrollView.Height / 3);
        return isInView ? Task.CompletedTask : scrollView.ScrollToAsync(0, Math.Max(0, top - margin), false);
    }
}
