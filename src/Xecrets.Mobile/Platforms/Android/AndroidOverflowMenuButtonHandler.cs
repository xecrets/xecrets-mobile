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

using _Microsoft.Android.Resource.Designer;

using Android.Content.Res;
using Android.Util;
using Android.Views;

using AndroidX.AppCompat.Widget;

using Microsoft.Maui;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

using Xecrets.Mobile.Controls;
using Xecrets.Mobile.Models.Models;

using AndroidView = Android.Views.View;

namespace Xecrets.Mobile.Platforms.Android;

/// <summary>
/// The button has the overflow icon, description and ripple of the overflow button of the toolbar, and opens a popup
/// menu, which is what the overflow menu of the toolbar is, with the icons shown as there.
/// </summary>
internal sealed class AndroidOverflowMenuButtonHandler()
    : ViewHandler<OverflowMenuButton, AppCompatImageButton>(Mapper)
{
    public static readonly IPropertyMapper<OverflowMenuButton, AndroidOverflowMenuButtonHandler> Mapper =
        new PropertyMapper<OverflowMenuButton, AndroidOverflowMenuButtonHandler>(ViewMapper)
        {
            [nameof(OverflowMenuButton.IconColor)] = MapIconColor,
        };

    protected override AppCompatImageButton CreatePlatformView()
    {
        AppCompatImageButton button = new(Context)
        {
            ContentDescription = Context.GetString(ResourceConstant.String.abc_action_menu_overflow_description),
        };
        button.SetImageResource(ResourceConstant.Drawable.abc_ic_menu_overflow_material);

        TypedValue background = new();
        Context.Theme!.ResolveAttribute(
            global::Android.Resource.Attribute.SelectableItemBackgroundBorderless,
            background,
            true);
        button.SetBackgroundResource(background.ResourceId);
        return button;
    }

    protected override void ConnectHandler(AppCompatImageButton platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Click += OnClickBuildMenu;
    }

    protected override void DisconnectHandler(AppCompatImageButton platformView)
    {
        platformView.Click -= OnClickBuildMenu;
        base.DisconnectHandler(platformView);
    }

    private static void MapIconColor(AndroidOverflowMenuButtonHandler handler, OverflowMenuButton button) =>
        handler.PlatformView.ImageTintList = ColorStateList.ValueOf(button.IconColor.ToPlatform());

    private void OnClickBuildMenu(object? sender, System.EventArgs e)
    {
        IReadOnlyList<OverflowMenuItem> items = VirtualView.Items;
        PopupMenu popup = new(Context, (AndroidView)sender!);
        for (int index = 0; index < items.Count; index++)
        {
            OverflowMenuItem item = items[index];
            IMenuItem menuItem = popup.Menu.Add(0, index, index, item.Text)!;
            menuItem.SetIcon(OverflowMenuButton.GetIcon(item), MauiContext!);
            menuItem.SetEnabled(item.Command.CanExecute(item.Parameter));
        }

        popup.Menu.ShowIcons();
        popup.MenuItemClick += (_, args) =>
        {
            OverflowMenuItem item = items[args.Item!.ItemId];
            item.Command.Execute(item.Parameter);
        };
        popup.Show();
    }
}
