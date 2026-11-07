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

using Fonts;

using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

using Xecrets.Mobile.Abstractions;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.PageModels;
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Utilities;

internal sealed class PageHeaderService(
    IProfileService profileService,
    IPlatformServices platformServices) : IPageHeaderService
{
    public void ApplyStandardHeader(ContentPage page)
    {
        if (page.BindingContext is IPageInformationPageModel)
        {
            page.ToolbarItems.Add(CreatePageInformationItem());
        }

        page.ToolbarItems.Add(CreateOverflowItem(MobileTexts.MenuWelcome, HeaderCommand.Welcome));
        page.ToolbarItems.Add(CreateOverflowItem(MobileTexts.MenuHelp, HeaderCommand.Help));
        page.ToolbarItems.Add(CreateOverflowItem(MobileTexts.MenuSupport, HeaderCommand.Support));
        page.ToolbarItems.Add(CreateOverflowItem(MobileTexts.MenuInfo, HeaderCommand.XecretsHome));
        page.ToolbarItems.Add(CreateOverflowItem(MobileTexts.MenuXecretsDesktop, HeaderCommand.XecretsDesktop));
        page.ToolbarItems.Add(CreateOverflowItem(MobileTexts.MenuGitHub, HeaderCommand.GitHub));
        page.ToolbarItems.Add(CreateOverflowItem(MobileTexts.MenuThirdPartyLicenses, HeaderCommand.ThirdPartyLicenses));

        ApplyStandardHeaderTitle(page, profileService.IsAuthenticated ? profileService.CurrentEmail : string.Empty);
    }

    public void ApplyStandardHeaderTitle(ContentPage page, string email)
    {
        Grid titleView = new()
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
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
            // The phone size of Body1Strong on every idiom, since the navigation bar is no taller on a tablet.
            FontSize = 17,
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

        titleView.Children.Add(titleLayout);
        View content = page.Content;

        // Shell hosts the title view natively and never sizes it, but lays out its content. The content moves without
        // resizing when the space Shell gives the title view changes, such as when the toolbar items appear.
        titleLayout.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(VisualElement.X) or nameof(VisualElement.Width))
            {
                CenterTitle(page, content, titleView, titleLayout);
            }
        };
        // The page content moves without the title view moving when the display cutout changes sides, as when the
        // phone is turned from one landscape orientation to the other. The title view is replaced when the title
        // changes, so the old one stops following the content when it is unloaded.
        void OnContentLaidOut(object? sender, EventArgs args) => CenterTitle(page, content, titleView, titleLayout);
        titleView.Loaded += (_, _) =>
        {
            content.BatchCommitted += OnContentLaidOut;
            CenterTitle(page, content, titleView, titleLayout);
        };
        titleView.Unloaded += (_, _) => content.BatchCommitted -= OnContentLaidOut;
        Shell.SetTitleView(page, titleView);
    }

    // Shell places the title view between its own navigation button and toolbar, which differ in width. Pad the
    // narrower side so that the title is centered in the window, but only as much as leaves room for the whole title,
    // so that a long title is moved off center before it is cut off. When the height is compact, as on a phone in
    // landscape, the content is inset from the side with a display cutout, such as a camera, and the title is centered
    // above the content instead.
    private void CenterTitle(Page page, View content, View titleView, View titleLayout)
    {
        if (!titleView.IsLoaded || !content.IsLoaded)
        {
            return;
        }

        (double left, double right) = platformServices.GetWindowGaps(titleView);
        (double contentLeft, double contentRight) = LayoutMetrics.IsCompactHeight(page.Window.Width, page.Window.Height)
            ? platformServices.GetWindowGaps(content)
            : (0, 0);
        double availableWidth = page.Window.Width - left - right;
        double titleWidth = titleLayout.Measure(double.PositiveInfinity, double.PositiveInfinity).Width
            - titleLayout.Margin.HorizontalThickness;
        double shift = right - left + contentLeft - contentRight;
        double padding = Math.Min(Math.Abs(shift), Math.Max(0, availableWidth - titleWidth));
        titleLayout.Margin = shift > 0 ? new Thickness(padding, 0, 0, 0) : new Thickness(0, 0, padding, 0);

        // Measured without a width limit above, the title is laid out at that width until it is measured again.
        ((IView)titleLayout).InvalidateMeasure();
    }

    // The icon takes the color of the other header icons from the Shell foreground color. There is no text, since it
    // would be shown with the icon on some platforms.
    private static ToolbarItem CreatePageInformationItem()
    {
        ToolbarItem item = new()
        {
            IconImageSource = new FontImageSource
            {
                Glyph = FluentUI.question_circle_24_regular,
                FontFamily = FluentUI.FontFamily,
                Size = 24,
            },
        };
        item.Clicked += OnPageInformationClicked;
        return item;
    }

    // The toolbar item has the binding context of its page.
    private static async void OnPageInformationClicked(object? sender, EventArgs e)
    {
        IPageInformationPageModel pageModel = (IPageInformationPageModel)((ToolbarItem)sender!).BindingContext;
        await Shell.Current!.DisplayAlertAsync(pageModel.Heading, pageModel.Information, MobileTexts.ButtonOk);
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
            case HeaderCommand.Support:
                item.SetBinding(MenuItem.CommandProperty, static (PageModelBase pageModel) => pageModel.OpenSupportCommand);
                break;
            case HeaderCommand.Welcome:
                item.SetBinding(MenuItem.CommandProperty, static (PageModelBase pageModel) => pageModel.OpenWelcomeCommand);
                break;
            case HeaderCommand.XecretsHome:
                item.SetBinding(MenuItem.CommandProperty, static (PageModelBase pageModel) => pageModel.OpenXecretsHomeCommand);
                break;
            case HeaderCommand.XecretsDesktop:
                item.SetBinding(MenuItem.CommandProperty, static (PageModelBase pageModel) => pageModel.OpenXecretsDesktopCommand);
                break;
            case HeaderCommand.GitHub:
                item.SetBinding(MenuItem.CommandProperty, static (PageModelBase pageModel) => pageModel.OpenGitHubCommand);
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
        Support,
        Welcome,
        XecretsHome,
        XecretsDesktop,
        GitHub,
        ThirdPartyLicenses,
    }
}
