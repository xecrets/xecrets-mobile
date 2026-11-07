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

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Services;
using Xecrets.Mobile.Models.Utilities;
using Xecrets.Words.Abstractions;
using Xecrets.Words.Model;

namespace Xecrets.Mobile.Models.PageModels;

public partial class CreateProfilePageModel(
    IProfileService profileService,
    IStrengthMeter strengthMeter,
    LocalProfileDeletionService deletionService,
    InstallationService installationService,
    IUserInterfaceService userInterfaceService)
    : PageModelBase(userInterfaceService), IPageInformationPageModel
{
    [ObservableProperty]
    public partial string Heading { get; private set; } = MobileTexts.HeadingCreateUserFirstTimeSetup;

    [ObservableProperty]
    public partial string Information { get; private set; } = MobileTexts.ToolTipEmail;

    [ObservableProperty]
    public partial string ButtonText { get; private set; } = MobileTexts.ButtonCreate;

    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    public partial string ConfirmPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double PasswordStrength { get; set; }

    [ObservableProperty]
    public partial StrengthColor PasswordStrengthColor { get; set; }

    [ObservableProperty]
    public partial string ErrorText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditable))]
    public partial bool IsDelete { get; private set; }

    public bool IsEditable => !IsDelete;

    // Neither changing nor deleting, but setting up a new profile.
    [ObservableProperty]
    public partial bool IsSetUp { get; private set; } = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    // ReSharper disable once MemberCanBeMadeStatic.Local
    private partial bool IsBusy { get; set; }

    // Pushed with a payload, the page changes the current profile instead of creating one.
    private bool _isChange;

    public void InitializeChange()
    {
        _isChange = true;
        IsSetUp = false;
        Heading = MobileTexts.MenuChangeProfile;
        Information = MobileTexts.ChangeProfileDescription;
        ButtonText = MobileTexts.ButtonSave;
        Email = profileService.CurrentEmail;
        Password = profileService.CurrentPassword;
        ConfirmPassword = profileService.CurrentPassword;
    }

    // As when changing, the current email and password are shown, but cannot be edited.
    public void InitializeDelete()
    {
        IsDelete = true;
        IsSetUp = false;
        Heading = MobileTexts.MenuDeleteProfile;
        Information = MobileTexts.MessageTextConfirmWipe;
        Email = profileService.CurrentEmail;
        Password = profileService.CurrentPassword;
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task Delete()
    {
        try
        {
            IsBusy = true;
            if (!await UserInterfaceService.DisplayConfirmationAsync(
                    MobileTexts.MessageTextConfirmWipe, MobileTexts.LabelYes, MobileTexts.LabelNo))
            {
                return;
            }

            await deletionService.DeleteAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanDelete() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task Create()
    {
        try
        {
            IsBusy = true;
            ErrorText = string.Empty;

            if (Password != ConfirmPassword)
            {
                ErrorText = MobileTexts.DialogValidationConfirmPassword;
                return;
            }

            if (!_isChange)
            {
                // Without a local profile, as when first installed or after deleting it, this is not done at startup, so
                // that nothing is written before a profile is created.
                await installationService.EnsureAsync();
            }

            ProfileActionResult result = _isChange
                ? await profileService.ChangeProfileAsync(Email, Password)
                : await profileService.CreateProfileAsync(Email, Password);
            if (!result.Succeeded)
            {
                ErrorText = result.Status switch
                {
                    ProfileActionStatus.InvalidEmail => MobileTexts.DialogValidationInvalidEmail,
                    ProfileActionStatus.AlreadyExists => MobileTexts.DialogValidationAlreadySignedIn,
                    _ => MobileTexts.DialogValidationInvalidEmail,
                };
                return;
            }

            if (_isChange)
            {
                await UserInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextProfileChanged);
                await UserInterfaceService.NavigateToAsync(AppDestination.Home);
                return;
            }

            await UserInterfaceService.NavigateToAsync(AppDestination.Login);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanCreate()
        => !IsBusy && Password.Length > 0 && ConfirmPassword.Length > 0;

    partial void OnPasswordChanged(string value)
    {
        int strength = strengthMeter.StrengthPercent(value);
        PasswordStrength = strength / 100.0;
        PasswordStrengthColor = strengthMeter.ToStrengthColor(strength);
    }
}
