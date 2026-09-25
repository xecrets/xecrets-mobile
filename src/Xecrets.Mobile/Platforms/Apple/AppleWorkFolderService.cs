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
using System.Text;
using System.Threading.Tasks;

using Foundation;

using UniformTypeIdentifiers;

using Xecrets.Common.Models;

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Utilities;
using Xecrets.Mobile.Services;

namespace Xecrets.Mobile.Platforms.Apple;

public sealed class AppleWorkFolderService(
    WorkFolderStorage storage,
    IPickedWritableFileFactory pickedWritableFileFactory) : IWorkFolderService
{
    private readonly Dictionary<string, (NSUrl Location, NSUrl AccessRoot)> _discoveredLocations = [];

    public async Task<IReadOnlyList<WorkFolder>> GetFoldersAsync() => await storage.LoadFoldersAsync();

    public IReadOnlyList<string> GetPathSegments(WorkFolder folder) =>
        WorkFolderStorage.BuildPathSegments(NSUrl.FromString(folder.Id)?.Path ?? folder.Id, folder, '/');

    public async Task<WorkFolderResult> AddFolderAsync(string? initialLocationId = null)
    {
        NSUrl? initialUrl = initialLocationId is null ? null : NSUrl.FromString(initialLocationId);
        NSUrl? url = await UTTypes.Folder.PickUrlAsync(initialUrl);
        if (url is null)
        {
            return WorkFolderResult.Canceled;
        }

        bool isAccessing = url.StartAccessingSecurityScopedResource();
        string displayName;
        try
        {
            if (!url.TryGetResource(NSUrl.IsDirectoryKey, out NSObject value, out NSError _))
            {
                return WorkFolderResult.NoAccess;
            }

            if (!((NSNumber)value).BoolValue)
            {
                return WorkFolderResult.NotFolder;
            }

            if (!await CanAccessFolderAsync(url.Path!))
            {
                return WorkFolderResult.NoAccess;
            }

            displayName = GetDisplayName(url);
            url.SaveGrant();
        }
        finally
        {
            if (isAccessing)
            {
                url.StopAccessingSecurityScopedResource();
            }
        }

        WorkFolder folder = new(url.AbsoluteString!, displayName, url.AbsoluteString!);
        await storage.SaveFolderAsync(folder);
        return WorkFolderResult.Valid(folder);
    }

    public async Task<WorkFolder> AddDiscoveredFolderAsync(WorkFolderFile file)
    {
        (NSUrl url, NSUrl accessRoot) = _discoveredLocations[file.LocationId];
        bool isAccessing = accessRoot.StartAccessingSecurityScopedResource();
        try
        {
            url.SaveGrant();
        }
        finally
        {
            if (isAccessing)
            {
                accessRoot.StopAccessingSecurityScopedResource();
            }
        }
        WorkFolder folder = new(file.LocationId, file.LocationDisplayName, url.AbsoluteString!);
        await storage.SaveFolderAsync(folder);
        return folder;
    }

    public async Task RemoveFolderAsync(WorkFolder folder)
    {
        string grantPath = AppleExtensions.GetGrantPath(folder.GrantId);
        if (File.Exists(grantPath))
        {
            File.Delete(grantPath);
        }

        _discoveredLocations.Remove(folder.Id);
        await storage.SaveFoldersAsync((await storage.LoadFoldersAsync()).Where(item => item.Id != folder.Id));
    }

    public Task RenameFolderAsync(WorkFolder folder, string displayName) =>
        storage.RenameFolderAsync(folder, displayName);

    public Task SaveFoldersAsync(IReadOnlyList<WorkFolder> folders) => storage.SaveFoldersAsync(folders);

    public async Task<WorkFolderFile?> PickFileAsync(WorkFolder? folder, FilePickerKind pickerKind)
    {
        NSUrl? folderUrl = folder is null ? null : AppleExtensions.ResolveGrant(folder.GrantId);
        bool isAccessing = folderUrl?.StartAccessingSecurityScopedResource() == true;
        NSUrl? fileUrl;
        try
        {
            UTType contentType = pickerKind == FilePickerKind.Encrypted
                ? UTType.CreateExportedType(EncryptedFileType.UniformTypeIdentifier)
                : UTTypes.Data;
            fileUrl = await contentType.PickUrlAsync(folderUrl);
        }
        finally
        {
            if (isAccessing)
            {
                folderUrl!.StopAccessingSecurityScopedResource();
            }
        }

        if (fileUrl is null)
        {
            return null;
        }

        return CreateFile(fileUrl, await storage.FindKnownGrantAsync(fileUrl));
    }

    public async Task<WorkFolderFileResult> OpenFileAsync(string fileId)
    {
        NSUrl fileUrl = NSUrl.FromString(fileId)!;
        NSUrl? grant = await storage.FindResolvableGrantAsync(fileUrl);
        if (grant is null)
        {
            return WorkFolderFileResult.NoAccess;
        }

        WorkFolderFileResultStatus status = await grant.WithAccessAsync(() => Task.FromResult(
            !Directory.Exists(grant.Path!)
                ? WorkFolderFileResultStatus.NoAccess
                : File.Exists(fileUrl.Path!)
                    ? WorkFolderFileResultStatus.IsValid
                    : WorkFolderFileResultStatus.NotFound));
        return status switch
        {
            WorkFolderFileResultStatus.IsValid => WorkFolderFileResult.Valid(CreateFile(fileUrl, grant)),
            WorkFolderFileResultStatus.NotFound => WorkFolderFileResult.NotFound,
            _ => WorkFolderFileResult.NoAccess,
        };
    }

    public IReadOnlyList<string> GetFilePathSegments(string fileId) =>
        NSUrl.FromString(fileId)!.Path!.Split('/', StringSplitOptions.RemoveEmptyEntries);

    private WorkFolderFile CreateFile(NSUrl fileUrl, NSUrl? knownGrant)
    {
        NSUrl locationUrl = fileUrl.RemoveLastPathComponent();
        string locationId = locationUrl.AbsoluteString!;
        bool isInKnownFolder = knownGrant is not null;
        NSUrl accessUrl = knownGrant ?? fileUrl;
        _discoveredLocations[locationId] = (locationUrl, accessUrl);

        return new WorkFolderFile(
            fileUrl.AbsoluteString!,
            fileUrl.LastPathComponent!,
            locationId,
            GetDisplayName(locationUrl),
            accessUrl.AbsoluteString!,
            isInKnownFolder,
            pickedWritableFileFactory.Create(fileUrl));
    }

    /// <summary>
    /// The name to show for a folder. The last path component is the name on disk, which is not always the
    /// name the user sees: the iCloud Drive folder is really named "com~apple~CloudDocs", for example. Ask
    /// the file system for the name it presents instead, and use the name on disk where it has none.
    /// </summary>
    private static string GetDisplayName(NSUrl url) =>
        url.TryGetResource(NSUrl.LocalizedNameKey, out NSObject value, out NSError _)
            ? value.ToString()!
            : url.LastPathComponent!;

    private static async Task ProbeAsync(string folderPath)
    {
        string path = Path.Combine(folderPath, $".xecrets-probe-{Guid.NewGuid():N}");
        byte[] expected = Encoding.UTF8.GetBytes(Path.GetFileName(path));
        try
        {
            await File.WriteAllBytesAsync(path, expected);
            byte[] actual = await File.ReadAllBytesAsync(path);
            if (!actual.AsSpan().SequenceEqual(expected))
            {
                throw new IOException("The work folder read probe returned different data.");
            }
        }
        finally
        {
            File.Delete(path);
            if (File.Exists(path))
            {
                throw new IOException("The work folder deletion probe failed.");
            }
        }
    }

    private static async Task<bool> CanAccessFolderAsync(string folderPath)
    {
        try
        {
            await ProbeAsync(folderPath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }
}
