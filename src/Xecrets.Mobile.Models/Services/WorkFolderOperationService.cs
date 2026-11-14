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

/// <summary>
/// Encrypts and decrypts files where they are, as part of the <see cref="RunningOperation"/>. Results are made in
/// temporary files, which are wiped once saved, or not.
/// </summary>
internal sealed class WorkFolderOperationService(
    ICoreServices coreServices,
    IProfileService profileService,
    IEncryptionPreparationService encryptionPreparationService,
    ITransientFileService transientFileService,
    IRecentFilesService recentFilesService,
    IFileAccess fileAccess,
    IInterruptedSaveService interruptedSaves,
    IFileWiper fileWiper,
    RunningOperation runningOperation,
    ICrashLogService crashLogService,
    IUserInterfaceService userInterfaceService)
    : IWorkFolderOperationService
{
    private readonly SingleFileCompletionReporter _singleFileReporter = new(recentFilesService, userInterfaceService);

    private PasswordRequest? _passwordRequest;

    /// <inheritdoc/>
    public bool HasPendingPasswordRequest => _passwordRequest is { Outcome: null };

    /// <inheritdoc/>
    public async Task<FileOperationOutcome> TransformAsync(
        FileReference file,
        WorkFolderOperation operation,
        FolderContents contents,
        IFileCompletionReporter reporter) =>
        operation switch
        {
            WorkFolderOperation.Encrypt => await EncryptAsync(file, contents, reporter),
            WorkFolderOperation.Decrypt =>
                await DecryptWithKnownPasswordsAsync(file, isElsewhere: false, contents, reporter),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, @"Unknown work folder operation."),
        };

    /// <summary>
    /// The result is encrypted to a temporary file, which is wiped once it is saved in the folder of the file, or not.
    /// </summary>
    private async Task<FileOperationOutcome> EncryptAsync(
        FileReference file,
        FolderContents contents,
        IFileCompletionReporter reporter)
    {
        string destinationName = file.Name.ToEncryptedName("");
        ListedFolder folder = await contents.GetFolderOfAsync(file);
        (bool isConfirmed, string? existingId) = await ConfirmOverwriteAsync(folder, destinationName);
        if (!isConfirmed)
        {
            await reporter.ReportReplaceDeclinedAsync();
            return FileOperationOutcome.Skipped;
        }

        EncryptionPreparationResult encrypted = await encryptionPreparationService.EncryptWithCurrentProfileAsync(
            new ReferencedReadableFile(fileAccess, file));
        string resultId;
        try
        {
            resultId = await SaveInFolderAsync(file, folder, destinationName, encrypted.FilePath, existingId);
        }
        finally
        {
            encrypted.File.Dispose();
        }

        FileReference result = file with { Id = resultId, Name = destinationName };
        await CompleteAsync(file, result, MobileTexts.DialogTextFileEncrypted, folder, reporter);

        return FileOperationOutcome.Completed(result);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Once the operation has started, any failure but canceling is reported as denied access, which is what it
    /// typically is for a file reached by itself.
    /// </remarks>
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
                    MobileTexts.DialogTextFileEncrypted,
                    FileTimes.UtcNow);
            }
            finally
            {
                encrypted.File.Dispose();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            crashLogService.WriteSwallowedException(ex);
            return await ReportAsync(DestinationStatus.AccessDenied);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A file elsewhere is in none of My folders, so its folder is never listed.
    /// </remarks>
    public Task<FileOperationOutcome> DecryptElsewhereWithKnownPasswordsAsync(FileReference file) =>
        DecryptWithKnownPasswordsAsync(
            file,
            isElsewhere: true,
            new FolderContents(fileAccess, []),
            _singleFileReporter);

    /// <inheritdoc/>
    /// <remarks>
    /// The outcome is kept in the request, and given to <see cref="WaitForPasswordRequestAsync"/> once the request
    /// ends.
    /// </remarks>
    public async Task<FileOperationOutcome> DecryptPendingWithPasswordAsync(string password)
    {
        if (_passwordRequest is not { Outcome: null } request)
        {
            return FileOperationOutcome.PasswordNeeded;
        }

        Identity identity = new(password, []);
        FileOperationOutcome? outcome =
            await TryDecryptAsync(request.File, identity, request.IsElsewhere, request.Contents, request.Reporter);
        if (outcome is null)
        {
            return FileOperationOutcome.PasswordNeeded;
        }

        request.Outcome = outcome;
        await profileService.RecordExtraPasswordUseAsync(password);

        return outcome;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// There is no request when the page for passwords is left after it was opened for something else.
    /// </remarks>
    public void CancelPasswordRequest()
    {
        _passwordRequest?.Completion.SetResult(_passwordRequest.Outcome ?? FileOperationOutcome.PasswordNeeded);
        _passwordRequest = null;
    }

    /// <inheritdoc/>
    public Task<FileOperationOutcome> WaitForPasswordRequestAsync() => _passwordRequest!.Completion.Task;

    private async Task<FileOperationOutcome> DecryptWithKnownPasswordsAsync(
        FileReference file,
        bool isElsewhere,
        FolderContents contents,
        IFileCompletionReporter reporter)
    {
        _passwordRequest = null;
        if (await TryDecryptAsync(file, profileService.GetIdentity(), isElsewhere, contents, reporter) is { } outcome)
        {
            return outcome;
        }

        foreach (PasswordUsage extraPassword in profileService.GetExtraPasswords())
        {
            outcome = await TryDecryptAsync(
                file,
                new Identity(extraPassword.Password, []),
                isElsewhere,
                contents,
                reporter);
            if (outcome is null)
            {
                continue;
            }

            await profileService.RecordExtraPasswordUseAsync(extraPassword.Password);
            return outcome;
        }

        _passwordRequest = new PasswordRequest(file, isElsewhere, contents, reporter);
        return FileOperationOutcome.PasswordNeeded;
    }

    // Returns null when the file cannot be decrypted with the identity. As for encryption, once the operation elsewhere
    // has started any failure is reported as denied access.
    private async Task<FileOperationOutcome?> TryDecryptAsync(
        FileReference file,
        Identity identity,
        bool isElsewhere,
        FolderContents contents,
        IFileCompletionReporter reporter)
    {
        if (!isElsewhere)
        {
            return await TryDecryptAndSaveAsync(file, identity, isElsewhere, contents, reporter);
        }

        try
        {
            return await TryDecryptAndSaveAsync(file, identity, isElsewhere, contents, reporter);
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
        bool isElsewhere,
        FolderContents contents,
        IFileCompletionReporter reporter)
    {
        // Made only once the file is found to be decryptable, and replacing any file of its name is confirmed.
        ITransientFile? resultFile = null;
        try
        {
            string resultName;
            FileTimes resultTimes;
            ListedFolder? folder = null;
            string? existingId = null;
            await using (Stream encrypted = await fileAccess.OpenReadAsync(file.Id))
            {
                using IDecryptionSession session = await OpenDecryptionAsync(encrypted, identity);
                if (!session.IsDecryptable)
                {
                    return null;
                }

                resultName = session.OriginalFileName;
                resultTimes = new FileTimes(session.CreationTimeUtc, session.LastAccessTimeUtc, session.LastWriteTimeUtc);
                if (!isElsewhere)
                {
                    folder = await contents.GetFolderOfAsync(file);
                    (bool isConfirmed, existingId) = await ConfirmOverwriteAsync(folder, resultName);
                    if (!isConfirmed)
                    {
                        await reporter.ReportReplaceDeclinedAsync();
                        return FileOperationOutcome.Skipped;
                    }
                }

                resultFile = transientFileService.CreateHandoffFile(resultName);
                await using FileStream decrypted = File.Create(resultFile.FilePath);
                await session.DecryptAsync(decrypted);
            }

            if (isElsewhere)
            {
                return await ExportResultAsync(
                    file, resultFile.FilePath, resultName, MobileTexts.DialogTextFileDecrypted, resultTimes);
            }

            // The folder is listed for a file in one of My folders.
            ListedFolder destination = folder!;
            string resultId = await SaveInFolderAsync(file, destination, resultName, resultFile.FilePath, existingId);
            await fileAccess.SetSupportedTimesAsync(resultId, resultTimes);
            FileReference result = file with { Id = resultId, Name = resultName };
            await CompleteAsync(file, result, MobileTexts.DialogTextFileDecrypted, destination, reporter);
            return FileOperationOutcome.Completed(result);
        }
        finally
        {
            resultFile?.Dispose();
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
        string completedText,
        FileTimes resultTimes)
    {
        FileReference? saved = await fileAccess.SaveAsAsync(
            await fileAccess.GetParentFolderAsync(source.Id),
            suggestedName,
            resultPath);
        if (saved is null)
        {
            return await ReportAsync(DestinationStatus.Canceled);
        }

        await fileAccess.SetSupportedTimesAsync(saved.Id, resultTimes);

        if (IsProbablyChangedByOsFromSuggestion(suggestedName, saved.Name))
        {
            await userInterfaceService.DisplayMessageAsync(string.Format(MobileTexts.DialogTextSavedAs, saved.Name));
        }

        return await CompleteElsewhereAsync(source, saved, completedText);
    }

    // The source is only wiped once the result is saved, so that a save the app is ended in the middle of can be undone.
    private Task<string> SaveInFolderAsync(
        FileReference source,
        ListedFolder folder,
        string name,
        string completedFilePath,
        string? existingId) =>
        interruptedSaves.SaveAsync(
            new SaveInProgress { FolderId = folder.Id, Name = name, SourceId = source.Id },
            completedFilePath,
            existingId);

    private Task<IDecryptionSession> OpenDecryptionAsync(Stream encrypted, Identity identity) =>
        coreServices.OpenDecryptionAsync(
            encrypted,
            new DecryptRequest([identity], runningOperation.CreateProgress()));

    /// <summary>
    /// Wipes the source, and removes it from the recent files, under whatever ids it is listed. The source is removed
    /// explicitly, since a file in a trash may still be reported as existing. The source must no longer be open, since
    /// the wipe may rewrite it. The folder is kept up to date for the files of the operation that follow in it, and the
    /// reporter is told that the operation is done, and whether the source is left.
    /// </summary>
    private async Task CompleteAsync(
        FileReference source,
        FileReference result,
        string completedText,
        ListedFolder folder,
        IFileCompletionReporter reporter)
    {
        folder.Add(result.Name, result.Id);
        bool isWiped = await TryWipeSourceAsync(source);
        if (isWiped)
        {
            folder.Remove(source.Name);
        }

        await recentFilesService.RemoveFileAsync(source.Id);
        await reporter.ReportReplacedAsync(result, completedText, isWiped);
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

    // The number may be before the extension, or after it, as a provider adds it for a type it knows no extension for.
    private static bool IsProbablyChangedByOsFromSuggestion(string suggestedName, string name) =>
        name.Length > suggestedName.Length &&
        (name.WithoutCopyNumber().Equals(suggestedName, StringComparison.OrdinalIgnoreCase) ||
            (name.StartsWith(Path.GetFileNameWithoutExtension(suggestedName), StringComparison.OrdinalIgnoreCase) &&
                name.EndsWith(Path.GetExtension(suggestedName), StringComparison.OrdinalIgnoreCase)));

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

    /// <summary>
    /// Tells if the result may be saved under the name, which it may when there is no file of the name, or when the user
    /// confirms replacing it. The id is that of the file the result is to replace, or null when there is none.
    /// </summary>
    private async Task<(bool IsConfirmed, string? ExistingId)> ConfirmOverwriteAsync(
        ListedFolder folder,
        string destinationName)
    {
        if (folder.FindId(destinationName) is not { } existingId)
        {
            return (true, null);
        }

        bool overwrite = await userInterfaceService.DisplayConfirmationAsync(
            string.Format(MobileTexts.DialogTextConfirmOverwrite, destinationName),
            MobileTexts.LabelYes,
            MobileTexts.LabelNo);
        return (overwrite, existingId);
    }

    /// <summary>
    /// A file that no known password decrypts, kept while the user is asked for a password, with what became of it.
    /// </summary>
    /// <param name="File">The file to decrypt.</param>
    /// <param name="IsElsewhere">Whether the file is in none of My folders, so that the result is saved where the user
    /// chooses.</param>
    /// <param name="Contents">The folders of the operation that the file is decrypted for.</param>
    /// <param name="Reporter">What is told when the file is decrypted.</param>
    private sealed record PasswordRequest(
        FileReference File,
        bool IsElsewhere,
        FolderContents Contents,
        IFileCompletionReporter Reporter)
    {
        // Run later, so that the one waiting goes on only after the request has ended, and not while the page for
        // passwords is still being left.
        public TaskCompletionSource<FileOperationOutcome> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public FileOperationOutcome? Outcome { get; set; }
    }
}
