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

public sealed class TransientFileService(IFileService fileService, IFileWiper fileWiper) : ITransientFileService
{
    private readonly string _rootDirectory = CreateRootDirectory(fileService);

    public string CreateHandoffPath(string originalFileName)
        => CreatePath(Guid.NewGuid().ToString("N"), originalFileName);

    public string CreateIncomingPath(string originalFileName)
        => CreatePath(Path.Combine("incoming", Guid.NewGuid().ToString("N")), originalFileName);

    public string CreateEncryptedInputPath(string originalFileName)
        => CreatePath(Path.Combine("decrypt", Guid.NewGuid().ToString("N")), originalFileName);

    public string CreateEncryptedOutputPath(string originalFileName)
        => CreatePath(Path.Combine("encrypt", Guid.NewGuid().ToString("N")), originalFileName);

    private string CreatePath(string scope, string originalFileName)
    {
        string fileName = CreateFriendlyFileName(originalFileName);
        string sessionDirectory = Path.Combine(_rootDirectory, scope);
        Directory.CreateDirectory(sessionDirectory);

        return Path.Combine(sessionDirectory, fileName);
    }

    public void WipeTrackedFiles()
    {
        if (!Directory.Exists(_rootDirectory))
        {
            return;
        }

        string[] files = [.. Directory.EnumerateFiles(_rootDirectory, "*", SearchOption.AllDirectories)];
        string[] directories = [.. Directory.EnumerateDirectories(_rootDirectory, "*", SearchOption.AllDirectories).OrderByDescending(path => path.Length)];

        foreach (string path in files)
        {
            TryWipe(path);
        }

        foreach (string directory in directories)
        {
            try
            {
                if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                }
            }
            catch
            {
                // Best effort.
            }
        }

        try
        {
            if (Directory.Exists(_rootDirectory) && !Directory.EnumerateFileSystemEntries(_rootDirectory).Any())
            {
                Directory.Delete(_rootDirectory);
            }
        }
        catch
        {
            // Best effort.
        }
    }

    private void TryWipe(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }
        catch
        {
            // Best effort.
        }

        try
        {
            string directory = Path.GetDirectoryName(path)!;
            PickedWritableFile file = new(
                Path.GetFileName(path),
                action => action(),
                () => Task.FromResult(true),
                () => Task.FromResult(true),
                () => Task.FromResult(new FileInfo(path).Length),
                () => Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None)),
                name =>
                {
                    string renamedPath = Path.Combine(directory, name);
                    File.Move(path, renamedPath, true);
                    path = renamedPath;
                    return Task.FromResult(true);
                },
                () =>
                {
                    File.Delete(path);
                    return Task.CompletedTask;
                });
            fileWiper.WipeAsync(file).GetAwaiter().GetResult();
            PruneEmptyDirectories(directory);
        }
        catch
        {
            // Best effort.
        }
    }

    private void PruneEmptyDirectories(string directory)
    {
        string current = directory;
        while (!string.IsNullOrWhiteSpace(current) && current.StartsWith(_rootDirectory, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                if (!Directory.Exists(current) || Directory.EnumerateFileSystemEntries(current).Any())
                {
                    return;
                }

                Directory.Delete(current);
            }
            catch
            {
                return;
            }

            current = Path.GetDirectoryName(current) ?? string.Empty;
        }
    }

    private static string CreateRootDirectory(IFileService fileService)
    {
        string rootDirectory = Path.Combine(fileService.CacheDirectory, "XecretsHandoff");
        Directory.CreateDirectory(rootDirectory);

        return rootDirectory;
    }

    private static string CreateFriendlyFileName(string originalFileName)
    {
        string fileName = Path.GetFileName(string.IsNullOrWhiteSpace(originalFileName) ? "decrypted.bin" : originalFileName);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "decrypted.bin";
        }

        char[] invalidChars = Path.GetInvalidFileNameChars();
        Span<char> buffer = fileName.ToCharArray();
        for (int i = 0; i < buffer.Length; i++)
        {
            if (Array.IndexOf(invalidChars, buffer[i]) >= 0)
            {
                buffer[i] = '_';
            }
        }

        string sanitized = new(buffer);
        return string.IsNullOrWhiteSpace(sanitized) ? "decrypted.bin" : sanitized;
    }
}
