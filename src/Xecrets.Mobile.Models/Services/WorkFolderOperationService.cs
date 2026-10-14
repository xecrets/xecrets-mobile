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
    EncryptRequestFactory encryptRequestFactory,
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

    public async Task<FileReference> EncryptAsync(FileReference file)
    {
        string destinationName = file.Name.ToEncryptedName(string.Empty);
        string folderId = await fileAccess.GetFolderOfFileAsync(file.Id);
        bool overwrite = await ConfirmOverwriteAsync(folderId, destinationName);
        EncryptRequest request = encryptRequestFactory.ForCurrentProfile(file.Name);
        string resultId;

        await using (Stream cleartext = await fileAccess.OpenReadAsync(file.Id))
        {
            await using IFileWriteSession destination = await fileAccess.OpenWriteInFolderAsync(
                folderId,
                destinationName,
                overwrite);
            await coreServices.EncryptAsync(cleartext, destination.OutputStream, request);
            resultId = await destination.CommitAsync();
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
            PickedDestination? destination = await CreateDestinationAsync(
                file,
                file.Name.ToEncryptedName(string.Empty),
                isEncrypted: true);
            if (destination is null)
            {
                return FileOperationOutcome.NotDone;
            }

            EncryptRequest request = encryptRequestFactory.ForCurrentProfile(file.Name);
            await using (Stream cleartext = await fileAccess.OpenReadAsync(file.Id))
            {
                await WriteDestinationAsync(
                    destination.File,
                    output => coreServices.EncryptAsync(cleartext, output, request));
            }

            return await CompleteElsewhereAsync(file, destination.Reference, MobileTexts.DialogTextFileEncrypted);
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

    // Returns null when the file cannot be decrypted with the identity.
    private async Task<FileOperationOutcome?> TryDecryptAsync(FileReference file, Identity identity, bool isElsewhere)
    {
        if (isElsewhere)
        {
            return await TryDecryptElsewhereAsync(file, identity);
        }

        FileReference result;
        await using (Stream encrypted = await fileAccess.OpenReadAsync(file.Id))
        {
            using IDecryptionSession session = await OpenDecryptionAsync(encrypted, identity);
            if (!session.IsDecryptable)
            {
                return null;
            }

            string folderId = await fileAccess.GetFolderOfFileAsync(file.Id);
            bool overwrite = await ConfirmOverwriteAsync(folderId, session.OriginalFileName);

            await using IFileWriteSession destination = await fileAccess.OpenWriteInFolderAsync(
                folderId,
                session.OriginalFileName,
                overwrite);
            await session.DecryptAsync(destination.OutputStream);
            string resultId = await destination.CommitAsync();
            result = file with { Id = resultId, Name = session.OriginalFileName };
        }

        await CompleteAsync(file, result, MobileTexts.DialogTextFileDecrypted);
        return FileOperationOutcome.Completed(result);
    }

    // As for encryption, once the operation has started any failure is reported as denied access.
    private async Task<FileOperationOutcome?> TryDecryptElsewhereAsync(FileReference file, Identity identity)
    {
        try
        {
            PickedDestination? destination;
            await using (Stream encrypted = await fileAccess.OpenReadAsync(file.Id))
            {
                using IDecryptionSession session = await OpenDecryptionAsync(encrypted, identity);
                if (!session.IsDecryptable)
                {
                    return null;
                }

                destination = await CreateDestinationAsync(file, session.OriginalFileName, isEncrypted: false);
                if (destination is null)
                {
                    return FileOperationOutcome.NotDone;
                }

                await WriteDestinationAsync(destination.File, session.DecryptAsync);
            }

            return await CompleteElsewhereAsync(file, destination.Reference, MobileTexts.DialogTextFileDecrypted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            crashLogService.WriteSwallowedException(ex);
            return await ReportAsync(DestinationStatus.AccessDenied);
        }
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

    /// <summary>
    /// Lets the user choose where to save the result, and checks that the file chosen can be used for it. A file that
    /// cannot is removed again only when it is empty, such as when just created, since it may be an existing file the
    /// user chose to replace. Returns null, once the user has been told why, when there is no file to save it in.
    /// </summary>
    private async Task<PickedDestination?> CreateDestinationAsync(FileReference source, string name, bool isEncrypted)
    {
        FileReference? picked = await fileAccess.CreateFileAsync(await fileAccess.GetParentFolderAsync(source.Id), name);
        if (picked is null)
        {
            await ReportAsync(DestinationStatus.Canceled);
            return null;
        }

        IWritableFile file = await fileAccess.OpenWriteAsync(picked.Id);
        DestinationStatus status = await CheckDestinationAsync(picked, name, isEncrypted);
        if (status != DestinationStatus.Ready)
        {
            if (await file.GetLengthAsync() == 0)
            {
                await DeleteCreatedAsync(file, picked.Name);
            }

            await ReportAsync(status);
            return null;
        }

        return new PickedDestination(picked, file);
    }

    /// <summary>
    /// The user is told when the file could not be deleted, since it is left where the user chose to save it. Storage
    /// such as Dropbox may not yet delete a file it has only just created, which the platform retries for a while.
    /// </summary>
    private async Task DeleteCreatedAsync(IWritableFile file, string name)
    {
        try
        {
            using IDisposable access = file.AcquireAccess();
            await file.DeleteAsync();
        }
        catch (Exception ex)
        {
            crashLogService.WriteSwallowedException(ex);
            await userInterfaceService.DisplayMessageAsync(string.Format(MobileTexts.DialogTextCouldNotDeleteFile, name));
        }
    }

    /// <summary>
    /// The rights to the file are not checked, since storage such as Dropbox cannot tell them for a file it has only
    /// just created, and does not tell that a file can be written even when it can. Writing it tells, and a failure to
    /// write it is reported as denied access.
    /// <para>
    /// A name changed from the one suggested, by something added before the extension, is confirmed by the user. It is
    /// typically the storage giving the file another name since the one chosen is taken, such as Dropbox does, where
    /// an existing file cannot be chosen to be replaced.
    /// </para>
    /// </summary>
    private async Task<DestinationStatus> CheckDestinationAsync(FileReference picked, string suggestedName, bool isEncrypted)
    {
        if (picked.Name.IsEncrypted() != isEncrypted)
        {
            return DestinationStatus.FileTypeNotAllowed;
        }

        if (IsProbablyChangedByOsFromSuggestion(suggestedName, picked.Name) &&
            !await userInterfaceService.DisplayConfirmationAsync(
                string.Format(MobileTexts.DialogTextConfirmSaveAs, picked.Name)))
        {
            return DestinationStatus.Canceled;
        }

        return DestinationStatus.Ready;
    }

    private static bool IsProbablyChangedByOsFromSuggestion(string suggestedName, string name) =>
        name.Length > suggestedName.Length &&
        name.StartsWith(Path.GetFileNameWithoutExtension(suggestedName), StringComparison.OrdinalIgnoreCase) &&
        name.EndsWith(Path.GetExtension(suggestedName), StringComparison.OrdinalIgnoreCase);

    // A result that could not be written is removed, so that what is left of it is not taken for the result. A file
    // that could not even be opened is left as it is. The file may be an existing one that is replaced, and is only
    // truncated at the end, where the stream allows it, since a stream that cannot seek is truncated when opened.
    private async Task WriteDestinationAsync(IWritableFile destination, Func<Stream, Task> writeAsync)
    {
        using IDisposable access = destination.AcquireAccess();
        Stream output = await destination.OpenWriteAsync();
        try
        {
            await using (output)
            {
                await writeAsync(output);
                if (output.CanSeek)
                {
                    output.SetLength(output.Position);
                }
            }
        }
        catch
        {
            await TryDeleteAsync(destination);
            throw;
        }
    }

    private async Task TryDeleteAsync(IWritableFile file)
    {
        try
        {
            using IDisposable access = file.AcquireAccess();
            await file.DeleteAsync();
        }
        catch (Exception ex)
        {
            // Best effort - what made the file unusable is what is reported.
            crashLogService.WriteSwallowedException(ex);
        }
    }

    private async Task<FileOperationOutcome> ReportAsync(DestinationStatus status)
    {
        switch (status)
        {
            case DestinationStatus.Canceled:
                await userInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextOperationNotCompleted);
                break;

            case DestinationStatus.AccessDenied:
                await userInterfaceService.DisplayMessageAsync(MobileTexts.DialogTextAccessDenied);
                break;

            case DestinationStatus.FileTypeNotAllowed:
                await userInterfaceService.DisplayMessageAsync(MobileTexts.DialogTextFileTypeNotAllowed);
                break;

            case DestinationStatus.Ready:
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

    private sealed record PickedDestination(FileReference Reference, IWritableFile File);
}
