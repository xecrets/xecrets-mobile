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
using Xecrets.Core.Abstractions;

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Models.Services;

public sealed class WorkFolderWorkflow(
    IWorkFolderService workFolderService,
    IWorkFolderOperationService operationService,
    IWorkFolderFileOperations fileOperations,
    IPreviewService previewService,
    IFlowContext flowContext,
    ICoreServices coreServices,
    IFileWiper fileWiper,
    IUserInterfaceService userInterfaceService)
{
    /// <summary>
    /// Lets the user pick a file in a known folder, to work on it where it is. The user is offered to add the folder
    /// of a file picked outside the known folders, and the file is then used through it. If the folder added does not
    /// contain the file, the user picks again. A file whose folder cannot be determined cannot be used.
    /// </summary>
    public async Task<WorkFolderFile?> PickFileAsync(FilePickerKind pickerKind, WorkFolder? initialFolder = null)
    {
        while (true)
        {
            WorkFolderFile? file = await workFolderService.PickFileAsync(initialFolder, pickerKind);
            if (file is null)
            {
                return null;
            }

            if (!file.IsLocationKnown)
            {
                await userInterfaceService.DisplayMessageAsync(MobileTexts.DialogTextLocationNotSupported);
                return null;
            }

            if (await TryUseKnownFolderAsync(file))
            {
                return file;
            }

            if (!await ConfirmAddFolderAsync(file.LocationDisplayName))
            {
                return null;
            }

            WorkFolderFile? reopened = await AddFolderAndReopenAsync(file);
            if (reopened is not null)
            {
                return reopened;
            }
        }
    }

    /// <summary>
    /// Lets the user pick any file to make a copy of. A file in a known folder is used through it, and is
    /// <see cref="WorkFolderFile.IsInKnownWorkFolder"/>. Other files are used as they are, but cannot be listed among
    /// the recent files.
    /// </summary>
    public async Task<WorkFolderFile?> PickFileForCopyAsync(FilePickerKind pickerKind)
    {
        WorkFolderFile? file = await workFolderService.PickFileAsync(null, pickerKind);
        if (file is not null)
        {
            await TryUseKnownFolderAsync(file);
        }

        return file;
    }

    /// <summary>
    /// Lets the user save a new file, starting in the known folder of the source file if there is one. Returns null
    /// when the user cancels, and otherwise the saved file, which is <see cref="WorkFolderFile.IsInKnownWorkFolder"/>
    /// if it was saved in a known folder, so that it can be opened again.
    /// </summary>
    public async Task<WorkFolderFile?> SaveFileAsync(string fileName, Stream content, WorkFolderFile? source)
    {
        IReadOnlyList<WorkFolder> folders = await workFolderService.GetFoldersAsync();
        WorkFolder? initialFolder = folders.FirstOrDefault(folder => folder.Id == source?.LocationId);
        WorkFolderFile? file = await workFolderService.SaveFileAsync(initialFolder, fileName, content);
        if (file is null)
        {
            return null;
        }

        await userInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextFileSaved);
        await TryUseKnownFolderAsync(file);
        return file;
    }

    /// <summary>
    /// Decrypts a file temporarily to show it, or sends the user on to enter a password for it. Returns false when the
    /// file could not be decrypted.
    /// </summary>
    public async Task<bool> PreviewAsync(WorkFolderFile file)
    {
        flowContext.Begin(FlowOrigin.Navigated, WorkFolderOperation.Decrypt, file);
        DocumentPreviewFile previewFile = new(file.FileName, () => fileOperations.OpenReadAsync(file));
        if (await previewService.PrepareAsync(previewFile, enableTextEditing: false))
        {
            await userInterfaceService.NavigateToAsync(AppDestination.Preview);
            return true;
        }

        if (previewService.HasPendingPasswordRequest)
        {
            await userInterfaceService.NavigateToAsync(AppDestination.EnterPassword);
            return true;
        }

        return false;
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

    /// <summary>
    /// Lets the user pick a file and encrypts or decrypts it where it is. Without an operation, the contents of the
    /// file decide which. A file that is not encrypted is not decrypted, but the user is told so.
    /// </summary>
    public async Task PickAndTransformAsync(WorkFolderOperation? operation, WorkFolder? initialFolder = null)
    {
        FilePickerKind pickerKind = operation == WorkFolderOperation.Decrypt ? FilePickerKind.Encrypted : FilePickerKind.Any;
        WorkFolderFile? file = await PickFileAsync(pickerKind, initialFolder);
        if (file is null)
        {
            return;
        }

        if (operation == WorkFolderOperation.Encrypt)
        {
            await TransformAsync(file, WorkFolderOperation.Encrypt);
            return;
        }

        bool isEncrypted = await coreServices.IsEncryptedAsync(() => fileOperations.OpenReadAsync(file));
        if (operation == WorkFolderOperation.Decrypt && !isEncrypted)
        {
            await userInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextNotEncrypted);
            return;
        }

        await TransformAsync(file, isEncrypted ? WorkFolderOperation.Decrypt : WorkFolderOperation.Encrypt);
    }

    /// <summary>
    /// Encrypts or decrypts a file, returning false when the user was sent on to enter a password for it. The file is
    /// replaced by the result, so nothing is done unless the file can be wiped afterwards.
    /// </summary>
    public async Task<bool> TransformAsync(WorkFolderFile file, WorkFolderOperation operation)
    {
        if (!await fileWiper.CanWipeAsync(file.WritableFile))
        {
            await userInterfaceService.DisplayMessageAsync(MobileTexts.DialogTextInsufficientRights);
            return true;
        }

        flowContext.Begin(FlowOrigin.Navigated, operation);
        if (operation == WorkFolderOperation.Encrypt)
        {
            if (await coreServices.IsEncryptedAsync(() => fileOperations.OpenReadAsync(file)))
            {
                await userInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextAlreadyEncrypted);
                return true;
            }

            await operationService.EncryptAsync(file);
            await userInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextFileEncrypted);
            return true;
        }

        if (await operationService.DecryptWithKnownPasswordsAsync(file))
        {
            await userInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextFileDecrypted);
            return true;
        }

        await userInterfaceService.NavigateToAsync(AppDestination.EnterPassword);
        return false;
    }

    /// <summary>
    /// Offers to add the folder of a file that can no longer be accessed, such as a recent file whose folder was
    /// removed from the known folders, starting at the folder of the file. Returns whether a folder was added, which
    /// may still not be the one containing the file.
    /// </summary>
    public async Task<bool> AddFolderForFileAsync(string fileId)
    {
        IReadOnlyList<string> segments = workFolderService.GetFilePathSegments(fileId);
        string folderName = segments.Count > 1 ? segments[^2] : string.Empty;
        return await ConfirmAddFolderAsync(folderName) &&
            await AddFolderAsync(workFolderService.GetFileLocationId(fileId)) is not null;
    }

    /// <summary>
    /// Asks the user whether to add a folder outside the known folders, naming it when its name is known.
    /// </summary>
    private Task<bool> ConfirmAddFolderAsync(string folderName) =>
        userInterfaceService.DisplayConfirmationAsync(string.Format(
            MobileTexts.DialogTextAddUnknownWorkFolderFormat,
            folderName.Length > 0 ? folderName : "…"));

    /// <summary>
    /// Lets the user add a folder for a file outside the known folders, starting at the folder of the file, and opens
    /// the file again through it. Returns null when the user cancels, or the folder added does not contain the file.
    /// </summary>
    private async Task<WorkFolderFile?> AddFolderAndReopenAsync(WorkFolderFile file)
    {
        if (await AddFolderAsync(file.LocationId) is null)
        {
            return null;
        }

        WorkFolderFileResult result = await workFolderService.OpenFileAsync(file.Id);
        if (result.Status != WorkFolderFileResultStatus.IsValid)
        {
            return null;
        }

        // A folder above that of the file may have been added, and the folder of the file is then added as well,
        // as when a file within a known folder is picked.
        await TryUseKnownFolderAsync(result.File!);
        return result.File;
    }

    /// <summary>
    /// Moves the known folder of a file to the top, adding it first if it is within a known folder. Returns false
    /// when the file is outside the known folders.
    /// </summary>
    private async Task<bool> TryUseKnownFolderAsync(WorkFolderFile file)
    {
        IReadOnlyList<WorkFolder> folders = await workFolderService.GetFoldersAsync();
        WorkFolder? knownFolder = folders.FirstOrDefault(folder => folder.Id == file.LocationId);
        if (knownFolder is not null)
        {
            await MoveFolderToTopAsync(knownFolder);
            return true;
        }

        if (file.IsInKnownWorkFolder)
        {
            WorkFolder discoveredFolder = await workFolderService.AddDiscoveredFolderAsync(file);
            await MoveFolderToTopAsync(discoveredFolder);
            return true;
        }

        return false;
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
