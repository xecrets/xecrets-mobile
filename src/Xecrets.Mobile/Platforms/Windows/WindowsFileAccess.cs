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

namespace Xecrets.Mobile.Platforms.Windows;

// This app is built and shipped unpackaged on Windows (WindowsPackageType is None), so it has no package
// identity. Windows.Storage.AccessCache.StorageApplicationPermissions (the "future access list" that the other
// platform services persist their access grants through) requires that identity to resolve. An unpackaged Win32
// process runs with the user's own token, though, so there is no grant to persist in the first place: a plain
// path is all that is needed to regain access on a later run.
[SupportedOSPlatform("windows10.0.19041")]
public sealed class WindowsFileAccess(WorkFolderStorage storage) : IFileAccess
{
    public async Task<FileReference?> PickFileAsync(string initialFolder, FilePickerKind kind)
    {
        FileOpenPicker picker = new()
        {
            SettingsIdentifier = CreateSettingsIdentifier(initialFolder),
        };
        picker.FileTypeFilter.Add(kind == FilePickerKind.Encrypted ? Texts.Extensions.EncryptedExtension : "*");
        InitializeWithWindow.Initialize(picker, GetWindowHandle());
        StorageFile? file = await picker.PickSingleFileAsync();
        return file is null ? null : new FileReference(file.Path, file.Name);
    }

    public async Task<WorkFolderResult> PickFolderAsync(string initialFolder)
    {
        FolderPicker picker = new()
        {
            SettingsIdentifier = CreateSettingsIdentifier(initialFolder),
        };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, GetWindowHandle());
        StorageFolder? folder = await picker.PickSingleFolderAsync();
        if (folder is null)
        {
            return WorkFolderResult.Canceled;
        }

        return await CanAccessFolderAsync(folder)
            ? WorkFolderResult.Valid(new WorkFolder(folder.Path, folder.DisplayName, folder.Path))
            : WorkFolderResult.NoAccess;
    }

    public async Task<WorkFolder?> FindFolderAsync(string fileId) =>
        (await storage.LoadFoldersAsync())
            .Where(folder => fileId.StartsWith(
                folder.Id.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(folder => folder.Id.Length)
            .FirstOrDefault();

    public FileReference GetFileInFolder(WorkFolder folder, FileReference file) => file;

    public string GetParentFolder(string fileId) => Path.GetDirectoryName(fileId) ?? string.Empty;

    public IReadOnlyList<string> GetPathSegments(string id, string? rememberedName = null)
    {
        string[] segments = id.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        return rememberedName is null ||
            (segments.Length > 0 && string.Equals(segments[^1], rememberedName, StringComparison.OrdinalIgnoreCase))
            ? segments
            : [.. segments, rememberedName];
    }

    public bool IsSameFile(string fileId, string otherFileId) =>
        string.Equals(fileId, otherFileId, StringComparison.OrdinalIgnoreCase);

    // The path is all that is needed to access the file again.
    public Task<bool> KeepFileGrantAsync(string fileId) => Task.FromResult(true);

    public bool HasFileGrant(string fileId) => true;

    public Task ReleaseUnusedGrantsAsync(GrantReferences references) => Task.CompletedTask;

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

    public Task<IWritableFile> OpenWritableAsync(string fileId) =>
        Task.FromResult<IWritableFile>(new LocalWritableFile(fileId));

    public Task<string> GetFolderOfFileAsync(string fileId) => Task.FromResult(GetParentFolder(fileId));

    public Task<bool> FileExistsInFolderAsync(string folderId, string name) =>
        Task.FromResult(File.Exists(Path.Combine(folderId, name)));

    public Task<string> WriteFileInFolderAsync(
        string folderPath,
        string name,
        bool overwrite,
        Func<Stream, Task> writer) =>
        folderPath.WriteFileInFolderAsync(name, overwrite, writer);

    public async Task<FileReference?> SaveAsAsync(string fileName, Stream content)
    {
        // The picker requires a file type, and "." stands for a name without an extension.
        string extension = Path.GetExtension(fileName);
        FileSavePicker picker = new()
        {
            SuggestedFileName = Path.GetFileNameWithoutExtension(fileName),
        };
        picker.FileTypeChoices.Add(extension.Length > 0 ? extension : ".", [extension.Length > 0 ? extension : "."]);
        InitializeWithWindow.Initialize(picker, GetWindowHandle());
        StorageFile? file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return null;
        }

        await using (Stream output = await file.OpenStreamForWriteAsync())
        {
            output.SetLength(0);
            await content.CopyToAsync(output);
        }

        return new FileReference(file.Path, file.Name);
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

    private static async Task<bool> CanAccessFolderAsync(StorageFolder folder)
    {
        try
        {
            await ProbeAsync(folder);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException)
        {
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
