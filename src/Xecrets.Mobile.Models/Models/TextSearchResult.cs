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
/// The matches of a search of a text, of which one is the current match when there are any.
/// </summary>
/// <remarks>
/// A class rather than a record, so that every result is a new value, also when it equals the previous one.
/// </remarks>
/// <param name="starts">The start indexes of the matches in the text, in ascending order.</param>
/// <param name="length">The length of each match.</param>
/// <param name="currentIndex">The index in <paramref name="starts"/> of the current match, or -1 if there are no
/// matches.</param>
public sealed class TextSearchResult(IReadOnlyList<int> starts, int length, int currentIndex)
{
    /// <summary>
    /// No matches.
    /// </summary>
    public static TextSearchResult None { get; } = new([], 0, -1);

    /// <summary>
    /// The start indexes of the matches in the text, in ascending order.
    /// </summary>
    public IReadOnlyList<int> Starts { get; } = starts;

    /// <summary>
    /// The length of each match.
    /// </summary>
    public int Length { get; } = length;

    /// <summary>
    /// The index in <see cref="Starts"/> of the current match, or -1 if there are no matches.
    /// </summary>
    public int CurrentIndex { get; } = currentIndex;

    /// <summary>
    /// Gets the matches that are in a range of the text, in whole or in part.
    /// </summary>
    /// <param name="startOffset">The index in the text where the range starts.</param>
    /// <param name="endOffset">The index in the text right after the range.</param>
    /// <returns>The index in <see cref="Starts"/> of the first match that ends after <paramref name="startOffset"/>,
    /// and the index right after the last match that starts before <paramref name="endOffset"/>, which are the same
    /// when there are no matches in the range.</returns>
    public (int First, int End) GetMatchesWithin(int startOffset, int endOffset)
    {
        int first = FirstIndexWhere(start => start + Length > startOffset);
        int end = GetFirstMatchFrom(endOffset);
        return (first, Math.Max(first, end));
    }

    /// <summary>
    /// Gets the first match that starts at or after an index in the text.
    /// </summary>
    /// <param name="offset">The index in the text.</param>
    /// <returns>The index in <see cref="Starts"/> of the match, or the number of matches if there is none.</returns>
    public int GetFirstMatchFrom(int offset) => FirstIndexWhere(start => start >= offset);

    // The starts are in ascending order, so the predicate is false up to an index, and true from it.
    private int FirstIndexWhere(Func<int, bool> predicate)
    {
        int low = 0;
        int high = Starts.Count;
        while (low < high)
        {
            int middle = low + ((high - low) / 2);
            if (predicate(Starts[middle]))
            {
                high = middle;
            }
            else
            {
                low = middle + 1;
            }
        }

        return low;
    }
}
