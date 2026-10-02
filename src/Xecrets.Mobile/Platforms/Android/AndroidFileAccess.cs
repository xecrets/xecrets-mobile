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
using Android.Content;
using Android.Database;
using Android.Provider;
using Microsoft.Maui.ApplicationModel;
using Xecrets.Common.Models;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Services;
using Xecrets.Mobile.Models.Utilities;
using Xecrets.Texts;
using AndroidUri = Android.Net.Uri;

namespace Xecrets.Mobile.Platforms.Android;

/// <summary>
/// Files and folders are documents of the Storage Access Framework. A folder in My folders has a persisted tree grant,
/// and a file in it is referred to by a document uri built on that grant, which gives the access to change it. A file
/// only read is referred to by the document uri the picker returned, with a persisted grant to read it alone.
/// </summary>
public sealed class AndroidFileAccess(WorkFolderStorage storage, IFileWiper fileWiper) : IFileAccess
{
    private const string _externalStorageAuthority = "com.android.externalstorage.documents";

    private static ContentResolver ContentResolver => Platform.AppContext.ContentResolver!;

    /// <summary>
    /// Only reading is granted, since a file is changed through the grant of its folder. The grant can be persisted.
    /// </summary>
    public async Task<FileReference?> PickFileAsync(string initialFolder, FilePickerKind kind)
    {
        Intent intent = new(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("*/*");
        if (kind == FilePickerKind.Encrypted)
        {
            intent.PutExtra(
                Intent.ExtraMimeTypes,
                [EncryptedFileType.ContentType, "application/octet-stream"]);
        }

        if (initialFolder.Length > 0)
        {
            intent.PutExtra(DocumentsContract.ExtraInitialUri, AndroidUri.Parse(initialFolder));
        }

        intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantPersistableUriPermission);
        Intent? result = await StartPickerAsync(intent);
        AndroidUri? fileUri = result?.Data;
        return fileUri is null ? null : new FileReference(fileUri.ToString()!, GetName(fileUri));
    }

    /// <summary>
    /// The tree grant is persisted, and the folder is probed for being writable. A grant that ends up not being used is
    /// released by the next cleanup.
    /// </summary>
    public async Task<WorkFolderResult> PickFolderAsync(string initialFolder)
    {
        Intent intent = new(Intent.ActionOpenDocumentTree);
        intent.AddFlags(ActivityFlags.GrantReadUriPermission |
                        ActivityFlags.GrantWriteUriPermission |
                        ActivityFlags.GrantPersistableUriPermission |
                        ActivityFlags.GrantPrefixUriPermission);
        if (initialFolder.Length > 0)
        {
            intent.PutExtra(DocumentsContract.ExtraInitialUri, AndroidUri.Parse(initialFolder));
        }

        Intent? result = await StartPickerAsync(intent);
        AndroidUri? treeUri = result?.Data;
        if (treeUri is null)
        {
            return WorkFolderResult.Canceled;
        }

        try
        {
            ContentResolver.TakePersistableUriPermission(
                treeUri,
                result!.Flags & (ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission));
        }
        catch (Java.Lang.SecurityException)
        {
            return WorkFolderResult.NoAccess;
        }

        AndroidUri folderUri = GetTreeDocumentUri(treeUri);
        if (!await CanAccessFolderAsync(folderUri))
        {
            return WorkFolderResult.NoAccess;
        }

        return WorkFolderResult.Valid(new WorkFolder(folderUri.ToString()!, GetName(folderUri), treeUri.ToString()!));
    }

    /// <summary>
    /// A file referred to through the grant of a folder is in it, which is told without asking the provider, so that a
    /// file that is gone, or cannot be reached now, is still found to be in its folder. Other files are looked for in
    /// the folders by the provider, or by their path on local storage.
    /// </summary>
    public async Task<WorkFolder?> FindFolderAsync(string fileId)
    {
        AndroidUri fileUri = AndroidUri.Parse(fileId)!;
        List<WorkFolder> folders = [.. (await storage.LoadFoldersAsync()).Where(HasPersistedGrant)];
        if (DocumentsContract.IsTreeUri(fileUri) &&
            folders.FirstOrDefault(folder => IsGrantedThrough(folder, fileUri)) is { } grantedFolder)
        {
            return grantedFolder;
        }

        return await Task.Run(() => folders
            .Where(folder => IsDescendant(folder, fileUri))
            .OrderByDescending(GetDocumentDepth)
            .FirstOrDefault());
    }

    public FileReference GetFileInFolder(WorkFolder folder, FileReference file) =>
        file with
        {
            Id = DocumentsContract.BuildDocumentUriUsingTree(
                AndroidUri.Parse(folder.GrantId)!,
                DocumentsContract.GetDocumentId(AndroidUri.Parse(file.Id)!)!)!.ToString()!,
        };

    /// <summary>
    /// The parent of a file in a folder is found through the grant of the folder, and that of a file on local storage
    /// from its path. Other providers do not tell the parent of a file reached by itself.
    /// </summary>
    public string GetParentFolder(string fileId)
    {
        try
        {
            AndroidUri fileUri = AndroidUri.Parse(fileId)!;
            if (DocumentsContract.IsTreeUri(fileUri) && FindParentInPath(fileUri) is { } parentDocumentId)
            {
                return DocumentsContract.BuildDocumentUriUsingTree(fileUri, parentDocumentId)!.ToString()!;
            }

            return fileUri.Authority == _externalStorageAuthority
                ? DocumentsContract.BuildDocumentUri(
                    fileUri.Authority,
                    GetExternalStorageParentDocumentId(DocumentsContract.GetDocumentId(fileUri)!))!.ToString()!
                : string.Empty;
        }
        catch (Exception ex) when (ex is Java.Lang.SecurityException || IsUnsupportedDocumentProviderOperation(ex))
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Only the document ids of local storage are paths. Other providers have opaque document ids, so only the name
    /// remembered is known without asking the provider.
    /// </summary>
    public IReadOnlyList<string> GetPathSegments(string id, string? rememberedName = null)
    {
        AndroidUri uri = AndroidUri.Parse(id)!;
        if (uri.Authority != _externalStorageAuthority)
        {
            return [rememberedName ?? string.Empty];
        }

        string[] segments = DocumentsContract.GetDocumentId(uri)!.Split([':', '/'], StringSplitOptions.RemoveEmptyEntries);
        return rememberedName is null ||
            (segments.Length > 0 && string.Equals(segments[^1], rememberedName, StringComparison.OrdinalIgnoreCase))
            ? segments
            : [.. segments, rememberedName];
    }

    /// <summary>
    /// A document is the same whether it is referred to by itself or through the grant of a folder, so the provider and
    /// document id are compared.
    /// </summary>
    public bool IsSameFile(string fileId, string otherFileId)
    {
        AndroidUri fileUri = AndroidUri.Parse(fileId)!;
        AndroidUri otherFileUri = AndroidUri.Parse(otherFileId)!;
        try
        {
            return fileUri.Authority == otherFileUri.Authority &&
                DocumentsContract.GetDocumentId(fileUri) == DocumentsContract.GetDocumentId(otherFileUri);
        }
        catch (Java.Lang.IllegalArgumentException)
        {
            // An id that is not a document uri.
            return fileId == otherFileId;
        }
    }

    /// <summary>
    /// The grant the picker gave is persisted, which also works for one persisted before. A file referred to through
    /// the grant of a folder has no grant of its own that can be persisted, and is only reached through the grant of
    /// the folder, which only My folders keep.
    /// </summary>
    public Task<bool> KeepFileGrantAsync(string fileId)
    {
        AndroidUri fileUri = AndroidUri.Parse(fileId)!;
        if (DocumentsContract.IsTreeUri(fileUri))
        {
            return Task.FromResult(false);
        }

        try
        {
            ContentResolver.TakePersistableUriPermission(fileUri, ActivityFlags.GrantReadUriPermission);
            return Task.FromResult(true);
        }
        catch (Java.Lang.SecurityException)
        {
            return Task.FromResult(HasFileGrant(fileId));
        }
    }

    public bool HasFileGrant(string fileId)
    {
        AndroidUri fileUri = AndroidUri.Parse(fileId)!;
        return DocumentsContract.IsTreeUri(fileUri)
            ? ContentResolver.PersistedUriPermissions.Any(permission =>
                permission is { IsReadPermission: true, Uri: { } grantUri } &&
                grantUri.Authority == fileUri.Authority &&
                DocumentsContract.IsTreeUri(grantUri) &&
                DocumentsContract.GetTreeDocumentId(grantUri) == DocumentsContract.GetTreeDocumentId(fileUri))
            : ContentResolver.PersistedUriPermissions.Any(permission =>
                permission.IsReadPermission && permission.Uri?.ToString() == fileId);
    }

    /// <summary>
    /// A persisted tree grant is held by My folders, and a persisted grant to a document by itself by the recent files.
    /// Any other is released. A grant may already be gone, such as when the user revoked it outside the app.
    /// </summary>
    public Task ReleaseUnusedGrantsAsync(GrantReferences references)
    {
        foreach (UriPermission permission in ContentResolver.PersistedUriPermissions.ToList())
        {
            if (permission.Uri is not { } uri)
            {
                continue;
            }

            string id = uri.ToString()!;
            bool isReferenced = DocumentsContract.IsTreeUri(uri)
                ? references.FolderGrantIds.Contains(id)
                : references.FileIds.Contains(id);
            if (isReferenced)
            {
                continue;
            }

            try
            {
                ContentResolver.ReleasePersistableUriPermission(
                    uri,
                    (permission.IsReadPermission ? ActivityFlags.GrantReadUriPermission : 0) |
                    (permission.IsWritePermission ? ActivityFlags.GrantWriteUriPermission : 0));
            }
            catch (Java.Lang.SecurityException)
            {
                // The grant is already gone.
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// The providers report a file that is gone, or a volume that is not mounted, as not found, and a file renamed or
    /// moved by another app as no longer granted. Through a tree grant, a file that cannot be found gives an
    /// IllegalArgumentException instead. None of these tell whether the file is gone or cannot be reached now.
    /// </summary>
    public Task<Stream> OpenReadAsync(string fileId) =>
        Task.Run(() =>
        {
            try
            {
                return ContentResolver.OpenInputStream(AndroidUri.Parse(fileId)!) ??
                    throw new FileNotAccessibleException();
            }
            catch (Exception ex) when (ex is Java.IO.FileNotFoundException or
                                           Java.Lang.IllegalArgumentException or
                                           Java.Lang.SecurityException)
            {
                throw new FileNotAccessibleException(ex);
            }
        });

    public Task<IWritableFile> OpenWritableAsync(string fileId) =>
        Task.FromResult<IWritableFile>(new AndroidWritableFile(AndroidUri.Parse(fileId)!));

    /// <summary>
    /// The file is referred to through the grant of a folder, through which the provider is asked for the path of the
    /// file. A provider that cannot tell the path, as some with opaque document ids, has the folders of the grant
    /// searched for the document of the file instead.
    /// </summary>
    public Task<string> GetFolderOfFileAsync(string fileId) =>
        Task.Run(async () =>
        {
            AndroidUri fileUri = AndroidUri.Parse(fileId)!;
            if (!DocumentsContract.IsTreeUri(fileUri))
            {
                throw new IOException("The file is not reached through one of My folders.");
            }

            string parentDocumentId = FindParentInPath(fileUri) ??
                await SearchParentAsync(fileUri) ??
                throw new FileNotAccessibleException();
            return DocumentsContract.BuildDocumentUriUsingTree(fileUri, parentDocumentId)!.ToString()!;
        });

    public Task<bool> FileExistsInFolderAsync(string folderId, string name) =>
        Task.Run(() => AndroidUri.Parse(folderId)!.FindChild(name) is not null);

    public Task<string> WriteFileInFolderAsync(
        string folderId,
        string name,
        bool overwrite,
        Func<Stream, Task> writer) =>
        AndroidUri.Parse(folderId)!.WriteDocumentAsync(name, overwrite, writer, WipeReplacedAsync);

    // The replaced document holds the previous contents, such as before an edit, so it is wiped like any other file.
    private async Task WipeReplacedAsync(AndroidUri replacedUri)
    {
        if (await fileWiper.WipeAsync(new AndroidWritableFile(replacedUri)) == FileWipeStatus.InsufficientRights)
        {
            throw new IOException("The replaced destination file could not be removed.");
        }
    }

    public async Task<FileReference?> SaveAsAsync(string fileName, Stream content)
    {
        Intent intent = new(Intent.ActionCreateDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType(fileName.IsEncrypted()
            ? EncryptedFileType.ContentType
            : ContentTypeDetector.DetectContentType(fileName));
        intent.PutExtra(Intent.ExtraTitle, fileName);
        intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
        Intent? result = await StartPickerAsync(intent);
        AndroidUri? fileUri = result?.Data;
        if (fileUri is null)
        {
            return null;
        }

        await using (Stream output = ContentResolver.OpenOutputStream(fileUri, "w")!)
        {
            await content.CopyToAsync(output);
        }

        return new FileReference(fileUri.ToString()!, GetName(fileUri, fileName));
    }

    private static Task<Intent?> StartPickerAsync(Intent intent) =>
        ((MainActivity)Platform.CurrentActivity!).StartDocumentPickerAsync(intent);

    // The name is asked from the provider, which may not tell it, such as when it cannot be reached now.
    private static string GetName(AndroidUri uri, string fallback = "")
    {
        try
        {
            return uri.GetDisplayName();
        }
        catch (Java.Lang.Exception)
        {
            return fallback;
        }
    }

    /// <summary>
    /// Searches the folders of the grant the file is referred to through, from its top, for the folder that lists the
    /// document of the file. Returns null only when the search completed without finding it. A folder that cannot be
    /// listed makes the search fail, since the file may be in it.
    /// </summary>
    private static async Task<string?> SearchParentAsync(AndroidUri fileUri)
    {
        try
        {
            return await SearchFolderAsync(
                fileUri,
                DocumentsContract.GetTreeDocumentId(fileUri)!,
                DocumentsContract.GetDocumentId(fileUri)!);
        }
        catch (Java.Lang.Exception ex)
        {
            throw new IOException("The folder of the file could not be searched.", ex);
        }
    }

    /// <summary>
    /// Searches the folder and the folders below it, depth first, skipping those the provider tells do not contain the
    /// file. Returns the id of the first folder that lists the document, or null when none does.
    /// </summary>
    private static async Task<string?> SearchFolderAsync(AndroidUri fileUri, string folderDocumentId, string documentId)
    {
        List<string> childFolderIds = [];
        foreach ((string childId, string? mimeType) in await ListChildrenAsync(fileUri, folderDocumentId))
        {
            if (childId == documentId)
            {
                return folderDocumentId;
            }

            if (mimeType == DocumentsContract.Document.MimeTypeDir)
            {
                childFolderIds.Add(childId);
            }
        }

        foreach (string childFolderId in childFolderIds.Where(childFolderId => MayContain(fileUri, childFolderId)))
        {
            if (await SearchFolderAsync(fileUri, childFolderId, documentId) is { } parentDocumentId)
            {
                return parentDocumentId;
            }
        }

        return null;
    }

    // A provider that cannot tell whether a folder contains the file has the folder searched.
    private static bool MayContain(AndroidUri fileUri, string folderDocumentId)
    {
        try
        {
            return DocumentsContract.IsChildDocument(
                ContentResolver,
                DocumentsContract.BuildDocumentUriUsingTree(fileUri, folderDocumentId)!,
                fileUri);
        }
        catch (Java.Lang.UnsupportedOperationException)
        {
            return true;
        }
    }

    /// <summary>
    /// Lists the documents in a folder, through the grant the file is referred to through. A provider still loading
    /// the folder, such as from the cloud, tells so with the listing, and notifies when it has loaded, after which the
    /// folder is listed again, as the system document picker does. There is no timeout, as reaching the storage is up to
    /// the provider.
    /// </summary>
    private static async Task<List<(string Id, string? MimeType)>> ListChildrenAsync(
        AndroidUri fileUri,
        string folderDocumentId)
    {
        AndroidUri childrenUri = DocumentsContract.BuildChildDocumentsUriUsingTree(fileUri, folderDocumentId)!;
        while (true)
        {
            using ICursor cursor = ContentResolver.Query(
                childrenUri,
                [DocumentsContract.Document.ColumnDocumentId, DocumentsContract.Document.ColumnMimeType],
                null, null, null) ?? throw new IOException("The folder could not be listed.");
            try
            {
                if (cursor.Extras?.GetBoolean(DocumentsContract.ExtraLoading) == true)
                {
                    ChangeObserver observer = new();
                    cursor.RegisterContentObserver(observer);
                    try
                    {
                        await observer.Changed;
                    }
                    finally
                    {
                        cursor.UnregisterContentObserver(observer);
                        observer.Dispose();
                    }

                    continue;
                }

                List<(string Id, string? MimeType)> children = [];
                while (cursor.MoveToNext())
                {
                    children.Add((cursor.GetString(0)!, cursor.GetString(1)));
                }

                return children;
            }
            finally
            {
                cursor.Close(); // Dispose does not call Java close()
            }
        }
    }

    private sealed class ChangeObserver : ContentObserver
    {
        private readonly TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Without a handler, the change is told on a binder thread, which only completes the task.
        public ChangeObserver()
            : base(null)
        {
        }

        public Task Changed => _changed.Task;

        public override void OnChange(bool selfChange) => _changed.TrySetResult();
    }

    private static async Task<string> ProbeAsync(AndroidUri folderUri)
    {
        string status = string.Empty;
        string name = $".xecrets-probe-{Guid.NewGuid():N}";
        AndroidUri? createdUri = DocumentsContract.CreateDocument(
            ContentResolver,
            folderUri,
            "application/octet-stream",
            name);
        if (createdUri is null)
        {
            return "The work folder probe could not be created.";
        }

        AndroidUri probeUri = createdUri;
        try
        {
            byte[] expected = Encoding.UTF8.GetBytes(name);
            Stream? output = ContentResolver.OpenOutputStream(probeUri, "w");
            if (output is null)
            {
                return "The work folder probe could not be opened for writing.";
            }

            await using (output)
            {
                await output.WriteAsync(expected);
            }

            Stream? input = ContentResolver.OpenInputStream(probeUri);
            if (input is null)
            {
                return "The work folder probe could not be opened for reading.";
            }

            await using (input)
            {
                byte[] actual = new byte[expected.Length];
                await input.ReadExactlyAsync(actual);
                if (!actual.AsSpan().SequenceEqual(expected))
                {
                    return "The work folder read probe returned different data.";
                }
            }

            string renamedName = $".xecrets-probe-{Guid.NewGuid():N}";
            probeUri = probeUri.RenameDocument(renamedName);
            if (probeUri.GetDisplayName() != renamedName)
            {
                return "The work folder rename probe returned a different name.";
            }
        }
        finally
        {
            if (!DocumentsContract.DeleteDocument(ContentResolver, probeUri))
            {
                status = "The work folder deletion probe failed.";
            }
        }

        return status;
    }

    private static async Task<bool> CanAccessFolderAsync(AndroidUri folderUri)
    {
        try
        {
            return string.IsNullOrEmpty(await ProbeAsync(folderUri));
        }
        catch (Exception ex) when (ex is IOException or Java.Lang.SecurityException ||
                                   IsUnsupportedDocumentProviderOperation(ex))
        {
            return false;
        }
    }

    private static bool IsDescendant(WorkFolder folder, AndroidUri fileUri)
    {
        AndroidUri folderUri = GetFolderDocumentUri(folder);
        if (folderUri.Authority != fileUri.Authority)
        {
            return false;
        }

        try
        {
            // The file is identified by a plain document uri, since only the folder needs a grant, and a uri built on a
            // grant claims that the document is within it, which the provider rejects when it is not. The grant the
            // file id may be built on can also be gone.
            AndroidUri candidateUri = DocumentsContract.BuildDocumentUri(
                fileUri.Authority,
                DocumentsContract.GetDocumentId(fileUri))!;
            if (DocumentsContract.IsChildDocument(ContentResolver, folderUri, candidateUri))
            {
                return true;
            }
        }
        catch (Exception ex) when (ex is Java.Lang.SecurityException || IsUnsupportedDocumentProviderOperation(ex))
        {
        }

        if (fileUri.Authority != _externalStorageAuthority)
        {
            return false;
        }

        string folderDocumentId = DocumentsContract.GetDocumentId(folderUri)!;
        string fileDocumentId = DocumentsContract.GetDocumentId(fileUri)!;
        return fileDocumentId.StartsWith(folderDocumentId.TrimEnd('/') + "/", StringComparison.Ordinal);
    }

    /// <summary>
    /// Tells if the file uri is built on the tree grant of the folder, which is what gives access to it. Folders below
    /// a folder in My folders, added by earlier versions, may share its grant.
    /// </summary>
    private static bool IsGrantedThrough(WorkFolder folder, AndroidUri fileUri)
    {
        AndroidUri grantUri = AndroidUri.Parse(folder.GrantId)!;
        return grantUri.Authority == fileUri.Authority &&
            DocumentsContract.IsTreeUri(grantUri) &&
            DocumentsContract.GetTreeDocumentId(grantUri) == DocumentsContract.GetTreeDocumentId(fileUri);
    }

    private static bool HasPersistedGrant(WorkFolder folder) =>
        ContentResolver.PersistedUriPermissions.Any(permission =>
            permission.Uri?.ToString() == folder.GrantId &&
            permission.IsReadPermission &&
            permission.IsWritePermission);

    private static AndroidUri GetTreeDocumentUri(AndroidUri treeUri) =>
        DocumentsContract.BuildDocumentUriUsingTree(treeUri, DocumentsContract.GetTreeDocumentId(treeUri)!)!;

    // The path of a document ends with the document itself, after its folder. Returns null for providers that cannot
    // tell the path of a document, such as some with opaque document ids.
    private static string? FindParentInPath(AndroidUri fileUri) =>
        TryFindDocumentPath(fileUri) is { Count: >= 2 } documentIds ? documentIds[documentIds.Count - 2] : null;

    private static int GetDocumentDepth(WorkFolder folder)
    {
        AndroidUri folderUri = GetFolderDocumentUri(folder);
        IList<string>? documentIds = TryFindDocumentPath(folderUri);
        return documentIds is not null
            ? documentIds.Count
            : folderUri.Authority == _externalStorageAuthority
                ? DocumentsContract.GetDocumentId(folderUri)!
                    .Split([':', '/'], StringSplitOptions.RemoveEmptyEntries)
                    .Length
                : 0;
    }

    private static AndroidUri GetFolderDocumentUri(WorkFolder folder) =>
        DocumentsContract.BuildDocumentUriUsingTree(
            AndroidUri.Parse(folder.GrantId)!,
            DocumentsContract.GetDocumentId(AndroidUri.Parse(folder.Id)!)!)!;

    private static IList<string>? TryFindDocumentPath(AndroidUri uri)
    {
        try
        {
            return DocumentsContract.FindDocumentPath(ContentResolver, uri)?.GetPath();
        }
        catch (Java.Lang.SecurityException)
        {
            // Finding the path of a document that is not under a tree grant, such as a file picked by itself, requires
            // MANAGE_DOCUMENTS, which only the system has.
            return null;
        }
        catch (Exception ex) when (IsUnsupportedDocumentProviderOperation(ex))
        {
            return null;
        }
    }

    private static string GetExternalStorageParentDocumentId(string documentId)
    {
        int separatorIndex = documentId.LastIndexOf('/');
        return separatorIndex >= 0
            ? documentId[..separatorIndex]
            : documentId[..(documentId.IndexOf(':') + 1)];
    }

    private static bool IsUnsupportedDocumentProviderOperation(Exception exception) =>
        exception is Java.IO.FileNotFoundException or
        Java.Lang.IllegalArgumentException or
        Java.Lang.UnsupportedOperationException;
}
