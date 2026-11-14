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

using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Models.Abstractions;

/// <summary>
/// Wipes files, overwriting their contents before deleting them where that is meaningful.
/// </summary>
public interface IFileWiper
{
    /// <summary>
    /// Tells if there are the rights needed to wipe a file.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <returns>True if the file can be wiped, false otherwise.</returns>
    Task<bool> CanWipeAsync(IWritableFile file);

    /// <summary>
    /// Wipes a file, overwriting it first only when it is on the device. Elsewhere it is only deleted. If wiping fails,
    /// the file is still deleted if possible.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <param name="isOnDevice">True if the file is stored on the device, false if it is stored elsewhere, such as in
    /// cloud storage or on removable media.</param>
    /// <returns>How the file was wiped.</returns>
    Task<FileWipeStatus> WipeAsync(IWritableFile file, bool isOnDevice);
}
