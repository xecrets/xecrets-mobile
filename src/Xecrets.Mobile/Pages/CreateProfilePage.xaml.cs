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
using System.Collections.Generic;

using Microsoft.Maui;
using Microsoft.Maui.Controls;

using Xecrets.Mobile.Abstractions;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.PageModels;
using Xecrets.Mobile.Utilities;

namespace Xecrets.Mobile.Pages;

/// <summary>
/// The page that creates the local profile, changes it, or deletes it.
/// </summary>
internal partial class CreateProfilePage : IQueryAttributable
{
    private readonly IPlatformServices _platformServices;
    private readonly IUserInterfaceService _userInterfaceService;
    private bool _isCompact;
    private bool _isAppeared;

    /// <summary>
    /// The entry of the new password.
    /// </summary>
    public Entry PasswordTextEntry => NewPasswordControl.PasswordTextEntry;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateProfilePage"/> class, which creates the profile, with the
    /// standard header.
    /// </summary>
    public CreateProfilePage(
        CreateProfilePageModel model,
        IPlatformServices platformServices,
        IUserInterfaceService userInterfaceService,
        IPageHeaderService pageHeaderService)
        : base(model)
    {
        _platformServices = platformServices;
        _userInterfaceService = userInterfaceService;
        InitializeComponent();
        platformServices.ConfigurePasswordEntry(EmailEntry, PasswordEntryPurpose.UserName);
        platformServices.ConfigurePasswordEntry(NewPasswordControl.PasswordEntry.Entry, PasswordEntryPurpose.NewPassword);
        platformServices.ConfigurePasswordEntry(NewPasswordControl.ConfirmPasswordEntry.Entry, PasswordEntryPurpose.NewPassword);
        pageHeaderService.ApplyStandardHeader(this);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A <see cref="ProfileAction"/> payload makes the page create, change or delete the profile. Without one, the page
    /// creates the profile.
    /// </remarks>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue(nameof(NavigationParameter.Payload), out object? payload))
        {
            return;
        }

        ProfileAction action = (ProfileAction)payload;
        switch (action)
        {
            case ProfileAction.Create:
                break;
            case ProfileAction.Change:
                Model.InitializeChange();
                break;
            case ProfileAction.Delete:
                // As on the verification page, Shell applies the query before the handlers are created. Without a user
                // name and a new password, it's not a form for password managers to save.
                PasswordEntryProperties.SetPurpose(EmailEntry, null);
                _platformServices.ConfigurePasswordEntry(NewPasswordControl.PasswordEntry.Entry, PasswordEntryPurpose.VerifyPassword);
                _platformServices.ConfigurePasswordEntry(NewPasswordControl.ConfirmPasswordEntry.Entry, PasswordEntryPurpose.VerifyPassword);

                // Only the entry, so that the password can still be shown.
                NewPasswordControl.PasswordEntry.Entry.IsEnabled = false;

                // The only field, so Done instead of Next. Not Go, so that the keyboard can't start the deletion.
                NewPasswordControl.PasswordEntry.ReturnType = ReturnType.Done;
                Model.InitializeDelete();
                break;
            case ProfileAction.Unknown:
            default:
                throw new ArgumentOutOfRangeException(nameof(query), action, @"Unsupported profile action.");
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _isAppeared = true;
        if (_isCompact)
        {
            ShowInformation();
        }
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
        UpdateCenteredContentLayout(width);
    }

    private void UpdateCenteredContentLayout(double pageWidth)
    {
        LayoutMetrics.UpdateCenteredFormLayout(pageWidth, ContentRoot, Card, ActionButtonStack);
    }

    // On a phone in landscape, shows the page information as a transient message instead of on the page, so that the
    // form fits the height. The information is also shown by the information button.
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
