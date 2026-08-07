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
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Utilities;
using Xecrets.Texts;

namespace Xecrets.Mobile.Models.Services;

public sealed class EncryptionPreparationService(
    IProfileService profileService,
    ITransientFileService transientFileService,
    ICoreServices coreServices)
    : IEncryptionPreparationService
{
    public async Task<EncryptionPreparationResult> EncryptForCurrentProfileAsync(PickedFile file)
    {
        await using Stream cleartext = await file.OpenReadAsync();
        return await EncryptAsync(
            cleartext,
            file.FileName,
            file.SourcePath,
            CreateCurrentProfileEncryptRequest(file.FileName));
    }

    public async Task<EncryptionPreparationResult> EncryptForPasswordAsync(PickedFile file, string password)
    {
        await using Stream cleartext = await file.OpenReadAsync();
        return await EncryptAsync(
            cleartext,
            file.FileName,
            file.SourcePath,
            CreatePasswordEncryptRequest(file.FileName, password));
    }

    private async Task<EncryptionPreparationResult> EncryptAsync(
        Stream cleartext,
        string originalFileName,
        string originalSourcePath,
        EncryptRequest request)
    {
        string fileName = originalFileName.ToEncryptedName(string.Empty);
        string temporaryPath = transientFileService.CreateEncryptedOutputPath(fileName);
        await using FileStream encrypted = File.Create(temporaryPath);
        await coreServices.EncryptAsync(cleartext, encrypted, request);

        long fileSize = new FileInfo(temporaryPath).Length;
        return new EncryptionPreparationResult(
            temporaryPath,
            fileName,
            originalSourcePath,
            EncryptedFileType.ContentType,
            fileSize);
    }

    private EncryptRequest CreateCurrentProfileEncryptRequest(string originalFileName)
    {
        DateTime utcNow = DateTime.UtcNow;
        var identity = profileService.GetIdentity();
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

    private static EncryptRequest CreatePasswordEncryptRequest(string originalFileName, string password)
    {
        DateTime utcNow = DateTime.UtcNow;
        return new EncryptRequest(
            password,
            [],
            [],
            originalFileName,
            utcNow,
            utcNow,
            utcNow,
            true,
            new Progress<Progress>(_ => { }));
    }
}
