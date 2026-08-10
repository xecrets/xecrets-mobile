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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Xecrets.Common.Abstractions;
using Xecrets.Common.Models;

using Xecrets.Mobile.Models.Services;

namespace Xecrets.Mobile.Services;

/// <summary>
/// Storage and naming shared by the platform work folder services. The folder list is kept as part of the
/// signed-in profile's <see cref="LocalProfileData"/>, alongside its other settings.
/// </summary>
public sealed class WorkFolderStorage(ProfileSession profileSession)
{
    /// <summary>
    /// Splits a folder path or document id into path segments, appending the display name unless the split
    /// already ends with it. The result is non-empty and its final segment is the display name.
    /// </summary>
    public static IReadOnlyList<string> BuildPathSegments(string path, WorkFolder folder, params char[] separators)
    {
        string[] segments = path.Split(
            separators,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length > 0 && string.Equals(
            segments[^1],
            folder.DisplayName,
            StringComparison.OrdinalIgnoreCase)
            ? segments
            : [.. segments, folder.DisplayName];
    }

    public async Task<List<WorkFolder>> LoadFoldersAsync() =>
        (await RequireUserStore().LoadWorkFoldersAsync()).Value.Folders;

    public async Task SaveFolderAsync(WorkFolder folder)
    {
        await using IEditScope<WorkFolders> scope = (await RequireUserStore().LoadWorkFoldersAsync()).BeginEdit();
        if (scope.Value.Folders.Any(item => item.Id == folder.Id))
        {
            return;
        }

        scope.Value.Folders.Add(folder);
    }

    /// <summary>
    /// Replaces the display name of the stored folder. A new record rather than a with-expression, so that
    /// the derived <see cref="WorkFolder.ListDisplayName"/> follows the new name instead of being copied.
    /// </summary>
    public async Task RenameFolderAsync(WorkFolder folder, string displayName)
    {
        await using IEditScope<WorkFolders> scope = (await RequireUserStore().LoadWorkFoldersAsync()).BeginEdit();
        scope.Value.Folders = [.. scope.Value.Folders.Select(item => item.Id == folder.Id
            ? new WorkFolder(item.Id, displayName, item.GrantId)
            : item)];
    }

    public async Task SaveFoldersAsync(IEnumerable<WorkFolder> folders)
    {
        await using IEditScope<WorkFolders> scope = (await RequireUserStore().LoadWorkFoldersAsync()).BeginEdit();
        scope.Value.Folders = [.. folders];
    }

    private IUserDataStore RequireUserStore() =>
        profileSession.UserStore ?? throw new InvalidOperationException("No authenticated profile is available.");
}
