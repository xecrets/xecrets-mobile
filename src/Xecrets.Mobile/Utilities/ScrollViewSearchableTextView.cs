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

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

using Xecrets.Mobile.Abstractions;
using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Utilities;

/// <summary>
/// The searchable text of an editor that is as tall as its text, in the scroll view that is its parent, which scrolls
/// it. The platforms that have it so get the lines and highlight the matches.
/// </summary>
internal abstract class ScrollViewSearchableTextView : ISearchableTextView
{
    private readonly ScrollView _scrollView;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScrollViewSearchableTextView"/> class.
    /// </summary>
    /// <param name="editor">The laid out editor, in a scroll view.</param>
    protected ScrollViewSearchableTextView(Editor editor)
    {
        Editor = editor;
        _scrollView = (ScrollView)editor.Parent;
        _scrollView.Scrolled += OnScrolled;
        Editor.SizeChanged += OnEditorSizeChanged;
    }

    public event EventHandler? Scrolled;

    public event EventHandler? LayoutChanged;

    public double Top => _scrollView.ScrollY - Editor.Y;

    public double Height => _scrollView.Height;

    public double TextHeight => Editor.Height;

    /// <summary>
    /// The editor.
    /// </summary>
    protected Editor Editor { get; }

    public Task ScrollToLineAsync(int index, double room) =>
        _scrollView.ScrollToAsync(0, Math.Max(0, Editor.Y + GetLineBounds(index).Top - room), false);

    // The editor is as tall as its text, so the text in view is drawn.
    public bool IsDrawn(int index) => true;

    public abstract (double Top, double Bottom) GetLineBounds(int index);

    public abstract int GetLineStart(double y);

    public abstract void HighlightMatches(
        TextSearchResult result,
        int first,
        int end,
        Color matchColor,
        Color currentMatchColor);

    public void Dispose()
    {
        _scrollView.Scrolled -= OnScrolled;
        Editor.SizeChanged -= OnEditorSizeChanged;
    }

    private void OnScrolled(object? sender, ScrolledEventArgs e) => Scrolled?.Invoke(this, EventArgs.Empty);

    // The editor grows with its text, so it is resized when the text is laid out again.
    private void OnEditorSizeChanged(object? sender, EventArgs e) => LayoutChanged?.Invoke(this, EventArgs.Empty);
}
