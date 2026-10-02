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

using Xecrets.Common.Models;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Services;

namespace Xecrets.Mobile.Models.Test;

internal sealed class FakeFileAccess(WorkFolderStorage storage) : IFileAccess
{
    // The contents of the files, by path.
    public Dictionary<string, byte[]> Files { get; } = [];

    // The paths of files that cannot be reached now, such as when offline.
    public HashSet<string> Offline { get; } = [];

    public HashSet<string> FileGrants { get; } = [];

    // The grants of folders in My folders that are gone, such as when revoked outside the app.
    public HashSet<string> RevokedFolderGrants { get; } = [];

    public bool CanKeepFileGrants { get; set; } = true;

    public bool UsesFolderIds { get; init; }

    // The paths the user picks in turn, null for canceling.
    public Queue<string?> PickedFiles { get; } = new();

    public Queue<WorkFolderResult> PickedFolders { get; } = new();

    public List<string> FilePickerStarts { get; } = [];

    public List<string> FolderPickerStarts { get; } = [];

    public int ReadCount { get; private set; }

    public int FolderLookupCount { get; private set; }

    // The failure to find the folder of a file with, when set, such as when the provider cannot list a folder.
    public Exception? FolderLookupFailure { get; set; }

    public GrantReferences? Released { get; private set; }

    public static string PathOf(string id) => id.Contains('|') ? id[(id.IndexOf('|') + 1)..] : id;

    public static string NameOf(string path) => path[(path.LastIndexOf('/') + 1)..];

    public static WorkFolderResult Folder(string path) =>
        WorkFolderResult.Valid(new WorkFolder(path, NameOf(path), $"grant:{path}"));

    public Task<FileReference?> PickFileAsync(string initialFolder, FilePickerKind kind)
    {
        FilePickerStarts.Add(initialFolder);
        string? path = PickedFiles.Dequeue();
        return Task.FromResult(path is null ? null : new FileReference(path, NameOf(path)));
    }

    public Task<WorkFolderResult> PickFolderAsync(string initialFolder)
    {
        FolderPickerStarts.Add(initialFolder);
        return Task.FromResult(PickedFolders.Dequeue());
    }

    public async Task<WorkFolder?> FindFolderAsync(string fileId) =>
        (await storage.LoadFoldersAsync())
            .Where(folder => !RevokedFolderGrants.Contains(folder.GrantId) &&
                PathOf(fileId).StartsWith(folder.Id + "/", StringComparison.Ordinal))
            .OrderByDescending(folder => folder.Id.Length)
            .FirstOrDefault();

    public FileReference GetFileInFolder(WorkFolder folder, FileReference file) =>
        UsesFolderIds ? file with { Id = $"{folder.GrantId}|{PathOf(file.Id)}" } : file;

    public string GetParentFolder(string fileId)
    {
        string path = PathOf(fileId);
        return path.Contains('/') ? path[..path.LastIndexOf('/')] : string.Empty;
    }

    // An id that cannot be made sense of, such as from an earlier version, starts with "bad:".
    public IReadOnlyList<string> GetPathSegments(string id, string? rememberedName = null)
    {
        if (id.StartsWith("bad:", StringComparison.Ordinal))
        {
            throw new FormatException("The id is not valid.");
        }

        string[] segments = PathOf(id).Split('/', StringSplitOptions.RemoveEmptyEntries);
        return rememberedName is null || (segments.Length > 0 && segments[^1] == rememberedName)
            ? segments
            : [.. segments, rememberedName];
    }

    // As on Android, a file reached through a folder has no grant of its own that can be kept.
    public Task<bool> KeepFileGrantAsync(string fileId)
    {
        bool canKeep = CanKeepFileGrants && !fileId.Contains('|');
        if (canKeep)
        {
            FileGrants.Add(fileId);
        }

        return Task.FromResult(canKeep);
    }

    public bool HasFileGrant(string fileId) => FileGrants.Contains(fileId);

    public bool IsSameFile(string fileId, string otherFileId) =>
        !fileId.StartsWith("bad:", StringComparison.Ordinal)
            ? PathOf(fileId) == PathOf(otherFileId)
            : throw new FormatException("The id is not valid.");

    public Task ReleaseUnusedGrantsAsync(GrantReferences references)
    {
        Released = references;
        return Task.CompletedTask;
    }

    public Task<Stream> OpenReadAsync(string fileId)
    {
        ReadCount++;
        string path = PathOf(fileId);
        return Offline.Contains(path) || !Files.TryGetValue(path, out byte[]? content)
            ? throw new FileNotAccessibleException()
            : Task.FromResult<Stream>(new MemoryStream(content));
    }

    public Task<IWritableFile> OpenWritableAsync(string fileId) =>
        Task.FromResult<IWritableFile>(new FakeWritableFile(fileId));

    // A folder is reached through the same grant as its file, and a file is only found when it exists.
    public Task<string> GetFolderOfFileAsync(string fileId)
    {
        FolderLookupCount++;
        if (FolderLookupFailure is not null)
        {
            throw FolderLookupFailure;
        }

        if (!Files.ContainsKey(PathOf(fileId)))
        {
            throw new FileNotAccessibleException();
        }

        string folder = GetParentFolder(fileId);
        return Task.FromResult(fileId.Contains('|') ? $"{fileId[..fileId.IndexOf('|')]}|{folder}" : folder);
    }

    public Task<bool> FileExistsInFolderAsync(string folderId, string name) =>
        Task.FromResult(Files.ContainsKey(PathIn(folderId, name)));

    public async Task<string> WriteFileInFolderAsync(
        string folderId,
        string name,
        bool overwrite,
        Func<Stream, Task> writer)
    {
        MemoryStream output = new();
        await writer(output);
        string path = PathIn(folderId, name);
        Files[path] = output.ToArray();
        return folderId.Contains('|') ? $"{folderId[..folderId.IndexOf('|')]}|{path}" : path;
    }

    public async Task<FileReference?> SaveAsAsync(string fileName, Stream content)
    {
        string? path = PickedFiles.Dequeue();
        if (path is null)
        {
            return null;
        }

        MemoryStream output = new();
        await content.CopyToAsync(output);
        Files[path] = output.ToArray();
        return new FileReference(path, NameOf(path));
    }

    private static string PathIn(string folderId, string name)
    {
        string folder = PathOf(folderId);
        return folder.Length > 0 ? $"{folder}/{name}" : name;
    }

    private sealed class FakeWritableFile(string id) : IWritableFile
    {
        public string Id => id;
        public Task<T> WithAccessAsync<T>(Func<Task<T>> action) => action();
        public Task<bool> CanWriteAsync() => throw new NotSupportedException();
        public Task<bool> CanDeleteAsync() => throw new NotSupportedException();
        public Task<long> GetLengthAsync() => throw new NotSupportedException();
        public Task<Stream> OpenWriteAsync() => throw new NotSupportedException();
        public Task<bool> RenameIfPossibleAsync(string newFileName) => throw new NotSupportedException();
        public Task DeleteAsync() => throw new NotSupportedException();
    }
}
