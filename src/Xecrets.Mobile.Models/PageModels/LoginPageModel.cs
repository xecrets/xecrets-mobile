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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Xecrets.Common.Models;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Services;
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Models.PageModels;

public partial class LoginPageModel(
    IProfileService profileService,
    IIncomingFileService incomingFileService,
    IInterruptedSaveService interruptedSaves,
    IUserInterfaceService userInterfaceService)
    : PageModelBase(userInterfaceService), IPageInformationPageModel, IBreadcrumbPageModel
{
    [ObservableProperty] public partial string Email { get; private set; } = string.Empty;

    [ObservableProperty] public partial string Breadcrumb { get; private set; } = string.Empty;

    [ObservableProperty] public partial string Heading { get; private set; } = MobileTexts.SignInHeading;

    [ObservableProperty] public partial string Information { get; private set; } = MobileTexts.ToolTipSignIn;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty] public partial string ErrorText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private partial bool IsBusy { get; set; }

    // As the Shell root, the page signs in. Pushed on top of another page, it is initialized with a payload and only
    // verifies the password before the profile is changed or deleted.
    private ProfileAction _verifiedAction = ProfileAction.Unknown;

    private const int WrongPasswordsBeforeReset = 3;

    private const int WrongPasswordsBetweenResets = 5;

    private int _wrongPasswordsBeforeReset = WrongPasswordsBeforeReset;

    public void InitializeVerification(ProfileAction action)
    {
        _verifiedAction = action;
        string actionBreadcrumb = action switch
        {
            ProfileAction.Change => MobileTexts.BreadcrumbChangeProfile,
            ProfileAction.Delete => MobileTexts.BreadcrumbDeleteProfile,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, @"Unknown verified profile action."),
        };
        Breadcrumb = BreadcrumbFormatter.Format(FlowOrigin.Home, actionBreadcrumb, MobileTexts.BreadcrumbPassword);
        Heading = MobileTexts.VerifyPasswordHeading;
        Information = MobileTexts.VerifyPasswordDescription;
    }

    [RelayCommand]
    private async Task Initialize()
    {
        if (IsBusy)
        {
            return;
        }

        // Cleared here, when the page appears again, not when leaving it: password managers read the fields as the
        // page goes away, and the Shell root page is kept, so the last password must not still be there after sign out.
        Password = string.Empty;
        ErrorText = string.Empty;
        _wrongPasswordsBeforeReset = WrongPasswordsBeforeReset;

        SignInKey? profile = await profileService.LoadProfileAsync();
        if (profile is null)
        {
            await UserInterfaceService.NavigateToAsync(AppDestination.Introduction);
            return;
        }

        Email = profile.Email;
    }

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task Login()
    {
        try
        {
            IsBusy = true;
            ErrorText = string.Empty;

            ProfileActionResult result = await profileService.LoginAsync(Password);
            if (!result.Succeeded)
            {
                ErrorText = MobileTexts.DialogValidationWrongPassword;
                if (_verifiedAction == ProfileAction.Unknown && --_wrongPasswordsBeforeReset == 0)
                {
                    _wrongPasswordsBeforeReset = WrongPasswordsBetweenResets;
                    await OfferPasswordResetAsync();
                }

                return;
            }

            if (_verifiedAction != ProfileAction.Unknown)
            {
                await UserInterfaceService.NavigateReplacingAsync(AppDestination.ChangeProfile, _verifiedAction);
                return;
            }

            await UserInterfaceService.NavigateToAsync(AppDestination.Home);
            // An app that was ended starts signed out, so a save it was ended in is undone when the profile signs in.
            await interruptedSaves.RecoverAsync();
            if (await profileService.ShouldShowAsync(DontShowAgain.WelcomeInformation))
            {
                await UserInterfaceService.NavigateToAsync(AppDestination.Welcome, true);
            }

            await incomingFileService.ProcessPendingAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanLogin() => !IsBusy && Password.Length > 0;

    // As on the desktop, the reset gives a new password and key pair, but the old password is still needed for files
    // encrypted with it.
    private async Task OfferPasswordResetAsync()
    {
        if (!await UserInterfaceService.DisplayConfirmationAsync(
                MobileTexts.MessageTextForgotPassword, MobileTexts.LabelYes, MobileTexts.LabelNo))
        {
            return;
        }

        if (!await UserInterfaceService.DisplayConfirmationAsync(
                MobileTexts.ToolTipEmergencyReset, MobileTexts.ButtonReset, MobileTexts.LabelCancel))
        {
            return;
        }

        await UserInterfaceService.NavigateToAsync(AppDestination.ResetPassword);
    }
}
