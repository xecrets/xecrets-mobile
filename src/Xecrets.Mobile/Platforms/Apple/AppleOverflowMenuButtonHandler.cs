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

using System.Linq;
using System.Threading.Tasks;

using Microsoft.Maui;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

using UIKit;

using Xecrets.Mobile.Controls;
using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Platforms.Apple;

/// <summary>
/// The button has the icon of the overflow button in the page header, and opens a menu of actions as that button does.
/// </summary>
internal sealed class AppleOverflowMenuButtonHandler() : ViewHandler<OverflowMenuButton, UIButton>(Mapper)
{
    /// <summary>
    /// Maps <see cref="OverflowMenuButton.IconColor"/> to the tint of the button, in addition to the properties of any
    /// view.
    /// </summary>
    public static readonly IPropertyMapper<OverflowMenuButton, AppleOverflowMenuButtonHandler> Mapper =
        new PropertyMapper<OverflowMenuButton, AppleOverflowMenuButtonHandler>(ViewMapper)
        {
            [nameof(OverflowMenuButton.IconColor)] = MapIconColor,
        };

    protected override UIButton CreatePlatformView()
    {
        UIButton button = UIButton.FromType(UIButtonType.System);
        button.SetImage(UIImage.GetSystemImage("ellipsis.circle"), UIControlState.Normal);
        button.ShowsMenuAsPrimaryAction = true;
        button.Menu = UIMenu.Create([UIDeferredMenuElement.CreateUncached(OnMenuRequested)]);
        return button;
    }

    private static void MapIconColor(AppleOverflowMenuButtonHandler handler, OverflowMenuButton button) =>
        handler.PlatformView.TintColor = button.IconColor.ToPlatform();

    private async void OnMenuRequested(UIDeferredMenuElementCompletionHandler completion) =>
        completion(await Task.WhenAll(VirtualView.Items.Select(CreateActionAsync)));

    private async Task<UIMenuElement> CreateActionAsync(OverflowMenuItem item)
    {
        UIImage? icon = (await OverflowMenuButton.GetIcon(item).GetPlatformImageAsync(MauiContext!))?.Value;
        UIAction action = UIAction.Create(item.Text, icon, null, _ => item.Command.Execute(item.Parameter));
        if (!item.Command.CanExecute(item.Parameter))
        {
            action.Attributes = UIMenuElementAttributes.Disabled;
        }

        return action;
    }
}
