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
using Xecrets.Common.Abstractions;
using Xecrets.Common.Models;

namespace Xecrets.Mobile.Models.Services;

/// <summary>
/// The local profile signed in, held in memory while signed in.
/// </summary>
public sealed class ProfileSession
{
    /// <summary>
    /// True if signed in, false otherwise.
    /// </summary>
    public bool IsAuthenticated => ProfileKeyPair is not null;

    /// <summary>
    /// The key pair of the profile, or null if not signed in.
    /// </summary>
    internal KeyPair? ProfileKeyPair { get; private set; }

    /// <summary>
    /// The password of the profile, or "" if not signed in.
    /// </summary>
    internal string Password { get; private set; } = string.Empty;

    /// <summary>
    /// The extra credentials of the profile, or null if not signed in.
    /// </summary>
    internal IPersistentData<ExtraCredentials>? ExtraCredentials { get; private set; }

    /// <summary>
    /// The other key pairs among the private keys that are decrypted, such as one kept from before a password reset.
    /// </summary>
    internal IReadOnlyList<KeyPair> PrivateKeyPairs { get; set; } = [];

    /// <summary>
    /// The data store of the profile, or null if not signed in.
    /// </summary>
    public IUserDataStore? UserStore { get; private set; }

    /// <summary>
    /// The email of the profile, or "" if not signed in.
    /// </summary>
    public string Email => ProfileKeyPair?.Email ?? string.Empty;

    /// <summary>
    /// Signs in, replacing what is held.
    /// </summary>
    /// <param name="keyPair">The key pair of the profile.</param>
    /// <param name="password">The password of the profile.</param>
    /// <param name="extraCredentials">The extra credentials of the profile.</param>
    /// <param name="userStore">The data store of the profile.</param>
    /// <param name="privateKeyPairs">The other key pairs among the private keys that are decrypted.</param>
    internal void SignIn(
        KeyPair keyPair,
        string password,
        IPersistentData<ExtraCredentials> extraCredentials,
        IUserDataStore userStore,
        IReadOnlyList<KeyPair> privateKeyPairs)
    {
        ProfileKeyPair = keyPair;
        Password = password;
        ExtraCredentials = extraCredentials;
        UserStore = userStore;
        PrivateKeyPairs = privateKeyPairs;
    }

    /// <summary>
    /// Signs out, clearing all that is held.
    /// </summary>
    public void SignOut()
    {
        ProfileKeyPair = null;
        Password = string.Empty;
        ExtraCredentials = null;
        PrivateKeyPairs = [];
        UserStore = null;
    }

    /// <summary>
    /// Creates the identity of the profile signed in.
    /// </summary>
    /// <returns>The password, with the key pair of the profile and the other key pairs that are decrypted.</returns>
    /// <exception cref="InvalidOperationException">Not signed in.</exception>
    internal Identity CreateIdentity()
    {
        if (ProfileKeyPair is null || Password.Length == 0)
        {
            throw new InvalidOperationException("No authenticated profile is available.");
        }

        // The other key pairs are only used for decryption, as files are encrypted only with the password and the
        // public key of the profile.
        return new Identity(Password, [ProfileKeyPair, .. PrivateKeyPairs]);
    }
}
