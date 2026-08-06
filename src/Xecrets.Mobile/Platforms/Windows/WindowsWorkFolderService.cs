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
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Maui;
using Microsoft.Maui.Storage;

using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;

using WinRT.Interop;

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Services;
using Xecrets.Texts;

namespace Xecrets.Mobile.Platforms.Windows;

[SupportedOSPlatform("windows10.0.19041")]
public sealed class WindowsWorkFolderService : IWorkFolderService
{
    private const string _folderOrderPreferenceKey = "work-folder-order";
    private const string _metadataPrefix = "XecretsWorkFolder|";
    private readonly Dictionary<string, StorageFolder> _discoveredLocations = [];

    public IReadOnlyList<string> GetPathSegments(WorkFolder folder) => WorkFolderStorage.BuildPathSegments(
        folder.Id,
        folder,
        Path.DirectorySeparatorChar,
        Path.AltDirectorySeparatorChar);

    public async Task<IReadOnlyList<WorkFolder>> GetFoldersAsync()
    {
        List<WorkFolder> folders = [];
        foreach (AccessListEntry entry in StorageApplicationPermissions.FutureAccessList.Entries)
        {
            if (!entry.Metadata.StartsWith(_metadataPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            StorageFolder folder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(entry.Token);
            folders.Add(new WorkFolder(folder.Path, entry.Metadata[_metadataPrefix.Length..], entry.Token));
        }

        Dictionary<string, int> order = Preferences.Default
            .Get(_folderOrderPreferenceKey, string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select((token, index) => (Token: Uri.UnescapeDataString(token), Index: index))
            .ToDictionary(item => item.Token, item => item.Index);
        return folders
            .OrderBy(folder => order.TryGetValue(folder.GrantId, out int index) ? index : int.MaxValue)
            .ToList();
    }

    public async Task<WorkFolder?> AddFolderAsync(string? initialLocationId = null)
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
            return null;
        }

        await ProbeAsync(folder);
        WorkFolder? existing = (await GetFoldersAsync()).FirstOrDefault(item => item.Id == folder.Path);
        if (existing is not null)
        {
            return existing;
        }

        string token = StorageApplicationPermissions.FutureAccessList.Add(
            folder,
            _metadataPrefix + folder.DisplayName);
        return new WorkFolder(folder.Path, folder.DisplayName, token);
    }

    public Task<WorkFolder> AddDiscoveredFolderAsync(WorkFolderFile file)
    {
        StorageFolder folder = _discoveredLocations[file.LocationId];
        string token = StorageApplicationPermissions.FutureAccessList.Add(
            folder,
            _metadataPrefix + folder.DisplayName);
        return Task.FromResult(new WorkFolder(folder.Path, folder.DisplayName, token));
    }

    public Task RemoveFolderAsync(WorkFolder folder)
    {
        StorageApplicationPermissions.FutureAccessList.Remove(folder.GrantId);
        _discoveredLocations.Remove(folder.Id);
        return Task.CompletedTask;
    }

    public async Task RenameFolderAsync(WorkFolder folder, string displayName)
    {
        StorageFolder storageFolder = await StorageApplicationPermissions.FutureAccessList
            .GetFolderAsync(folder.GrantId);
        StorageApplicationPermissions.FutureAccessList.AddOrReplace(
            folder.GrantId,
            storageFolder,
            _metadataPrefix + displayName);
    }

    public Task SaveFolderOrderAsync(IReadOnlyList<WorkFolder> folders)
    {
        Preferences.Default.Set(
            _folderOrderPreferenceKey,
            string.Join('\n', folders.Select(folder => Uri.EscapeDataString(folder.GrantId))));
        return Task.CompletedTask;
    }

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
        _discoveredLocations[location.Path] = location;
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
                StorageFile existing = (StorageFile)(await folder.GetItemAsync(name));
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
