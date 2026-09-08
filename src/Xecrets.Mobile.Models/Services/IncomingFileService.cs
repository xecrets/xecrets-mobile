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

namespace Xecrets.Mobile.Models.Services;

public sealed class IncomingFileService(
    IProfileService profileService,
    ITransientFileService transientFileService,
    IPreviewService previewService,
    IEncryptionPreparationService encryptionPreparationService,
    IFlowContext flowContext,
    ICoreServices coreServices,
    IUserInterfaceService userInterfaceService)
    : IIncomingFileService
{
    private IncomingFileRequest _pendingRequest = IncomingFileRequest.Empty;

    public Task ReceiveAsync(Func<Task<IncomingFileInfo>> receiveFileAsync)
        => transientFileService.RunExclusiveAsync(() => userInterfaceService.InvokeOnMainThreadAsync(async () =>
        {
            if (_pendingRequest != IncomingFileRequest.Empty || !userInterfaceService.CanReceiveIncomingFiles)
            {
                return;
            }

            _pendingRequest = new IncomingFileRequest(await receiveFileAsync());
            if (userInterfaceService.CanProcessIncomingFiles)
            {
                await ProcessPendingCoreAsync();
            }
        }));

    public Task ReceiveMessageAsync(string message)
        => transientFileService.RunExclusiveAsync(() => userInterfaceService.InvokeOnMainThreadAsync(async () =>
        {
            _pendingRequest = new IncomingFileRequest(message);
            await ProcessPendingCoreAsync();
        }));

    public Task ProcessPendingAsync() => transientFileService.RunExclusiveAsync(ProcessPendingCoreAsync);

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

    private async Task HandleAuthenticatedAsync(IncomingFileInfo file)
    {
        bool isEncrypted = await coreServices.IsEncryptedAsync(
            () => Task.FromResult<Stream>(File.OpenRead(file.FilePath)));
        flowContext.Begin(
            FlowOrigin.ReceivedFile,
            isEncrypted ? WorkFolderOperation.Decrypt : WorkFolderOperation.Encrypt);

        if (isEncrypted)
        {
            PreviewPreparationStatus status = await previewService.PrepareImportedAsync(file.FilePath);
            if (status == PreviewPreparationStatus.Cancelled)
            {
                return;
            }

            if (status == PreviewPreparationStatus.WrongPassword)
            {
                if (previewService.HasPendingPasswordRequest)
                {
                    await userInterfaceService.NavigateToAsync(AppDestination.EnterPassword);
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

            PickedFile pickedFile = new(
                file.DisplayName,
                file.FilePath,
                () => Task.FromResult<Stream>(File.OpenRead(file.FilePath)));
            EncryptionPreparationResult result = await encryptionPreparationService.EncryptForCurrentProfileAsync(pickedFile);
            await userInterfaceService.NavigateToAsync(AppDestination.EncryptResult, result);
        }
        catch (Exception ex)
        {
            await userInterfaceService.DisplayMessageAsync(ex.FormatException());
        }
    }

    private async Task NavigateToAuthenticationAsync()
    {
        AppDestination destination = await profileService.HasProfileAsync()
            ? AppDestination.Login
            : AppDestination.CreateProfile;
        await userInterfaceService.NavigateToAsync(destination);
    }
}
