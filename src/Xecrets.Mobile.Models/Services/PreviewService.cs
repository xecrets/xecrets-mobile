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
using Xecrets.Core.Public;
using Xecrets.Common.Models;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Models.Services;

internal sealed class PreviewService(
    ICoreServices coreServices,
    IProfileService profileService,
    ITransientFileService transientFileService,
    PreviewState previewState,
    DecryptionPasswordRequestState passwordRequestState,
    RunningOperation runningOperation,
    ICrashLogService crashLogService)
    : IPreviewService
{
    public IPreviewState Current => previewState;

    public bool HasPendingPasswordRequest => passwordRequestState.HasPendingRequest;

    public void CancelPasswordRequest() => passwordRequestState.Clear();

    public async Task PrepareTextAsync(DecryptedFileInfo file, EncryptedWithParameters encryptedWith)
    {
        byte[] plaintext = await File.ReadAllBytesAsync(file.FilePath);
        previewState.SetText(file with { FilePath = "" }, plaintext, encryptedWith);
    }

    public async Task<DecryptedFileInfo> MaterializeAsync()
    {
        DecryptedFileInfo file = previewState.File;
        if (file.FilePath.Length > 0)
        {
            return file;
        }

        ITransientFile decryptedFile = transientFileService.CreateHandoffFile(file.OriginalFileName);
        try
        {
            await File.WriteAllBytesAsync(decryptedFile.FilePath, previewState.Plaintext);
        }
        catch
        {
            // What was written before it failed is not left until the cache is cleaned.
            decryptedFile.Dispose();
            throw;
        }

        TryMakeReadOnly(decryptedFile.FilePath);
        previewState.SetDecryptedFile(decryptedFile);
        return previewState.File;
    }

    public async Task<bool> PrepareAsync(IReadableFile encryptedFile)
    {
        ITransientFile encryptedCopy = transientFileService.CreateEncryptedInputFile(encryptedFile.FileName);
        try
        {
            await using Stream inputStream = await encryptedFile.OpenReadAsync();
            await using FileStream outputStream =
                File.Open(encryptedCopy.FilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            await inputStream.CopyToAsync(outputStream);
        }
        catch
        {
            // What was copied before it failed is not left until the cache is cleaned.
            encryptedCopy.Dispose();
            throw;
        }

        return await PrepareWithKnownPasswordsAsync(encryptedCopy)
            == PreviewPreparationStatus.Prepared;
    }

    public async Task<PreviewPreparationStatus> PrepareImportedAsync(ITransientFile encryptedFile)
    {
        return await PrepareWithKnownPasswordsAsync(encryptedFile);
    }

    public async Task<PreviewPreparationStatus> PrepareWithPasswordAsync(string password)
    {
        if (!passwordRequestState.HasPendingRequest)
        {
            return PreviewPreparationStatus.Cancelled;
        }

        PreviewPreparationStatus status = await TryPrepareAsync(
            passwordRequestState.EncryptedPath,
            new Identity(password, []));

        if (status == PreviewPreparationStatus.WrongPassword)
        {
            return status;
        }

        passwordRequestState.Clear();
        if (status == PreviewPreparationStatus.Cancelled)
        {
            return status;
        }

        await profileService.RecordExtraPasswordUseAsync(password);
        return status;
    }

    /// <summary>
    /// Tries the known passwords on the encrypted file. On a wrong password, the file is kept for the password request,
    /// and otherwise it is disposed. The file of any earlier request is disposed.
    /// </summary>
    private async Task<PreviewPreparationStatus> PrepareWithKnownPasswordsAsync(ITransientFile encryptedFile)
    {
        passwordRequestState.Clear();

        PreviewPreparationStatus status;
        try
        {
            status = await TryKnownPasswordsAsync(encryptedFile.FilePath);
        }
        catch
        {
            encryptedFile.Dispose();
            throw;
        }

        if (status == PreviewPreparationStatus.WrongPassword)
        {
            passwordRequestState.Set(encryptedFile);
        }
        else
        {
            encryptedFile.Dispose();
        }

        return status;
    }

    private async Task<PreviewPreparationStatus> TryKnownPasswordsAsync(string encryptedPath)
    {
        PreviewPreparationStatus status = await TryPrepareAsync(
            encryptedPath,
            profileService.GetIdentity());

        if (status != PreviewPreparationStatus.WrongPassword)
        {
            return status;
        }

        foreach (PasswordUsage extraPassword in profileService.GetExtraPasswords())
        {
            status = await TryPrepareAsync(
                encryptedPath,
                new Identity(extraPassword.Password, []));

            if (status == PreviewPreparationStatus.Cancelled)
            {
                return status;
            }

            if (status == PreviewPreparationStatus.WrongPassword)
            {
                continue;
            }

            await profileService.RecordExtraPasswordUseAsync(extraPassword.Password);
            return status;
        }

        return PreviewPreparationStatus.WrongPassword;
    }

    private async Task<PreviewPreparationStatus> TryPrepareAsync(
        string encryptedPath,
        Identity identity)
    {
        await using FileStream? encryptedStream = encryptedPath.OpenReadIfExists();
        if (encryptedStream is null)
        {
            return PreviewPreparationStatus.Cancelled;
        }

        using IDecryptionSession session = await coreServices.OpenDecryptionAsync(
            encryptedStream,
            new DecryptRequest([identity], runningOperation.CreateProgress()));

        if (!session.IsDecryptable)
        {
            return PreviewPreparationStatus.WrongPassword;
        }

        // Text that the app shows and edits itself is kept in memory, and anything else is written to a file.
        ITransientFile? decryptedFile = null;
        InMemoryOrFileStream output = new(
            session.OriginalFileName.WithoutCopyNumber(),
            new Dictionary<FileCategory, long> { [FileCategory.Text] = PreviewInfoFactory.MaxInlineTextSizeBytes },
            () =>
            {
                decryptedFile = transientFileService.CreateHandoffFile(session.OriginalFileName);
                return File.Open(decryptedFile.FilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            });
        try
        {
            await using (output)
            {
                await session.DecryptAsync(output);
                output.Commit();
            }
        }
        catch
        {
            // What was decrypted before the operation was canceled, or failed, is not left until the cache is cleaned.
            decryptedFile?.Dispose();
            throw;
        }

        if (decryptedFile is null)
        {
            DecryptedFileInfo text = PreviewInfoFactory.CreateInfo(
                "",
                session.OriginalFileName,
                output.Category,
                output.Data.Length,
                CreateTimes(session));
            previewState.SetText(text, output.Data.ToArray(), session.EncryptedWithParameters);
            return PreviewPreparationStatus.Prepared;
        }

        TryMakeReadOnly(decryptedFile.FilePath);
        DecryptedFileInfo file = PreviewInfoFactory.CreateInfo(
            decryptedFile.FilePath,
            session.OriginalFileName,
            output.Category,
            new FileInfo(decryptedFile.FilePath).Length,
            CreateTimes(session));
        previewState.SetFile(file, session.EncryptedWithParameters, decryptedFile);

        return PreviewPreparationStatus.Prepared;
    }

    private static FileTimes CreateTimes(IDecryptionSession session) =>
        new(session.CreationTimeUtc, session.LastAccessTimeUtc, session.LastWriteTimeUtc);

    private void TryMakeReadOnly(string filePath)
    {
        try
        {
            File.SetAttributes(filePath, File.GetAttributes(filePath) | FileAttributes.ReadOnly);
        }
        catch (Exception ex)
        {
            // Best effort. Some mobile filesystems do not support this attribute.
            crashLogService.WriteSwallowedException(ex);
        }
    }
}
