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

using System.Text;

using Xecrets.Core.Models;

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Models.Services;

/// <summary>
/// The preview of a decrypted file, held in memory.
/// </summary>
internal sealed class PreviewState : IPreviewState
{
    // The text as it was decrypted, in any encoding, or as it was last saved, and otherwise empty.
    private byte[] _plaintext = [];

    // The file shown, if any, which is disposed when it is replaced or the preview is cleared.
    private ITransientFile? _decryptedFile;

    /// <inheritdoc/>
    public string Text
    {
        get
        {
            using StreamReader reader = new(new MemoryStream(_plaintext), new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }
    }

    /// <summary>
    /// The text of the preview as it was decrypted or last saved, or empty if the preview holds no text.
    /// </summary>
    public ReadOnlyMemory<byte> Plaintext => _plaintext;

    /// <inheritdoc/>
    public DecryptedFileInfo File { get; private set; } = DecryptedFileInfo.Empty;

    /// <inheritdoc/>
    public EncryptedWithParameters EncryptedWith { get; private set; } = EncryptedWithParameters.Empty;

    /// <inheritdoc/>
    public bool IsReady => File.Kind != PreviewKind.Unknown;

    /// <summary>
    /// Makes text held in memory the preview, and disposes the temporary file of the earlier preview, if any.
    /// </summary>
    /// <param name="file">The decrypted file of the preview.</param>
    /// <param name="plaintext">The decrypted text.</param>
    /// <param name="encryptedWith">What the file is encrypted with.</param>
    public void SetText(DecryptedFileInfo file, byte[] plaintext, EncryptedWithParameters encryptedWith)
    {
        ReleaseDecryptedFile();
        File = file;
        EncryptedWith = encryptedWith;
        _plaintext = plaintext;
    }

    /// <summary>
    /// Makes a decrypted temporary file the preview, without text, and disposes the temporary file of the earlier
    /// preview, if any.
    /// </summary>
    /// <param name="file">The decrypted file of the preview.</param>
    /// <param name="encryptedWith">What the file is encrypted with.</param>
    /// <param name="decryptedFile">The temporary file, which the preview takes over.</param>
    public void SetFile(DecryptedFileInfo file, EncryptedWithParameters encryptedWith, ITransientFile decryptedFile)
    {
        SetText(file, [], encryptedWith);
        _decryptedFile = decryptedFile;
    }

    /// <summary>
    /// Sets the temporary file that the text of the preview is written to.
    /// </summary>
    /// <param name="decryptedFile">The temporary file, which the preview takes over.</param>
    public void SetDecryptedFile(ITransientFile decryptedFile)
    {
        _decryptedFile = decryptedFile;
        File = File with { FilePath = decryptedFile.FilePath };
    }

    /// <inheritdoc/>
    public void UpdateText(byte[] plaintext)
    {
        ReleaseDecryptedFile();
        _plaintext = plaintext;
        File = File with { FilePath = string.Empty, FileSize = _plaintext.Length };
    }

    /// <inheritdoc/>
    public void MarkHandedOff()
    {
        _decryptedFile?.MarkHandedOff();
    }

    /// <inheritdoc/>
    public void Clear()
    {
        ReleaseDecryptedFile();
        _plaintext = [];
        File = DecryptedFileInfo.Empty;
        EncryptedWith = EncryptedWithParameters.Empty;
    }

    private void ReleaseDecryptedFile()
    {
        _decryptedFile?.Dispose();
        _decryptedFile = null;
    }
}
