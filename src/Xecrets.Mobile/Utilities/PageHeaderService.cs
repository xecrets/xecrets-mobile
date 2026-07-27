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

using System;

using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

using Xecrets.Mobile.Abstractions;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.PageModels;
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Utilities;

public sealed class PageHeaderService(
    IProfileService profileService) : IPageHeaderService
{
    public void ApplyStandardHeader(ContentPage page)
    {
        page.ToolbarItems.Add(CreateOverflowItem(MobileTexts.MenuHelp, HeaderCommand.Help));
        page.ToolbarItems.Add(CreateOverflowItem(MobileTexts.MenuInfo, HeaderCommand.Info));
        page.ToolbarItems.Add(CreateOverflowItem(MobileTexts.MenuDesktopPricing, HeaderCommand.DesktopPricing));
        page.ToolbarItems.Add(CreateOverflowItem(MobileTexts.MenuThirdPartyLicenses, HeaderCommand.ThirdPartyLicenses));

        ApplyStandardHeaderTitle(page, profileService.IsAuthenticated ? profileService.CurrentEmail : string.Empty);
    }

    public void ApplyStandardHeaderTitle(ContentPage page, string email)
    {
        VerticalStackLayout titleLayout = new()
        {
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Spacing = 0,
        };
        Label appNameLabel = new()
        {
            Text = AppInfo.Current.Name,
            HorizontalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalOptions = LayoutOptions.Center,
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation,
            LineHeight = 1.1,
            TextColor = Colors.White,
        };
        appNameLabel.SetDynamicResource(VisualElement.StyleProperty, "Body1Strong");
        titleLayout.Children.Add(appNameLabel);

        if (email.Length > 0)
        {
            Label emailLabel = new()
            {
                Text = email,
                HorizontalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalOptions = LayoutOptions.Center,
                VerticalTextAlignment = TextAlignment.Center,
                LineBreakMode = LineBreakMode.TailTruncation,
                Margin = new Thickness(0, -3, 0, 0),
                FontSize = 16,
                LineHeight = 1.1,
                TextColor = Colors.White,
            };
            emailLabel.SetDynamicResource(VisualElement.StyleProperty, "Body2");
            titleLayout.Children.Add(emailLabel);
        }

        Shell.SetTitleView(page, titleLayout);
    }

    private static ToolbarItem CreateOverflowItem(string text, HeaderCommand command)
    {
        ToolbarItem item = new()
        {
            Text = text,
            Order = ToolbarItemOrder.Secondary,
        };
        switch (command)
        {
            case HeaderCommand.Help:
                item.SetBinding(MenuItem.CommandProperty, static (PageModelBase pageModel) => pageModel.OpenHelpCommand);
                break;
            case HeaderCommand.Info:
                item.SetBinding(MenuItem.CommandProperty, static (PageModelBase pageModel) => pageModel.OpenInfoCommand);
                break;
            case HeaderCommand.DesktopPricing:
                item.SetBinding(MenuItem.CommandProperty, static (PageModelBase pageModel) => pageModel.OpenDesktopPricingCommand);
                break;
            case HeaderCommand.ThirdPartyLicenses:
                item.SetBinding(MenuItem.CommandProperty, static (PageModelBase pageModel) => pageModel.OpenThirdPartyLicensesCommand);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command));
        }

        return item;
    }

    private enum HeaderCommand
    {
        Help,
        Info,
        DesktopPricing,
        ThirdPartyLicenses,
    }
}
