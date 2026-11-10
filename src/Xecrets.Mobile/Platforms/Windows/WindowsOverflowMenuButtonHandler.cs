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
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

using Xecrets.Mobile.Controls;
using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Platforms.Windows;

/// <summary>
/// The button has the icon of the overflow button of a command bar, which the page header is, and opens a menu flyout,
/// as that button does.
/// </summary>
[SupportedOSPlatform("windows10.0.19041")]
internal sealed class WindowsOverflowMenuButtonHandler() : ViewHandler<OverflowMenuButton, Button>(Mapper)
{
    public static readonly IPropertyMapper<OverflowMenuButton, WindowsOverflowMenuButtonHandler> Mapper =
        new PropertyMapper<OverflowMenuButton, WindowsOverflowMenuButtonHandler>(ViewMapper)
        {
            [nameof(OverflowMenuButton.IconColor)] = MapIconColor,
        };

    // MAUI clears the background of the button when the view has none, which gives it the background of a button in
    // the theme. As for the overflow button of a command bar, the button only shows a background when pointed at.
    protected override Button CreatePlatformView()
    {
        Button button = new()
        {
            Content = new SymbolIcon(Symbol.More),
            Flyout = new MenuFlyout(),
        };
        SolidColorBrush transparent = new(Microsoft.UI.Colors.Transparent);
        button.Resources["ButtonBackground"] = transparent;
        button.Resources["ButtonBorderBrush"] = transparent;
        return button;
    }

    protected override void ConnectHandler(Button platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Flyout.Opening += OnFlyoutOpeningBuildMenu;
    }

    protected override void DisconnectHandler(Button platformView)
    {
        platformView.Flyout.Opening -= OnFlyoutOpeningBuildMenu;
        base.DisconnectHandler(platformView);
    }

    private static void MapIconColor(WindowsOverflowMenuButtonHandler handler, OverflowMenuButton button) =>
        handler.PlatformView.Foreground = button.IconColor.ToPlatform();

    // The menu is made each time it is opened, so that the items can be used as the commands allow then.
    private void OnFlyoutOpeningBuildMenu(object? sender, object e)
    {
        MenuFlyout flyout = (MenuFlyout)PlatformView.Flyout;
        flyout.Items.Clear();
        foreach (OverflowMenuItem item in VirtualView.Items)
        {
            MenuFlyoutItem flyoutItem = new()
            {
                Text = item.Text,
                Command = item.Command,
                CommandParameter = item.Parameter,
            };
            OverflowMenuButton.GetIcon(item).LoadImage(
                MauiContext!,
                result => flyoutItem.Icon = new ImageIcon { Source = result?.Value });
            flyout.Items.Add(flyoutItem);
        }
    }
}
