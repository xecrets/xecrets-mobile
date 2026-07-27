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

using AppTexts = Xecrets.Texts.Texts;

namespace Xecrets.Mobile.Models.PageModels;

public partial class HomePageModel(
    IProfileService profileService,
    IFileService fileService,
    IPreviewService previewService,
    IEncryptionPreparationService encryptionPreparationService,
    ICrashTestService crashTestService,
    SessionExitService sessionExitService,
    IUserInterfaceService userInterfaceService)
    : PageModelBase(userInterfaceService)
{
    [ObservableProperty]
    public partial string Email { get; set; } = profileService.CurrentEmail;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EncryptCommand))]
    [NotifyCanExecuteChangedFor(nameof(EncryptToShareCommand))]
    [NotifyCanExecuteChangedFor(nameof(DecryptCommand))]
    [NotifyCanExecuteChangedFor(nameof(SignOutCommand))]
    public partial bool IsBusy { get; set; }

    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private async Task Encrypt()
    {
        crashTestService.CrashIfArmed(CrashTestOperation.Encrypt);

        try
        {
            IsBusy = true;
            StatusText = string.Empty;

            PickedFile? file = await fileService.PickFileAsync(
                AppTexts.DialogTitleSelectFilesToEncrypt,
                FilePickerKind.Any);
            if (file is null)
            {
                return;
            }

            EncryptionPreparationResult result = await encryptionPreparationService.EncryptForCurrentProfileAsync(file);
            await UserInterfaceService.NavigateToAsync(AppDestination.EncryptResult, result);
        }
        catch (OperationCanceledException)
        {
            StatusText = string.Empty;
        }
        catch (Exception ex)
        {
            StatusText = FormatStatusText(MobileTexts.DialogTextEncryptFailed, ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private async Task EncryptToShare()
    {
        await UserInterfaceService.NavigateToAsync(AppDestination.EncryptToShare);
    }

    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private async Task Decrypt()
    {
        crashTestService.CrashIfArmed(CrashTestOperation.Decrypt);

        try
        {
            IsBusy = true;
            StatusText = string.Empty;

            bool isPrepared = await PickAndPrepareAsync(enableTextEditing: false);
            if (!isPrepared)
            {
                if (previewService.HasPendingPasswordRequest)
                {
                    await UserInterfaceService.NavigateToAsync(AppDestination.EnterPassword);
                }

                return;
            }

            await UserInterfaceService.NavigateToAsync(AppDestination.Preview);
        }
        catch (OperationCanceledException)
        {
            StatusText = string.Empty;
        }
        catch (Exception ex)
        {
            StatusText = FormatStatusText(MobileTexts.DialogTextOpenEncryptedFileFailed, ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private Task SignOut() => sessionExitService.ExitAsync();

    private bool CanUseCommand()
        => !IsBusy;

    private async Task<bool> PickAndPrepareAsync(bool enableTextEditing)
    {
        PickedFile? file = await fileService.PickFileAsync(
            AppTexts.DialogTitleSelectFileToOpen,
            FilePickerKind.Encrypted);
        if (file is null)
        {
            return false;
        }

        DocumentPreviewFile previewFile = new(file.FileName, file.SourcePath, file.OpenReadAsync);
        bool isPrepared = await previewService.PrepareAsync(previewFile, enableTextEditing);
        if (!isPrepared && !previewService.HasPendingPasswordRequest)
        {
            StatusText = AppTexts.DialogTextWrongPasswordOpen;
        }

        return isPrepared;
    }

    private static string FormatStatusText(string message, Exception exception)
    {
        string exceptionMessage = string.IsNullOrWhiteSpace(exception.Message)
            ? exception.GetType().Name
            : exception.Message;

        return $"{message} {exceptionMessage}";
    }
}
