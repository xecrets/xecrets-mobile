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

using AppTexts = Xecrets.Texts.Texts;

namespace Xecrets.Mobile.Models.PageModels;

public partial class LoginPageModel(
    IProfileService profileService,
    IIncomingFileService incomingFileService,
    IUserInterfaceService userInterfaceService)
    : PageModelBase(userInterfaceService)
{
    [ObservableProperty] public partial string Email { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty] public partial string ErrorText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private partial bool IsBusy { get; set; }

    [RelayCommand]
    private async Task Initialize()
    {
        if (IsBusy)
        {
            return;
        }

        SignInKey? profile = await profileService.LoadProfileAsync();
        if (profile is null)
        {
            await UserInterfaceService.NavigateToAsync(AppDestination.CreateProfile);
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
                ErrorText = AppTexts.DialogValidationWrongPassword;
                return;
            }

            Password = string.Empty;
            await UserInterfaceService.NavigateToAsync(AppDestination.Home);
            await incomingFileService.ProcessPendingAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanLogin() => !IsBusy && Password.Length > 0;
}
