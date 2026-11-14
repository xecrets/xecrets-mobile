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
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Foundation;
using Xecrets.Common.Models;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Services;

namespace Xecrets.Mobile.Platforms.Apple;

/// <summary>
/// Resolves what gives the access to a file or folder: the url a picker gave while the app runs, the grant of one of
/// My folders containing it, or its own bookmark.
/// </summary>
internal sealed class AppleFileGrants(WorkFolderStorage storage)
{
    // The urls the pickers gave while the app runs, by file id, which carry the access to the file picked.
    private readonly Dictionary<string, NSUrl> _picked = [];

    /// <summary>
    /// Keeps the url a picker gave for a file, which gives access to it while the app runs.
    /// </summary>
    /// <param name="fileUrl">The url the picker gave.</param>
    /// <returns>The url.</returns>
    public NSUrl Add(NSUrl fileUrl)
    {
        _picked[fileUrl.AbsoluteString!] = fileUrl;
        return fileUrl;
    }

    /// <summary>
    /// Tells if a picker gave the url of a file while the app runs.
    /// </summary>
    /// <param name="fileId">The id of the file.</param>
    /// <returns>True if the file was picked while the app runs, false otherwise.</returns>
    public bool IsPicked(string fileId) => _picked.ContainsKey(fileId);

    /// <summary>
    /// Finds the innermost of My folders that contains a file or folder, or is the folder itself when that is
    /// allowed. The paths are normalized with the access of each folder, which also gives access to what is in it.
    /// </summary>
    /// <param name="id">The id of the file or folder.</param>
    /// <param name="isFolderItself">True if one of My folders that is the folder itself is found too, false
    /// otherwise.</param>
    /// <returns>The folder, or null if there is none.</returns>
    public async Task<WorkFolder?> FindFolderAsync(string id, bool isFolderItself)
    {
        string idPath = NSUrl.FromString(id)!.Path!;
        WorkFolder? innermost = null;
        int innermostLength = -1;
        foreach (WorkFolder folder in await storage.LoadFoldersAsync())
        {
            if (folder.GrantId.TryResolveGrant() is not { Path: { } grantPath } grant)
            {
                continue;
            }

            string folderPath = NormalizePath(grant, grantPath);
            string path = NormalizePath(grant, idPath);
            bool isInFolder = path.StartsWith(folderPath + "/", StringComparison.Ordinal) ||
                (isFolderItself && path == folderPath);
            if (isInFolder && folderPath.Length > innermostLength)
            {
                innermost = folder;
                innermostLength = folderPath.Length;
            }
        }

        return innermost;
    }

    /// <summary>
    /// Finds what gives access to the file: the url a picker gave, the grant of a folder containing it, or its own
    /// bookmark. A bookmark follows a file that is renamed, but such a file is not where it was, so it gives no access.
    /// </summary>
    /// <param name="fileId">The id of the file.</param>
    /// <returns>The url to access through and the url of the file, or null if there is no access.</returns>
    public async Task<ResolvedUrl?> ResolveAccessAsync(string fileId)
    {
        if (_picked.TryGetValue(fileId, out NSUrl? picked))
        {
            return new ResolvedUrl(picked, picked);
        }

        NSUrl fileUrl = NSUrl.FromString(fileId)!;
        if (await FindFolderAsync(fileId, isFolderItself: false) is { } folder &&
            folder.GrantId.TryResolveGrant() is { } grant)
        {
            return new ResolvedUrl(grant, fileUrl);
        }

        return fileId.TryResolveGrant() is { } own && own.LastPathComponent == fileUrl.LastPathComponent
            ? new ResolvedUrl(own, own)
            : null;
    }

    /// <summary>
    /// Resolves the grant of the innermost of My folders that contains a file or folder, as found by
    /// <see cref="FindFolderAsync"/>. The access belongs to the url resolved from the grant, not to a url created from
    /// the same string, so it is resolved again for each use.
    /// </summary>
    /// <param name="id">The id of the file or folder.</param>
    /// <param name="isFolderItself">True if one of My folders that is the folder itself is found too, false
    /// otherwise.</param>
    /// <returns>The url of the grant, whose security scope gives the access.</returns>
    /// <exception cref="IOException">The file or folder is in none of My folders.</exception>
    public async Task<NSUrl> GetFolderGrantAsync(string id, bool isFolderItself) =>
        await FindFolderAsync(id, isFolderItself) is { } folder
            ? folder.GrantId.ResolveGrant()
            : throw new IOException("The file is not in one of My folders.");

    /// <summary>
    /// Gets the path of a file, normalized as by <see cref="NormalizePath"/> with the access it is reached through.
    /// </summary>
    /// <param name="fileId">The id of the file.</param>
    /// <returns>The normalized path, or null if there is no access to the file.</returns>
    public async Task<string?> GetNormalizedPathAsync(string fileId) =>
        await ResolveAccessAsync(fileId) is { } access
            ? NormalizePath(access.AccessUrl, NSUrl.FromString(fileId)!.Path!)
            : null;

    /// <summary>
    /// The path with its symbolic links resolved, since the same location may be reached by different paths, such as
    /// through /var and /private/var, and in the same Unicode form, since a name such as "Bjärke" may be told with the
    /// "ä" as one character or as an "a" followed by a combining mark. A link is only resolved where the path can be
    /// seen to exist, so this is done while accessing what gives access to the path.
    /// </summary>
    /// <param name="access">The url whose security scope gives access to the path.</param>
    /// <param name="path">The path.</param>
    /// <returns>The normalized path, without a trailing slash.</returns>
    public static string NormalizePath(NSUrl access, string path)
    {
        using IDisposable scope = new AppleSecurityScope(access);
        return new NSString(path).ResolveSymlinksInPath().ToString().TrimEnd('/').Normalize(NormalizationForm.FormC);
    }
}
