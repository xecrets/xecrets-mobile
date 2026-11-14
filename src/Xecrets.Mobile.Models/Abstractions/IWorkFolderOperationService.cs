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
/// Encrypts and decrypts files where they are, in one of My folders, replacing the file with the result. A file in none
/// of them is replaced by a result saved where the user chooses. The known passwords are the password of the current
/// profile and its extra passwords. A file that none of them decrypts is kept in a request for a password.
/// </summary>
public interface IWorkFolderOperationService
{
    /// <summary>
    /// True if a file waits for a password that decrypts it, false otherwise.
    /// </summary>
    bool HasPendingPasswordRequest { get; }

    /// <summary>
    /// Encrypts a file in one of My folders, or decrypts it with the known passwords, replacing it with the result in
    /// its folder.
    /// </summary>
    /// <param name="file">The file, reached through one of My folders.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="contents">The contents of the folders of the operation, which tell what is in the folder of the
    /// file.</param>
    /// <param name="reporter">Is told the outcome of the file.</param>
    /// <returns>The file is replaced by the result, left since the user declined to replace the existing file with the
    /// name of the result, or waits for a password.</returns>
    Task<FileOperationOutcome> TransformAsync(
        FileReference file,
        WorkFolderOperation operation,
        FolderContents contents,
        IFileCompletionReporter reporter);

    /// <summary>
    /// Encrypts a file that is in none of My folders, saving the result where the user chooses, and then wipes the
    /// file.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <returns>The saved file, or that nothing is done, in which case the user has been told why.</returns>
    Task<FileOperationOutcome> EncryptElsewhereAsync(FileReference file);

    /// <summary>
    /// Decrypts a file that is in none of My folders with the known passwords, saving the result where the user
    /// chooses, and then wipes the file.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <returns>The saved file, that nothing is done, in which case the user has been told why, or that the file waits
    /// for a password.</returns>
    Task<FileOperationOutcome> DecryptElsewhereWithKnownPasswordsAsync(FileReference file);

    /// <summary>
    /// Decrypts the file that waits for a password with a password, as the operation on it was started. An extra
    /// password that decrypts it is recorded as used.
    /// </summary>
    /// <param name="password">The password to try.</param>
    /// <returns>The outcome as for the operation started, or that a password is still needed if the password is wrong
    /// or no file waits for one.</returns>
    Task<FileOperationOutcome> DecryptPendingWithPasswordAsync(string password);

    /// <summary>
    /// Ends the request for a password, if any, whether or not a password has decrypted the file.
    /// </summary>
    void CancelPasswordRequest();

    /// <summary>
    /// Waits for the request for a password to end. Requires a request for a password, and is called before the user is
    /// asked for the password, since the request may end as soon as the user is asked.
    /// </summary>
    /// <returns>The outcome as for the operation started when a password has decrypted the file, that nothing is done
    /// when the user has been told why, and otherwise that a password is still needed.</returns>
    Task<FileOperationOutcome> WaitForPasswordRequestAsync();
}
