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

namespace Xecrets.Mobile.Models.Models;

/// <summary>
/// The part of a text shown in a scroll view in which the matches of a search are highlighted: what is visible, and a
/// visible height above and below it, within the text.
/// </summary>
/// <param name="Top">The top of the window, from the top of the text.</param>
/// <param name="Bottom">The bottom of the window, from the top of the text.</param>
public readonly record struct TextHighlightWindow(double Top, double Bottom)
{
    /// <summary>
    /// No window, which covers nothing.
    /// </summary>
    public static TextHighlightWindow None { get; } = new(double.NaN, double.NaN);

    /// <summary>
    /// Gets the window around what is visible of a text.
    /// </summary>
    /// <param name="visibleTop">The top of what is visible, from the top of the text.</param>
    /// <param name="visibleBottom">The bottom of what is visible, from the top of the text.</param>
    /// <param name="textHeight">The height of the text.</param>
    /// <returns>The window, from a visible height above to a visible height below, within the text.</returns>
    public static TextHighlightWindow Around(double visibleTop, double visibleBottom, double textHeight)
    {
        double margin = visibleBottom - visibleTop;
        return new(Math.Max(0, visibleTop - margin), Math.Min(textHeight, visibleBottom + margin));
    }

    /// <summary>
    /// Tells whether all that is visible of a text is in the window.
    /// </summary>
    /// <param name="visibleTop">The top of what is visible, from the top of the text, which may be above the
    /// text.</param>
    /// <param name="visibleBottom">The bottom of what is visible, from the top of the text, which may be below the
    /// text.</param>
    /// <param name="textHeight">The height of the text.</param>
    /// <returns>True if what is visible of the text is in the window, false otherwise.</returns>
    public bool Covers(double visibleTop, double visibleBottom, double textHeight) =>
        Math.Max(0, visibleTop) >= Top && Math.Min(textHeight, visibleBottom) <= Bottom;
}
