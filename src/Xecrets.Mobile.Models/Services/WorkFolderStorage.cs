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
public sealed class WorkFolderStorage(ProfileSession profileSession)
{
    public async Task<IReadOnlyList<WorkFolder>> LoadFoldersAsync() =>
        (await RequireUserStore().LoadWorkFoldersAsync()).Value.Folders;

    /// <summary>
    /// Adds the folder first. A folder already stored keeps the name the user may have given it, and is reached through
    /// the new grant from now on. Returns the stored folder.
    /// </summary>
    public async Task<WorkFolder> AddFolderAsync(WorkFolder folder)
    {
        await using IEditScope<WorkFolders> scope = (await RequireUserStore().LoadWorkFoldersAsync()).BeginEdit();
        WorkFolder stored = scope.Value.Folders.FirstOrDefault(item => item.Id == folder.Id) is { } existing
            ? new WorkFolder(existing.Id, existing.DisplayName, folder.GrantId)
            : folder;
        scope.Value.Folders = [stored, .. scope.Value.Folders.Where(item => item.Id != folder.Id)];
        return stored;
    }

    /// <summary>
    /// Moves the folder first, so that it is where the user is likely to look next time.
    /// </summary>
    public async Task MoveFolderToTopAsync(WorkFolder folder)
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

    public async Task RemoveFolderAsync(WorkFolder folder)
    {
        await using IEditScope<WorkFolders> scope = (await RequireUserStore().LoadWorkFoldersAsync()).BeginEdit();
        scope.Value.Folders = [.. scope.Value.Folders.Where(item => item.Id != folder.Id)];
    }

    /// <summary>
    /// Replaces the display name of the stored folder. A new record rather than a with-expression, so that the derived
    /// <see cref="WorkFolder.ListDisplayName"/> follows the new name instead of being copied.
    /// </summary>
    public async Task RenameFolderAsync(WorkFolder folder, string displayName)
    {
        await using IEditScope<WorkFolders> scope = (await RequireUserStore().LoadWorkFoldersAsync()).BeginEdit();
        scope.Value.Folders = [.. scope.Value.Folders.Select(item => item.Id == folder.Id
            ? new WorkFolder(item.Id, displayName, item.GrantId)
            : item)];
    }

    private IUserDataStore RequireUserStore() =>
        profileSession.UserStore ?? throw new InvalidOperationException("No authenticated profile is available.");
}
