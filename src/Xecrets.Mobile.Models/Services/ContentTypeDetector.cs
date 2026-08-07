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

using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Utilities;
using Xecrets.Texts;

namespace Xecrets.Mobile.Models.Services;

public static class ContentTypeDetector
{
    private static readonly Dictionary<string, string> _contentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".txt", "text/plain" },
        { ".text", "text/plain" },
        { ".asc", "text/plain" },
        { ".log", "text/plain" },
        { ".md", "text/markdown" },
        { ".markdown", "text/markdown" },
        { ".rst", "text/x-rst" },
        { ".adoc", "text/plain" },
        { ".org", "text/plain" },
        { ".csv", "text/csv" },
        { ".tsv", "text/tab-separated-values" },
        { ".json", "application/json" },
        { ".jsonc", "application/json" },
        { ".ndjson", "application/x-ndjson" },
        { ".xml", "application/xml" },
        { ".yaml", "application/yaml" },
        { ".yml", "application/yaml" },
        { ".toml", "application/toml" },
        { ".ini", "text/plain" },
        { ".config", "text/plain" },
        { ".conf", "text/plain" },
        { ".cnf", "text/plain" },
        { ".cfg", "text/plain" },
        { ".properties", "text/plain" },
        { ".editorconfig", "text/plain" },
        { ".env", "text/plain" },
        { ".html", "text/html" },
        { ".htm", "text/html" },
        { ".css", "text/css" },
        { ".scss", "text/plain" },
        { ".sass", "text/plain" },
        { ".less", "text/plain" },
        { ".js", "text/javascript" },
        { ".jsx", "text/javascript" },
        { ".mjs", "text/javascript" },
        { ".cjs", "text/javascript" },
        { ".ts", "text/plain" },
        { ".tsx", "text/plain" },
        { ".mts", "text/plain" },
        { ".cts", "text/plain" },
        { ".py", "text/x-python" },
        { ".pyw", "text/x-python" },
        { ".java", "text/x-java-source" },
        { ".cs", "text/plain" },
        { ".xaml", "application/xml" },
        { ".csproj", "application/xml" },
        { ".vbproj", "application/xml" },
        { ".fsproj", "application/xml" },
        { ".props", "application/xml" },
        { ".targets", "application/xml" },
        { ".sln", "text/plain" },
        { ".slnx", "application/xml" },
        { ".c", "text/x-c" },
        { ".h", "text/x-c" },
        { ".cpp", "text/x-c++" },
        { ".cc", "text/x-c++" },
        { ".cxx", "text/x-c++" },
        { ".hpp", "text/x-c++" },
        { ".hxx", "text/x-c++" },
        { ".hh", "text/x-c++" },
        { ".m", "text/plain" },
        { ".mm", "text/plain" },
        { ".go", "text/x-go" },
        { ".rs", "text/plain" },
        { ".swift", "text/plain" },
        { ".kt", "text/plain" },
        { ".kts", "text/plain" },
        { ".rb", "text/plain" },
        { ".php", "application/x-httpd-php" },
        { ".pl", "text/plain" },
        { ".pm", "text/plain" },
        { ".sh", "application/x-sh" },
        { ".bash", "application/x-sh" },
        { ".zsh", "application/x-sh" },
        { ".fish", "text/plain" },
        { ".ps1", "text/plain" },
        { ".psm1", "text/plain" },
        { ".psd1", "text/plain" },
        { ".bat", "text/plain" },
        { ".cmd", "text/plain" },
        { ".sql", "application/sql" },
        { ".r", "text/plain" },
        { ".lua", "text/plain" },
        { ".dart", "text/plain" },
        { ".scala", "text/plain" },
        { ".sc", "text/plain" },
        { ".fs", "text/plain" },
        { ".fsx", "text/plain" },
        { ".fsi", "text/plain" },
        { ".vb", "text/plain" },
        { ".f", "text/plain" },
        { ".for", "text/plain" },
        { ".f77", "text/plain" },
        { ".f90", "text/plain" },
        { ".f95", "text/plain" },
        { ".hs", "text/plain" },
        { ".lhs", "text/plain" },
        { ".erl", "text/plain" },
        { ".hrl", "text/plain" },
        { ".ex", "text/plain" },
        { ".exs", "text/plain" },
        { ".clj", "text/plain" },
        { ".cljs", "text/plain" },
        { ".cljc", "text/plain" },
        { ".groovy", "text/plain" },
        { ".gradle", "text/plain" },
        { ".jl", "text/plain" },
        { ".nim", "text/plain" },
        { ".zig", "text/plain" },
        { ".d", "text/plain" },
        { ".pas", "text/plain" },
        { ".pp", "text/plain" },
        { ".asm", "text/plain" },
        { ".s", "text/plain" },
        { ".vue", "text/plain" },
        { ".svelte", "text/plain" },
        { ".astro", "text/plain" },
        { ".graphql", "text/plain" },
        { ".gql", "text/plain" },
        { ".proto", "text/plain" },
        { ".thrift", "text/plain" },
        { ".tex", "application/x-tex" },
        { ".bib", "text/plain" },
        { ".diff", "text/x-diff" },
        { ".patch", "text/x-diff" },
        { ".reg", "text/plain" },
        { ".png", "image/png" },
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".gif", "image/gif" },
        { ".bmp", "image/bmp" },
        { ".webp", "image/webp" },
        { ".tif", "image/tiff" },
        { ".tiff", "image/tiff" },
        { ".svg", "image/svg+xml" },
        { ".pdf", "application/pdf" },
    };

    private static readonly HashSet<string> _textExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt",
        ".text",
        ".asc",
        ".log",
        ".md",
        ".markdown",
        ".rst",
        ".adoc",
        ".org",
        ".csv",
        ".tsv",
        ".json",
        ".jsonc",
        ".ndjson",
        ".xml",
        ".yaml",
        ".yml",
        ".toml",
        ".ini",
        ".config",
        ".conf",
        ".cnf",
        ".cfg",
        ".properties",
        ".editorconfig",
        ".env",
        ".html",
        ".htm",
        ".css",
        ".scss",
        ".sass",
        ".less",
        ".js",
        ".jsx",
        ".mjs",
        ".cjs",
        ".ts",
        ".tsx",
        ".mts",
        ".cts",
        ".py",
        ".pyw",
        ".java",
        ".cs",
        ".xaml",
        ".csproj",
        ".vbproj",
        ".fsproj",
        ".props",
        ".targets",
        ".sln",
        ".slnx",
        ".c",
        ".h",
        ".cpp",
        ".cc",
        ".cxx",
        ".hpp",
        ".hxx",
        ".hh",
        ".m",
        ".mm",
        ".go",
        ".rs",
        ".swift",
        ".kt",
        ".kts",
        ".rb",
        ".php",
        ".pl",
        ".pm",
        ".sh",
        ".bash",
        ".zsh",
        ".fish",
        ".ps1",
        ".psm1",
        ".psd1",
        ".bat",
        ".cmd",
        ".sql",
        ".r",
        ".lua",
        ".dart",
        ".scala",
        ".sc",
        ".fs",
        ".fsx",
        ".fsi",
        ".vb",
        ".f",
        ".for",
        ".f77",
        ".f90",
        ".f95",
        ".hs",
        ".lhs",
        ".erl",
        ".hrl",
        ".ex",
        ".exs",
        ".clj",
        ".cljs",
        ".cljc",
        ".groovy",
        ".gradle",
        ".jl",
        ".nim",
        ".zig",
        ".d",
        ".pas",
        ".pp",
        ".asm",
        ".s",
        ".vue",
        ".svelte",
        ".astro",
        ".graphql",
        ".gql",
        ".proto",
        ".thrift",
        ".tex",
        ".bib",
        ".diff",
        ".patch",
        ".reg",
    };

    private static readonly HashSet<string> _imageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".bmp",
        ".webp",
        ".tif",
        ".tiff",
        ".svg",
    };

    public static DecryptedFileInfo CreateInfo(string filePath, string displayName)
    {
        string fileName = string.IsNullOrWhiteSpace(displayName)
            ? Path.GetFileName(filePath)
            : displayName;
        string contentType = DetectContentType(fileName);
        PreviewKind kind = GetPreviewKind(fileName, contentType);
        if (kind == PreviewKind.External && LooksLikeTextFile(filePath))
        {
            kind = PreviewKind.Text;
            contentType = "text/plain";
        }

        long fileSize = new FileInfo(filePath).Length;

        return new DecryptedFileInfo(filePath, fileName, contentType, fileSize, kind);
    }

    public static string DetectContentType(string fileName)
    {
        string extension = Path.GetExtension(fileName);
        return _contentTypes.GetValueOrDefault(extension, "application/octet-stream");
    }

    private static PreviewKind GetPreviewKind(string fileName, string contentType)
    {
        string extension = Path.GetExtension(fileName);
        return _textExtensions.Contains(extension)
            ? PreviewKind.Text
            : contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
            ? PreviewKind.Text
            : _imageExtensions.Contains(extension)
            ? PreviewKind.Image
            : contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? PreviewKind.Image : PreviewKind.External;
    }

    public static bool IsEncryptedFile(string fileName, string contentType)
        => fileName.IsEncrypted() ||
           contentType.Equals(EncryptedFileType.ContentType, StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeTextFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        Span<byte> buffer = stackalloc byte[4096];
        using FileStream stream = File.OpenRead(filePath);
        int bytesRead = stream.Read(buffer);
        if (bytesRead == 0)
        {
            return true;
        }

        ReadOnlySpan<byte> bytes = buffer[..bytesRead];
        if (HasUtf16Bom(bytes))
        {
            return true;
        }

        if (HasUtf8Bom(bytes))
        {
            bytes = bytes[3..];
        }

        return !Encoding.UTF8.GetString(bytes).Contains('\uFFFD', StringComparison.Ordinal) && !ContainsBinaryControlBytes(bytes);
    }

    private static bool HasUtf8Bom(ReadOnlySpan<byte> bytes)
        => bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;

    private static bool HasUtf16Bom(ReadOnlySpan<byte> bytes)
        => bytes.Length >= 2 &&
           ((bytes[0] == 0xFF && bytes[1] == 0xFE) ||
            (bytes[0] == 0xFE && bytes[1] == 0xFF));

    private static bool ContainsBinaryControlBytes(ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes)
        {
            if (value == 0)
            {
                return true;
            }

            if (value is < 0x20 and not (0x09 or 0x0A or 0x0D or 0x0C))
            {
                return true;
            }
        }

        return false;
    }
}
