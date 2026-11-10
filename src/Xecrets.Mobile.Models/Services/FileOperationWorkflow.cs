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
    IEncryptionPreparationService encryptionPreparationService,
    ICrashTestService crashTestService,
    ICrashLogService crashLogService,
    RunningOperation runningOperation,
    IUserInterfaceService userInterfaceService)
{
    // The core tells an encrypted file by its format id at the start, which is far shorter than this.
    private const int _encryptedStartLength = 512;

    private readonly SingleFileCompletionReporter _singleFileReporter = new(recentFilesService, userInterfaceService);

    /// <summary>
    /// Lets the user pick a file, starting in the folder, or where the platform decides without one, and does what is
    /// intended with it. Unless it is deleted or shared, the name of the file decides whether it is encrypted or
    /// decrypted. A file in one of My folders is changed where it is, through the folder. A file in none of them,
    /// elsewhere, is changed by the access to the file itself, with the result saved where the user chooses. A copy is
    /// made of the file as picked, wherever it is, and the file is not changed.
    /// </summary>
    public async Task PickAndRunAsync(WorkFolderIntent intent, WorkFolder? folder)
    {
        // Before anything is caught, so that an armed crash is not swallowed.
        if (intent == WorkFolderIntent.Copy)
        {
            crashTestService.CrashIfArmed(CrashTestOperation.Encrypt);
            crashTestService.CrashIfArmed(CrashTestOperation.Decrypt);
        }

        FileReference? picked = await fileAccess.PickFileAsync(folder?.Id ?? "", FilePickerKind.Any);
        if (picked is null)
        {
            return;
        }

        switch (intent)
        {
            case WorkFolderIntent.Copy:
                await CopyAsync(picked);
                return;

            case WorkFolderIntent.Share:
                await ShareAsync(picked);
                return;

            case WorkFolderIntent.Auto:
            case WorkFolderIntent.Delete:
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(intent), intent, @"Unknown work folder intent.");
        }

        if (await folderAccess.FindInFoldersAsync(picked) is { } file)
        {
            await RunIntentAsync(intent, file, isElsewhere: false);
            return;
        }

        await RunIntentAsync(intent, picked, isElsewhere: true);
    }

    /// <summary>
    /// Whether the contents of the file show that it is encrypted, whatever its name. Only the start of the file is
    /// read, which is all that tells it.
    /// </summary>
    public Task<bool> IsFileEncryptedAsync(FileReference file) =>
        coreServices.IsEncryptedAsync(async () =>
            new MemoryStream(await fileAccess.ReadStartAsync(file.Id, _encryptedStartLength)));

    /// <summary>
    /// Whether the file is not to be encrypted, since its contents or its name show that it is encrypted already.
    /// </summary>
    public async Task<bool> IsFileOrNameEncryptedAsync(FileReference file) =>
        file.Name.IsNameEncrypted() || await IsFileEncryptedAsync(file);

    /// <summary>
    /// Encrypts or decrypts a file in one of My folders, returning the file it is replaced by, that nothing was done, or
    /// that it needed a password the user did not enter. The file is replaced by the result, so nothing is done unless
    /// the file can be wiped afterward. A file named or found to be encrypted is not encrypted, and a file not found to
    /// be encrypted is not decrypted, but the user is told so.
    /// </summary>
    public Task<FileOperationOutcome> TransformAsync(
        FileReference file,
        WorkFolderOperation operation,
        FlowOrigin origin) =>
        RunAsync([file], ToFileOperation(operation), origin, isElsewhere: false);

    /// <summary>
    /// Securely deletes a file in one of My folders, once the user confirms it, and removes it from the recent files.
    /// </summary>
    public Task WipeAsync(FileReference file) => WipeAsync(file, isElsewhere: false);

    /// <summary>
    /// Does the operation on the files in turn, each as for a file by itself, with what is in their folders from the
    /// folder contents. Deleting the files is to be confirmed by the user before. The first file not done ends the
    /// operation, as does a file after which the reporter does not go on. For a file that needs a password the app does
    /// not know, the user is sent on to enter one, and the operation waits until the user is done, going on once a
    /// password has decrypted the file, and ending when the user leaves without one. A canceled operation ends before
    /// the next file, or while a file is encrypted or decrypted, which the reporter tells. Returns what became of the
    /// last file done, or of the file that ended the operation.
    /// </summary>
    public async Task<FileOperationOutcome> RunAsync(
        IReadOnlyList<FileReference> files,
        FileOperation operation,
        FlowOrigin origin,
        bool isElsewhere,
        FolderContents contents,
        IFileCompletionReporter reporter)
    {
        FileOperationOutcome outcome = FileOperationOutcome.NotDone;
        try
        {
            foreach (FileReference file in files)
            {
                await runningOperation.ThrowIfCanceledAsync();
                outcome = await RunOnFileAsync(file, operation, origin, isElsewhere, contents, reporter);
                if (outcome.Status == FileOperationStatus.Skipped)
                {
                    continue;
                }

                if (outcome.Status != FileOperationStatus.Completed || !await reporter.ContinuesAfterFileAsync())
                {
                    return outcome;
                }
            }
        }
        catch (OperationCanceledException)
        {
            await reporter.ReportRunCanceledAsync();
            return FileOperationOutcome.Canceled;
        }

        await reporter.ReportRunCompletedAsync();
        return outcome;
    }

    // A file chosen by itself is the only file of the operation, and its folder is listed when it is needed.
    private Task<FileOperationOutcome> RunAsync(
        FileReference[] files,
        FileOperation operation,
        FlowOrigin origin,
        bool isElsewhere) =>
        RunAsync(files, operation, origin, isElsewhere, new FolderContents(fileAccess, []), _singleFileReporter);

    private Task<FileOperationOutcome> RunOnFileAsync(
        FileReference file,
        FileOperation operation,
        FlowOrigin origin,
        bool isElsewhere,
        FolderContents contents,
        IFileCompletionReporter reporter) => operation switch
        {
            FileOperation.Encrypt =>
                TransformAsync(file, WorkFolderOperation.Encrypt, origin, isElsewhere, contents, reporter),
            FileOperation.Decrypt =>
                TransformAsync(file, WorkFolderOperation.Decrypt, origin, isElsewhere, contents, reporter),
            FileOperation.Delete => WipeConfirmedAsync(file, isElsewhere, reporter),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, @"Unknown file operation."),
        };

    private static FileOperation ToFileOperation(WorkFolderOperation operation) => operation switch
    {
        WorkFolderOperation.Encrypt => FileOperation.Encrypt,
        WorkFolderOperation.Decrypt => FileOperation.Decrypt,
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, @"Unknown work folder operation."),
    };

    private async Task RunIntentAsync(WorkFolderIntent intent, FileReference file, bool isElsewhere)
    {
        switch (intent)
        {
            case WorkFolderIntent.Delete:
                await WipeAsync(file, isElsewhere);
                return;

            case WorkFolderIntent.Auto:
                await RunAsync(
                    [file],
                    file.Name.IsNameEncrypted() ? FileOperation.Decrypt : FileOperation.Encrypt,
                    FlowOrigin.MyFolders,
                    isElsewhere);
                return;

            case WorkFolderIntent.Copy:
            case WorkFolderIntent.Share:
            default:
                throw new ArgumentOutOfRangeException(nameof(intent), intent, @"Unknown work folder intent.");
        }
    }

    /// <summary>
    /// The name of the file decides whether it is decrypted to a preview, or encrypted to a copy.
    /// </summary>
    private async Task CopyAsync(FileReference file)
    {
        if (!file.Name.IsNameEncrypted())
        {
            await EncryptCopyAsync(file);
            return;
        }

        // The name alone does not make the file encrypted.
        if (!await IsFileEncryptedAsync(file))
        {
            await userInterfaceService.DisplayMessageAsync(
                string.Format(MobileTexts.DialogTextNotEncryptedFormat, file.Name));
            return;
        }

        if (!await PreviewAsync(file, FlowOrigin.Home))
        {
            await userInterfaceService.DisplayMessageAsync(MobileTexts.DialogTextWrongPasswordOpen);
        }
    }

    private async Task EncryptCopyAsync(FileReference file)
    {
        if (await IsFileOrNameEncryptedAsync(file))
        {
            await userInterfaceService.DisplayMessageAsync(
                string.Format(MobileTexts.DialogTextAlreadyEncryptedFormat, file.Name));
            return;
        }

        flowContext.Begin(FlowOrigin.Home, file);
        EncryptionPreparationResult result = await encryptionPreparationService.EncryptWithCurrentProfileAsync(
            new ReferencedReadableFile(fileAccess, file));
        await userInterfaceService.NavigateToAsync(AppDestination.EncryptResult, result);
    }

    /// <summary>
    /// The user is sent on to enter the password to encrypt, or re-encrypt, a copy of the file with. The extension of the file
    /// must match the contents so a .axx file must be actually encrypted, and a file without .axx must not be encrypted.
    /// </summary>
    private async Task ShareAsync(FileReference file)
    {
        if (!file.Name.IsNameEncrypted() && await IsFileEncryptedAsync(file))
        {
            await userInterfaceService.DisplayMessageAsync(
                string.Format(MobileTexts.DialogTextAlreadyEncryptedFormat, file.Name));
            return;
        }

        if (file.Name.IsNameEncrypted() && !await IsFileEncryptedAsync(file))
        {
            await userInterfaceService.DisplayMessageAsync(
                string.Format(MobileTexts.DialogTextNotEncryptedFormat, file.Name));
            return;
        }

        flowContext.Begin(FlowOrigin.Home, file);
        await userInterfaceService.NavigateToAsync(AppDestination.EncryptToShare, file);
    }

    /// <summary>
    /// A file elsewhere is replaced by a result saved where the user chooses, and the user is told when the access to
    /// it does not allow that.
    /// </summary>
    private async Task<FileOperationOutcome> TransformAsync(
        FileReference file,
        WorkFolderOperation operation,
        FlowOrigin origin,
        bool isElsewhere,
        FolderContents contents,
        IFileCompletionReporter reporter)
    {
        if (operation == WorkFolderOperation.Encrypt && await IsFileOrNameEncryptedAsync(file))
        {
            await reporter.ReportSkippedAsync(string.Format(MobileTexts.DialogTextAlreadyEncryptedFormat, file.Name));
            return FileOperationOutcome.Skipped;
        }

        if (operation == WorkFolderOperation.Decrypt && !await IsFileEncryptedAsync(file))
        {
            await reporter.ReportSkippedAsync(string.Format(MobileTexts.DialogTextNotEncryptedFormat, file.Name));
            return FileOperationOutcome.Skipped;
        }

        if (!await fileWiper.CanWipeAsync(await fileAccess.OpenWriteAsync(file.Id)))
        {
            await userInterfaceService.DisplayMessageAsync(
                isElsewhere ? MobileTexts.DialogTextAccessDeniedOrNotSupported : MobileTexts.DialogTextInsufficientRights);
            return FileOperationOutcome.NotDone;
        }

        flowContext.Begin(origin);
        if (isElsewhere)
        {
            return await TransformElsewhereAsync(file, operation);
        }

        FileOperationOutcome outcome = await operationService.TransformAsync(file, operation, contents, reporter);
        return outcome.Status == FileOperationStatus.PasswordNeeded ? await AskForPasswordAsync() : outcome;
    }

    /// <summary>
    /// Asks the user for a password for the file just found to need one, and returns what became of the file once the
    /// request ends. The request is taken before the page for passwords is shown, since it ends when the page is left,
    /// which may be before the navigation to it is done.
    /// </summary>
    private async Task<FileOperationOutcome> AskForPasswordAsync()
    {
        Task<FileOperationOutcome> request = operationService.WaitForPasswordRequestAsync();
        await ThrowIfCanceledAsync(operationService.CancelPasswordRequest);
        await userInterfaceService.NavigateToAsync(AppDestination.EnterPassword, DecryptionPasswordsIntent.Add);
        return await request;
    }

    // Before the user is asked for a password, the request for it is ended when the operation is canceled, so that it
    // is not left for the next time the page for passwords is shown.
    private async Task ThrowIfCanceledAsync(Action cancelPasswordRequest)
    {
        try
        {
            await runningOperation.ThrowIfCanceledAsync();
        }
        catch (OperationCanceledException)
        {
            cancelPasswordRequest();
            throw;
        }
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

            case FileOperationStatus.PasswordNeeded:
                return await AskForPasswordAsync();

            case FileOperationStatus.NotDone:
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome.Status, @"Unknown file operation status.");
        }

        return outcome;
    }

    /// <summary>
    /// A file elsewhere is checked before the user is asked. The user is always asked to confirm, also where other
    /// storage may still keep the file as versions or in a trash. Any failure to wipe it is reported as denied access,
    /// which is what it typically is.
    /// </summary>
    private async Task WipeAsync(FileReference file, bool isElsewhere)
    {
        if (isElsewhere && !await fileWiper.CanWipeAsync(await fileAccess.OpenWriteAsync(file.Id)))
        {
            await userInterfaceService.DisplayMessageAsync(MobileTexts.DialogTextAccessDeniedOrNotSupported);
            return;
        }

        if (!await userInterfaceService.DisplayConfirmationAsync(
                MobileTexts.MessageTextConfirmWipe, MobileTexts.ButtonOk, MobileTexts.LabelCancel))
        {
            return;
        }

        await RunAsync([file], FileOperation.Delete, FlowOrigin.MyFolders, isElsewhere);
    }

    /// <summary>
    /// Securely deletes a file that the user has already confirmed deleting, removes it from the recent files, and
    /// tells the reporter. Returns that nothing was done when the user has been told that the file can't be deleted.
    /// </summary>
    private async Task<FileOperationOutcome> WipeConfirmedAsync(
        FileReference file,
        bool isElsewhere,
        IFileCompletionReporter reporter)
    {
        IWritableFile writable = await fileAccess.OpenWriteAsync(file.Id);
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
                isElsewhere ? MobileTexts.DialogTextAccessDeniedOrNotSupported : MobileTexts.DialogTextInsufficientRights);
            return FileOperationOutcome.NotDone;
        }

        await recentFilesService.RemoveFileAsync(file.Id);
        // Storage that keeps versions or a trash may keep what was only deleted.
        string message = status == FileWipeStatus.OnlyDelete
            ? file.Root.IsWipedForGood() ? MobileTexts.DialogTextFileDeleted : MobileTexts.DialogTextFileDeletedInCloud
            : MobileTexts.DialogTextFileOverwritten;
        await reporter.ReportDeletedAsync(message);
        return FileOperationOutcome.Completed(file);
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
            await ThrowIfCanceledAsync(previewService.CancelPasswordRequest);
            await userInterfaceService.NavigateToAsync(AppDestination.EnterPassword, DecryptionPasswordsIntent.Add);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Lets the user save a copy of the complete file anywhere, with the times given, as far as they can be set.
    /// Returns null when the user cancels, and otherwise the saved file.
    /// </summary>
    public async Task<FileReference?> SaveAsAsync(string fileName, string completedFilePath, FileTimes times)
    {
        FileReference? file = await fileAccess.SaveAsAsync("", fileName, completedFilePath);
        if (file is not null)
        {
            await fileAccess.SetSupportedTimesAsync(file.Id, times);

            await userInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextFileSaved);
        }

        return file;
    }
}
