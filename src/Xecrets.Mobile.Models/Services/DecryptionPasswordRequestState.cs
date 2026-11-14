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

using Xecrets.Mobile.Models.Abstractions;

namespace Xecrets.Mobile.Models.Services;

/// <summary>
/// The encrypted file to preview, while the user is asked for its password. <see cref="Clear"/> disposes the file.
/// </summary>
public sealed class DecryptionPasswordRequestState
{
    private ITransientFile? _encryptedFile;

    /// <summary>
    /// The full path of the encrypted file, or "" if there is none.
    /// </summary>
    public string EncryptedPath => _encryptedFile?.FilePath ?? "";

    /// <summary>
    /// True if there is an encrypted file waiting for its password, false otherwise.
    /// </summary>
    public bool HasPendingRequest => _encryptedFile is not null;

    /// <summary>
    /// Makes a temporary encrypted file the one waiting for its password.
    /// </summary>
    /// <param name="encryptedFile">The temporary encrypted file, which the state takes over.</param>
    /// <remarks>Requires that no file is waiting, since that file would not be disposed.</remarks>
    public void Set(ITransientFile encryptedFile)
    {
        _encryptedFile = encryptedFile;
    }

    /// <summary>
    /// Ends the wait for a password, and disposes the encrypted file, if any.
    /// </summary>
    public void Clear()
    {
        ITransientFile? encryptedFile = _encryptedFile;
        _encryptedFile = null;
        encryptedFile?.Dispose();
    }
}
