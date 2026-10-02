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
public sealed class AppleFileGrants(WorkFolderStorage storage)
{
    // The urls the pickers gave while the app runs, by file id, which carry the access to the file picked.
    private readonly Dictionary<string, NSUrl> _picked = [];

    public FileReference Remember(NSUrl fileUrl)
    {
        _picked[fileUrl.AbsoluteString!] = fileUrl;
        return new FileReference(fileUrl.AbsoluteString!, fileUrl.LastPathComponent!);
    }

    public bool IsPicked(string fileId) => _picked.ContainsKey(fileId);

    /// <summary>
    /// Finds the innermost of My folders that contains the file or folder, or is the folder itself when that is
    /// allowed. The paths are compared with their symbolic links resolved, since the same location may be reached by
    /// different paths, such as through /var and /private/var.
    /// </summary>
    public async Task<WorkFolder?> FindFolderAsync(string id, bool isFolderItself)
    {
        string path = ResolvePath(NSUrl.FromString(id)!.Path!).TrimEnd('/');
        WorkFolder? innermost = null;
        int innermostLength = -1;
        foreach (WorkFolder folder in await storage.LoadFoldersAsync())
        {
            if (AppleExtensions.TryResolveGrant(folder.GrantId)?.Path is not { } grantPath)
            {
                continue;
            }

            string folderPath = ResolvePath(grantPath).TrimEnd('/');
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
    /// Runs the action with a url of the file while there is access to it, such as to hand it to another app.
    /// </summary>
    public async Task<T> WithAccessAsync<T>(string fileId, Func<NSUrl, Task<T>> action)
    {
        (NSUrl access, NSUrl fileUrl) = await ResolveAccessAsync(fileId) ?? throw new FileNotAccessibleException();
        return await access.WithAccessAsync(() => action(fileUrl));
    }

    /// <summary>
    /// Finds what gives access to the file: the url a picker gave, the grant of a folder containing it, or its own
    /// bookmark. A bookmark follows a file that is renamed, but such a file is not where it was. Returns the url to
    /// access through and the url of the file, or null when there is no access.
    /// </summary>
    public async Task<(NSUrl Access, NSUrl File)?> ResolveAccessAsync(string fileId)
    {
        if (_picked.TryGetValue(fileId, out NSUrl? picked))
        {
            return (picked, picked);
        }

        NSUrl fileUrl = NSUrl.FromString(fileId)!;
        if (await FindFolderAsync(fileId, isFolderItself: false) is { } folder &&
            AppleExtensions.TryResolveGrant(folder.GrantId) is { } grant)
        {
            return (grant, fileUrl);
        }

        return AppleExtensions.TryResolveGrant(fileId) is { } own && own.LastPathComponent == fileUrl.LastPathComponent
            ? (own, own)
            : null;
    }

    // The access belongs to the url resolved from the grant, not to a url created from the same string, so it is
    // resolved again for each use.
    public async Task<NSUrl> GetFolderGrantAsync(string id, bool isFolderItself) =>
        await FindFolderAsync(id, isFolderItself) is { } folder
            ? AppleExtensions.ResolveGrant(folder.GrantId)
            : throw new IOException("The file is not in one of My folders.");

    public static string ResolvePath(string path) => new NSString(path).ResolveSymlinksInPath().ToString();
}
