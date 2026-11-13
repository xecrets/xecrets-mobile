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

using Xecrets.Mobile.Abstractions;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.PageModels;
using Xecrets.Mobile.Utilities;

namespace Xecrets.Mobile.Pages;

internal partial class ResetPasswordPage
{
    private readonly IUserInterfaceService _userInterfaceService;
    private bool _isCompact;
    private bool _isAppeared;

    public Entry PasswordTextEntry => NewPasswordControl.PasswordTextEntry;

    public ResetPasswordPage(
        ResetPasswordPageModel model,
        IPlatformServices platformServices,
        IUserInterfaceService userInterfaceService,
        IPageHeaderService pageHeaderService)
        : base(model)
    {
        _userInterfaceService = userInterfaceService;
        InitializeComponent();
        platformServices.ConfigurePasswordEntry(EmailEntry, PasswordEntryPurpose.UserName);
        platformServices.ConfigurePasswordEntry(NewPasswordControl.PasswordEntry.Entry, PasswordEntryPurpose.NewPassword);
        platformServices.ConfigurePasswordEntry(NewPasswordControl.ConfirmPasswordEntry.Entry, PasswordEntryPurpose.NewPassword);
        pageHeaderService.ApplyStandardHeader(this);
    }

    // ReSharper disable once AsyncVoidMethod
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _isAppeared = true;
        if (_isCompact)
        {
            ShowInformation();
        }

        await Model.InitializeCommand.ExecuteAsync(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _isAppeared = false;
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        UpdateCompactForm(LayoutMetrics.IsCompactHeight(width, height));
        LayoutMetrics.UpdateCenteredFormLayout(width, ContentRoot, Card, ActionButtonStack);
    }

    // As on the page for the local profile, on a phone in landscape the information is shown as a transient message
    // instead of on the page, so that the form fits the height.
    private void UpdateCompactForm(bool isCompact)
    {
        if (isCompact == _isCompact)
        {
            return;
        }

        _isCompact = isCompact;
        InformationBox.IsVisible = !isCompact;
        if (isCompact && _isAppeared)
        {
            ShowInformation();
        }
    }

    private void ShowInformation()
    {
        _ = _userInterfaceService.DisplayTransientMessageAsync(Model.Information);
    }
}
