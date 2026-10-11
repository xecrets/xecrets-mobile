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
using System.Runtime.Versioning;
using Windows.Storage;
using Xecrets.Common.Models;
using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Platforms.Windows;

/// <summary>
/// Tells where a file is stored from its path. A path has no provider, so the folder path starts with the drive, such
/// as C:, or the share of a UNC path.
/// </summary>
[SupportedOSPlatform("windows10.0.19041")]
internal static class WindowsFileLocation
{
    public static FileReference Locate(StorageFile file) => Locate(file.Path, file.Name);

    public static FileReference Locate(StorageFolder folder) => Locate(folder.Path, folder.DisplayName);

    private static FileReference Locate(string path, string name)
    {
        string folder = Path.GetDirectoryName(path) ?? "";
        string root = Path.GetPathRoot(folder) ?? "";

        List<string> folderPath = [.. folder[root.Length..].Split(
            Path.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries)];

        if (root.Length > 0)
        {
            folderPath.Insert(0, root.TrimEnd(Path.DirectorySeparatorChar));
        }

        return new FileReference(path, name) { Root = RootProvider.None, FolderPath = folderPath };
    }
}
