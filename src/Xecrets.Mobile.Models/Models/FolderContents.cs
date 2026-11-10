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

using Xecrets.Mobile.Models.Abstractions;

namespace Xecrets.Mobile.Models.Models;

/// <summary>
/// The folders of the files of an operation, each listed once for the operation. The folders of the files of a folder
/// are known from when they were listed, while the folder of a file chosen by itself is listed when it is first needed.
/// </summary>
public sealed class FolderContents
{
    private readonly IFileAccess _fileAccess;

    private readonly Dictionary<string, ListedFolder> _foldersByFileId = [];

    public FolderContents(IFileAccess fileAccess, IEnumerable<FolderListing> listings)
    {
        _fileAccess = fileAccess;
        foreach (FolderListing listing in listings)
        {
            ListedFolder folder = new(listing);
            foreach (FolderFile folderFile in listing.Files)
            {
                _foldersByFileId[folderFile.File.Id] = folder;
            }
        }
    }

    /// <summary>
    /// The folder of a file of the operation, listed if it has not been.
    /// </summary>
    public async Task<ListedFolder> GetFolderOfAsync(FileReference file)
    {
        if (_foldersByFileId.TryGetValue(file.Id, out ListedFolder? folder))
        {
            return folder;
        }

        string folderId = await _fileAccess.GetFolderOfFileAsync(file.Id);
        folder = new ListedFolder((await _fileAccess.ListFolderAsync(folderId, FolderScope.Folder)).Single());
        _foldersByFileId[file.Id] = folder;
        return folder;
    }
}
