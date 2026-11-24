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
using Microsoft.Maui.Devices;
using Microsoft.Maui.Platform;

using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Utilities;

using AndroidEditText = Android.Widget.EditText;
using MauiColor = Microsoft.Maui.Graphics.Color;
using TextLayout = Android.Text.Layout;

namespace Xecrets.Mobile.Platforms.Android;

/// <summary>
/// The searchable text of an editor on Android, which is as tall as its text in a scroll view.
/// </summary>
internal sealed class AndroidSearchableTextView(Editor editor) : ScrollViewSearchableTextView(editor)
{
    private AndroidEditText PlatformView => (AndroidEditText)Editor.Handler!.PlatformView!;

    public override (double Top, double Bottom) GetLineBounds(int index)
    {
        AndroidEditText platformView = PlatformView;
        TextLayout layout = platformView.Layout!;
        int line = layout.GetLineForOffset(index);
        double density = DeviceDisplay.Current.MainDisplayInfo.Density;
        return (
            (platformView.ExtendedPaddingTop + layout.GetLineTop(line)) / density,
            (platformView.ExtendedPaddingTop + layout.GetLineBottom(line)) / density);
    }

    public override int GetLineStart(double y)
    {
        AndroidEditText platformView = PlatformView;
        TextLayout layout = platformView.Layout!;
        double density = DeviceDisplay.Current.MainDisplayInfo.Density;
        int vertical = (int)(y * density) - platformView.ExtendedPaddingTop;
        return layout.GetLineStart(layout.GetLineForVertical(vertical));
    }

    public override void HighlightMatches(
        TextSearchResult result,
        int first,
        int end,
        MauiColor matchColor,
        MauiColor currentMatchColor) =>
        ((SearchableEditText)PlatformView).HighlightMatches(
            result,
            first,
            end,
            matchColor.ToPlatform(),
            currentMatchColor.ToPlatform());
}
