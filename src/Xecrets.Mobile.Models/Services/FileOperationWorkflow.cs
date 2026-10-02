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

using Xecrets.Common.Models;
using Xecrets.Core.Abstractions;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Models.Services;

/// <summary>
/// The operations on a file that the user starts from My folders, the recent files or the home page.
/// </summary>
public sealed class FileOperationWorkflow(
    FolderAccessWorkflow folderAccess,
    IFileAccess fileAccess,
    IWorkFolderOperationService operationService,
    IPreviewService previewService,
    IFlowContext flowContext,
    ICoreServices coreServices,
    IFileWiper fileWiper,
    IRecentFilesService recentFilesService,
    IUserInterfaceService userInterfaceService)
{
    /// <summary>
    /// Lets the user pick a file in the folder, and does what is intended with it. Without a given operation, the
    /// contents of the file decide whether it is encrypted or decrypted. A file that is not encrypted is not decrypted,
    /// but the user is told so.
    /// </summary>
    public async Task PickAndRunAsync(WorkFolderIntent intent, WorkFolder folder)
    {
        FilePickerKind pickerKind = intent == WorkFolderIntent.Decrypt ? FilePickerKind.Encrypted : FilePickerKind.Any;
        FileReference? file = await folderAccess.PickWritableFileAsync(folder, pickerKind);
        if (file is null)
        {
            return;
        }

        switch (intent)
        {
            case WorkFolderIntent.Delete:
                await WipeAsync(file);
                return;

            case WorkFolderIntent.Encrypt:
                await TransformAsync(file, WorkFolderOperation.Encrypt);
                return;
        }

        bool isEncrypted = await IsEncryptedAsync(file);
        if (intent == WorkFolderIntent.Decrypt && !isEncrypted)
        {
            await userInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextNotEncrypted);
            return;
        }

        await TransformAsync(file, isEncrypted ? WorkFolderOperation.Decrypt : WorkFolderOperation.Encrypt);
    }

    public Task<bool> IsEncryptedAsync(FileReference file) =>
        coreServices.IsEncryptedAsync(() => fileAccess.OpenReadAsync(file.Id));

    /// <summary>
    /// Encrypts or decrypts a file in one of My folders, returning the file it is replaced by, that nothing was done, or
    /// that the user was sent on to enter a password for it. The file is replaced by the result, so nothing is done
    /// unless the file can be wiped afterwards.
    /// </summary>
    public async Task<FileOperationOutcome> TransformAsync(FileReference file, WorkFolderOperation operation)
    {
        if (!await fileWiper.CanWipeAsync(await fileAccess.OpenWritableAsync(file.Id)))
        {
            await userInterfaceService.DisplayMessageAsync(MobileTexts.DialogTextInsufficientRights);
            return FileOperationOutcome.NotDone;
        }

        flowContext.Begin(FlowOrigin.Navigated, operation);
        if (operation == WorkFolderOperation.Encrypt)
        {
            if (await IsEncryptedAsync(file))
            {
                await userInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextAlreadyEncrypted);
                return FileOperationOutcome.NotDone;
            }

            FileReference encrypted = await operationService.EncryptAsync(file);
            await userInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextFileEncrypted);
            return FileOperationOutcome.Completed(encrypted);
        }

        if (await operationService.DecryptWithKnownPasswordsAsync(file) is { } decrypted)
        {
            await userInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextFileDecrypted);
            return FileOperationOutcome.Completed(decrypted);
        }

        await userInterfaceService.NavigateToAsync(AppDestination.EnterPassword);
        return FileOperationOutcome.AwaitingPassword;
    }

    /// <summary>
    /// Securely deletes a file in one of My folders, once the user confirms it, and removes it from the recent files.
    /// </summary>
    public async Task WipeAsync(FileReference file)
    {
        if (!await userInterfaceService.DisplayConfirmationAsync(MobileTexts.MessageTextConfirmWipe))
        {
            return;
        }

        FileWipeStatus status = await fileWiper.WipeAsync(await fileAccess.OpenWritableAsync(file.Id));
        if (status == FileWipeStatus.InsufficientRights)
        {
            await userInterfaceService.DisplayMessageAsync(MobileTexts.DialogTextInsufficientRights);
            return;
        }


        await recentFilesService.RemoveFileAsync(file.Id);
        await userInterfaceService.DisplayTransientMessageAsync(status == FileWipeStatus.OnlyDelete ? MobileTexts.DialogTextFileDeleted : MobileTexts.DialogTextFileOverwritten);
    }

    /// <summary>
    /// Decrypts a file temporarily to show it, or sends the user on to enter a password for it. Returns false when the
    /// file could not be decrypted.
    /// </summary>
    public async Task<bool> PreviewAsync(FileReference file)
    {
        flowContext.Begin(FlowOrigin.Navigated, WorkFolderOperation.Decrypt, file);
        DocumentPreviewFile previewFile = new(file.Name, () => fileAccess.OpenReadAsync(file.Id));
        if (await previewService.PrepareAsync(previewFile))
        {
            await userInterfaceService.NavigateToAsync(AppDestination.Preview);
            return true;
        }

        if (previewService.HasPendingPasswordRequest)
        {
            await userInterfaceService.NavigateToAsync(AppDestination.EnterPassword);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Lets the user save a copy anywhere. Returns null when the user cancels, and otherwise the saved file.
    /// </summary>
    public async Task<FileReference?> SaveAsAsync(string fileName, Stream content)
    {
        FileReference? file = await fileAccess.SaveAsAsync(fileName, content);
        if (file is not null)
        {
            await userInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextFileSaved);
        }

        return file;
    }
}
