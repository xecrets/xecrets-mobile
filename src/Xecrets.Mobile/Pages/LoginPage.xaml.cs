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

using Microsoft.Maui.Controls;

using Xecrets.Mobile.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.PageModels;
using Xecrets.Mobile.Utilities;

namespace Xecrets.Mobile.Pages;

/// <summary>
/// The page that signs in to the local profile, or verifies its password.
/// </summary>
internal partial class LoginPage : IQueryAttributable
{
    private readonly IPlatformServices _platformServices;

    private readonly IPageHeaderService _pageHeaderService;

    /// <summary>
    /// The entry of the password.
    /// </summary>
    public Entry PasswordTextEntry => PasswordEntryControl.Entry;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginPage"/> class, which signs in, with the standard header.
    /// </summary>
    public LoginPage(
        LoginPageModel model,
        IPlatformServices platformServices,
        IPageHeaderService pageHeaderService)
        : base(model)
    {
        _platformServices = platformServices;
        _pageHeaderService = pageHeaderService;
        InitializeComponent();
        platformServices.ConfigurePasswordEntry(EmailEntry, PasswordEntryPurpose.UserName);
        platformServices.ConfigurePasswordEntry(PasswordEntryControl.Entry, PasswordEntryPurpose.ExistingPassword);
        pageHeaderService.ApplyStandardHeader(this);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A <see cref="ProfileAction.Change"/> or <see cref="ProfileAction.Delete"/> payload makes the page verify the
    /// password before that action. Without one, the page signs in.
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
            case ProfileAction.Change:
            case ProfileAction.Delete:
                // Shell applies the query before the page is pushed and its handlers are created, so the entries
                // get this configuration when they connect. Without a user name, it's not a sign-in form.
                PasswordEntryProperties.SetPurpose(EmailEntry, null);
                _platformServices.ConfigurePasswordEntry(PasswordEntryControl.Entry, PasswordEntryPurpose.VerifyPassword);
                Model.InitializeVerification(action);
                break;
            case ProfileAction.Unknown:
            case ProfileAction.Create:
            default:
                throw new ArgumentOutOfRangeException(nameof(query), action, @"Unsupported profile action.");
        }
    }

    // Every time, since the page is kept by Shell and the email changes when the profile is changed.
    // ReSharper disable once AsyncVoidMethod
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await Model.InitializeCommand.ExecuteAsync(null);
        _pageHeaderService.ApplyStandardHeaderTitle(this, Model.Email);
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        UpdateCenteredContentLayout(width);
    }

    private void UpdateCenteredContentLayout(double pageWidth)
    {
        LayoutMetrics.UpdateCenteredFormLayout(pageWidth, ContentRoot, Card, ActionButtonStack);
    }
}
