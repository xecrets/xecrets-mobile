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

public sealed class WorkFolderOperationService(
    ICoreServices coreServices,
    IProfileService profileService,
    EncryptRequestFactory encryptRequestFactory,
    IRecentFilesService recentFilesService,
    IFileAccess fileAccess,
    IFileWiper fileWiper,
    IUserInterfaceService userInterfaceService)
    : IWorkFolderOperationService
{
    private FileReference? _pendingFile;

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
            resultId = await fileAccess.WriteFileInFolderAsync(
                folderId,
                destinationName,
                overwrite,
                encrypted => coreServices.EncryptAsync(cleartext, encrypted, request));
        }

        FileReference result = new(resultId, destinationName);
        await CompleteAsync(file, result);
        return result;
    }

    public async Task<FileReference?> DecryptWithKnownPasswordsAsync(FileReference file)
    {
        _pendingFile = null;
        if (await TryDecryptAsync(file, profileService.GetIdentity()) is { } result)
        {
            return result;
        }

        foreach (PasswordUsage extraPassword in profileService.GetExtraPasswords())
        {
            result = await TryDecryptAsync(file, new Identity(extraPassword.Password, []));
            if (result is null)
            {
                continue;
            }

            await profileService.RecordExtraPasswordUseAsync(extraPassword.Password);
            return result;
        }

        _pendingFile = file;
        return null;
    }

    public async Task<FileReference?> DecryptWithPasswordAsync(string password)
    {
        FileReference? file = _pendingFile;
        if (file is null || await TryDecryptAsync(file, new Identity(password, [])) is not { } result)
        {
            return null;
        }

        _pendingFile = null;
        await profileService.RecordExtraPasswordUseAsync(password);
        return result;
    }

    public void CancelPasswordRequest() => _pendingFile = null;

    private async Task<FileReference?> TryDecryptAsync(FileReference file, Identity identity)
    {
        FileReference result;
        await using (Stream encrypted = await fileAccess.OpenReadAsync(file.Id))
        {
            using IDecryptionSession session = await coreServices.OpenDecryptionAsync(
                encrypted,
                new DecryptRequest([identity], new Progress<Progress>(_ => { })));
            if (!session.IsDecryptable)
            {
                return null;
            }

            string folderId = await fileAccess.GetFolderOfFileAsync(file.Id);
            bool overwrite = await ConfirmOverwriteAsync(folderId, session.OriginalFileName);
            string resultId = await fileAccess.WriteFileInFolderAsync(
                folderId,
                session.OriginalFileName,
                overwrite,
                session.DecryptAsync);
            result = new FileReference(resultId, session.OriginalFileName);
        }

        await CompleteAsync(file, result);
        return result;
    }

    /// <summary>
    /// Wipes the source, since storage such as Google Drive only moves a deleted file to its trash, and replaces it with
    /// the result in the recent files, under whatever ids it is listed. The source is removed explicitly, since a file in a trash may still be reported
    /// as existing. The source must no longer be open, since the wipe renames and rewrites it.
    /// <para>
    /// The rights to wipe the source are checked before the operation, so a failure to wipe it is unexpected. The
    /// operation itself has succeeded by then, so the recent files are updated anyway.
    /// </para>
    /// </summary>
    private async Task CompleteAsync(FileReference source, FileReference result)
    {
        try
        {
            if (await fileWiper.WipeAsync(await fileAccess.OpenWritableAsync(source.Id)) == FileWipeStatus.InsufficientRights)
            {
                throw new IOException("The source file could not be deleted.");
            }
        }
        finally
        {
            await recentFilesService.RemoveFileAsync(source.Id);
            await recentFilesService.AddAsync(result, RecentFileOperation.InPlace);
        }
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
