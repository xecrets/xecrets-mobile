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

using Xecrets.Mobile.Models.Abstractions;

namespace Xecrets.Mobile.Models.Services;

/// <summary>
/// Creates requests to encrypt a file, with progress reported to the <see cref="RunningOperation"/>, which also
/// cancels the encryption. All times of the requests are the current time.
/// </summary>
internal sealed class EncryptRequestFactory(IProfileService profileService, RunningOperation runningOperation)
{
    /// <summary>
    /// Creates a request to encrypt with the password and the public key of the current profile.
    /// </summary>
    /// <param name="originalFileName">The original name of the file to encrypt.</param>
    /// <returns>The request.</returns>
    public EncryptRequest Create(string originalFileName) => Create(originalFileName, profileService.GetIdentity().Passphrase);

    /// <summary>
    /// Creates a request to encrypt with a password and the public key of the current profile.
    /// </summary>
    /// <param name="originalFileName">The original name of the file to encrypt.</param>
    /// <param name="password">The password to encrypt with.</param>
    /// <returns>The request.</returns>
    public EncryptRequest Create(string originalFileName, string password)
    {
        EncryptedWithParameters encryptedWith = new(password, [profileService.GetPublicKey()], []);
        return Create(originalFileName, password, encryptedWith);
    }

    /// <summary>
    /// Creates a request to encrypt with the password of the current profile, for the recipients and master keys a
    /// file is encrypted for, and for the public key of the current profile.
    /// </summary>
    /// <param name="originalFileName">The original name of the file to encrypt.</param>
    /// <param name="encryptedWith">What the file is encrypted with. Its password is not used.</param>
    /// <returns>The request.</returns>
    public EncryptRequest Create(string originalFileName, EncryptedWithParameters encryptedWith) =>
        Create(originalFileName, profileService.GetIdentity().Passphrase, encryptedWith);

    /// <summary>
    /// Creates a request to encrypt with a password, for the recipients and master keys a file is encrypted for, and
    /// for the public key of the current profile.
    /// </summary>
    /// <param name="originalFileName">The original name of the file to encrypt.</param>
    /// <param name="password">The password to encrypt with.</param>
    /// <param name="encryptedWith">What the file is encrypted with. Its password is not used.</param>
    /// <returns>The request.</returns>
    public EncryptRequest Create(string originalFileName, string password, EncryptedWithParameters encryptedWith)
    {
        DateTime utcNow = DateTime.UtcNow;
        EncryptedWithParameters reEncryptWith = encryptedWith.ReEncryptWith(password, [profileService.GetPublicKey()]);
        return new EncryptRequest(
            reEncryptWith.Passphrase,
            reEncryptWith.Recipients,
            reEncryptWith.MasterKeys,
            originalFileName,
            utcNow,
            utcNow,
            utcNow,
            true,
            runningOperation.CreateProgress());
    }
}
