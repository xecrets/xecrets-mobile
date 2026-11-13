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
using Xecrets.Mobile.Models.Utilities;
using Xecrets.Words.Abstractions;

namespace Xecrets.Mobile.Models.PageModels;

/// <summary>
/// Sets a new password, with a new key pair, for the profile when the password is forgotten. As on the desktop, the old
/// password is still needed for files encrypted with it. It's done signed out, and then the new password signs in.
/// </summary>
public partial class ResetPasswordPageModel(
    IProfileService profileService,
    IStrengthMeter strengthMeter,
    IUserInterfaceService userInterfaceService)
    : NewPasswordPageModelBase(strengthMeter, userInterfaceService), IPageInformationPageModel
{
    public string Heading => MobileTexts.HeadingEmergencyReset;

    public string Information => MobileTexts.ToolTipEmergencyReset;

    [ObservableProperty]
    public partial string Email { get; private set; } = string.Empty;

    public override bool IsEditable => true;

    [ObservableProperty]
    public partial string ErrorText { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
    // ReSharper disable once MemberCanBeMadeStatic.Local
    private partial bool IsBusy { get; set; }

    [RelayCommand]
    private async Task Initialize()
    {
        Email = (await profileService.LoadProfileAsync())!.Email;
    }

    [RelayCommand(CanExecute = nameof(CanReset))]
    private async Task Reset()
    {
        try
        {
            IsBusy = true;
            ErrorText = string.Empty;

            if (!IsConfirmed)
            {
                ErrorText = MobileTexts.DialogValidationConfirmPassword;
                return;
            }

            await profileService.ResetProfileAsync(Password);
            await UserInterfaceService.NavigateToAsync(AppDestination.Login);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanReset() => !IsBusy && HasPasswords;

    protected override void OnPasswordsChanged() => ResetCommand.NotifyCanExecuteChanged();
}
