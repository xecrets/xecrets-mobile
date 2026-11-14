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

namespace Xecrets.Mobile.Models.Services;

/// <summary>
/// Gets access to the folder of a file to change it. A file is changed through the grant of one of My folders that
/// contains it, and when there is none the user is asked to choose the folder and add it to My folders. This is
/// repeated until the file is in one of My folders, or the user cancels. A file in a place that can never be added,
/// such as some locations shown by the system, can thus not be changed, but the user can always cancel.
/// </summary>
public sealed class FolderAccessWorkflow(
    IFileAccess fileAccess,
    WorkFolderStorage storage,
    IUserInterfaceService userInterfaceService)
{
    /// <summary>
    /// Lets the user pick a file to change. When the file is not in one of My folders, and the user adds its folder,
    /// the user picks the file again, starting in the folder added.
    /// </summary>
    /// <param name="folder">The folder to start in, or null to let the platform decide.</param>
    /// <param name="pickerKind">The kind of files to pick from.</param>
    /// <returns>The file as reached through one of My folders, as by <see cref="FindInFoldersAsync"/>, or null if the
    /// user cancels.</returns>
    public async Task<FileReference?> PickWritableFileAsync(WorkFolder? folder, FilePickerKind pickerKind)
    {
        string initialFolder = folder?.Id ?? string.Empty;
        while (true)
        {
            FileReference? file = await fileAccess.PickFileAsync(initialFolder, pickerKind);
            if (file is null)
            {
                return null;
            }

            if (await FindInFoldersAsync(file) is { } reachable)
            {
                return reachable;
            }

            WorkFolder? added = await RequestFolderAsync(file);
            if (added is null)
            {
                return null;
            }

            initialFolder = added.Id;
        }
    }

    /// <summary>
    /// Gets access to the folder of a file already chosen, without picking it again.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <returns>The file as reached through one of My folders, as by <see cref="FindInFoldersAsync"/>, or null if the
    /// user cancels.</returns>
    public async Task<FileReference?> EnsureFolderAccessAsync(FileReference file)
    {
        while (true)
        {
            if (await FindInFoldersAsync(file) is { } reachable)
            {
                return reachable;
            }

            if (await RequestFolderAsync(file) is null)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Tells if a file is in one of My folders, without asking the user.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <returns>True if the file is in one of My folders, false otherwise.</returns>
    public async Task<bool> IsInFoldersAsync(FileReference file) =>
        await fileAccess.FindFolderAsync(file.Id) is not null;

    /// <summary>
    /// Gets a file as reached through one of My folders, without asking the user. The folder it is reached through is
    /// moved first in My folders.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <returns>The file as reached through the innermost of My folders that contains it, or null if it is in none of
    /// them.</returns>
    public async Task<FileReference?> FindInFoldersAsync(FileReference file)
    {
        WorkFolder? folder = await fileAccess.FindFolderAsync(file.Id);
        if (folder is null)
        {
            return null;
        }

        await storage.MoveFolderToTopAsync(folder);
        return await fileAccess.GetFileInFolderAsync(folder, file);
    }

    /// <summary>
    /// Lets the user choose a folder and adds it first in My folders. A folder that cannot be used is reported, and the
    /// user chooses again.
    /// </summary>
    /// <param name="initialFolder">The folder to start in, or "" to let the platform decide.</param>
    /// <returns>The added folder, or null if the user cancels.</returns>
    public async Task<WorkFolder?> AddFolderAsync(string initialFolder = "") =>
        await PickUsableFolderAsync(() => fileAccess.PickFolderAsync(initialFolder, FilePickerKind.Any)) is { } folder
            ? await storage.AddFolderAsync(folder)
            : null;

    /// <summary>
    /// Lets the user pick a folder to change files in. A folder that is one of My folders, or in one of them, is used
    /// as it is, and the one of My folders that contains it is moved first. A folder in none of them is added first in
    /// My folders when the user allows access to it.
    /// </summary>
    /// <param name="initialFolder">The folder to start in, or "" to let the platform decide.</param>
    /// <param name="kind">The kind of files to change, which the picker may show.</param>
    /// <returns>The folder, or null if the user cancels or does not allow access.</returns>
    public async Task<WorkFolder?> PickWritableFolderAsync(string initialFolder, FilePickerKind kind)
    {
        WorkFolder? picked = await PickUsableFolderAsync(() => fileAccess.PickFolderAsync(initialFolder, kind));
        if (picked is null)
        {
            return null;
        }

        if (await fileAccess.FindFolderOfFolderAsync(picked.Id) is { } containing)
        {
            await storage.MoveFolderToTopAsync(containing);
            return picked;
        }

        return await userInterfaceService.DisplayConfirmationAsync(
            MobileTexts.DialogTextAllowAccessToFolder,
            MobileTexts.LabelYes,
            MobileTexts.LabelNo)
            ? await storage.AddFolderAsync(picked)
            : null;
    }

    // A folder that cannot be used is reported, and the user chooses again.
    private async Task<WorkFolder?> PickUsableFolderAsync(Func<Task<WorkFolderResult>> pickFolderAsync)
    {
        while (true)
        {
            WorkFolderResult result = await pickFolderAsync();
            string message;
            switch (result.Status)
            {
                case WorkFolderResultStatus.IsValid:
                    return result.Folder!;

                case WorkFolderResultStatus.Canceled:
                    return null;

                case WorkFolderResultStatus.NoAccess:
                    message = MobileTexts.DialogTextFolderNoAccess;
                    break;

                case WorkFolderResultStatus.NotFolder:
                    message = MobileTexts.DialogTextSelectFolderFirst;
                    break;

                default:
                    throw new InvalidOperationException($"Unknown result status {result.Status}.");
            }

            await userInterfaceService.DisplayMessageAsync(message);
        }
    }

    private async Task<WorkFolder?> RequestFolderAsync(FileReference file) =>
        await userInterfaceService.DisplayConfirmationAsync(
            MobileTexts.DialogTextAllowAccessToFolder,
            MobileTexts.LabelYes,
            MobileTexts.LabelNo)
            ? await AddFolderAsync(await fileAccess.GetParentFolderAsync(file.Id))
            : null;
}
