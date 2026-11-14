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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using Xecrets.Common.Models;
using Xecrets.Mobile.Models;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Services;
using Xecrets.Mobile.Utilities;

namespace Xecrets.Mobile.Platforms.Windows;

/// <summary>
/// Files and folders are paths, which are their ids. The app is unpackaged, so it runs with the user's own token, and
/// a path is all that is needed to access a file again, without any grant.
/// </summary>
/// <remarks>
/// An unpackaged app (WindowsPackageType is None) has no package identity, which
/// <see cref="global::Windows.Storage.AccessCache.StorageApplicationPermissions"/> requires.
/// </remarks>
[SupportedOSPlatform("windows10.0.19041")]
internal sealed class WindowsFileAccess(WorkFolderStorage storage, ICrashLogService crashLogService) : IFileAccess
{
    /// <inheritdoc/>
    /// <remarks>
    /// The picker cannot be pointed at a folder, but remembers where it was last used for each initial folder.
    /// </remarks>
    public async Task<FileReference?> PickFileAsync(string initialFolder, FilePickerKind kind)
    {
        ThreadGuard.ThrowIfNotMainThread();

        FileOpenPicker picker = new()
        {
            SettingsIdentifier = CreateSettingsIdentifier(initialFolder),
        };
        picker.FileTypeFilter.Add(kind == FilePickerKind.Encrypted ? Texts.Extensions.EncryptedExtension : "*");
        InitializeWithWindow.Initialize(picker, GetWindowHandle());
        StorageFile? file = await picker.PickSingleFileAsync();
        return file is null ? null : WindowsFileLocation.Locate(file);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The folder picker shows no files, so the user picks a file in the folder instead.
    /// </remarks>
    public async Task<WorkFolderResult> PickFolderAsync(string initialFolder, FilePickerKind kind)
    {
        FileReference? file = await PickFileAsync(initialFolder, kind);
        if (file is null)
        {
            return WorkFolderResult.Canceled;
        }

        StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(file.Id)!);
        return await ToWorkFolderResultAsync(folder);
    }

    private async Task<WorkFolderResult> ToWorkFolderResultAsync(StorageFolder folder)
    {
        if (!await CanAccessFolderAsync(folder))
        {
            return WorkFolderResult.NoAccess;
        }

        FileReference located = WindowsFileLocation.Locate(folder);
        return WorkFolderResult.Valid(new WorkFolder(located.Id, located.Name, located.Id)
        {
            Root = located.Root,
            FolderPath = located.FolderPath,
        });
    }

    /// <inheritdoc/>
    public async Task<WorkFolder?> FindFolderAsync(string fileId) =>
        (await storage.LoadFoldersAsync())
            .Where(folder => fileId.StartsWith(
                folder.Id.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(folder => folder.Id.Length)
            .FirstOrDefault();

    /// <inheritdoc/>
    public async Task<WorkFolder?> FindFolderOfFolderAsync(string folderId) =>
        (await storage.LoadFoldersAsync())
            .Where(folder =>
                string.Equals(
                    folderId.TrimEnd(Path.DirectorySeparatorChar),
                    folder.Id.TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase) ||
                folderId.StartsWith(
                    folder.Id.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(folder => folder.Id.Length)
            .FirstOrDefault();

    /// <inheritdoc/>
    /// <remarks>
    /// A path is the same in a folder, so the file is returned as it is.
    /// </remarks>
    public Task<FileReference> GetFileInFolderAsync(WorkFolder folder, FileReference file) => Task.FromResult(file);

    /// <inheritdoc/>
    /// <remarks>
    /// The folders are read off the main thread. Names are compared as NTFS does by default, ignoring case.
    /// </remarks>
    public Task<IReadOnlyList<FolderListing>> ListFolderAsync(string folderId, FolderScope scope) =>
        Task.Run(() => LocalFolderLister.List(
            new DirectoryInfo(folderId),
            scope,
            info => info.FullName,
            WindowsFileLocation.Locate,
            StringComparer.OrdinalIgnoreCase));

    /// <inheritdoc/>
    public Task<string> GetParentFolderAsync(string fileId) => Task.FromResult(Path.GetDirectoryName(fileId) ?? string.Empty);

    /// <inheritdoc/>
    public IReadOnlyList<string> GetPathSegments(string id, string rememberedName)
    {
        string[] segments = id.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        return rememberedName.Length == 0 ||
            (segments.Length > 0 && string.Equals(segments[^1], rememberedName, StringComparison.OrdinalIgnoreCase))
            ? segments
            : [.. segments, rememberedName];
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The paths are compared ignoring case.
    /// </remarks>
    public Task<bool> IsSameFileAsync(string fileId, string otherFileId) =>
        Task.FromResult(string.Equals(fileId, otherFileId, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc/>
    /// <remarks>
    /// The path is all that is needed to access the file again, so the access is always kept.
    /// </remarks>
    public Task<bool> KeepFileGrantAsync(string fileId) => Task.FromResult(true);

    /// <inheritdoc/>
    /// <remarks>
    /// A path tells where the file is from the start, so the file is returned as it is.
    /// </remarks>
    public Task<FileReference> LocateAgainAsync(FileReference file) => Task.FromResult(file);

    /// <inheritdoc/>
    /// <remarks>
    /// The path is all that is needed to access a file, so there always is access.
    /// </remarks>
    public bool HasFileGrant(string fileId) => true;

    /// <inheritdoc/>
    /// <remarks>
    /// No grants are held, so there is nothing to release.
    /// </remarks>
    public Task ReleaseUnusedGrantsAsync(GrantReferences references) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task<Stream> OpenReadAsync(string fileId)
    {
        try
        {
            return Task.FromResult<Stream>(File.OpenRead(fileId));
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new FileNotAccessibleException(ex);
        }
    }

    /// <inheritdoc/>
    public Task<FileTimes> GetTimesAsync(string fileId) =>
        Task.FromResult(new FileTimes(
            File.GetCreationTimeUtc(fileId),
            File.GetLastAccessTimeUtc(fileId),
            File.GetLastWriteTimeUtc(fileId)));

    /// <inheritdoc/>
    public Task SetSupportedTimesAsync(string fileId, FileTimes times)
    {
        File.SetCreationTimeUtc(fileId, times.CreationTimeUtc);
        File.SetLastAccessTimeUtc(fileId, times.LastAccessTimeUtc);
        File.SetLastWriteTimeUtc(fileId, times.LastWriteTimeUtc);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<byte[]> ReadStartAsync(string fileId, int count)
    {
        await using Stream stream = await OpenReadAsync(fileId);
        byte[] buffer = new byte[count];
        return buffer[..await stream.ReadAtLeastAsync(buffer, count, throwOnEndOfStream: false)];
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The file is opened for reading and closed again.
    /// </remarks>
    public async Task CheckReachableAsync(string fileId)
    {
        await using (await OpenReadAsync(fileId))
        {
        }
    }

    /// <inheritdoc/>
    public Task<IWritableFile> OpenWriteAsync(string fileId) =>
        Task.FromResult<IWritableFile>(new LocalWritableFile(fileId));

    /// <inheritdoc/>
    /// <remarks>
    /// The folder of a file is told by its path, without looking for the file.
    /// </remarks>
    public Task<string> GetFolderOfFileAsync(string fileId) => GetParentFolderAsync(fileId);

    /// <inheritdoc/>
    public Task<string> SaveInFolderAsync(
        string folderId,
        string name,
        string completedFilePath,
        string? existingId) =>
        Task.Run(() =>
        {
            string destinationPath = Path.Combine(folderId, name);
            LocalFileSaver.Save(completedFilePath, destinationPath, overwrite: existingId is not null);
            return destinationPath;
        });

    /// <inheritdoc/>
    public Task<string> RenameAsync(string fileId, string newName)
    {
        string renamedPath = Path.Combine(Path.GetDirectoryName(fileId)!, newName);
        File.Move(fileId, renamedPath);
        return Task.FromResult(renamedPath);
    }

    /// <inheritdoc/>
    public async Task<FileReference?> SaveAsAsync(string initialFolder, string fileName, string completedFilePath)
    {
        ThreadGuard.ThrowIfNotMainThread();

        // The picker requires a file type, and "." stands for a name without an extension.
        string extension = Path.GetExtension(fileName);
        FileSavePicker picker = new()
        {
            SettingsIdentifier = CreateSettingsIdentifier(initialFolder),
            SuggestedFileName = Path.GetFileNameWithoutExtension(fileName),
        };
        picker.FileTypeChoices.Add(extension.Length > 0 ? extension : ".", [extension.Length > 0 ? extension : "."]);
        InitializeWithWindow.Initialize(picker, GetWindowHandle());
        StorageFile? file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return null;
        }

        try
        {
            await using FileStream input = File.OpenRead(completedFilePath);
            await using Stream output = await file.OpenStreamForWriteAsync();
            output.SetLength(0);
            await input.CopyToAsync(output);
        }
        catch
        {
            await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
            throw;
        }

        return WindowsFileLocation.Locate(file);
    }

    private static async Task ProbeAsync(StorageFolder folder)
    {
        string name = $".xecrets-probe-{Guid.NewGuid():N}";
        StorageFile file = await folder.CreateFileAsync(name, CreationCollisionOption.FailIfExists);
        try
        {
            byte[] expected = Encoding.UTF8.GetBytes(name);
            await FileIO.WriteBytesAsync(file, expected);
            byte[] actual = (await FileIO.ReadBufferAsync(file)).ToArray();
            if (!actual.AsSpan().SequenceEqual(expected))
            {
                throw new IOException("The work folder read probe returned different data.");
            }
        }
        finally
        {
            await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
        }

        if (await folder.TryGetItemAsync(name) is not null)
        {
            throw new IOException("The work folder deletion probe failed.");
        }
    }

    private async Task<bool> CanAccessFolderAsync(StorageFolder folder)
    {
        try
        {
            await ProbeAsync(folder);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException)
        {
            crashLogService.WriteSwallowedException(ex);
            return false;
        }
    }

    // The picker cannot be pointed at an arbitrary path, it only remembers where it was last used for a given settings
    // identifier, so the initial folder can be honored no further than that.
    private static string CreateSettingsIdentifier(string initialFolder)
    {
        if (initialFolder.Length == 0)
        {
            return string.Empty;
        }

        byte[] bytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(initialFolder));
        return Convert.ToHexString(bytes);
    }

    private static IntPtr GetWindowHandle()
    {
        Microsoft.UI.Xaml.Window window = (Microsoft.UI.Xaml.Window)
            Microsoft.Maui.Controls.Application.Current!.Windows[0].Handler!.PlatformView!;
        return WindowNative.GetWindowHandle(window);
    }
}
