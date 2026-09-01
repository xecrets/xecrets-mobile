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

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Models.Services;

public sealed class IncomingFileService(
    IProfileService profileService,
    IPreviewService previewService,
    IEncryptionPreparationService encryptionPreparationService,
    IFlowContext flowContext,
    IUserInterfaceService userInterfaceService)
    : IIncomingFileService
{
    private IncomingFileInfo? _pendingFile;

    public Task ReceiveAsync(IncomingFileInfo file)
        => userInterfaceService.InvokeOnMainThreadAsync(async () =>
        {
            _pendingFile = file;
            if (userInterfaceService.CanProcessIncomingFiles)
            {
                await ProcessPendingAsync();
            }
        });

    public async Task ProcessPendingAsync()
    {
        if (_pendingFile is null || !userInterfaceService.IsShellAvailable)
        {
            return;
        }

        if (!profileService.IsAuthenticated)
        {
            await NavigateToAuthenticationAsync();
            return;
        }

        IncomingFileInfo file = _pendingFile;
        _pendingFile = null;
        await HandleAuthenticatedAsync(file);
    }

    private async Task HandleAuthenticatedAsync(IncomingFileInfo file)
    {
        bool isEncrypted = ContentTypeDetector.IsEncryptedFile(file.DisplayName, file.ContentType);
        flowContext.Begin(
            FlowOrigin.ReceivedFile,
            isEncrypted ? WorkFolderOperation.Decrypt : WorkFolderOperation.Encrypt);

        if (isEncrypted)
        {
            bool isPrepared = await previewService.PrepareImportedAsync(file.FilePath);
            if (!isPrepared)
            {
                if (previewService.HasPendingPasswordRequest)
                {
                    await userInterfaceService.NavigateToAsync(AppDestination.EnterPassword);
                }
                else
                {
                    await userInterfaceService.DisplayMessageAsync(Texts.Texts.DialogTextWrongPasswordOpen);
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
