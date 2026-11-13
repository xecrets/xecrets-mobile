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

internal sealed class PreviewState : IPreviewState
{
    // The text as it was decrypted, in any encoding, or as it was last saved, and otherwise empty.
    private byte[] _plaintext = [];

    // The file shown, if any, which is disposed when it is replaced or the preview is cleared.
    private ITransientFile? _decryptedFile;

    public string Text
    {
        get
        {
            using StreamReader reader = new(new MemoryStream(_plaintext), new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }
    }

    public ReadOnlyMemory<byte> Plaintext => _plaintext;

    public DecryptedFileInfo File { get; private set; } = DecryptedFileInfo.Empty;

    public EncryptedWithParameters EncryptedWith { get; private set; } = EncryptedWithParameters.Empty;

    public bool IsReady => File.Kind != PreviewKind.Unknown;

    public void SetText(DecryptedFileInfo file, byte[] plaintext, EncryptedWithParameters encryptedWith)
    {
        ReleaseDecryptedFile();
        File = file;
        EncryptedWith = encryptedWith;
        _plaintext = plaintext;
    }

    /// <summary>
    /// A file that is not shown as text, so no text is kept.
    /// </summary>
    public void SetFile(DecryptedFileInfo file, EncryptedWithParameters encryptedWith, ITransientFile decryptedFile)
    {
        SetText(file, [], encryptedWith);
        _decryptedFile = decryptedFile;
    }

    /// <summary>
    /// The file the text was written to, to hand it over to the platform or another app.
    /// </summary>
    public void SetDecryptedFile(ITransientFile decryptedFile)
    {
        _decryptedFile = decryptedFile;
        File = File with { FilePath = decryptedFile.FilePath };
    }

    public void UpdateText(byte[] plaintext)
    {
        ReleaseDecryptedFile();
        _plaintext = plaintext;
        File = File with { FilePath = string.Empty, FileSize = _plaintext.Length };
    }

    public void MarkHandedOff()
    {
        _decryptedFile?.MarkHandedOff();
    }

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
