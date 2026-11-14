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

using Xecrets.Core.Models;

using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Models.Abstractions;

/// <summary>
/// Decrypts encrypted files for previewing them. Decrypted text up to a size is held in memory, and any other decrypted
/// file is written to a read-only temporary file. The known passwords are the password of the current profile and its
/// extra passwords. A file that none of them decrypts waits for a password to be given.
/// </summary>
public interface IPreviewService
{
    /// <summary>
    /// The current preview.
    /// </summary>
    IPreviewState Current { get; }

    /// <summary>
    /// True if a file waits for a password, false otherwise.
    /// </summary>
    bool HasPendingPasswordRequest { get; }

    /// <summary>
    /// Ends the wait for a password for the file waiting, if any, which is then no longer kept.
    /// </summary>
    void CancelPasswordRequest();

    /// <summary>
    /// Makes a decrypted text file the preview, reading the whole file into memory.
    /// </summary>
    /// <param name="file">The decrypted text file.</param>
    /// <param name="encryptedWith">What the file is encrypted with.</param>
    Task PrepareTextAsync(DecryptedFileInfo file, EncryptedWithParameters encryptedWith);

    /// <summary>
    /// Writes the text of the preview to a read-only temporary file, unless the preview already has a file.
    /// </summary>
    /// <returns>The decrypted file of the preview, with the path of its file.</returns>
    Task<DecryptedFileInfo> MaterializeAsync();

    /// <summary>
    /// Decrypts a copy of an encrypted file with the known passwords, and makes it the preview. Any file waiting for a
    /// password is replaced.
    /// </summary>
    /// <param name="encryptedFile">The encrypted file.</param>
    /// <returns>True if the preview is prepared, false if the file waits for a password or the operation is
    /// canceled.</returns>
    Task<bool> PrepareAsync(IReadableFile encryptedFile);

    /// <summary>
    /// Decrypts a temporary encrypted file with the known passwords, and makes it the preview. Any file waiting for a
    /// password is replaced.
    /// </summary>
    /// <param name="encryptedFile">The temporary encrypted file, which the service takes over, so that the caller must
    /// not dispose it.</param>
    /// <returns>The outcome, which is <see cref="PreviewPreparationStatus.WrongPassword"/> when the file waits for a
    /// password.</returns>
    Task<PreviewPreparationStatus> PrepareImportedAsync(ITransientFile encryptedFile);

    /// <summary>
    /// Decrypts the file waiting for a password with a password, and makes it the preview. The file no longer waits
    /// unless the password is wrong. An extra password that decrypts it is recorded as used.
    /// </summary>
    /// <param name="password">The password to try.</param>
    /// <returns>The outcome, which is <see cref="PreviewPreparationStatus.Cancelled"/> if no file waits for a password,
    /// or the file is gone.</returns>
    Task<PreviewPreparationStatus> PrepareWithPasswordAsync(string password);
}
