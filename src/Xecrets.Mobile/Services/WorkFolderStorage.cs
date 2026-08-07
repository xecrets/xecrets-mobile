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
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.Maui.Storage;

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Data;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Services;

/// <summary>
/// Storage and naming shared by the platform work folder services. The folder list is kept as JSON in the
/// preferences. Windows keeps its folder list in the future access list instead, and only uses the path
/// segment splitting here, which is why that is static and needs no instance.
/// </summary>
public sealed class WorkFolderStorage(IUserInterfaceService userInterfaceService)
{
    private const string _foldersPreferenceKey = "work-folders";

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

    public async Task<List<WorkFolder>> LoadFoldersAsync()
    {
        string json = Preferences.Default.Get(_foldersPreferenceKey, string.Empty);
        if (json.Length == 0)
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize(json, JsonContext.Default.ListWorkFolder) ?? [];
        }
        catch (JsonException)
        {
            // An unreadable folder list (a list written by an older version, or a write that was
            // interrupted mid-way) must not leave the page permanently unusable. Clear it, say so, and
            // carry on with no folders. Only the list is lost: the folders themselves are untouched and
            // can be added again, which is also what re-establishes the access grants.
            Preferences.Default.Remove(_foldersPreferenceKey);
            await userInterfaceService.DisplayMessageAsync(MobileTexts.DialogTextWorkFolderListReset);
            return [];
        }
    }

    public async Task SaveFolderAsync(WorkFolder folder)
    {
        List<WorkFolder> folders = await LoadFoldersAsync();
        if (folders.Any(item => item.Id == folder.Id))
        {
            return;
        }

        folders.Add(folder);
        SaveFolders(folders);
    }

    /// <summary>
    /// Replaces the display name of the stored folder. A new record rather than a with-expression, so that
    /// the derived <see cref="WorkFolder.ListDisplayName"/> follows the new name instead of being copied.
    /// </summary>
    public async Task RenameFolderAsync(WorkFolder folder, string displayName) =>
        SaveFolders((await LoadFoldersAsync()).Select(item => item.Id == folder.Id
            ? new WorkFolder(item.Id, displayName, item.GrantId)
            : item));

    public void SaveFolders(IEnumerable<WorkFolder> folders) =>
        Preferences.Default.Set(
            _foldersPreferenceKey,
            JsonSerializer.Serialize(folders.ToList(), JsonContext.Default.ListWorkFolder));
}
