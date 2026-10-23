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
using Xecrets.Texts;

namespace Xecrets.Mobile.Models.PageModels;

public partial class HomePageModel(
    IProfileService profileService,
    IFileAccess fileAccess,
    FileOperationWorkflow fileOperationWorkflow,
    IEncryptionPreparationService encryptionPreparationService,
    ICrashTestService crashTestService,
    SessionExitService sessionExitService,
    IFlowContext flowContext,
    ICrashLogService crashLogService,
    IUserInterfaceService userInterfaceService)
    : PageModelBase(userInterfaceService), IStatusTextPageModel, IBreadcrumbPageModel
{
    public string Breadcrumb => MobileTexts.BreadcrumbHome;

    [ObservableProperty]
    public partial string Email { get; set; } = profileService.CurrentEmail;

    [ObservableProperty]
    public partial string MessageText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MyFoldersCommand))]
    [NotifyCanExecuteChangedFor(nameof(RecentFilesCommand))]
    [NotifyCanExecuteChangedFor(nameof(InAppCommand))]
    [NotifyCanExecuteChangedFor(nameof(EncryptToShareCommand))]
    [NotifyCanExecuteChangedFor(nameof(WipeCommand))]
    [NotifyCanExecuteChangedFor(nameof(SignOutCommand))]
    public partial bool IsBusy { get; set; }

    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private Task MyFoldersAsync() => UserInterfaceService.NavigateToAsync(AppDestination.WorkFolders);

    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private Task RecentFilesAsync() => UserInterfaceService.NavigateToAsync(AppDestination.RecentFiles);

    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private Task Wipe() => UserInterfaceService.NavigateToAsync(AppDestination.WorkFolders, WorkFolderIntent.Delete);

    // The name of the picked file decides whether it is decrypted to a preview, or encrypted to a copy.
    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private async Task InApp()
    {
        flowContext.Begin(FlowOrigin.Home);

        // Before anything is caught, so that an armed crash is not swallowed.
        crashTestService.CrashIfArmed(CrashTestOperation.Encrypt);
        crashTestService.CrashIfArmed(CrashTestOperation.Decrypt);
        try
        {
            IsBusy = true;
            StatusText = string.Empty;
            FileReference? file = await fileAccess.PickFileAsync(string.Empty, FilePickerKind.Any);
            if (file is null)
            {
                return;
            }

            if (file.Name.IsEncrypted())
            {
                await DecryptInAppAsync(file);
            }
            else
            {
                await EncryptInAppAsync(file);
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = string.Empty;
        }
        catch (Exception ex)
        {
            crashLogService.WriteSwallowedException(ex);
            StatusText = ex.FormatException();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private async Task EncryptToShare()
    {
        flowContext.Begin(FlowOrigin.Home);
        await UserInterfaceService.NavigateToAsync(AppDestination.EncryptToShare);
    }

    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private Task SignOut() => sessionExitService.ExitAsync();

    private bool CanUseCommand()
        => !IsBusy;

    private async Task EncryptInAppAsync(FileReference file)
    {
        if (await fileOperationWorkflow.IsFileOrNameEncryptedAsync(file))
        {
            await UserInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextAlreadyEncrypted);
            return;
        }

        flowContext.UpdateSource(file);
        EncryptionPreparationResult result = await encryptionPreparationService.EncryptForCurrentProfileAsync(
            new ReferencedReadableFile(fileAccess, file));
        await UserInterfaceService.NavigateToAsync(AppDestination.EncryptResult, result);
    }

    private async Task DecryptInAppAsync(FileReference file)
    {
        // The name alone does not make the file encrypted.
        if (!await fileOperationWorkflow.IsFileEncryptedAsync(file))
        {
            await UserInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextNotEncrypted);
            return;
        }

        if (!await fileOperationWorkflow.PreviewAsync(file, FlowOrigin.Home))
        {
            StatusText = MobileTexts.DialogTextWrongPasswordOpen;
        }
    }
}
