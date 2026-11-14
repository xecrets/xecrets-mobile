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
/// Saves results in My folders so that a save that the app ended in the middle of can be undone.
/// </summary>
public interface IInterruptedSaveService
{
    /// <summary>
    /// Saves a complete file as <see cref="IFileAccess.SaveInFolderAsync"/> does, keeping the save on the device until
    /// it is done, so that <see cref="RecoverAsync"/> can undo it. The source file of the save must not be removed
    /// before this returns.
    /// </summary>
    /// <param name="save">The save. Its <see cref="SaveInProgress.UserId"/> is not used, since the save is of the
    /// profile signed in.</param>
    /// <param name="completedFilePath">The full path of the complete file to save.</param>
    /// <param name="existingId">The id of the file with the name in the folder, which is replaced, or null if there is
    /// none.</param>
    /// <returns>The id of the saved file.</returns>
    Task<string> SaveAsync(SaveInProgress save, string completedFilePath, string? existingId);

    /// <summary>
    /// Undoes a save of the profile signed in that was not done, if any. When the source file can be reached, the file
    /// saved in part is removed, and the source file gets its name back if it was renamed. The user is told when the
    /// files may have changed. The save is no longer kept afterwards, whether it could be undone or not.
    /// </summary>
    Task RecoverAsync();
}
