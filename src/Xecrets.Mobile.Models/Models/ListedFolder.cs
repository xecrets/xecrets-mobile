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

namespace Xecrets.Mobile.Models.Models;

/// <summary>
/// A folder as listed once for an operation, and kept up to date with what the operation saves in it and removes from
/// it, so that it tells what is there without being listed again.
/// </summary>
/// <param name="listing">The listing of the folder.</param>
public sealed class ListedFolder(FolderListing listing)
{
    private readonly Dictionary<string, string> _idsByName = new(listing.IdsByName, listing.IdsByName.Comparer);

    /// <summary>
    /// The id of the folder.
    /// </summary>
    public string Id => listing.FolderId;

    /// <summary>
    /// Finds what has a name in the folder, compared as the storage compares names.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The id of what has the name, or null if nothing has.</returns>
    public string? FindId(string name) => _idsByName.GetValueOrDefault(name);

    /// <summary>
    /// Records a file that the operation has saved in the folder, replacing what had the name.
    /// </summary>
    /// <param name="name">The name of the file.</param>
    /// <param name="id">The id of the file.</param>
    public void Add(string name, string id) => _idsByName[name] = id;

    /// <summary>
    /// Records that the operation has removed a file from the folder.
    /// </summary>
    /// <param name="name">The name of the file.</param>
    public void Remove(string name) => _idsByName.Remove(name);
}
