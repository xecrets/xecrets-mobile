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
/// An encrypted copy of a file.
/// </summary>
/// <param name="File">The temporary encrypted file, which the holder of the result owns and must dispose.</param>
/// <param name="DisplayName">The name of the encrypted file.</param>
/// <param name="ContentType">The MIME type of the encrypted file.</param>
/// <param name="FileSize">The size of the encrypted file in bytes.</param>
/// <param name="EncryptCopyForSharing">True if the file is encrypted with a password chosen for sharing it, false if
/// it is encrypted with the password of the current profile.</param>
public sealed record EncryptionPreparationResult(
    ITransientFile File,
    string DisplayName,
    string ContentType,
    long FileSize,
    bool EncryptCopyForSharing)
{
    /// <summary>
    /// No encrypted copy. Its file has the path "", and disposing it does nothing.
    /// </summary>
    public static EncryptionPreparationResult Empty { get; } = new(
        new NoFile(),
        string.Empty,
        string.Empty,
        0,
        false);

    /// <summary>
    /// The full path of <see cref="File"/>.
    /// </summary>
    public string FilePath => File.FilePath;

    private sealed class NoFile : ITransientFile
    {
        public string FilePath => "";

        public void MarkHandedOff()
        {
        }

        public void Dispose()
        {
        }
    }
}
