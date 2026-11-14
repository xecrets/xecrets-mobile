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
using Xecrets.Common.Models;

using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Services;

namespace Xecrets.Mobile.Models.Abstractions;

/// <summary>
/// The local profile of the app, signing in to it, and its passwords and keys. The members that use the profile
/// signed in require it to be signed in. At most 25 extra passwords are kept, and the least used are dropped to make
/// room for a new one.
/// </summary>
public interface IProfileService
{
    /// <summary>
    /// The email of the profile signed in, or "" if not signed in.
    /// </summary>
    string CurrentEmail { get; }

    /// <summary>
    /// The password of the profile signed in, or "" if not signed in.
    /// </summary>
    string CurrentPassword { get; }

    /// <summary>
    /// True if the profile is signed in, false otherwise.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Tells if there is a local profile.
    /// </summary>
    /// <returns>True if there is a local profile, false otherwise.</returns>
    Task<bool> HasProfileAsync();

    /// <summary>
    /// Loads the sign-in key of the local profile.
    /// </summary>
    /// <returns>The sign-in key, or null if there is no local profile.</returns>
    Task<SignInKey?> LoadProfileAsync();

    /// <summary>
    /// Creates the local profile with a new key pair, without signing in.
    /// </summary>
    /// <param name="email">The email of the profile.</param>
    /// <param name="password">The password of the profile.</param>
    /// <returns>The outcome, which is <see cref="ProfileActionStatus.InvalidEmail"/> or
    /// <see cref="ProfileActionStatus.AlreadyExists"/> when no profile is created.</returns>
    Task<ProfileActionResult> CreateProfileAsync(string email, string password);

    /// <summary>
    /// Changes the email and password of the profile signed in, keeping its key pair, and signs in with the new
    /// password. The private keys are encrypted with the new password, and a changed password is kept among the extra
    /// passwords.
    /// </summary>
    /// <param name="email">The new email of the profile.</param>
    /// <param name="password">The new password of the profile.</param>
    /// <returns>The outcome, which is <see cref="ProfileActionStatus.InvalidEmail"/> when nothing is changed.</returns>
    Task<ProfileActionResult> ChangeProfileAsync(string email, string password);

    /// <summary>
    /// Signs in to the local profile. Signing in encrypts with the password the private keys that the password or the
    /// extra passwords decrypt.
    /// </summary>
    /// <param name="password">The password of the profile.</param>
    /// <returns>The outcome, which is <see cref="ProfileActionStatus.NotFound"/> if there is no local profile and
    /// <see cref="ProfileActionStatus.WrongPassword"/> if the password is wrong.</returns>
    Task<ProfileActionResult> LoginAsync(string password);

    /// <summary>
    /// Resets the password of the local profile, which is not signed in, for when the password is forgotten. The
    /// profile gets a new key pair, the old one is kept among the private keys, and the extra passwords are cleared.
    /// </summary>
    /// <param name="password">The new password of the profile.</param>
    Task ResetProfileAsync(string password);

    /// <summary>
    /// Signs out, if signed in.
    /// </summary>
    void SignOut();

    /// <summary>
    /// Gets the identity of the profile signed in.
    /// </summary>
    /// <returns>The password of the profile, with its key pair and the other private keys that are decrypted.</returns>
    /// <exception cref="InvalidOperationException">The profile is not signed in.</exception>
    Identity GetIdentity();

    /// <summary>
    /// Gets the extra passwords of the profile signed in.
    /// </summary>
    /// <returns>The extra passwords, the most used first.</returns>
    IReadOnlyList<PasswordUsage> GetExtraPasswords();

    /// <summary>
    /// Records that a password decrypted a file, adding it to the extra passwords if it is not among them. A new extra
    /// password also decrypts the private keys encrypted with it.
    /// </summary>
    /// <param name="password">The password.</param>
    Task RecordExtraPasswordUseAsync(string password);

    /// <summary>
    /// Adds a password to the extra passwords, as not yet used, unless it is already among them. The password also
    /// decrypts the private keys encrypted with it.
    /// </summary>
    /// <param name="password">The password.</param>
    Task AddExtraPasswordAsync(string password);

    /// <summary>
    /// Removes a password from the extra passwords, if it is among them.
    /// </summary>
    /// <param name="password">The password.</param>
    Task RemoveExtraPasswordAsync(string password);

    /// <summary>
    /// Counts the private keys of the profile signed in, besides its key pair.
    /// </summary>
    /// <returns>The counts.</returns>
    Task<PrivateKeyCounts> GetPrivateKeyCountsAsync();

    /// <summary>
    /// Gets the public key of the profile signed in.
    /// </summary>
    /// <returns>The public key.</returns>
    /// <exception cref="InvalidOperationException">The profile is not signed in.</exception>
    PublicKey GetPublicKey();
}
