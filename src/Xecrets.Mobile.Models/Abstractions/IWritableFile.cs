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

namespace Xecrets.Mobile.Models.Abstractions;

/// <summary>
/// A file to overwrite and delete. The other members are called while the access from <see cref="AcquireAccess"/> is
/// held.
/// </summary>
public interface IWritableFile
{
    /// <summary>
    /// Acquires the access to the file that the other members need.
    /// </summary>
    /// <returns>A scope that holds the access until it is disposed.</returns>
    IDisposable AcquireAccess();

    /// <summary>
    /// Tells if the contents of the file can be written.
    /// </summary>
    /// <returns>True if the contents can be written, false otherwise.</returns>
    Task<bool> CanWriteAsync();

    /// <summary>
    /// Tells if the file can be deleted.
    /// </summary>
    /// <returns>True if the file can be deleted, false otherwise.</returns>
    Task<bool> CanDeleteAsync();

    /// <summary>
    /// Gets the length of the file.
    /// </summary>
    /// <returns>The length of the file in bytes, or null if the storage does not tell it.</returns>
    Task<long?> GetLengthAsync();

    /// <summary>
    /// Overwrites the file in place, as one operation, coordinated with other processes where the platform does so.
    /// </summary>
    /// <param name="overwrite">Does all the writing to the stream of the file given to it, which is positioned at the
    /// start of the file.</param>
    Task OverwriteAsync(Action<Stream> overwrite);

    /// <summary>
    /// Deletes the file, together with the other versions of it that the platform keeps, where it can remove them.
    /// Fails if the file is not deleted.
    /// </summary>
    Task DeleteAsync();
}
