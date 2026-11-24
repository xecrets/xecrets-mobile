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

using Microsoft.Maui.Graphics;

using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Abstractions;

/// <summary>
/// The text of a laid out editor held by a <see cref="Controls.TextScrollHost"/>, as it is searched: the part of it that
/// is scrolled into view, its lines, and the highlights of the matches. Positions are in device-independent units from
/// the top of the text. Disposing it stops the events.
/// </summary>
internal interface ISearchableTextView : IDisposable
{
    /// <summary>
    /// The top of the part in view, which may be above the text while the text bounces at its top.
    /// </summary>
    double Top { get; }

    /// <summary>
    /// The height of the part in view.
    /// </summary>
    double Height { get; }

    /// <summary>
    /// The height of all of the text.
    /// </summary>
    double TextHeight { get; }

    /// <summary>
    /// Raised when the text is scrolled.
    /// </summary>
    event EventHandler? Scrolled;

    /// <summary>
    /// Raised when the text is laid out again, such as for another width or font, which moves its lines.
    /// </summary>
    event EventHandler? LayoutChanged;

    /// <summary>
    /// Scrolls the text without animation so that the line that a character is on is a distance below the top of the
    /// part in view, or as near it as the text can be scrolled.
    /// </summary>
    /// <param name="index">The index of the character in the text.</param>
    /// <param name="room">The distance.</param>
    /// <returns>A task that completes when the text is scrolled.</returns>
    Task ScrollToLineAsync(int index, double room);

    /// <summary>
    /// Tells whether a character is in the text that is drawn in view. The positions of text that is not drawn may only
    /// be estimates, which may have it seem to be in view.
    /// </summary>
    /// <param name="index">The index of the character in the text.</param>
    /// <returns>True if the character is drawn, false otherwise.</returns>
    bool IsDrawn(int index);

    /// <summary>
    /// Gets the top and bottom of the line that a character of the text is on.
    /// </summary>
    /// <param name="index">The index of the character in the text.</param>
    /// <returns>The top and bottom.</returns>
    (double Top, double Bottom) GetLineBounds(int index);

    /// <summary>
    /// Gets the first character of the line at a vertical position.
    /// </summary>
    /// <param name="y">The position, within the height of the text.</param>
    /// <returns>The index of the character in the text.</returns>
    int GetLineStart(double y);

    /// <summary>
    /// Highlights some of the matches of a search, the current match in a color of its own if it is one of them, in
    /// place of the matches highlighted before.
    /// </summary>
    /// <param name="result">The matches.</param>
    /// <param name="first">The index in <see cref="TextSearchResult.Starts"/> of the first match to highlight.</param>
    /// <param name="end">The index in <see cref="TextSearchResult.Starts"/> right after the last match to highlight.
    /// </param>
    /// <param name="matchColor">The color of the matches other than the current match.</param>
    /// <param name="currentMatchColor">The color of the current match.</param>
    void HighlightMatches(TextSearchResult result, int first, int end, Color matchColor, Color currentMatchColor);
}
