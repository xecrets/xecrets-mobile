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

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Utilities;
using Xecrets.Texts;

namespace Xecrets.Mobile.Models.Services;

internal sealed class EncryptionPreparationService(
    EncryptRequestFactory encryptRequestFactory,
    IProfileService profileService,
    ITransientFileService transientFileService,
    ICoreServices coreServices)
    : IEncryptionPreparationService
{
    public async Task<EncryptionPreparationResult> EncryptWithCurrentProfileAsync(IReadableFile source)
    {
        await using Stream cleartext = await source.OpenReadAsync();
        return await EncryptAsync(cleartext, source.FileName, encryptRequestFactory.Create(source.FileName), false);
    }

    public async Task<EncryptionPreparationResult> EncryptWithPasswordAsync(IReadableFile source, string password)
    {
        await using Stream cleartext = await source.OpenReadAsync();
        return await EncryptAsync(cleartext, source.FileName, encryptRequestFactory.Create(source.FileName, password), true);
    }

    /// <summary>
    /// The file is decrypted with any of the known passwords to a temporary file, as for a decrypted copy,
    /// which is wiped once the copy is encrypted, keeping the original name and times of the file.
    /// </summary>
    public async Task<EncryptionPreparationResult?> ReEncryptWithPasswordAsync(IReadableFile encryptedSource, string password)
    {
        await using Stream encrypted = await encryptedSource.OpenReadAsync();
        using IDecryptionSession session = await coreServices.OpenDecryptionAsync(
            encrypted,
            new DecryptRequest(KnownIdentities(), new Progress<Progress>(_ => { })));
        if (!session.IsDecryptable)
        {
            return null;
        }

        string cleartextPath = transientFileService.CreateHandoffPath(session.OriginalFileName);
        try
        {
            await using (FileStream decrypted = File.Create(cleartextPath))
            {
                await session.DecryptAsync(decrypted);
            }

            EncryptRequest request = encryptRequestFactory.Create(session.OriginalFileName, password, session.EncryptedWithParameters) with
            {
                CreationTimeUtc = session.CreationTimeUtc,
                LastAccessTimeUtc = session.LastAccessTimeUtc,
                LastWriteTimeUtc = session.LastWriteTimeUtc,
            };
            await using FileStream cleartext = File.OpenRead(cleartextPath);
            return await EncryptAsync(cleartext, session.OriginalFileName, request, true);
        }
        finally
        {
            await transientFileService.WipeTransientFileAsync(cleartextPath);
        }
    }

    private List<Identity> KnownIdentities() =>
    [
        profileService.GetIdentity(),
        .. profileService.GetExtraPasswords().Select(extraPassword => new Identity(extraPassword.Password, [])),
    ];

    private async Task<EncryptionPreparationResult> EncryptAsync(
        Stream cleartext,
        string originalFileName,
        EncryptRequest request,
        bool encryptCopyForSharing)
    {
        string fileName = originalFileName.ToEncryptedName(string.Empty);
        string temporaryPath = transientFileService.CreateEncryptedOutputPath(fileName);
        await using FileStream encrypted = File.Create(temporaryPath);
        await coreServices.EncryptAsync(cleartext, encrypted, request);

        long fileSize = new FileInfo(temporaryPath).Length;
        return new EncryptionPreparationResult(
            temporaryPath,
            fileName,
            EncryptedFileType.ContentType,
            fileSize,
            encryptCopyForSharing);
    }

}
