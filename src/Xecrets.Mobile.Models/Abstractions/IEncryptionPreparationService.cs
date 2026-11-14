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

using Xecrets.Mobile.Models.Services;

namespace Xecrets.Mobile.Models.Abstractions;

/// <summary>
/// Encrypts copies of files to temporary files, which keep the name and times of the original file.
/// </summary>
public interface IEncryptionPreparationService
{
    /// <summary>
    /// Encrypts a copy of a file with the password and the public key of the current profile.
    /// </summary>
    /// <param name="source">The file to encrypt.</param>
    /// <returns>The encrypted copy.</returns>
    Task<EncryptionPreparationResult> EncryptWithCurrentProfileAsync(IReadableFile source);

    /// <summary>
    /// Encrypts a copy of a file with a password and the public key of the current profile.
    /// </summary>
    /// <param name="source">The file to encrypt.</param>
    /// <param name="password">The password to encrypt with.</param>
    /// <returns>The encrypted copy, with <see cref="EncryptionPreparationResult.EncryptCopyForSharing"/> true.</returns>
    Task<EncryptionPreparationResult> EncryptWithPasswordAsync(IReadableFile source, string password);

    /// <summary>
    /// Encrypts a copy of an encrypted file with a password, for the recipients and master keys the file is encrypted
    /// for, and for the public key of the current profile. The file is decrypted with the password of the current
    /// profile or one of its extra passwords.
    /// </summary>
    /// <param name="encryptedSource">The encrypted file.</param>
    /// <param name="password">The password to encrypt with.</param>
    /// <returns>The encrypted copy, with <see cref="EncryptionPreparationResult.EncryptCopyForSharing"/> true, or null
    /// if none of the passwords decrypts the file.</returns>
    Task<EncryptionPreparationResult?> ReEncryptWithPasswordAsync(IReadableFile encryptedSource, string password);
}
