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

using Xecrets.Common.Models;

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Utilities;
using Xecrets.Texts;

namespace Xecrets.Mobile.Models.Services;

public sealed class WorkFolderWorkflow(
    IWorkFolderService workFolderService,
    IWorkFolderOperationService operationService,
    IFlowContext flowContext,
    IUserInterfaceService userInterfaceService)
{
    public async Task<WorkFolderFile?> PickFileAsync(FilePickerKind pickerKind, WorkFolder? initialFolder = null)
    {
        while (true)
        {
            WorkFolderFile? file = await workFolderService.PickFileAsync(initialFolder, pickerKind);
            if (file is null)
            {
                return null;
            }

            IReadOnlyList<WorkFolder> folders = await workFolderService.GetFoldersAsync();
            WorkFolder? knownFolder = folders.FirstOrDefault(folder => folder.Id == file.LocationId);
            if (knownFolder is not null)
            {
                await MoveFolderToTopAsync(knownFolder);
                return file;
            }

            if (file.IsInKnownWorkFolder)
            {
                WorkFolder discoveredFolder = await workFolderService.AddDiscoveredFolderAsync(file);
                await MoveFolderToTopAsync(discoveredFolder);
                return file;
            }

            if (!await userInterfaceService.DisplayConfirmationAsync(MobileTexts.DialogTextAddUnknownWorkFolder))
            {
                return null;
            }

            await AddFolderAsync(file.LocationId);
        }
    }

    public async Task<WorkFolder?> AddFolderAsync(string? initialLocationId = null)
    {
        while (true)
        {
            WorkFolderResult result = await workFolderService.AddFolderAsync(initialLocationId);
            if (result.Status == WorkFolderResultStatus.IsValid)
            {
                await MoveFolderToTopAsync(result.Folder!);
                return result.Folder;
            }

            if (result.Status == WorkFolderResultStatus.Canceled)
            {
                return null;
            }

            string message = result.Status switch
            {
                WorkFolderResultStatus.NoAccess => MobileTexts.DialogTextFolderNoAccess,
                WorkFolderResultStatus.NotFolder => MobileTexts.DialogTextSelectFolderFirst,
                _ => throw new InvalidOperationException($"Unknown result status {result.Status}."),
            };
            await userInterfaceService.DisplayMessageAsync(message);
        }
    }

    public async Task TransformAsync(WorkFolderFile file, WorkFolderOperation operation)
    {
        flowContext.Begin(FlowOrigin.Navigated, operation);
        if (operation == WorkFolderOperation.Encrypt)
        {
            if (file.FileName.IsEncrypted())
            {
                await userInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextAlreadyEncrypted);
                return;
            }

            await operationService.EncryptAsync(file);
            await userInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextFileEncrypted);
            return;
        }

        if (await operationService.DecryptWithKnownPasswordsAsync(file))
        {
            await userInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextFileDecrypted);
            return;
        }

        await userInterfaceService.NavigateToAsync(AppDestination.EnterPassword);
    }

    private async Task MoveFolderToTopAsync(WorkFolder folder)
    {
        IReadOnlyList<WorkFolder> folders = await workFolderService.GetFoldersAsync();
        if (folders.Count > 0 && folders[0].Id == folder.Id)
        {
            return;
        }

        await workFolderService.SaveFoldersAsync([folder, .. folders.Where(item => item.Id != folder.Id)]);
    }
}
