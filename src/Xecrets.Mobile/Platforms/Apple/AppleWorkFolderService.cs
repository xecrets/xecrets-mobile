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
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Foundation;

using Microsoft.Maui.Storage;

using UniformTypeIdentifiers;

using Xecrets.Common.Models;

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Utilities;
using Xecrets.Mobile.Services;

namespace Xecrets.Mobile.Platforms.Apple;

public sealed class AppleWorkFolderService(WorkFolderStorage storage) : IWorkFolderService
{
    private readonly Dictionary<string, (NSUrl Location, NSUrl AccessRoot)> _discoveredLocations = [];

    private static string GrantDirectory => Path.Combine(FileSystem.AppDataDirectory, "WorkFolderGrants");

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
            SaveGrant(url);
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
            SaveGrant(url);
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
        string grantPath = GetGrantPath(folder.GrantId);
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

    public async Task<WorkFolderFile?> PickFileAsync(WorkFolder folder, FilePickerKind pickerKind)
    {
        NSUrl folderUrl = ResolveGrant(folder.GrantId);
        bool isAccessing = folderUrl.StartAccessingSecurityScopedResource();
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
                folderUrl.StopAccessingSecurityScopedResource();
            }
        }

        if (fileUrl is null)
        {
            return null;
        }

        NSUrl locationUrl = fileUrl.RemoveLastPathComponent();
        string locationId = locationUrl.AbsoluteString!;
        NSUrl? knownGrant = await FindKnownGrantAsync(fileUrl);
        bool isInKnownFolder = knownGrant is not null;
        NSUrl accessUrl = knownGrant ?? fileUrl;
        _discoveredLocations[locationId] = (locationUrl, accessUrl);

        return new WorkFolderFile(
            fileUrl.LastPathComponent!,
            locationId,
            GetDisplayName(locationUrl),
            accessUrl.AbsoluteString!,
            isInKnownFolder,
            () => Task.FromResult(OpenRead(accessUrl, fileUrl.Path!)),
            name => WithAccessAsync(
                accessUrl,
                () => Task.FromResult(File.Exists(Path.Combine(locationUrl.Path!, name)))),
            (name, overwrite, writer) => WithAccessAsync(
                accessUrl,
                () => WriteFileAsync(locationUrl.Path!, name, overwrite, writer)),
            () => WithAccessAsync(accessUrl, () =>
            {
                File.Delete(fileUrl.Path!);
                if (File.Exists(fileUrl.Path!))
                {
                    throw new IOException("The source file could not be deleted.");
                }

                return Task.CompletedTask;
            }));
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

    private static async Task WriteFileAsync(
        string folderPath,
        string name,
        bool overwrite,
        Func<Stream, Task> writer)
    {
        string destinationPath = Path.Combine(folderPath, name);
        string temporaryPath = Path.Combine(folderPath, $".xecrets-{Guid.NewGuid():N}.tmp");
        try
        {
            await using (FileStream output = File.Create(temporaryPath))
            {
                await writer(output);
            }

            File.Move(temporaryPath, destinationPath, overwrite);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static async Task<T> WithAccessAsync<T>(NSUrl url, Func<Task<T>> action)
    {
        bool isAccessing = url.StartAccessingSecurityScopedResource();
        try
        {
            return await action();
        }
        finally
        {
            if (isAccessing)
            {
                url.StopAccessingSecurityScopedResource();
            }
        }
    }

    private static async Task WithAccessAsync(NSUrl url, Func<Task> action)
    {
        bool isAccessing = url.StartAccessingSecurityScopedResource();
        try
        {
            await action();
        }
        finally
        {
            if (isAccessing)
            {
                url.StopAccessingSecurityScopedResource();
            }
        }
    }

    private static bool IsDescendant(NSUrl folder, NSUrl file) =>
        file.Path!.StartsWith(folder.Path!.TrimEnd('/') + "/", StringComparison.Ordinal);

    private async Task<NSUrl?> FindKnownGrantAsync(NSUrl file)
    {
        foreach (WorkFolder folder in (await storage.LoadFoldersAsync()).OrderByDescending(item => item.Id.Length))
        {
            NSUrl grant = ResolveGrant(folder.GrantId);
            if (IsDescendant(grant, file))
            {
                return grant;
            }
        }

        return null;
    }

    private static void SaveGrant(NSUrl url) => SaveGrant(url, url.AbsoluteString!);

    private static void SaveGrant(NSUrl url, string id)
    {
        Directory.CreateDirectory(GrantDirectory);
#if __MACCATALYST__
#pragma warning disable CA1416 // The Apple SDK declares this option available on Mac Catalyst 13.0 and later.
        NSUrlBookmarkCreationOptions options = NSUrlBookmarkCreationOptions.WithSecurityScope;
#pragma warning restore CA1416
#else
        NSUrlBookmarkCreationOptions options = default;
#endif
        NSData bookmark = url.CreateBookmarkData(
            options,
            [],
            null,
            out NSError? error);
        if (error is not null)
        {
            throw new NSErrorException(error);
        }

        File.WriteAllBytes(GetGrantPath(id), bookmark.ToArray());
    }

    private static NSUrl ResolveGrant(string id)
    {
        NSData bookmark = NSData.FromArray(File.ReadAllBytes(GetGrantPath(id)));
#if __MACCATALYST__
#pragma warning disable CA1416 // The Apple SDK declares this option available on Mac Catalyst 13.0 and later.
        NSUrlBookmarkResolutionOptions options = NSUrlBookmarkResolutionOptions.WithSecurityScope;
#pragma warning restore CA1416
#else
        NSUrlBookmarkResolutionOptions options = default;
#endif
        NSUrl url = NSUrl.FromBookmarkData(
            bookmark,
            options,
            null,
            out bool isStale,
            out NSError? error);
        if (error is not null)
        {
            throw new NSErrorException(error);
        }

        if (isStale)
        {
            SaveGrant(url, id);
        }

        return url;
    }

    private static string GetGrantPath(string id)
    {
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)));
        return Path.Combine(GrantDirectory, key);
    }

    private static Stream OpenRead(NSUrl accessUrl, string path)
    {
        bool isAccessing = accessUrl.StartAccessingSecurityScopedResource();
        try
        {
            return new SecurityScopedStream(File.OpenRead(path), accessUrl, isAccessing);
        }
        catch
        {
            if (isAccessing)
            {
                accessUrl.StopAccessingSecurityScopedResource();
            }

            throw;
        }
    }

    private sealed class SecurityScopedStream(Stream stream, NSUrl accessUrl, bool isAccessing) : Stream
    {
        private bool _isAccessing = isAccessing;

        public override bool CanRead => stream.CanRead;

        public override bool CanSeek => stream.CanSeek;

        public override bool CanWrite => stream.CanWrite;

        public override long Length => stream.Length;

        public override long Position
        {
            get => stream.Position;
            set => stream.Position = value;
        }

        public override void Flush() => stream.Flush();

        public override int Read(byte[] buffer, int offset, int count) => stream.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => stream.Seek(offset, origin);

        public override void SetLength(long value) => stream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) => stream.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    try
                    {
                        stream.Dispose();
                    }
                    finally
                    {
                        if (_isAccessing)
                        {
                            accessUrl.StopAccessingSecurityScopedResource();
                            _isAccessing = false;
                        }
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
