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

using Xecrets.Common.Models;
using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Models.Abstractions;

/// <summary>
/// The list of recently used files, each with the operation that put it there. Files that are changed are listed as
/// reached through one of My folders, while files that are only read are listed with a grant to the file itself. The
/// list is not checked against the files, which is only done when a file on it is used.
/// </summary>
public interface IRecentFilesService
{
    Task<IReadOnlyList<RecentFile>> GetFilesAsync();

    /// <summary>
    /// Lists the file first, in place of any entry for the same id. Returns false, and lists nothing, when the access
    /// to a file that is only read cannot be kept.
    /// </summary>
    Task<bool> AddAsync(FileReference file, RecentFileOperation operation);

    /// <summary>
    /// Lists the file the current flow works on, if any.
    /// </summary>
    Task AddFlowSourceAsync(RecentFileOperation operation);

    Task RemoveAsync(string fileId);

    /// <summary>
    /// Removes every entry for the file, under any id, such as when the file is replaced or deleted.
    /// </summary>
    Task RemoveFileAsync(string fileId);

    /// <summary>
    /// Refers to a listed file by a new id, such as when it is now reached through another of My folders.
    /// </summary>
    Task ReplaceIdAsync(string fileId, string newFileId);
}
