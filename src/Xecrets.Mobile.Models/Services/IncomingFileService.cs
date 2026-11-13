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

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Utilities;
using Xecrets.Texts;

namespace Xecrets.Mobile.Models.Services;

internal sealed class IncomingFileService(
    IProfileService profileService,
    IPreviewService previewService,
    IEncryptionPreparationService encryptionPreparationService,
    IFlowContext flowContext,
    ICoreServices coreServices,
    ICrashLogService crashLogService,
    IUserInterfaceService userInterfaceService)
    : IIncomingFileService
{
    // Received files are handled one at a time.
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IncomingFileRequest _pendingRequest = IncomingFileRequest.Empty;

    public Task ReceiveAsync(IIncomingFileSource source)
        => RunExclusiveAsync(() => userInterfaceService.InvokeOnMainThreadAsync(async () =>
        {
            if (_pendingRequest != IncomingFileRequest.Empty || !userInterfaceService.CanReceiveIncomingFiles)
            {
                return;
            }

            _pendingRequest = new IncomingFileRequest(await source.ImportAsync());
            if (userInterfaceService.CanProcessIncomingFiles)
            {
                await ProcessPendingCoreAsync();
            }
        }));

    public Task ReceiveMessageAsync(string message)
        => RunExclusiveAsync(() => userInterfaceService.InvokeOnMainThreadAsync(async () =>
        {
            // A file waiting for the user to sign in is replaced, so it is not needed any more.
            _pendingRequest.File?.File.Dispose();
            _pendingRequest = new IncomingFileRequest(message);
            await ProcessPendingCoreAsync();
        }));

    public Task ProcessPendingAsync() => RunExclusiveAsync(ProcessPendingCoreAsync);

    private async Task RunExclusiveAsync(Func<Task> operation)
    {
        await _gate.WaitAsync();
        try
        {
            await operation();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task ProcessPendingCoreAsync()
    {
        if (_pendingRequest.Message is not null && userInterfaceService.IsShellAvailable)
        {
            await userInterfaceService.DisplayMessageAsync(_pendingRequest.Message);
            _pendingRequest = IncomingFileRequest.Empty;
            return;
        }

        if (_pendingRequest.File is null)
        {
            return;
        }

        if (!File.Exists(_pendingRequest.File.FilePath))
        {
            _pendingRequest = IncomingFileRequest.Empty;
            return;
        }

        if (!profileService.IsAuthenticated)
        {
            await NavigateToAuthenticationAsync();
            return;
        }

        IncomingFileInfo file = _pendingRequest.File;
        _pendingRequest = IncomingFileRequest.Empty;
        await HandleAuthenticatedAsync(file);
    }

    /// <summary>
    /// Shows a received file that is found to be encrypted, whatever its name, and encrypts any other file, unless it
    /// is named as encrypted, which the user is told is not so.
    /// </summary>
    private async Task HandleAuthenticatedAsync(IncomingFileInfo file)
    {
        bool isEncrypted = await coreServices.IsEncryptedAsync(
            () => Task.FromResult<Stream>(File.OpenRead(file.FilePath)));
        if (!isEncrypted && file.DisplayName.IsNameEncrypted())
        {
            file.File.Dispose();
            await userInterfaceService.DisplayMessageAsync(
                string.Format(MobileTexts.DialogTextNotEncryptedFormat, file.DisplayName));
            return;
        }

        flowContext.Begin(FlowOrigin.ReceivedFile);

        if (isEncrypted)
        {
            PreviewPreparationStatus status = await previewService.PrepareImportedAsync(file.File);
            if (status == PreviewPreparationStatus.Cancelled)
            {
                return;
            }

            if (status == PreviewPreparationStatus.WrongPassword)
            {
                if (previewService.HasPendingPasswordRequest)
                {
                    await userInterfaceService.NavigateToAsync(
                        AppDestination.EnterPassword,
                        DecryptionPasswordsIntent.Add);
                }
                else
                {
                    await userInterfaceService.DisplayMessageAsync(MobileTexts.DialogTextWrongPasswordOpen);
                }

                return;
            }

            await userInterfaceService.NavigateToAsync(AppDestination.Preview);
            return;
        }

        await SaveUnencryptedFileAsEncryptedAsync(file);
    }

    private async Task SaveUnencryptedFileAsEncryptedAsync(IncomingFileInfo file)
    {
        try
        {
            await using (FileStream? input = file.FilePath.OpenReadIfExists())
            {
                if (input is null)
                {
                    return;
                }
            }

            EncryptionPreparationResult result = await encryptionPreparationService.EncryptWithCurrentProfileAsync(
                new LocalReadableFile(file.FilePath, file.DisplayName));
            await userInterfaceService.NavigateToAsync(AppDestination.EncryptResult, result);
        }
        catch (Exception ex)
        {
            crashLogService.WriteSwallowedException(ex);
            await userInterfaceService.DisplayMessageAsync(ex.FormatException());
        }
        finally
        {
            // Not needed once encrypted, since the encrypted copy is a file of its own.
            file.File.Dispose();
        }
    }

    private async Task NavigateToAuthenticationAsync()
    {
        AppDestination destination = await profileService.HasProfileAsync()
            ? AppDestination.Login
            : AppDestination.Introduction;
        await userInterfaceService.NavigateToAsync(destination);
    }
}
