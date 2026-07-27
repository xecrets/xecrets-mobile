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

namespace Xecrets.Mobile.Models.Services;

public sealed class PreviewService(
    ICoreServices coreServices,
    IProfileService profileService,
    ITransientFileService transientFileService,
    PreviewState previewState,
    DecryptionPasswordRequestState passwordRequestState)
    : IPreviewService
{
    public IPreviewState Current => previewState;

    public bool HasPendingPasswordRequest => passwordRequestState.HasPendingRequest;

    public async Task<bool> PrepareAsync(DocumentPreviewFile encryptedFile, bool enableTextEditing)
    {
        string sourcePath = encryptedFile.SourcePath;
        string encryptedPath = transientFileService.CreateEncryptedInputPath(encryptedFile.FileName);
        await using (Stream inputStream = await encryptedFile.OpenReadAsync())
        await using (FileStream outputStream =
                     File.Open(encryptedPath, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            await inputStream.CopyToAsync(outputStream);
        }

        return await PrepareWithKnownPasswordsAsync(encryptedPath, sourcePath, enableTextEditing);
    }

    public async Task<bool> PrepareImportedAsync(string encryptedFilePath)
    {
        return await PrepareWithKnownPasswordsAsync(encryptedFilePath, string.Empty, enableTextEditing: false);
    }

    public async Task<bool> PrepareWithPasswordAsync(string password)
    {
        if (!passwordRequestState.HasPendingRequest)
        {
            return false;
        }

        bool isPrepared = await TryPrepareAsync(
            passwordRequestState.EncryptedPath,
            passwordRequestState.SourcePath,
            passwordRequestState.EnableTextEditing,
            new Identity(password, []));

        if (!isPrepared)
        {
            return false;
        }

        passwordRequestState.Clear();
        await profileService.RecordExtraPasswordUseAsync(password);
        return true;
    }

    private async Task<bool> PrepareWithKnownPasswordsAsync(
        string encryptedPath,
        string sourcePath,
        bool enableTextEditing)
    {
        passwordRequestState.Clear();

        bool isPrepared = await TryPrepareAsync(
            encryptedPath,
            sourcePath,
            enableTextEditing,
            profileService.GetIdentity());

        if (isPrepared)
        {
            return true;
        }

        foreach (ExtraPasswordSetting extraPassword in profileService.GetExtraPasswords())
        {
            isPrepared = await TryPrepareAsync(
                encryptedPath,
                sourcePath,
                enableTextEditing,
                new Identity(extraPassword.Password, []));

            if (!isPrepared)
            {
                continue;
            }

            await profileService.RecordExtraPasswordUseAsync(extraPassword.Password);
            return true;
        }

        passwordRequestState.Set(encryptedPath, sourcePath, enableTextEditing);
        return false;
    }

    private async Task<bool> TryPrepareAsync(
        string encryptedPath,
        string sourcePath,
        bool enableTextEditing,
        Identity identity)
    {
        await using FileStream encryptedStream = File.OpenRead(encryptedPath);
        using IDecryptionSession session = await coreServices.OpenDecryptionAsync(
            encryptedStream,
            new DecryptRequest([identity], new Progress<Progress>(_ => { })));

        if (!session.IsDecryptable)
        {
            return false;
        }

        string decryptedPath = transientFileService.CreateHandoffPath(session.OriginalFileName);
        await using (FileStream outputStream =
                     File.Open(decryptedPath, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            await session.DecryptAsync(outputStream);
        }

        TryMakeReadOnly(decryptedPath);

        DecryptedFileInfo file = ContentTypeDetector.CreateInfo(decryptedPath, session.OriginalFileName);
        if (file.Kind == PreviewKind.Image)
        {
            previewState.SetImage(file, sourcePath);
        }
        else if (file.Kind == PreviewKind.Text)
        {
            string text = await File.ReadAllTextAsync(decryptedPath);
            previewState.SetText(file, sourcePath, text, enableTextEditing);
        }
        else
        {
            previewState.SetExternal(file, sourcePath);
        }

        return true;
    }

    private static void TryMakeReadOnly(string filePath)
    {
        try
        {
            File.SetAttributes(filePath, File.GetAttributes(filePath) | FileAttributes.ReadOnly);
        }
        catch
        {
            // Best effort. Some mobile filesystems do not support this attribute.
        }
    }
}
