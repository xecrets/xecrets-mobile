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
using Xecrets.Mobile.Models;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Services;

namespace Xecrets.Mobile.Platforms.Apple;

/// <summary>
/// Files and folders are file urls, which are their ids. A folder in My folders has a security-scoped bookmark, which
/// gives the access to the files in it, and a file only read has a bookmark of its own. A url a picker gave carries
/// the access to the file until the app ends.
/// </summary>
public sealed class AppleFileAccess(WorkFolderStorage storage, AppleFileGrants grants) : IFileAccess
{
    /// <summary>
    /// Any file can be picked, also when an encrypted one is wanted, which is checked once it is read. A file provider
    /// such as Google Drive types its files by the MIME type it stored for them, rather than by their extension, so an
    /// encrypted file it got from elsewhere is just data to it, and would be disabled if only the encrypted file type
    /// was allowed.
    /// </summary>
    public async Task<FileReference?> PickFileAsync(string initialFolder, FilePickerKind kind)
    {
        UTType contentType = UTTypes.Data;
        NSUrl? initialUrl = await GetInitialUrlAsync(initialFolder);
        NSUrl? fileUrl = initialUrl is null
            ? await contentType.PickUrlAsync(null)
            : await initialUrl.WithAccessAsync(() => contentType.PickUrlAsync(initialUrl));
        return fileUrl is null ? null : grants.Remember(fileUrl);
    }

    public async Task<WorkFolderResult> PickFolderAsync(string initialFolder)
    {
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
            return WorkFolderResult.Valid(new WorkFolder(url.AbsoluteString!, GetDisplayName(url), url.AbsoluteString!));
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
    public FileReference GetFileInFolder(WorkFolder folder, FileReference file) => file;

    public string GetParentFolder(string fileId) =>
        NSUrl.FromString(fileId)?.RemoveLastPathComponent().AbsoluteString ?? string.Empty;

    public IReadOnlyList<string> GetPathSegments(string id, string? rememberedName = null)
    {
        string[] segments = (NSUrl.FromString(id)?.Path ?? id).Split('/', StringSplitOptions.RemoveEmptyEntries);
        return rememberedName is null ||
            (segments.Length > 0 && string.Equals(segments[^1], rememberedName, StringComparison.OrdinalIgnoreCase))
            ? segments
            : [.. segments, rememberedName];
    }

    // The same location may be reached by different paths, such as through /var and /private/var.
    public bool IsSameFile(string fileId, string otherFileId) =>
        NSUrl.FromString(fileId)?.Path is { } path && NSUrl.FromString(otherFileId)?.Path is { } otherPath
            ? AppleFileGrants.ResolvePath(path) == AppleFileGrants.ResolvePath(otherPath)
            : fileId == otherFileId;

    /// <summary>
    /// Keeps a bookmark of the file, made while there is access to it. A file that already has one keeps it, since a
    /// bookmark follows the file.
    /// </summary>
    public async Task<bool> KeepFileGrantAsync(string fileId)
    {
        if (File.Exists(AppleExtensions.GetGrantPath(fileId)))
        {
            return true;
        }

        try
        {
            return await grants.WithAccessAsync(fileId, url =>
            {
                url.SaveGrant(fileId);
                return Task.FromResult(true);
            });
        }
        catch (Exception ex) when (ex is NSErrorException or FileNotAccessibleException)
        {
            return false;
        }
    }

    public bool HasFileGrant(string fileId) =>
        grants.IsPicked(fileId) || AppleExtensions.TryResolveGrant(fileId) is not null;

    public Task ReleaseUnusedGrantsAsync(GrantReferences references)
    {
        AppleExtensions.ReleaseGrantsExcept([.. references.FolderGrantIds, .. references.FileIds]);
        return Task.CompletedTask;
    }

    public async Task<Stream> OpenReadAsync(string fileId)
    {
        (NSUrl access, NSUrl fileUrl) =
            await grants.ResolveAccessAsync(fileId) ?? throw new FileNotAccessibleException();
        try
        {
            return await access.OpenScopedReadAsync(fileUrl);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new FileNotAccessibleException(ex);
        }
    }

    public async Task<IWritableFile> OpenWritableAsync(string fileId) =>
        new AppleWritableFile(
            NSUrl.FromString(fileId)!,
            await grants.GetFolderGrantAsync(fileId, isFolderItself: false));

    // The folder of a file is told by its url.
    public Task<string> GetFolderOfFileAsync(string fileId) =>
        Task.FromResult(NSUrl.FromString(fileId)!.RemoveLastPathComponent().AbsoluteString!);

    public async Task<bool> FileExistsInFolderAsync(string folderId, string name)
    {
        NSUrl folderUrl = NSUrl.FromString(folderId)!;
        return await (await grants.GetFolderGrantAsync(folderId, isFolderItself: true)).WithAccessAsync(() =>
            Task.FromResult(File.Exists(Path.Combine(folderUrl.Path!, name))));
    }

    public async Task<string> WriteFileInFolderAsync(
        string folderId,
        string name,
        bool overwrite,
        Func<Stream, Task> writer)
    {
        NSUrl folderUrl = NSUrl.FromString(folderId)!;
        await (await grants.GetFolderGrantAsync(folderId, isFolderItself: true)).WithAccessAsync(() =>
            folderUrl.Path!.WriteFileInFolderAsync(name, overwrite, writer));
        return folderUrl.Append(name, false).AbsoluteString!;
    }

    /// <summary>
    /// The picker moves a file to where the user chooses, so the content is first written to a temporary file. Moving
    /// it, rather than exporting a copy, gives access to the saved file while the app runs.
    /// </summary>
    public async Task<FileReference?> SaveAsAsync(string fileName, Stream content)
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        NSUrl? fileUrl;
        try
        {
            string temporaryPath = Path.Combine(directory, fileName);
            await using (FileStream output = File.Create(temporaryPath))
            {
                await content.CopyToAsync(output);
            }

            fileUrl = await NSUrl.FromFilename(temporaryPath).SaveUrlAsync(null);
        }
        finally
        {
            Directory.Delete(directory, true);
        }

        return fileUrl is null ? null : grants.Remember(fileUrl);
    }

    // The initial folder is reached through the grant of one of My folders, when it is one of them.
    private async Task<NSUrl?> GetInitialUrlAsync(string initialFolder)
    {
        if (initialFolder.Length == 0)
        {
            return null;
        }

        WorkFolder? folder = (await storage.LoadFoldersAsync()).FirstOrDefault(item => item.Id == initialFolder);
        return (folder is null ? null : AppleExtensions.TryResolveGrant(folder.GrantId)) ??
            NSUrl.FromString(initialFolder);
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
