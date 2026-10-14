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
using Xecrets.Texts;

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
    ICrashLogService crashLogService,
    IUserInterfaceService userInterfaceService)
{
    /// <summary>
    /// Lets the user pick a file, starting in the folder, or where the platform decides without one, and does what is
    /// intended with it. Without a given operation, the name of the file decides whether it is encrypted or decrypted.
    /// A file in one of My folders is changed where it is, through the folder. A file in none of them, elsewhere, is
    /// changed by the access to the file itself, with the result saved where the user chooses.
    /// </summary>
    public async Task PickAndRunAsync(WorkFolderIntent intent, WorkFolder? folder)
    {
        FilePickerKind pickerKind = intent == WorkFolderIntent.Decrypt ? FilePickerKind.Encrypted : FilePickerKind.Any;
        FileReference? picked = await fileAccess.PickFileAsync(folder?.Id ?? string.Empty, pickerKind);
        if (picked is null)
        {
            return;
        }

        if (await folderAccess.FindInFoldersAsync(picked) is { } file)
        {
            await RunAsync(intent, file, isElsewhere: false);
            return;
        }

        await RunAsync(intent, picked, isElsewhere: true);
    }

    /// <summary>
    /// Whether the contents of the file show that it is encrypted, whatever its name.
    /// </summary>
    public Task<bool> IsFileEncryptedAsync(FileReference file) =>
        coreServices.IsEncryptedAsync(() => fileAccess.OpenReadAsync(file.Id));

    /// <summary>
    /// Whether the file is not to be encrypted, since its contents or its name show that it is encrypted already.
    /// </summary>
    public async Task<bool> IsFileOrNameEncryptedAsync(FileReference file) =>
        file.Name.IsEncrypted() || await IsFileEncryptedAsync(file);

    /// <summary>
    /// Encrypts or decrypts a file in one of My folders, returning the file it is replaced by, that nothing was done, or
    /// that the user was sent on to enter a password for it. The file is replaced by the result, so nothing is done
    /// unless the file can be wiped afterwards. A file named or found to be encrypted is not encrypted, and a file not
    /// found to be encrypted is not decrypted, but the user is told so.
    /// </summary>
    public Task<FileOperationOutcome> TransformAsync(
        FileReference file,
        WorkFolderOperation operation,
        FlowOrigin origin) =>
        TransformAsync(file, operation, origin, isElsewhere: false);

    /// <summary>
    /// Securely deletes a file in one of My folders, once the user confirms it, and removes it from the recent files.
    /// </summary>
    public Task WipeAsync(FileReference file) => WipeAsync(file, isElsewhere: false);

    private async Task RunAsync(WorkFolderIntent intent, FileReference file, bool isElsewhere)
    {
        switch (intent)
        {
            case WorkFolderIntent.Delete:
                await WipeAsync(file, isElsewhere);
                return;

            case WorkFolderIntent.Encrypt:
                await TransformAsync(file, WorkFolderOperation.Encrypt, FlowOrigin.MyFolders, isElsewhere);
                return;

            case WorkFolderIntent.Decrypt:
                await TransformAsync(file, WorkFolderOperation.Decrypt, FlowOrigin.MyFolders, isElsewhere);
                return;

            case WorkFolderIntent.Auto:
                await TransformAsync(
                    file,
                    file.Name.IsEncrypted() ? WorkFolderOperation.Decrypt : WorkFolderOperation.Encrypt,
                    FlowOrigin.MyFolders,
                    isElsewhere);
                return;

            default:
                throw new ArgumentOutOfRangeException(nameof(intent), intent, @"Unknown work folder intent.");
        }
    }

    /// <summary>
    /// A file elsewhere is replaced by a result saved where the user chooses, and the user is told when the access to
    /// it does not allow that.
    /// </summary>
    private async Task<FileOperationOutcome> TransformAsync(
        FileReference file,
        WorkFolderOperation operation,
        FlowOrigin origin,
        bool isElsewhere)
    {
        if (operation == WorkFolderOperation.Encrypt && await IsFileOrNameEncryptedAsync(file))
        {
            await userInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextAlreadyEncrypted);
            return FileOperationOutcome.NotDone;
        }

        if (operation == WorkFolderOperation.Decrypt && !await IsFileEncryptedAsync(file))
        {
            await userInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextNotEncrypted);
            return FileOperationOutcome.NotDone;
        }

        if (!await fileWiper.CanWipeAsync(await fileAccess.OpenWriteAsync(file.Id)))
        {
            await userInterfaceService.DisplayMessageAsync(
                isElsewhere ? MobileTexts.DialogTextAccessDenied : MobileTexts.DialogTextInsufficientRights);
            return FileOperationOutcome.NotDone;
        }

        flowContext.Begin(origin);
        if (isElsewhere)
        {
            return await TransformElsewhereAsync(file, operation);
        }

        if (operation == WorkFolderOperation.Encrypt)
        {
            return FileOperationOutcome.Completed(await operationService.EncryptAsync(file));
        }

        if (await operationService.DecryptWithKnownPasswordsAsync(file) is { } decrypted)
        {
            return FileOperationOutcome.Completed(decrypted);
        }

        await userInterfaceService.NavigateToAsync(AppDestination.EnterPassword);
        return FileOperationOutcome.AwaitingPassword;
    }

    private async Task<FileOperationOutcome> TransformElsewhereAsync(FileReference file, WorkFolderOperation operation)
    {
        FileOperationOutcome outcome = operation == WorkFolderOperation.Encrypt
            ? await operationService.EncryptElsewhereAsync(file)
            : await operationService.DecryptElsewhereWithKnownPasswordsAsync(file);
        switch (outcome.Status)
        {
            case FileOperationStatus.Completed:
                break;

            case FileOperationStatus.AwaitingPassword:
                await userInterfaceService.NavigateToAsync(AppDestination.EnterPassword);
                break;

            case FileOperationStatus.NotDone:
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome.Status, @"Unknown file operation status.");
        }

        return outcome;
    }

    /// <summary>
    /// A file elsewhere is checked before the user is asked. Any failure to wipe it is reported as denied access,
    /// which is what it typically is.
    /// </summary>
    private async Task WipeAsync(FileReference file, bool isElsewhere)
    {
        IWritableFile writable = await fileAccess.OpenWriteAsync(file.Id);
        if (isElsewhere && !await fileWiper.CanWipeAsync(writable))
        {
            await userInterfaceService.DisplayMessageAsync(MobileTexts.DialogTextAccessDenied);
            return;
        }

        if (!await userInterfaceService.DisplayConfirmationAsync(MobileTexts.MessageTextConfirmWipe))
        {
            return;
        }

        FileWipeStatus status;
        try
        {
            status = await fileWiper.WipeAsync(writable, file.Root.IsOnDevice());
        }
        catch (Exception ex) when (isElsewhere && ex is not OperationCanceledException)
        {
            crashLogService.WriteSwallowedException(ex);
            status = FileWipeStatus.InsufficientRights;
        }

        if (status == FileWipeStatus.InsufficientRights)
        {
            await userInterfaceService.DisplayMessageAsync(
                isElsewhere ? MobileTexts.DialogTextAccessDenied : MobileTexts.DialogTextInsufficientRights);
            return;
        }


        await recentFilesService.RemoveFileAsync(file.Id);
        await userInterfaceService.DisplayTransientMessageAsync(status == FileWipeStatus.OnlyDelete ? MobileTexts.DialogTextFileDeleted : MobileTexts.DialogTextFileOverwritten);
    }

    /// <summary>
    /// Decrypts a file temporarily to show it, or sends the user on to enter a password for it. Returns false when the
    /// file could not be decrypted.
    /// </summary>
    public async Task<bool> PreviewAsync(FileReference file, FlowOrigin origin)
    {
        flowContext.Begin(origin, file);
        IReadableFile previewFile = new ReferencedReadableFile(fileAccess, file);
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
