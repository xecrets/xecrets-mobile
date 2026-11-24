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

using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using Microsoft.UI.Xaml.Controls;

using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Utilities;

using MauiColor = Microsoft.Maui.Graphics.Color;
using WindowsSolidColorBrush = Microsoft.UI.Xaml.Media.SolidColorBrush;

namespace Xecrets.Mobile.Platforms.Windows;

/// <summary>
/// The searchable text of an editor on Windows, which is as tall as its text in a scroll view.
/// </summary>
internal sealed class WindowsSearchableTextView(Editor editor) : ScrollViewSearchableTextView(editor)
{
    private TextBox TextBox => (TextBox)Editor.Handler!.PlatformView!;

    public override (double Top, double Bottom) GetLineBounds(int index)
    {
        global::Windows.Foundation.Rect bounds = TextBox.GetRectFromCharacterIndex(index, false);
        return (bounds.Top, bounds.Bottom);
    }

    /// <remarks>
    /// A text box finds characters only by their index, so the first character below the position is searched for, which
    /// is the first character of the line at it, since the lines are in the order of the text.
    /// </remarks>
    public override int GetLineStart(double y)
    {
        TextBox textBox = TextBox;
        int low = 0;
        int high = textBox.Text.Length;
        while (low < high)
        {
            int middle = low + ((high - low) / 2);
            if (textBox.GetRectFromCharacterIndex(middle, false).Bottom > y)
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

    /// <remarks>
    /// Only the current match is highlighted, as the selection, since a text box cannot highlight more than one range.
    /// </remarks>
    public override void HighlightMatches(
        TextSearchResult result,
        int first,
        int end,
        MauiColor matchColor,
        MauiColor currentMatchColor)
    {
        TextBox textBox = TextBox;
        textBox.SelectionHighlightColorWhenNotFocused = new WindowsSolidColorBrush(currentMatchColor.ToWindowsColor());
        if (result.CurrentIndex >= first && result.CurrentIndex < end)
        {
            textBox.Select(result.Starts[result.CurrentIndex], result.Length);
        }
        else
        {
            textBox.Select(textBox.SelectionStart, 0);
        }
    }
}
