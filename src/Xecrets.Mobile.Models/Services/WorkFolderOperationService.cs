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

using Xecrets.Core.Abstractions;
using Xecrets.Core.Models;
using Xecrets.Common.Models;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Utilities;
using Xecrets.Texts;

namespace Xecrets.Mobile.Models.Services;

internal sealed class WorkFolderOperationService(
    ICoreServices coreServices,
    IProfileService profileService,
    IEncryptionPreparationService encryptionPreparationService,
    ITransientFileService transientFileService,
    IRecentFilesService recentFilesService,
    IFileAccess fileAccess,
    IFileWiper fileWiper,
    ICrashLogService crashLogService,
    IUserInterfaceService userInterfaceService)
    : IWorkFolderOperationService
{
    private FileReference? _pendingFile;

    // Whether the pending file is in none of My folders, so that the result is saved where the user chooses.
    private bool _isPendingElsewhere;

    public bool HasPendingPasswordRequest => _pendingFile is not null;

    /// <summary>
    /// The result is encrypted to a temporary file, which is wiped once it is saved in the folder of the file, or not.
    /// </summary>
    public async Task<FileReference> EncryptAsync(FileReference file)
    {
        string destinationName = file.Name.ToEncryptedName("");
        string folderId = await fileAccess.GetFolderOfFileAsync(file.Id);
        bool overwrite = await ConfirmOverwriteAsync(folderId, destinationName);
        EncryptionPreparationResult encrypted = await encryptionPreparationService.EncryptWithCurrentProfileAsync(
            new ReferencedReadableFile(fileAccess, file));
        string resultId;
        try
        {
            resultId = await fileAccess.SaveInFolderAsync(folderId, destinationName, encrypted.FilePath, overwrite);
        }
        finally
        {
            await transientFileService.WipeTransientFileAsync(encrypted.FilePath);
        }

        FileReference result = file with { Id = resultId, Name = destinationName };
        await CompleteAsync(file, result, MobileTexts.DialogTextFileEncrypted);

        return result;
    }

    /// <summary>
    /// Once the operation has started, any failure is reported as denied access, which is what it typically is for a
    /// file reached by itself.
    /// </summary>
    public async Task<FileOperationOutcome> EncryptElsewhereAsync(FileReference file)
    {
        try
        {
            EncryptionPreparationResult encrypted = await encryptionPreparationService.EncryptWithCurrentProfileAsync(
                new ReferencedReadableFile(fileAccess, file));
            try
            {
                return await ExportResultAsync(
                    file,
                    encrypted.FilePath,
                    encrypted.DisplayName,
                    MobileTexts.DialogTextFileEncrypted);
            }
            finally
            {
                await transientFileService.WipeTransientFileAsync(encrypted.FilePath);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            crashLogService.WriteSwallowedException(ex);
            return await ReportAsync(DestinationStatus.AccessDenied);
        }
    }

    public async Task<FileReference?> DecryptWithKnownPasswordsAsync(FileReference file) =>
        (await DecryptWithKnownPasswordsAsync(file, isElsewhere: false)).Result;

    public Task<FileOperationOutcome> DecryptElsewhereWithKnownPasswordsAsync(FileReference file) =>
        DecryptWithKnownPasswordsAsync(file, isElsewhere: true);

    public async Task<FileReference?> DecryptWithPasswordAsync(string password) =>
        (await DecryptPendingWithPasswordAsync(password)).Result;

    public async Task<FileOperationOutcome> DecryptPendingWithPasswordAsync(string password)
    {
        FileReference? file = _pendingFile;
        if (file is null ||
            await TryDecryptAsync(file, new Identity(password, []), _isPendingElsewhere) is not { } outcome)
        {
            return FileOperationOutcome.AwaitingPassword;
        }

        _pendingFile = null;
        await profileService.RecordExtraPasswordUseAsync(password);

        return outcome;
    }

    public void CancelPasswordRequest() => _pendingFile = null;

    private async Task<FileOperationOutcome> DecryptWithKnownPasswordsAsync(FileReference file, bool isElsewhere)
    {
        _pendingFile = null;
        if (await TryDecryptAsync(file, profileService.GetIdentity(), isElsewhere) is { } outcome)
        {
            return outcome;
        }

        foreach (PasswordUsage extraPassword in profileService.GetExtraPasswords())
        {
            outcome = await TryDecryptAsync(file, new Identity(extraPassword.Password, []), isElsewhere);
            if (outcome is null)
            {
                continue;
            }

            await profileService.RecordExtraPasswordUseAsync(extraPassword.Password);
            return outcome;
        }

        _pendingFile = file;
        _isPendingElsewhere = isElsewhere;
        return FileOperationOutcome.AwaitingPassword;
    }

    // Returns null when the file cannot be decrypted with the identity. As for encryption, once the operation elsewhere
    // has started any failure is reported as denied access.
    private async Task<FileOperationOutcome?> TryDecryptAsync(FileReference file, Identity identity, bool isElsewhere)
    {
        if (!isElsewhere)
        {
            return await TryDecryptAndSaveAsync(file, identity, isElsewhere);
        }

        try
        {
            return await TryDecryptAndSaveAsync(file, identity, isElsewhere);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            crashLogService.WriteSwallowedException(ex);
            return await ReportAsync(DestinationStatus.AccessDenied);
        }
    }

    /// <summary>
    /// The result is decrypted to a temporary file, as for a decrypted copy, which is wiped once it is saved, or not. In
    /// one of My folders, replacing a file of the same name is confirmed before decrypting. The source is closed before
    /// the result is saved, since it is wiped once it has been.
    /// </summary>
    private async Task<FileOperationOutcome?> TryDecryptAndSaveAsync(
        FileReference file,
        Identity identity,
        bool isElsewhere)
    {
        string resultPath = "";
        try
        {
            string resultName;
            string folderId = "";
            bool overwrite = false;
            await using (Stream encrypted = await fileAccess.OpenReadAsync(file.Id))
            {
                using IDecryptionSession session = await OpenDecryptionAsync(encrypted, identity);
                if (!session.IsDecryptable)
                {
                    return null;
                }

                resultName = session.OriginalFileName;
                if (!isElsewhere)
                {
                    folderId = await fileAccess.GetFolderOfFileAsync(file.Id);
                    overwrite = await ConfirmOverwriteAsync(folderId, resultName);
                }

                resultPath = transientFileService.CreateHandoffPath(resultName);
                await using FileStream decrypted = File.Create(resultPath);
                await session.DecryptAsync(decrypted);
            }

            if (isElsewhere)
            {
                return await ExportResultAsync(file, resultPath, resultName, MobileTexts.DialogTextFileDecrypted);
            }

            string resultId = await fileAccess.SaveInFolderAsync(folderId, resultName, resultPath, overwrite);
            FileReference result = file with { Id = resultId, Name = resultName };
            await CompleteAsync(file, result, MobileTexts.DialogTextFileDecrypted);
            return FileOperationOutcome.Completed(result);
        }
        finally
        {
            if (resultPath.Length > 0)
            {
                await transientFileService.WipeTransientFileAsync(resultPath);
            }
        }
    }

    /// <summary>
    /// Lets the user choose where to save a complete result, starting in the folder of the source, and completes the
    /// operation once it is saved. A name the user chose in the picker is not told, while one the storage changed from
    /// the name chosen, such as when it is taken, is told to the user, since the result is then already saved. The saved
    /// file is not opened again.
    /// </summary>
    private async Task<FileOperationOutcome> ExportResultAsync(
        FileReference source,
        string resultPath,
        string suggestedName,
        string completedText)
    {
        FileReference? saved = await fileAccess.SaveAsAsync(
            await fileAccess.GetParentFolderAsync(source.Id),
            suggestedName,
            resultPath);
        if (saved is null)
        {
            return await ReportAsync(DestinationStatus.Canceled);
        }

        if (IsProbablyChangedByOsFromSuggestion(suggestedName, saved.Name))
        {
            await userInterfaceService.DisplayMessageAsync(string.Format(MobileTexts.DialogTextSavedAs, saved.Name));
        }

        return await CompleteElsewhereAsync(source, saved, completedText);
    }

    private Task<IDecryptionSession> OpenDecryptionAsync(Stream encrypted, Identity identity) =>
        coreServices.OpenDecryptionAsync(
            encrypted,
            new DecryptRequest([identity], new Progress<Progress>(_ => { })));

    /// <summary>
    /// Wipes the source, and replaces it with the result in the recent files, under whatever ids it is listed. The
    /// source is removed explicitly, since a file in a trash may still be reported as existing. The source must no
    /// longer be open, since the wipe may rewrite it. The user is told that the operation is done, and also when the
    /// source is left.
    /// </summary>
    private async Task CompleteAsync(FileReference source, FileReference result, string completedText)
    {
        bool isWiped = await TryWipeSourceAsync(source);
        await recentFilesService.RemoveFileAsync(source.Id);
        await recentFilesService.AddAsync(result, RecentFileOperation.InPlace);
        await userInterfaceService.DisplayTransientMessageAsync(
            isWiped ? completedText : MobileTexts.DialogTextOriginalNotRemoved);
    }

    /// <summary>
    /// Wipes the source once the result has been saved and closed. Neither is in one of My folders, so the result is
    /// not listed in the recent files, while the source is no longer listed once wiped, should it have been. The user is
    /// told that the operation is done, and also when the source is left.
    /// </summary>
    private async Task<FileOperationOutcome> CompleteElsewhereAsync(
        FileReference source,
        FileReference result,
        string completedText)
    {
        if (!await TryWipeSourceAsync(source))
        {
            await userInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextOriginalNotRemoved);
            return FileOperationOutcome.Completed(result);
        }

        await recentFilesService.RemoveFileAsync(source.Id);
        await userInterfaceService.DisplayTransientMessageAsync(completedText);
        return FileOperationOutcome.Completed(result);
    }

    /// <summary>
    /// Wipes the source, which on the device overwrites it, and elsewhere only deletes it. The rights to wipe it are
    /// checked before the operation, but storage may still refuse, such as Dropbox when offline. The operation itself
    /// has succeeded by then, so the source is then only left, and the failure logged.
    /// </summary>
    private async Task<bool> TryWipeSourceAsync(FileReference source)
    {
        try
        {
            return await fileWiper.WipeAsync(await fileAccess.OpenWriteAsync(source.Id), source.Root.IsOnDevice()) !=
                FileWipeStatus.InsufficientRights;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            crashLogService.WriteSwallowedException(ex);
            return false;
        }
    }

    private static bool IsProbablyChangedByOsFromSuggestion(string suggestedName, string name) =>
        name.Length > suggestedName.Length &&
        name.StartsWith(Path.GetFileNameWithoutExtension(suggestedName), StringComparison.OrdinalIgnoreCase) &&
        name.EndsWith(Path.GetExtension(suggestedName), StringComparison.OrdinalIgnoreCase);

    private async Task<FileOperationOutcome> ReportAsync(DestinationStatus status)
    {
        switch (status)
        {
            case DestinationStatus.Canceled:
                await userInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextOperationNotCompleted);
                break;

            case DestinationStatus.AccessDenied:
                await userInterfaceService.DisplayMessageAsync(MobileTexts.DialogTextAccessDeniedOrNotSupported);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, @"Not a reason for not being done.");
        }

        return FileOperationOutcome.NotDone;
    }

    private async Task<bool> ConfirmOverwriteAsync(string folderId, string destinationName)
    {
        if (!await fileAccess.FileExistsInFolderAsync(folderId, destinationName))
        {
            return false;
        }

        bool overwrite = await userInterfaceService.DisplayConfirmationAsync(
            string.Format(MobileTexts.DialogTextConfirmOverwrite, destinationName));
        if (!overwrite)
        {
            throw new OperationCanceledException();
        }

        return true;
    }
}
