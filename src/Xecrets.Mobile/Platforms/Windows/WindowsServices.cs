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

using System.Runtime.Versioning;

using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using MauiColor = Microsoft.Maui.Graphics.Color;
using WindowsSolidColorBrush = Microsoft.UI.Xaml.Media.SolidColorBrush;
using WindowsThickness = Microsoft.UI.Xaml.Thickness;

using Xecrets.Mobile.Controls;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Services;
using Xecrets.Mobile.Utilities;

namespace Xecrets.Mobile.Platforms.Windows;

/// <summary>
/// The services of Windows.
/// </summary>
[SupportedOSPlatform("windows")]
internal class WindowsServices : PlatformServicesBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsServices"/> class, and adds the handler mappings of Windows
    /// for the font weight and the padding of search entries. Requires a single instance for the app.
    /// </summary>
    public WindowsServices()
    {
        LabelHandler.Mapper.AppendToMapping(nameof(ITextStyle.Font), (handler, view) =>
            ApplyFontWeight(view, handler.PlatformView, TextBlock.FontWeightProperty));
        ButtonHandler.Mapper.AppendToMapping(nameof(ITextStyle.Font), (handler, view) =>
            ApplyFontWeight(view, handler.PlatformView, Control.FontWeightProperty));

        // The text of a search entry starts at the left edge of the box, in line with the texts below and above it.
        EntryHandler.Mapper.AppendToMapping("Xecrets.SearchTextPadding", (handler, view) =>
        {
            if (view is SearchTextEntry)
            {
                WindowsThickness padding =
                    (WindowsThickness)Microsoft.UI.Xaml.Application.Current.Resources["TextControlThemePadding"];
                handler.PlatformView.Padding = new WindowsThickness(0, padding.Top, padding.Right, padding.Bottom);
            }
        });
    }

    /// <inheritdoc/>
    public override (double Left, double Right) GetWindowGaps(VisualElement view)
    {
        FrameworkElement platformView = (FrameworkElement)view.Handler!.PlatformView!;
        double left = platformView.TransformToVisual(null).TransformPoint(default).X;
        return (left, platformView.XamlRoot.Size.Width - left - platformView.ActualWidth);
    }

    /// <inheritdoc/>
    public override (double Top, double Bottom) GetTextLineBounds(Editor editor, int index)
    {
        global::Windows.Foundation.Rect bounds =
            ((TextBox)editor.Handler!.PlatformView!).GetRectFromCharacterIndex(index, false);
        return (bounds.Top, bounds.Bottom);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A text box finds characters only by their index, so the first character below the position is searched for, which
    /// is the first character of the line at it, since the lines are in the order of the text.
    /// </remarks>
    public override int GetTextLineStart(Editor editor, double y)
    {
        TextBox textBox = (TextBox)editor.Handler!.PlatformView!;
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

    /// <inheritdoc/>
    /// <remarks>
    /// Only the current match is highlighted, as the selection, since a text box cannot highlight more than one range.
    /// </remarks>
    public override void HighlightTextMatches(
        Editor editor,
        TextSearchResult result,
        int first,
        int end,
        MauiColor matchColor,
        MauiColor currentMatchColor)
    {
        TextBox textBox = (TextBox)editor.Handler!.PlatformView!;
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

    private static void ApplyFontWeight(
        IView view,
        DependencyObject platformView,
        DependencyProperty fontWeightProperty)
    {
        if (Typography.IsSemibold(view))
        {
            platformView.SetValue(fontWeightProperty, FontWeights.SemiBold);
        }
    }
}
