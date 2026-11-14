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

using Xecrets.Common.Abstractions;
using Xecrets.Common.Models;

namespace Xecrets.Mobile.Models.Services;

/// <summary>
/// My folders, kept as part of the signed-in profile's <see cref="LocalProfileData"/>, in the user's order. The grants
/// of the folders are kept by the platform, and released when no longer referenced from here.
/// </summary>
/// <remarks>
/// The members require a profile to be signed in, and throw <see cref="InvalidOperationException"/> otherwise.
/// </remarks>
public sealed class WorkFolderStorage(ProfileSession profileSession)
{
    /// <summary>
    /// Loads My folders.
    /// </summary>
    /// <returns>My folders, in the order of the user.</returns>
    public async Task<IReadOnlyList<WorkFolder>> LoadFoldersAsync() =>
        (await RequireUserStore().LoadWorkFoldersAsync()).Value.Folders;

    /// <summary>
    /// Adds a folder first. A folder already stored keeps the name the user may have given it, and is reached through
    /// the new grant from now on, where it is stored as told now.
    /// </summary>
    /// <param name="folder">The folder.</param>
    /// <returns>The stored folder.</returns>
    internal async Task<WorkFolder> AddFolderAsync(WorkFolder folder)
    {
        await using IEditScope<WorkFolders> scope = (await RequireUserStore().LoadWorkFoldersAsync()).BeginEdit();
        WorkFolder stored = scope.Value.Folders.FirstOrDefault(item => item.Id == folder.Id) is { } existing
            ? new WorkFolder(existing.Id, existing.DisplayName, folder.GrantId)
            {
                Root = folder.Root,
                FolderPath = folder.FolderPath,
            }
            : folder;
        scope.Value.Folders = [stored, .. scope.Value.Folders.Where(item => item.Id != folder.Id)];
        return stored;
    }

    /// <summary>
    /// Moves a stored folder first.
    /// </summary>
    /// <param name="folder">The folder.</param>
    internal async Task MoveFolderToTopAsync(WorkFolder folder)
    {
        IPersistentData<WorkFolders> data = await RequireUserStore().LoadWorkFoldersAsync();
        if (data.Value.Folders.Count == 0 || data.Value.Folders[0].Id == folder.Id)
        {
            return;
        }

        await using IEditScope<WorkFolders> scope = data.BeginEdit();
        scope.Value.Folders =
        [
            .. scope.Value.Folders.Where(item => item.Id == folder.Id),
            .. scope.Value.Folders.Where(item => item.Id != folder.Id),
        ];
    }

    /// <summary>
    /// Removes a folder, if it is stored.
    /// </summary>
    /// <param name="folder">The folder.</param>
    internal async Task RemoveFolderAsync(WorkFolder folder)
    {
        await using IEditScope<WorkFolders> scope = (await RequireUserStore().LoadWorkFoldersAsync()).BeginEdit();
        scope.Value.Folders = [.. scope.Value.Folders.Where(item => item.Id != folder.Id)];
    }

    /// <summary>
    /// Replaces the display name of a stored folder, which <see cref="WorkFolder.ListDisplayName"/> then follows.
    /// </summary>
    /// <param name="folder">The folder.</param>
    /// <param name="displayName">The new display name.</param>
    internal async Task RenameFolderAsync(WorkFolder folder, string displayName)
    {
        // A new record rather than a with-expression, so that the derived ListDisplayName follows the new name instead
        // of being copied.
        await using IEditScope<WorkFolders> scope = (await RequireUserStore().LoadWorkFoldersAsync()).BeginEdit();
        scope.Value.Folders = [.. scope.Value.Folders.Select(item => item.Id == folder.Id
            ? new WorkFolder(item.Id, displayName, item.GrantId) { Root = item.Root, FolderPath = item.FolderPath }
            : item)];
    }

    private IUserDataStore RequireUserStore() =>
        profileSession.UserStore ?? throw new InvalidOperationException("No authenticated profile is available.");
}
