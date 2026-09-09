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
    private static readonly IReadOnlyCollection<string> _protectedCacheDirectoryNames = ["oat_primary", "XecretsCrashLogs"];

    private readonly SemaphoreSlim _gate = new(1, 1);

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

    public async Task RunExclusiveAsync(Func<Task> operation)
    {
        await _gate.WaitAsync();
        try
        {
            await operation();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task MaybeWipeTrackedFilesAsync()
    {
        // Wiping is opportunistic
        if (!await _gate.WaitAsync(TimeSpan.Zero))
        {
            return;
        }

        try
        {
            await WipeTrackedFilesCoreAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task WipeTrackedFilesCoreAsync()
        => await WipeDirectoryAsync(fileService.CacheDirectory, _protectedCacheDirectoryNames);

    // Recursive post-order descent: fully wipe each subdirectory (and try to remove it) before touching
    // this directory's own files, then try to remove this directory once everything under it is gone.
    private async Task WipeDirectoryAsync(string directory, IReadOnlyCollection<string> protectedDirectoryNames)
    {
        string[] subdirectories;
        string[] files;
        try
        {
            subdirectories = Directory.GetDirectories(directory);
            files = Directory.GetFiles(directory);
        }
        catch
        {
            // The directory lives in the cache, which the OS (or the user, via "Clear cache") can reclaim
            // at any time - if it's already gone, there's nothing left to do here.
            return;
        }

        foreach (string subdirectory in subdirectories)
        {
            if (protectedDirectoryNames.Contains(Path.GetFileName(subdirectory), StringComparer.Ordinal))
            {
                continue;
            }

            await WipeDirectoryAsync(subdirectory, []);
        }

        foreach (string file in files)
        {
            await TryWipeAsync(file);
        }

        if (directory != fileService.CacheDirectory)
        {
            TryDeleteIfEmpty(directory);
        }
    }

    private async Task TryWipeAsync(string path)
    {
        try
        {
            await using FileStream stream = new(path, FileMode.Open, FileAccess.Write, FileShare.None);
            await fileWiper.OverwriteAsync(stream, stream.Length);
        }
        catch
        {
            // Best effort - still try to remove the file below even if the overwriting itself failed.
        }

        SafeDelete(path);
    }

    private static void SafeDelete(string path)
    {
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
            string renamedPath = Path.Combine(Path.GetDirectoryName(path)!, Path.GetRandomFileName());
            File.Move(path, renamedPath, true);
            path = renamedPath;
        }
        catch
        {
            // Best effort - fall back to deleting under the original name.
        }

        try
        {
            File.Delete(path);
        }
        catch
        {
            // Best effort.
        }
    }

    private static void TryDeleteIfEmpty(string directory)
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
