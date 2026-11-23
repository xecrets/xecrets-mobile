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

using Android.Content;
using Android.Graphics;
using Android.Text;
using AndroidColor = Android.Graphics.Color;

using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Platforms.Android;

/// <summary>
/// The platform view of an editor, which also highlights the matches of a search of its text.
/// </summary>
/// <remarks>
/// The highlights are drawn behind the text rather than set as spans, so that the text itself is left as it is.
/// </remarks>
/// <param name="context">The context of the view.</param>
internal sealed class SearchableEditText(Context context) : SensitiveEditText(context)
{
    private readonly Paint _matchPaint = new();

    private readonly Paint _currentMatchPaint = new();

    private readonly Path _matchPath = new();

    private TextSearchResult _result = TextSearchResult.None;

    private int _first;

    private int _end;

    /// <summary>
    /// Highlights some of the matches of a search of the text, in place of the matches highlighted before.
    /// </summary>
    /// <param name="result">The matches.</param>
    /// <param name="first">The index in <see cref="TextSearchResult.Starts"/> of the first match to highlight.</param>
    /// <param name="end">The index in <see cref="TextSearchResult.Starts"/> right after the last match to highlight.
    /// </param>
    /// <param name="matchColor">The color of the matches other than the current match.</param>
    /// <param name="currentMatchColor">The color of the current match.</param>
    public void HighlightMatches(
        TextSearchResult result,
        int first,
        int end,
        AndroidColor matchColor,
        AndroidColor currentMatchColor)
    {
        _result = result;
        _first = first;
        _end = end;
        _matchPaint.Color = matchColor;
        _currentMatchPaint.Color = currentMatchColor;
        Invalidate();
    }

    protected override void OnDraw(Canvas canvas)
    {
        if (_first < _end)
        {
            DrawMatches(canvas, Layout!);
        }

        base.OnDraw(canvas);
    }

    // Drawn where TextView draws the layout of the text, which with the gravity at the top is right below the padding.
    private void DrawMatches(Canvas canvas, Layout layout)
    {
        canvas.Save();
        canvas.Translate(CompoundPaddingLeft, ExtendedPaddingTop);
        for (int index = _first; index < _end; index++)
        {
            int start = _result.Starts[index];
            layout.GetSelectionPath(start, start + _result.Length, _matchPath);
            canvas.DrawPath(_matchPath, index == _result.CurrentIndex ? _currentMatchPaint : _matchPaint);
        }

        canvas.Restore();
    }
}
