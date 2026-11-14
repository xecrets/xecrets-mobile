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
/// The preview of a decrypted file, which owns the temporary file of the preview, if any.
/// </summary>
public interface IPreviewState
{
    /// <summary>
    /// The text of the preview, decoded as UTF-8 unless a byte order mark tells another encoding, or "" if the preview
    /// holds no text.
    /// </summary>
    string Text { get; }

    /// <summary>
    /// The decrypted file of the preview, or <see cref="DecryptedFileInfo.Empty"/> if there is no preview. Its
    /// <see cref="DecryptedFileInfo.FilePath"/> is "" while the text is only held in memory.
    /// </summary>
    DecryptedFileInfo File { get; }

    /// <summary>
    /// What the decrypted file is encrypted with, or <see cref="EncryptedWithParameters.Empty"/> if there is no
    /// preview.
    /// </summary>
    EncryptedWithParameters EncryptedWith { get; }

    /// <summary>
    /// True if there is a preview, false otherwise.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Makes saved text the text of the preview, held in memory, and disposes the temporary file the earlier text was
    /// written to, if any.
    /// </summary>
    /// <param name="plaintext">The text, encoded as it is saved.</param>
    void UpdateText(byte[] plaintext);

    /// <summary>
    /// Marks the temporary file of the preview, if any, as still in use by another app after the preview is cleared.
    /// </summary>
    void MarkHandedOff();

    /// <summary>
    /// Ends the preview, and disposes the temporary file of the preview, if any.
    /// </summary>
    void Clear();
}
