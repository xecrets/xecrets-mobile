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
using Xecrets.Mobile.Models.Services;

namespace Xecrets.Mobile.Platforms.Apple;

/// <summary>
/// Files and folders are file urls, which are their ids. A folder in My folders has a security-scoped bookmark, which
/// gives the access to the files in it, and a file only read has a bookmark of its own. A url a picker gave carries
/// the access to the file until the app ends.
/// </summary>
internal sealed class AppleFileAccess(
    WorkFolderStorage storage,
    AppleFileGrants grants,
    ITransientFileService transientFileService,
    ICrashLogService crashLogService)
    : IFileAccess
{
    /// <summary>
    /// Any file can be picked, also when an encrypted one is wanted, which is checked once it is read. A file provider
    /// such as Google Drive types its files by the MIME type it stored for them, rather than by their extension, so an
    /// encrypted file it got from elsewhere is just data to it, and would be disabled if only the encrypted file type
    /// was allowed.
    /// </summary>
    public async Task<FileReference?> PickFileAsync(string initialFolder, FilePickerKind kind)
    {
        ThreadGuard.ThrowIfNotMainThread();

        UTType contentType = UTTypes.Data;
        NSUrl? initialUrl = await GetInitialUrlAsync(initialFolder);
        NSUrl? fileUrl;
        using (IDisposable _ = new AppleSecurityScope(initialUrl))
        {
            fileUrl = await contentType.PickUrlAsync(initialUrl);
        }

        return fileUrl is null ? null : AppleFileLocation.Locate(grants.Add(fileUrl));
    }

    public async Task<WorkFolderResult> PickFolderAsync(string initialFolder)
    {
        ThreadGuard.ThrowIfNotMainThread();

        NSUrl? url = await UTTypes.Folder.PickUrlAsync(await GetInitialUrlAsync(initialFolder));
        if (url is null)
        {
            return WorkFolderResult.Canceled;
        }

        bool isAccessing = url.StartAccessingSecurityScopedResource();
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

            url.SaveGrant();

            // The top of a storage is shown as where it is stored, since a Dropbox account, for one, is named by an id.
            (FileReference folder, bool isTop) = AppleFileLocation.LocateFolder(url);
            string displayName = isTop ? folder.Root.ToString() : GetDisplayName(url);
            return WorkFolderResult.Valid(new WorkFolder(url.AbsoluteString!, displayName, url.AbsoluteString!)
            {
                Root = folder.Root,
                FolderPath = folder.FolderPath,
            });
        }
        finally
        {
            if (isAccessing)
            {
                url.StopAccessingSecurityScopedResource();
            }
        }
    }

    /// <summary>
    /// The paths are compared with their symbolic links resolved, since the same location may be reached by different
    /// paths, such as through /var and /private/var.
    /// </summary>
    public Task<WorkFolder?> FindFolderAsync(string fileId) => grants.FindFolderAsync(fileId, isFolderItself: false);

    // A file url does not tell what folder it is reached through, so it is the same in a folder.
    public Task<FileReference> GetFileInFolderAsync(WorkFolder folder, FileReference file) => Task.FromResult(file);

    public Task<string> GetParentFolderAsync(string fileId) =>
        Task.FromResult(NSUrl.FromString(fileId)?.RemoveLastPathComponent().AbsoluteString ?? "");

    public IReadOnlyList<string> GetPathSegments(string id, string rememberedName)
    {
        string[] segments = (NSUrl.FromString(id)?.Path ?? id).Split('/', StringSplitOptions.RemoveEmptyEntries);
        return rememberedName.Length == 0 ||
            (segments.Length > 0 && string.Equals(segments[^1], rememberedName, StringComparison.OrdinalIgnoreCase))
            ? segments
            : [.. segments, rememberedName];
    }

    // The same location may be reached by different paths, such as through /var and /private/var.
    public async Task<bool> IsSameFileAsync(string fileId, string otherFileId) =>
        fileId == otherFileId ||
        (await grants.GetNormalizedPathAsync(fileId) is { } path &&
            await grants.GetNormalizedPathAsync(otherFileId) is { } otherPath &&
            path == otherPath);

    /// <summary>
    /// Keeps a bookmark of the file, made while there is access to it. A file that already has one keeps it, since a
    /// bookmark follows the file.
    /// </summary>
    public async Task<bool> KeepFileGrantAsync(string fileId)
    {
        if (File.Exists(fileId.GetGrantPath()))
        {
            return true;
        }

        try
        {
            ResolvedUrl resolved =
                await grants.ResolveAccessAsync(fileId) ?? throw new FileNotAccessibleException();
            using IDisposable access = new AppleSecurityScope(resolved.AccessUrl);
            resolved.FileUrl.SaveGrant(fileId);
            return true;
        }
        catch (Exception ex) when (ex is NSErrorException or FileNotAccessibleException)
        {
            crashLogService.WriteSwallowedException(ex);
            return false;
        }
    }

    /// <summary>
    /// A file that could only be told to be in cloud storage may tell its provider once it is on the device, which it
    /// is after it has been used.
    /// </summary>
    public async Task<FileReference> LocateAgainAsync(FileReference file)
    {
        if (file.Root != RootProvider.CloudStorage || await grants.ResolveAccessAsync(file.Id) is not { } access)
        {
            return file;
        }

        using IDisposable scope = new AppleSecurityScope(access.AccessUrl);

        FileReference located = AppleFileLocation.Locate(access.FileUrl);
        return file with { Root = located.Root, FolderPath = located.FolderPath };
    }

    public bool HasFileGrant(string fileId) =>
        grants.IsPicked(fileId) || fileId.TryResolveGrant() is not null;

    public Task ReleaseUnusedGrantsAsync(GrantReferences references)
    {
        references.FolderGrantIds.Concat(references.FileIds).ReleaseGrantsExcept();
        return Task.CompletedTask;
    }

    /// <summary>
    /// The access lasts only while the coordinator reads the file, so the file is copied to a temporary file then,
    /// which on APFS is a clone that copies no data, and the copy is read. The copy is wiped once the stream is
    /// disposed.
    /// </summary>
    public async Task<Stream> OpenReadAsync(string fileId)
    {
        string copyPath = transientFileService.CreateHandoffPath(NSUrl.FromString(fileId)!.LastPathComponent!);
        try
        {
            await ReadResolvedAsync(fileId, coordinatedUrl =>
                NSFileManager.DefaultManager.Copy(coordinatedUrl, NSUrl.FromFilename(copyPath), out NSError copyError)
                    ? true
                    : throw new NSErrorException(copyError));
            return new OwningStream(
                File.OpenRead(copyPath),
                () => transientFileService.WipeTransientFileAsync(copyPath));
        }
        catch
        {
            await transientFileService.WipeTransientFileAsync(copyPath);
            throw;
        }
    }

    public Task<byte[]> ReadStartAsync(string fileId, int count) =>
        ReadResolvedAsync(fileId, coordinatedUrl =>
        {
            using FileStream stream = File.OpenRead(coordinatedUrl.Path!);
            byte[] buffer = new byte[count];
            return buffer[..stream.ReadAtLeast(buffer, count, throwOnEndOfStream: false)];
        });

    public Task CheckReachableAsync(string fileId) => ReadResolvedAsync(fileId, _ => true);

    /// <summary>
    /// Reads the file within one coordinated read, through the access resolved for it, held meanwhile. A file that is
    /// not there when the coordinator has made it available is not accessible.
    /// </summary>
    private async Task<T> ReadResolvedAsync<T>(string fileId, Func<NSUrl, T> read)
    {
        ResolvedUrl resolved = await grants.ResolveAccessAsync(fileId) ?? throw new FileNotAccessibleException();
        using IDisposable access = new AppleSecurityScope(resolved.AccessUrl);
        try
        {
            return await resolved.FileUrl.ReadCoordinatedAsync(coordinatedUrl =>
                File.Exists(coordinatedUrl.Path!)
                    ? read(coordinatedUrl)
                    : throw new FileNotFoundException("The file was not found.", coordinatedUrl.Path));
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new FileNotAccessibleException(ex);
        }
    }

    // A file in none of My folders is reached by the access the picker gave to it.
    public async Task<IWritableFile> OpenWriteAsync(string fileId)
    {
        if (await grants.FindFolderAsync(fileId, isFolderItself: false) is { } folder)
        {
            return new AppleWritableFile(NSUrl.FromString(fileId)!, folder.GrantId.ResolveGrant());
        }

        ResolvedUrl resolved = await grants.ResolveAccessAsync(fileId) ?? throw new FileNotAccessibleException();
        return new AppleWritableFile(resolved.FileUrl, resolved.AccessUrl);
    }

    // The folder of a file is told by its url.
    public Task<string> GetFolderOfFileAsync(string fileId) =>
        Task.FromResult(NSUrl.FromString(fileId)!.RemoveLastPathComponent().AbsoluteString!);

    public async Task<bool> FileExistsInFolderAsync(string folderId, string name)
    {
        NSUrl folderUrl = NSUrl.FromString(folderId)!;
        using IDisposable access = new AppleSecurityScope(
            await grants.GetFolderGrantAsync(folderId, isFolderItself: true));

        return File.Exists(Path.Combine(folderUrl.Path!, name));
    }

    /// <summary>
    /// The complete file is saved within one coordinated write, so that the file provider, such as Dropbox, takes the
    /// file only once it is written, or removed.
    /// </summary>
    public async Task<string> SaveInFolderAsync(string folderId, string name, string completedFilePath, bool overwrite)
    {
        NSUrl fileUrl = NSUrl.FromString(folderId)!.Append(name, false);
        using IDisposable access = new AppleSecurityScope(
            await grants.GetFolderGrantAsync(folderId, isFolderItself: true));
        await fileUrl.WriteCoordinatedAsync(
            overwrite ? NSFileCoordinatorWritingOptions.ForReplacing : default,
            coordinatedUrl => LocalFileSaver.Save(completedFilePath, coordinatedUrl.Path!, overwrite));
        return fileUrl.AbsoluteString!;
    }

    /// <summary>
    /// The file is moved within its folder through a file coordinator, as for writing.
    /// </summary>
    public async Task<string> RenameAsync(string fileId, string newName)
    {
        NSUrl fileUrl = NSUrl.FromString(fileId)!;
        NSUrl renamedUrl = fileUrl.RemoveLastPathComponent().Append(newName, false);
        using IDisposable access = new AppleSecurityScope(
            await grants.GetFolderGrantAsync(fileId, isFolderItself: false));
        await fileUrl.MoveCoordinatedAsync(renamedUrl);
        return renamedUrl.AbsoluteString!;
    }

    /// <summary>
    /// The picker moves a file to where the user chooses, so the complete file is first copied under the name, which on
    /// APFS is a clone that copies no data. Moving it, rather than exporting a copy, gives access to the saved file
    /// while the app runs. Where the saved file is, is only told by its path, since the provider may lose track of a
    /// file just saved when asked about it.
    /// </summary>
    public async Task<FileReference?> SaveAsAsync(string initialFolder, string fileName, string completedFilePath)
    {
        ThreadGuard.ThrowIfNotMainThread();

        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        NSUrl? fileUrl;
        try
        {
            NSUrl copyUrl = NSUrl.FromFilename(Path.Combine(directory, fileName));
            if (!NSFileManager.DefaultManager.Copy(NSUrl.FromFilename(completedFilePath), copyUrl, out NSError copyError))
            {
                throw new NSErrorException(copyError);
            }

            fileUrl = await copyUrl.SaveUrlAsync(await GetInitialUrlAsync(initialFolder));
        }
        finally
        {
            Directory.Delete(directory, true);
        }

        if (fileUrl is null)
        {
            return null;
        }

        FileReference saved = AppleFileLocation.Locate(grants.Add(fileUrl), isProviderAsked: false);
        return IsUnnamedPlaceholder(fileUrl) ? saved with { Name = fileName } : saved;
    }

    /// <summary>
    /// OneDrive tells a file just saved by a url to a placeholder of its own, such as
    /// ".../File Provider Storage/temp|1|&lt;guid&gt;/file", which does not tell the name the file is saved under. The file
    /// is saved under its proper name all the same, so the name given to the picker stands in for it, as if the url had
    /// told it. A name the user changed it to in the picker is then not known.
    /// </summary>
    private static bool IsUnnamedPlaceholder(NSUrl fileUrl) =>
        fileUrl.LastPathComponent == "file" &&
        fileUrl.RemoveLastPathComponent().LastPathComponent?.StartsWith("temp|", StringComparison.Ordinal) == true;

    // The initial folder is reached through the grant of one of My folders, when it is one of them.
    private async Task<NSUrl?> GetInitialUrlAsync(string initialFolder)
    {
        if (initialFolder.Length == 0)
        {
            return null;
        }

        WorkFolder? folder = (await storage.LoadFoldersAsync()).FirstOrDefault(item => item.Id == initialFolder);

        return (folder?.GrantId.TryResolveGrant()) ?? NSUrl.FromString(initialFolder);
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
        }
        if (File.Exists(path))
        {
            throw new IOException("The work folder deletion probe failed.");
        }
    }

    private async Task<bool> CanAccessFolderAsync(string folderPath)
    {
        try
        {
            await ProbeAsync(folderPath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            crashLogService.WriteSwallowedException(ex);
            return false;
        }
    }
}
