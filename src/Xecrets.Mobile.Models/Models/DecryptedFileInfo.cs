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

namespace Xecrets.Mobile.Models.Models;

/// <summary>
/// A decrypted file.
/// </summary>
/// <param name="FilePath">The full path of the decrypted file, or "" if it is only held in memory.</param>
/// <param name="OriginalFileName">The name of the file before it was encrypted.</param>
/// <param name="ContentType">The MIME type of the file.</param>
/// <param name="FileSize">The size of the decrypted file in bytes.</param>
/// <param name="Kind">How the file is previewed.</param>
/// <param name="OriginalTimes">The times of the file before it was encrypted.</param>
public sealed record DecryptedFileInfo(
    string FilePath,
    string OriginalFileName,
    string ContentType,
    long FileSize,
    PreviewKind Kind,
    FileTimes OriginalTimes)
{
    /// <summary>
    /// No decrypted file, with the kind <see cref="PreviewKind.Unknown"/>.
    /// </summary>
    public static DecryptedFileInfo Empty { get; } = new(
        string.Empty,
        string.Empty,
        string.Empty,
        0,
        PreviewKind.Unknown,
        new FileTimes(default, default, default));
}
