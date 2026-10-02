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
/// The primitive file operations each platform provides. Anything that changes a file does so through one of My
/// folders, which holds a grant to the folder, while a file that is only read may be reached through a grant to the
/// file itself.
/// <para>
/// A file id is the platform reference to it: a content uri on Android, a file url on Apple platforms and a path on
/// Windows. A folder id or initial folder is the <see cref="WorkFolder.Id"/> of a folder, or the parent of a file as
/// given by <see cref="GetParentFolder"/>. An empty initial folder lets the platform decide where to start.
/// </para>
/// </summary>
public interface IFileAccess
{
    /// <summary>
    /// Lets the user pick a file, starting in the initial folder if the platform allows it. The file can be read while
    /// the app runs, and later as well once <see cref="KeepFileGrantAsync"/> is called. Returns null when the user cancels.
    /// </summary>
    Task<FileReference?> PickFileAsync(string initialFolder, FilePickerKind kind);

    /// <summary>
    /// Lets the user pick a folder to give the app lasting access to, starting in the initial folder if the platform
    /// allows it. The folder is checked to be writable and its grant is kept, but it is not added to My folders.
    /// </summary>
    Task<WorkFolderResult> PickFolderAsync(string initialFolder);

    /// <summary>
    /// Finds the innermost of My folders that has a grant and contains the file, directly or in a folder below it.
    /// Returns null when there is none.
    /// </summary>
    Task<WorkFolder?> FindFolderAsync(string fileId);

    /// <summary>
    /// The file as reached through the grant of a folder known to contain it, which is the reference used to change
    /// it.
    /// </summary>
    FileReference GetFileInFolder(WorkFolder folder, FileReference file);

    /// <summary>
    /// The folder of the file, as a hint where to start a picker, or empty when it cannot be told.
    /// </summary>
    string GetParentFolder(string fileId);

    /// <summary>
    /// The path of the file or folder as segments to show, the last being its name. Only what can be told from the id
    /// is used, so that it works without access to the file. The remembered name is the last segment when the id does
    /// not tell it.
    /// </summary>
    IReadOnlyList<string> GetPathSegments(string id, string? rememberedName = null);

    /// <summary>
    /// Tells if two ids refer to the same file, such as one picked by itself and the same file reached through one of
    /// My folders. Only what can be told from the ids is used.
    /// </summary>
    bool IsSameFile(string fileId, string otherFileId);

    /// <summary>
    /// Keeps the access to a picked file for reading it again later. Returns false when it cannot be kept.
    /// </summary>
    Task<bool> KeepFileGrantAsync(string fileId);

    /// <summary>
    /// Tells if there is access to the file by itself, such as kept by <see cref="KeepFileGrantAsync"/>.
    /// </summary>
    bool HasFileGrant(string fileId);

    /// <summary>
    /// Releases all folder and file grants held that are not referenced.
    /// </summary>
    Task ReleaseUnusedGrantsAsync(GrantReferences references);

    /// <summary>
    /// Opens the file for reading. Throws <see cref="FileNotAccessibleException"/> when it is not found, or cannot be
    /// reached now.
    /// </summary>
    Task<Stream> OpenReadAsync(string fileId);

    /// <summary>
    /// The file for writing, renaming and deleting it, which requires it to be reached through one of My folders.
    /// </summary>
    Task<IWritableFile> OpenWritableAsync(string fileId);

    /// <summary>
    /// The folder of a file reached through one of My folders, as reached through the same folder. Finding it may take
    /// a while, so it is found once for an operation, and used throughout it. Throws
    /// <see cref="FileNotAccessibleException"/> only when the file was not found, and another exception when the
    /// folder could not be searched.
    /// </summary>
    Task<string> GetFolderOfFileAsync(string fileId);

    /// <summary>
    /// Tells if there is a file with the name in the folder, as given by <see cref="GetFolderOfFileAsync"/>.
    /// </summary>
    Task<bool> FileExistsInFolderAsync(string folderId, string name);

    /// <summary>
    /// Writes a file with the name in the folder, as given by <see cref="GetFolderOfFileAsync"/>, and returns its id.
    /// </summary>
    Task<string> WriteFileInFolderAsync(string folderId, string name, bool overwrite, Func<Stream, Task> writer);

    /// <summary>
    /// Lets the user save a copy anywhere. Returns null when the user cancels. No lasting access to the saved file is
    /// kept.
    /// </summary>
    Task<FileReference?> SaveAsAsync(string fileName, Stream content);
}
