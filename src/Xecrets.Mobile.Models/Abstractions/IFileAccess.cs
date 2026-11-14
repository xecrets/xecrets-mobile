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
/// The primitive file operations each platform provides. A file is changed where it is through one of My folders,
/// which holds a grant to the folder, or, when it is in none of them, by the access the picker gave to the file. A
/// result is always complete before it is saved, in the folder or where the user chooses. A file that is only read
/// may be reached through a grant to the file itself.
/// <para>
/// A file id is the platform reference to it: a content uri on Android, a file url on Apple platforms and a path on
/// Windows. A folder id or initial folder is the <see cref="WorkFolder.Id"/> of a folder, or the parent of a file as
/// given by <see cref="GetParentFolderAsync"/>. An empty initial folder lets the platform decide where to start.
/// </para>
/// </summary>
public interface IFileAccess
{
    /// <summary>
    /// Lets the user pick a file. The file can be read while the app runs, and also later once
    /// <see cref="KeepFileGrantAsync"/> is called for it.
    /// </summary>
    /// <param name="initialFolder">The folder to start in, if the platform allows it.</param>
    /// <param name="kind">The kind of files to pick from.</param>
    /// <returns>The picked file, or null if the user cancels.</returns>
    Task<FileReference?> PickFileAsync(string initialFolder, FilePickerKind kind);

    /// <summary>
    /// Lets the user pick a folder to give the app lasting access to. Where the folder picker of the platform shows no
    /// files, the user instead picks one of the files in the folder. A picked folder is checked to be writable and its
    /// grant is kept, but it is not added to My folders.
    /// </summary>
    /// <param name="initialFolder">The folder to start in, if the platform allows it.</param>
    /// <param name="kind">The kind of files in the folder that the picker shows, if it shows files.</param>
    /// <returns>The result of picking the folder.</returns>
    Task<WorkFolderResult> PickFolderAsync(string initialFolder, FilePickerKind kind);

    /// <summary>
    /// Finds the innermost of My folders that has a grant and contains a file, directly or in a folder below it.
    /// </summary>
    /// <param name="fileId">The id of the file.</param>
    /// <returns>The folder, or null if there is none.</returns>
    Task<WorkFolder?> FindFolderAsync(string fileId);

    /// <summary>
    /// Finds the innermost of My folders that has a grant and is a folder, or contains it, directly or in a folder
    /// below it.
    /// </summary>
    /// <param name="folderId">The id of the folder.</param>
    /// <returns>The folder of My folders, or null if there is none.</returns>
    Task<WorkFolder?> FindFolderOfFolderAsync(string folderId);

    /// <summary>
    /// Gets a file as reached through the grant of a folder that contains it.
    /// </summary>
    /// <param name="folder">One of My folders that contains the file.</param>
    /// <param name="file">The file.</param>
    /// <returns>The file reached through the folder, which can be changed.</returns>
    Task<FileReference> GetFileInFolderAsync(WorkFolder folder, FileReference file);

    /// <summary>
    /// Lists a folder, and each of the folders below it when the scope includes them. A folder that cannot be listed
    /// fails the listing.
    /// </summary>
    /// <param name="folderId">One of My folders, or a folder given by <see cref="GetFolderOfFileAsync"/>.</param>
    /// <param name="scope">Which folders to list.</param>
    /// <returns>One listing for each folder, the listing of the folder itself first.</returns>
    Task<IReadOnlyList<FolderListing>> ListFolderAsync(string folderId, FolderScope scope);

    /// <summary>
    /// Gets the folder that a file is in, as far as the platform tells it.
    /// </summary>
    /// <param name="fileId">The id of the file.</param>
    /// <returns>The id of the folder, which can be the initial folder of a picker, or "" if it cannot be told.</returns>
    Task<string> GetParentFolderAsync(string fileId);

    /// <summary>
    /// Gets the path of a file or folder as segments to show, the last being its name. Only what can be told from the
    /// id is used, so the file need not be accessible.
    /// </summary>
    /// <param name="id">The id of the file or folder.</param>
    /// <param name="rememberedName">The name of the file or folder, used as the last segment when the id does not tell
    /// it, or "" if not known.</param>
    /// <returns>The segments of the path, outermost first.</returns>
    IReadOnlyList<string> GetPathSegments(string id, string rememberedName);

    /// <summary>
    /// Tells if two ids refer to the same file, such as a file picked by itself and the same file reached through one
    /// of My folders.
    /// </summary>
    /// <param name="fileId">The id of one file.</param>
    /// <param name="otherFileId">The id of the other file.</param>
    /// <returns>True if the ids refer to the same file, false otherwise.</returns>
    Task<bool> IsSameFileAsync(string fileId, string otherFileId);

    /// <summary>
    /// Keeps the access to a picked file, so that it can be read again later, also after the app is restarted.
    /// </summary>
    /// <param name="fileId">The id of the picked file.</param>
    /// <returns>True if the access is kept, false if it cannot be kept.</returns>
    Task<bool> KeepFileGrantAsync(string fileId);

    /// <summary>
    /// Tells again where a file is stored, after it has been read. Some storage only tells it once the file is on the
    /// device, which it may not be when it is picked.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <returns>The file with where it is stored, or the file as it is if that cannot be told better.</returns>
    Task<FileReference> LocateAgainAsync(FileReference file);

    /// <summary>
    /// Tells if there is lasting access to a file by itself, such as kept by <see cref="KeepFileGrantAsync"/>.
    /// </summary>
    /// <param name="fileId">The id of the file.</param>
    /// <returns>True if there is lasting access to the file, false otherwise.</returns>
    bool HasFileGrant(string fileId);

    /// <summary>
    /// Releases all folder and file grants held that are not referenced.
    /// </summary>
    /// <param name="references">The grants that are referenced, which are kept.</param>
    Task ReleaseUnusedGrantsAsync(GrantReferences references);

    /// <summary>
    /// Opens a file for reading.
    /// </summary>
    /// <param name="fileId">The id of the file.</param>
    /// <returns>A stream that reads the file, which the caller owns.</returns>
    /// <exception cref="FileNotAccessibleException">The file is not found, or cannot be reached now.</exception>
    Task<Stream> OpenReadAsync(string fileId);

    /// <summary>
    /// Reads the start of a file.
    /// </summary>
    /// <param name="fileId">The id of the file.</param>
    /// <param name="count">The largest number of bytes to read.</param>
    /// <returns>The first bytes of the file, fewer than the count if the file is shorter.</returns>
    /// <exception cref="FileNotAccessibleException">The file is not found, or cannot be reached now.</exception>
    Task<byte[]> ReadStartAsync(string fileId, int count);

    /// <summary>
    /// Gets the times of a file.
    /// </summary>
    /// <param name="fileId">The id of the file.</param>
    /// <returns>The times of the file, where a time that the platform does not tell is the current time.</returns>
    Task<FileTimes> GetTimesAsync(string fileId);

    /// <summary>
    /// Sets the times of a file, as far as the platform and the storage allow. A time that cannot be set is left as it
    /// is, without failing.
    /// </summary>
    /// <param name="fileId">The id of the file.</param>
    /// <param name="times">The times to set.</param>
    Task SetSupportedTimesAsync(string fileId, FileTimes times);

    /// <summary>
    /// Checks that a file can be read now, without reading it. Storage that does not keep the file on the device may
    /// still have to get it.
    /// </summary>
    /// <param name="fileId">The id of the file.</param>
    /// <exception cref="FileNotAccessibleException">The file is not found, or cannot be reached now.</exception>
    Task CheckReachableAsync(string fileId);

    /// <summary>
    /// Gets a file for overwriting and deleting it.
    /// </summary>
    /// <param name="fileId">The id of a file reached through one of My folders, or of a file picked while the app
    /// runs.</param>
    /// <returns>The writable file.</returns>
    Task<IWritableFile> OpenWriteAsync(string fileId);

    /// <summary>
    /// Gets the folder that a file reached through one of My folders is in, as reached through the same folder. This
    /// may take a while.
    /// </summary>
    /// <param name="fileId">The id of a file reached through one of My folders.</param>
    /// <returns>The id of the folder of the file.</returns>
    /// <exception cref="FileNotAccessibleException">The file is not found. Any other exception means that the folders
    /// cannot be searched.</exception>
    Task<string> GetFolderOfFileAsync(string fileId);

    /// <summary>
    /// Saves a complete file under a name in a folder. A partly saved file is removed, and a file it replaced is then
    /// lost. The complete file is left as it is.
    /// </summary>
    /// <param name="folderId">A folder given by <see cref="GetFolderOfFileAsync"/>.</param>
    /// <param name="name">The name to save the file under.</param>
    /// <param name="completedFilePath">The full path of the complete file to save.</param>
    /// <param name="existingId">The id of the file with the name in the folder, as listed, which is replaced, or null if
    /// there is none, in which case saving fails if there already is a file with the name.</param>
    /// <returns>The id of the saved file.</returns>
    Task<string> SaveInFolderAsync(string folderId, string name, string completedFilePath, string? existingId);

    /// <summary>
    /// Renames a file reached through one of My folders. Fails if there already is a file with the new name, or if the
    /// storage gives the file another name than the one asked for.
    /// </summary>
    /// <param name="fileId">The id of the file.</param>
    /// <param name="newName">The new name of the file.</param>
    /// <returns>The id of the file under the new name.</returns>
    Task<string> RenameAsync(string fileId, string newName);

    /// <summary>
    /// Lets the user save a copy of a complete file anywhere. The user or the storage may change the name. No lasting
    /// access to the saved file is kept, and the complete file is left as it is.
    /// </summary>
    /// <param name="initialFolder">The folder to start in, if the platform allows it.</param>
    /// <param name="fileName">The suggested file name.</param>
    /// <param name="completedFilePath">The full path of the complete file to save.</param>
    /// <returns>The saved file, or null if the user cancels.</returns>
    Task<FileReference?> SaveAsAsync(string initialFolder, string fileName, string completedFilePath);
}
