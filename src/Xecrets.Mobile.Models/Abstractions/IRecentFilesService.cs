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
/// The files in a known folder most recently used, identified by <see cref="Models.WorkFolderFile.Id"/> together with
/// the operation that put them on the list, and ordered most recent first.
/// </summary>
public interface IRecentFilesService
{
    Task<IReadOnlyList<RecentFile>> GetFilesAsync();

    /// <summary>
    /// Puts a file, such as the result of an encryption or decryption, at the top of the list, replacing any earlier
    /// entry for it. The source of an encryption or decryption in place is kept until it is found missing.
    /// </summary>
    Task AddAsync(string fileId, RecentFileOperation operation);

    /// <summary>
    /// Adds the source of the current flow, if it has one in a known folder, after the user acted on it with the
    /// operation.
    /// </summary>
    Task AddFlowSourceAsync(RecentFileOperation operation);

    /// <summary>
    /// Adds the source of the current flow after the user saved a copy of it with the operation. For a file received
    /// from another app, which has no source, the saved copy is added instead, as if encrypted or decrypted in place,
    /// if it can be opened again.
    /// </summary>
    Task AddSavedCopyAsync(WorkFolderFile savedCopy, RecentFileOperation operation);

    /// <summary>
    /// Removes one or more files from the list, such as those found missing. The files themselves are not affected.
    /// </summary>
    Task RemoveAsync(IReadOnlyCollection<string> fileIds);
}
