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

using System.Collections.Concurrent;
using System.Threading.Channels;

using Xecrets.Mobile.Models.Abstractions;

namespace Xecrets.Mobile.Models.Services;

/// <summary>
/// Each temporary file is in a folder of its own, named by a GUID, inside a fixed folder for its kind. Sweeps wipe the
/// GUID folders as a whole. The fixed folders are only removed by a sweep of the whole cache, since a new file may be
/// created in one at any time. A handed-off file's folder gets the time it is disposed, which its age is measured from.
/// </summary>
public sealed class TransientFileService : ITransientFileService
{
    private readonly IFileService _fileService;

    private readonly IFileWiper _fileWiper;

    private readonly ICrashLogService _crashLogService;

    // Created only when needed, so that nothing is left in the cache before there are files in it.
    private readonly string _handoffDirectory;

    // The folders of the files in use, which sweeps leave. A folder is added before it is created, so that no sweep
    // finds it unregistered.
    private readonly ConcurrentDictionary<string, byte> _foldersInUse = new();

    private readonly Channel<SweepRequest> _requests =
        Channel.CreateUnbounded<SweepRequest>(new UnboundedChannelOptions { SingleReader = true });

    public TransientFileService(IFileService fileService, IFileWiper fileWiper, ICrashLogService crashLogService)
    {
        _fileService = fileService;
        _fileWiper = fileWiper;
        _crashLogService = crashLogService;
        _handoffDirectory = Path.Combine(fileService.CacheDirectory, "XecretsHandoff");

        // In the background, so that wiping large files does not hold up the user interface. The loop is async void, so
        // that an exception escaping it ends the app through the crash handlers, instead of faulting a task nobody
        // observes, which would stop all sweeps unnoticed and leave those waiting for one hanging.
        _ = Task.Run(SweepRequested);
    }

    public ITransientFile CreateHandoffFile(string originalFileName)
        => Create(_handoffDirectory, originalFileName);

    public ITransientFile CreateIncomingFile(string originalFileName)
        => Create(Path.Combine(_handoffDirectory, "incoming"), originalFileName);

    public ITransientFile CreateEncryptedInputFile(string originalFileName)
        => Create(Path.Combine(_handoffDirectory, "decrypt"), originalFileName);

    public ITransientFile CreateEncryptedOutputFile(string originalFileName)
        => Create(Path.Combine(_handoffDirectory, "encrypt"), originalFileName);

    public Task SweepAllAsync()
    {
        _foldersInUse.Clear();
        return RequestSweep(_fileService.CacheDirectory, TimeSpan.Zero);
    }

    public Task SweepAsync(TimeSpan maxAge) => RequestSweep(_handoffDirectory, maxAge);

    private ITransientFile Create(string kindDirectory, string originalFileName)
    {
        string directory = Path.Combine(kindDirectory, Guid.NewGuid().ToString("N"));
        _foldersInUse[directory] = 0;
        Directory.CreateDirectory(directory);

        return new TransientFile(this, Path.Combine(directory, CreateFriendlyFileName(originalFileName)));
    }

    private void Release(string directory, bool isHandedOff)
    {
        if (!isHandedOff)
        {
            if (_foldersInUse.TryRemove(directory, out _))
            {
                _ = RequestSweep(directory, TimeSpan.Zero);
            }

            return;
        }

        // A sweep of the whole cache, at sign-out, has ended the use of all files and wipes this folder too.
        if (!_foldersInUse.ContainsKey(directory))
        {
            return;
        }

        // Set while still registered, so that no sweep finds the folder unregistered with the time it was created.
        Directory.SetLastWriteTimeUtc(directory, DateTime.UtcNow);
        _foldersInUse.TryRemove(directory, out _);
    }

    private Task RequestSweep(string startDirectory, TimeSpan maxAge)
    {
        SweepRequest request = new(startDirectory, maxAge);
        _requests.Writer.TryWrite(request);
        return request.Completion.Task;
    }

    private async void SweepRequested()
    {
        await foreach (SweepRequest request in _requests.Reader.ReadAllAsync())
        {
            // The cache is only made when needed, and the OS, or the user, may clear it at any time.
            if (Directory.Exists(request.StartDirectory))
            {
                // Not even an empty cache directory is left behind when all of it is wiped.
                bool removesFolders = request.StartDirectory == _fileService.CacheDirectory;
                await SweepDirectoryAsync(request.StartDirectory, request.MaxAge, removesFolders);
            }

            request.Completion.SetResult();
        }
    }

    private async Task SweepDirectoryAsync(string directory, TimeSpan maxAge, bool removesFolders)
    {
        if (IsFileDirectory(directory))
        {
            if (!_foldersInUse.ContainsKey(directory) && IsOlderThan(Directory.GetLastWriteTimeUtc(directory), maxAge))
            {
                await WipeDirectoryAsync(directory);
            }

            return;
        }

        if (!TryList(directory, out string[] subdirectories, out string[] files))
        {
            return;
        }

        foreach (string subdirectory in subdirectories)
        {
            await SweepDirectoryAsync(subdirectory, maxAge, removesFolders);
        }

        foreach (string file in files)
        {
            if (IsOlderThan(File.GetLastWriteTimeUtc(file), maxAge))
            {
                await TryWipeAsync(file);
            }
        }

        if (removesFolders)
        {
            TryDeleteIfEmpty(directory);
        }
    }

    private static bool IsFileDirectory(string directory) =>
        Guid.TryParseExact(Path.GetFileName(directory), "N", out _);

    private static bool IsOlderThan(DateTime lastWriteTimeUtc, TimeSpan maxAge) =>
        DateTime.UtcNow - lastWriteTimeUtc >= maxAge;

    // Recursive post-order descent: fully wipe each subdirectory (and try to remove it) before touching
    // this directory's own files, then try to remove this directory once everything under it is gone.
    private async Task WipeDirectoryAsync(string directory)
    {
        if (!TryList(directory, out string[] subdirectories, out string[] files))
        {
            return;
        }

        foreach (string subdirectory in subdirectories)
        {
            await WipeDirectoryAsync(subdirectory);
        }

        foreach (string file in files)
        {
            await TryWipeAsync(file);
        }

        TryDeleteIfEmpty(directory);
    }

    private bool TryList(string directory, out string[] subdirectories, out string[] files)
    {
        try
        {
            subdirectories = Directory.GetDirectories(directory);
            files = Directory.GetFiles(directory);
            return true;
        }
        catch (Exception ex)
        {
            // The directory lives in the cache, which the OS (or the user, via "Clear cache") can reclaim
            // at any time - if it's already gone, there's nothing left to do here.
            _crashLogService.WriteSwallowedException(ex);
            subdirectories = [];
            files = [];
            return false;
        }
    }

    private async Task TryWipeAsync(string path)
    {
        try
        {
            File.SetAttributes(path, FileAttributes.Normal);

            // Should the wipe fail, the wiper still deletes the file if possible.
            await _fileWiper.WipeAsync(new LocalWritableFile(path), isOnDevice: true);
        }
        catch (Exception ex)
        {
            // Best effort.
            _crashLogService.WriteSwallowedException(ex);
        }
    }

    private void TryDeleteIfEmpty(string directory)
    {
        try
        {
            if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }
        catch (Exception ex)
        {
            // Best effort.
            _crashLogService.WriteSwallowedException(ex);
        }
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

    private sealed record SweepRequest(string StartDirectory, TimeSpan MaxAge)
    {
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class TransientFile(TransientFileService owner, string filePath) : ITransientFile
    {
        private bool _isHandedOff;

        public string FilePath => filePath;

        public void MarkHandedOff() => _isHandedOff = true;

        public void Dispose() => owner.Release(Path.GetDirectoryName(filePath)!, _isHandedOff);
    }
}
