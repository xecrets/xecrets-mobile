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
/// The list of recently used files of the profile signed in, each with the operation that put it there, the most
/// recent first. Files that are changed are listed as reached through one of My folders, while files that are only
/// read are listed with a lasting grant to the file itself. The list is not checked against the files.
/// </summary>
public interface IRecentFilesService
{
    /// <summary>
    /// Gets the listed files.
    /// </summary>
    /// <returns>The listed files, the most recent first.</returns>
    Task<IReadOnlyList<RecentFile>> GetFilesAsync();

    /// <summary>
    /// Lists a file first, in place of any entry for the same file under any id, keeping at most 50 files. A file that
    /// is listed as changed stays listed as changed when it is then only read, and is moved first.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <param name="operation">The operation done on the file.</param>
    /// <returns>True if the file is listed, false if nothing is listed, since the access to a file that is only read
    /// cannot be kept.</returns>
    Task<bool> AddAsync(FileReference file, RecentFileOperation operation);

    /// <summary>
    /// Lists the source file of the current flow, if any, as <see cref="AddAsync"/> does.
    /// </summary>
    /// <param name="operation">The operation done on the file.</param>
    Task AddFlowSourceAsync(RecentFileOperation operation);

    /// <summary>
    /// Removes the entries with an id, if any.
    /// </summary>
    /// <param name="fileId">The id of the entries.</param>
    Task RemoveAsync(string fileId);

    /// <summary>
    /// Removes every entry for a file, under any id.
    /// </summary>
    /// <param name="fileId">An id of the file.</param>
    Task RemoveFileAsync(string fileId);

    /// <summary>
    /// Refers to a listed file by a new id. If the file is also listed by the new id, only the entry listed first is
    /// kept.
    /// </summary>
    /// <param name="fileId">The listed id of the file.</param>
    /// <param name="newFileId">The new id of the file.</param>
    Task ReplaceIdAsync(string fileId, string newFileId);
}
