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

using Android.Views;

using AndroidX.AppCompat.View.Menu;

using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;

namespace Xecrets.Mobile.Platforms.Android;

/// <summary>
/// Shows the icons of menu items in the overflow menu of the page header and in the overflow menu buttons alike.
/// </summary>
internal static class AndroidMenuExtensions
{
    /// <summary>
    /// Android shows no icons in an overflow or popup menu, and there is no public API to change that. MenuBuilder is
    /// public, and is what both menus are, but Google considers it internal API.
    /// </summary>
    public static void ShowIcons(this IMenu menu)
    {
#pragma warning disable XAOBS001 // MenuBuilder is internal API according to Google.
        ((MenuBuilder)menu).SetOptionalIconsVisible(true);
#pragma warning restore XAOBS001
    }

    /// <summary>
    /// The icon keeps its own color, rather than the tint of the toolbar, which is meant for the toolbar itself.
    /// </summary>
    public static void SetIcon(this IMenuItem menuItem, ImageSource icon, IMauiContext mauiContext) =>
        icon.LoadImage(mauiContext, result => menuItem.SetIcon(result?.Value));
}
