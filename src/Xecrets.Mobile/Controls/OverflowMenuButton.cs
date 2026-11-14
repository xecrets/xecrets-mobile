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

using System.Collections.Generic;

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Controls;

/// <summary>
/// A button that opens a menu of items, shown with the overflow icon and as the native menu of the overflow menu in the
/// page header on each platform, through a platform specific handler. The icons of the items are app resources.
/// </summary>
internal partial class OverflowMenuButton : View
{
    /// <summary>
    /// Identifies the <see cref="Items"/> bindable property.
    /// </summary>
    public static readonly BindableProperty ItemsProperty = BindableProperty.Create(
        nameof(Items),
        typeof(IReadOnlyList<OverflowMenuItem>),
        typeof(OverflowMenuButton),
        System.Array.Empty<OverflowMenuItem>());

    /// <summary>
    /// Identifies the <see cref="IconColor"/> bindable property.
    /// </summary>
    public static readonly BindableProperty IconColorProperty = BindableProperty.Create(
        nameof(IconColor),
        typeof(Color),
        typeof(OverflowMenuButton),
        Colors.Black);

    /// <summary>
    /// The items of the menu, which are none unless set.
    /// </summary>
    public IReadOnlyList<OverflowMenuItem> Items
    {
        get => (IReadOnlyList<OverflowMenuItem>)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>
    /// The color of the overflow icon of the button itself.
    /// </summary>
    public Color IconColor
    {
        get => (Color)GetValue(IconColorProperty);
        set => SetValue(IconColorProperty, value);
    }

    /// <summary>
    /// Gets the icon of an item from the app resources.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The icon of the item.</returns>
    public static ImageSource GetIcon(OverflowMenuItem item) =>
        (ImageSource)Application.Current!.Resources[item.IconKey];
}
