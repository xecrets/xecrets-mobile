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

using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Utilities;

/// <summary>
/// Lists folders on storage reached by paths, one listing per folder. Hidden and system files and folders are skipped,
/// as the pickers do, but the names of hidden and system files are listed, since a result saved under one of them
/// replaces it. A folder that cannot be read fails the listing, since its files would otherwise be left out without the
/// user knowing.
/// </summary>
internal static class LocalFolderLister
{
    private const FileAttributes _skippedAttributes = FileAttributes.Hidden | FileAttributes.System;

    /// <summary>
    /// Lists the folder, and the folders below it when the scope includes them.
    /// </summary>
    /// <param name="folder">The folder to list.</param>
    /// <param name="scope">Whether the folders below it are listed.</param>
    /// <param name="getId">The id of a file or folder.</param>
    /// <param name="locate">Where a file to operate on is stored.</param>
    /// <param name="nameComparer">How the storage compares names.</param>
    public static IReadOnlyList<FolderListing> List(
        DirectoryInfo folder,
        FolderScope scope,
        Func<FileSystemInfo, string> getId,
        Func<FileInfo, FileReference> locate,
        IEqualityComparer<string> nameComparer)
    {
        List<FolderListing> listings = [];
        Add(folder, scope, getId, locate, nameComparer, listings);
        return listings;
    }

    private static void Add(
        DirectoryInfo folder,
        FolderScope scope,
        Func<FileSystemInfo, string> getId,
        Func<FileInfo, FileReference> locate,
        IEqualityComparer<string> nameComparer,
        List<FolderListing> listings)
    {
        List<FolderFile> files = [];
        Dictionary<string, string> idsByName = new(nameComparer);
        foreach (FileInfo file in folder.EnumerateFiles(
                     "*",
                     new EnumerationOptions { AttributesToSkip = 0, IgnoreInaccessible = false }))
        {
            idsByName.TryAdd(file.Name, getId(file));
            if ((file.Attributes & _skippedAttributes) == 0)
            {
                files.Add(new FolderFile(locate(file), file.Length));
            }
        }

        listings.Add(new FolderListing(getId(folder), files, idsByName));
        if (!IsBelowListed(scope))
        {
            return;
        }

        // Hidden and system folders are skipped, as by default.
        foreach (DirectoryInfo subfolder in folder.EnumerateDirectories(
                     "*",
                     new EnumerationOptions { IgnoreInaccessible = false }))
        {
            Add(subfolder, scope, getId, locate, nameComparer, listings);
        }
    }

    private static bool IsBelowListed(FolderScope scope) => scope switch
    {
        FolderScope.Folder => false,
        FolderScope.FolderAndSubfolders => true,
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, @"Unknown folder scope."),
    };
}
