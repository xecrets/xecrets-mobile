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
    IUserInterfaceService userInterfaceService)
    : IWorkFolderOperationService
{
    private WorkFolderFile? _pendingFile;

    public bool HasPendingPasswordRequest => _pendingFile is not null;

    public async Task EncryptAsync(WorkFolderFile file)
    {
        string destinationName = file.FileName.ToEncryptedName(string.Empty);
        bool overwrite = await ConfirmOverwriteAsync(file, destinationName);
        EncryptRequest request = CreateEncryptRequest(file.FileName);

        await using Stream cleartext = await file.OpenReadAsync();
        await file.WriteDestinationAsync(
            destinationName,
            overwrite,
            encrypted => coreServices.EncryptAsync(cleartext, encrypted, request));
        await file.DeleteAsync();
    }

    public async Task<bool> DecryptWithKnownPasswordsAsync(WorkFolderFile file)
    {
        _pendingFile = null;
        if (await TryDecryptAsync(file, profileService.GetIdentity()))
        {
            return true;
        }

        foreach (PasswordUsage extraPassword in profileService.GetExtraPasswords())
        {
            if (!await TryDecryptAsync(file, new Identity(extraPassword.Password, [])))
            {
                continue;
            }

            await profileService.RecordExtraPasswordUseAsync(extraPassword.Password);
            return true;
        }

        _pendingFile = file;
        return false;
    }

    public async Task<bool> DecryptWithPasswordAsync(string password)
    {
        WorkFolderFile? file = _pendingFile;
        if (file is null || !await TryDecryptAsync(file, new Identity(password, [])))
        {
            return false;
        }

        _pendingFile = null;
        await profileService.RecordExtraPasswordUseAsync(password);
        return true;
    }

    public void CancelPasswordRequest() => _pendingFile = null;

    private async Task<bool> TryDecryptAsync(WorkFolderFile file, Identity identity)
    {
        await using Stream encrypted = await file.OpenReadAsync();
        using IDecryptionSession session = await coreServices.OpenDecryptionAsync(
            encrypted,
            new DecryptRequest([identity], new Progress<Progress>(_ => { })));
        if (!session.IsDecryptable)
        {
            return false;
        }

        bool overwrite = await ConfirmOverwriteAsync(file, session.OriginalFileName);
        await file.WriteDestinationAsync(
            session.OriginalFileName,
            overwrite,
            session.DecryptAsync);
        await file.DeleteAsync();
        return true;
    }

    private async Task<bool> ConfirmOverwriteAsync(WorkFolderFile file, string destinationName)
    {
        if (!await file.DestinationExistsAsync(destinationName))
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

    private EncryptRequest CreateEncryptRequest(string originalFileName)
    {
        DateTime utcNow = DateTime.UtcNow;
        Identity identity = profileService.GetIdentity();
        return new EncryptRequest(
            identity.Passphrase,
            [profileService.GetPublicKey()],
            [],
            originalFileName,
            utcNow,
            utcNow,
            utcNow,
            true,
            new Progress<Progress>(_ => { }));
    }
}
