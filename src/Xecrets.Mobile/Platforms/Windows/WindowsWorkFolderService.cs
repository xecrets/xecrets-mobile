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

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Services;
using Xecrets.Texts;

namespace Xecrets.Mobile.Platforms.Windows;

// This app is built and shipped unpackaged on Windows (WindowsPackageType is None), so it has no package
// identity. Windows.Storage.AccessCache.StorageApplicationPermissions (the "future access list" that the other
// platform services persist their access grants through) requires that identity to resolve. An unpackaged Win32
// process runs with the user's own token, though, so there is no grant to persist in the first place: a plain
// path is all that is needed to regain access on a later run, which is what WorkFolderStorage keeps in preferences.
[SupportedOSPlatform("windows10.0.19041")]
public sealed class WindowsWorkFolderService(WorkFolderStorage storage) : IWorkFolderService
{
    public IReadOnlyList<string> GetPathSegments(WorkFolder folder) => WorkFolderStorage.BuildPathSegments(
        folder.Id,
        folder,
        Path.DirectorySeparatorChar,
        Path.AltDirectorySeparatorChar);

    public async Task<IReadOnlyList<WorkFolder>> GetFoldersAsync() => await storage.LoadFoldersAsync();

    public async Task<WorkFolderResult> AddFolderAsync(string? initialLocationId = null)
    {
        // The picker cannot be pointed at an arbitrary path, it only remembers where it was last used for a
        // given settings identifier, so the initial location can be honored no further than that.
        FolderPicker picker = new()
        {
            SettingsIdentifier = initialLocationId is null
                ? string.Empty
                : CreateSettingsIdentifier(initialLocationId),
        };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, GetWindowHandle());
        StorageFolder? folder = await picker.PickSingleFolderAsync();
        if (folder is null)
        {
            return WorkFolderResult.Canceled;
        }

        if (!await CanAccessFolderAsync(folder))
        {
            return WorkFolderResult.NoAccess;
        }

        WorkFolder newFolder = new(folder.Path, folder.DisplayName, folder.Path);
        await storage.SaveFolderAsync(newFolder);
        return WorkFolderResult.Valid((await GetFoldersAsync()).First(item => item.Id == folder.Path));
    }

    public async Task<WorkFolder> AddDiscoveredFolderAsync(WorkFolderFile file)
    {
        WorkFolder newFolder = new(file.LocationId, file.LocationDisplayName, file.LocationId);
        await storage.SaveFolderAsync(newFolder);
        return newFolder;
    }

    public async Task RemoveFolderAsync(WorkFolder folder) =>
        await storage.SaveFoldersAsync((await storage.LoadFoldersAsync()).Where(item => item.Id != folder.Id));

    public Task RenameFolderAsync(WorkFolder folder, string displayName) =>
        storage.RenameFolderAsync(folder, displayName);

    public Task SaveFoldersAsync(IReadOnlyList<WorkFolder> folders) => storage.SaveFoldersAsync(folders);

    public async Task<WorkFolderFile?> PickFileAsync(WorkFolder folder, FilePickerKind pickerKind)
    {
        FileOpenPicker picker = new()
        {
            SettingsIdentifier = CreateSettingsIdentifier(folder.Id),
        };
        picker.FileTypeFilter.Add(pickerKind == FilePickerKind.Encrypted ? Extensions.EncryptedExtension : "*");
        InitializeWithWindow.Initialize(picker, GetWindowHandle());
        StorageFile? file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return null;
        }

        StorageFolder location = await file.GetParentAsync();
        WorkFolder? accessFolder = (await GetFoldersAsync())
            .Where(item => IsDescendant(item.Id, file.Path))
            .OrderByDescending(item => item.Id.Length)
            .FirstOrDefault();

        return new WorkFolderFile(
            file.Name,
            location.Path,
            location.DisplayName,
            accessFolder?.GrantId ?? string.Empty,
            accessFolder is not null,
            async () => await file.OpenStreamForReadAsync(),
            async name => await location.TryGetItemAsync(name) is not null,
            (name, overwrite, writer) => WriteFileAsync(location, name, overwrite, writer),
            async () => await file.DeleteAsync(StorageDeleteOption.PermanentDelete));
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

    private static async Task WriteFileAsync(
        StorageFolder folder,
        string name,
        bool overwrite,
        Func<Stream, Task> writer)
    {
        StorageFile temporary = await folder.CreateFileAsync(
            $".xecrets-{Guid.NewGuid():N}.tmp",
            CreationCollisionOption.FailIfExists);
        string temporaryName = temporary.Name;
        bool destinationCommitted = false;
        try
        {
            await using (Stream output = await temporary.OpenStreamForWriteAsync())
            {
                await writer(output);
            }

            if (overwrite)
            {
                StorageFile existing = (StorageFile)await folder.GetItemAsync(name);
                await temporary.MoveAndReplaceAsync(existing);
                destinationCommitted = true;
            }
            else
            {
                await temporary.RenameAsync(name, NameCollisionOption.FailIfExists);
                destinationCommitted = true;
            }
        }
        finally
        {
            if (!destinationCommitted && await folder.TryGetItemAsync(temporaryName) is not null)
            {
                await temporary.DeleteAsync(StorageDeleteOption.PermanentDelete);
            }
        }
    }

    private static bool IsDescendant(string folderPath, string filePath) =>
        filePath.StartsWith(folderPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);

    private static string CreateSettingsIdentifier(string id)
    {
        byte[] bytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(id));
        return Convert.ToHexString(bytes);
    }

    private static IntPtr GetWindowHandle()
    {
        Microsoft.UI.Xaml.Window window = (Microsoft.UI.Xaml.Window)
            Microsoft.Maui.Controls.Application.Current!.Windows[0].Handler!.PlatformView!;
        return WindowNative.GetWindowHandle(window);
    }
}
