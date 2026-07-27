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
using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Models.Services;

public sealed class PreviewState : IPreviewState
{
    public PreviewKind Kind { get; private set; } = PreviewKind.Unknown;

    public string OriginalFileName { get; private set; } = string.Empty;

    public string SourcePath { get; private set; } = string.Empty;

    public string DecryptedPath { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long FileSize { get; private set; }

    public string Text { get; private set; } = string.Empty;

    public DecryptedFileInfo? File { get; private set; }

    public bool IsTextEditingEnabled { get; private set; }

    public bool IsReady => Kind != PreviewKind.Unknown && !string.IsNullOrWhiteSpace(DecryptedPath);

    public void SetImage(DecryptedFileInfo file, string sourcePath)
    {
        Kind = PreviewKind.Image;
        SetFile(file, sourcePath);
        Text = string.Empty;
    }

    public void SetText(DecryptedFileInfo file, string sourcePath, string text, bool isTextEditingEnabled)
    {
        Kind = PreviewKind.Text;
        SetFile(file, sourcePath);
        Text = text;
        IsTextEditingEnabled = isTextEditingEnabled;
    }

    public void SetExternal(DecryptedFileInfo file, string sourcePath)
    {
        Kind = PreviewKind.External;
        SetFile(file, sourcePath);
        Text = string.Empty;
    }

    public void UpdateText(string text)
    {
        Text = text;
    }

    public void EnableTextEditing()
    {
        IsTextEditingEnabled = true;
    }

    public void UpdateFileSize(long fileSize)
    {
        FileSize = fileSize;
        if (File is not null)
        {
            File = File with { FileSize = fileSize };
        }
    }

    public void UpdateSourcePath(string sourcePath)
    {
        SourcePath = sourcePath;
    }

    public void Clear()
    {
        Kind = PreviewKind.Unknown;
        OriginalFileName = string.Empty;
        SourcePath = string.Empty;
        DecryptedPath = string.Empty;
        ContentType = string.Empty;
        FileSize = 0;
        Text = string.Empty;
        File = null;
        IsTextEditingEnabled = false;
    }

    private void SetFile(DecryptedFileInfo file, string sourcePath)
    {
        File = file;
        IsTextEditingEnabled = false;
        SourcePath = sourcePath;
        OriginalFileName = file.DisplayName;
        DecryptedPath = file.FilePath;
        ContentType = file.ContentType;
        FileSize = file.FileSize;
    }
}
