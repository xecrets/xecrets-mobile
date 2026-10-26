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

using Xecrets.Core.Public;
using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Models.Services;

internal static class PreviewInfoFactory
{
    // The largest text file, in bytes, that the app's internal viewer/editor will handle. 
    public const long MaxInlineTextSizeBytes = 100 * 1024;

    private const int TextSampleSizeBytes = 4096;

    public static DecryptedFileInfo CreateInfo(string filePath, string displayName)
    {
        string fileName = string.IsNullOrWhiteSpace(displayName)
            ? Path.GetFileName(filePath)
            : displayName;
        string contentType = FileTypeClassifier.DetectContentType(fileName);
        PreviewKind kind = GetPreviewKind(fileName, contentType);

        if (kind == PreviewKind.External && contentType == FileTypeClassifier.UnknownContentType && LooksLikeTextFile(filePath))
        {
            kind = PreviewKind.Text;
            contentType = "text/plain";
        }

        long fileSize = new FileInfo(filePath).Length;
        if (kind == PreviewKind.Text && fileSize > MaxInlineTextSizeBytes)
        {
            kind = PreviewKind.External;
        }

        return new DecryptedFileInfo(filePath, fileName, contentType, fileSize, kind);
    }

    private static PreviewKind GetPreviewKind(string fileName, string contentType)
    {
        return FileTypeClassifier.IsTextExtension(fileName)
            ? PreviewKind.Text
            : contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
            ? PreviewKind.Text
            : FileTypeClassifier.IsImageExtension(fileName)
            ? PreviewKind.Image
            : contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? PreviewKind.Image : PreviewKind.External;
    }

    private static bool LooksLikeTextFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        Span<byte> buffer = stackalloc byte[TextSampleSizeBytes];
        using FileStream stream = File.OpenRead(filePath);
        int bytesRead = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);

        return FileTypeClassifier.LooksLikeText(buffer[..bytesRead]);
    }
}
