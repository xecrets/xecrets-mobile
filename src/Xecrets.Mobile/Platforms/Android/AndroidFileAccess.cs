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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Android.Content;
using Android.Database;
using Android.Provider;

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;

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
internal sealed class AndroidFileAccess(WorkFolderStorage storage, ICrashLogService crashLogService) : IFileAccess
{
    // A single media item has the document id type:id.
    private static readonly Dictionary<string, RootProvider> _mediaTypes = new()
    {
        ["image"] = RootProvider.MediaImages,
        ["video"] = RootProvider.MediaVideos,
        ["audio"] = RootProvider.MediaAudio,
        ["document"] = RootProvider.MediaDocuments,
    };

    // The authorities of providers that answered that they cannot find the path of a document. Asked from several
    // threads.
    private static readonly ConcurrentDictionary<string, bool> _findDocumentPathUnsupported = new();

    private static ContentResolver ContentResolver => Platform.AppContext.ContentResolver!;

    /// <summary>
    /// ACTION_OPEN_DOCUMENT returns read, write and persistable grants without requesting flags. Only reading is
    /// ever persisted, by <see cref="KeepFileGrantAsync"/> when the file is kept in the recent files.
    /// </summary>
    public async Task<FileReference?> PickFileAsync(string initialFolder, FilePickerKind kind)
    {
        ThreadGuard.ThrowIfNotMainThread();

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

        Intent? result = await StartPickerAsync(intent);
        AndroidUri? fileUri = result?.Data;
        return fileUri is null ? null : await LocateAsync(fileUri, "");
    }

    /// <summary>
    /// The tree grant is persisted, and the folder is probed for being writable. A grant that ends up not being used is
    /// released by the next cleanup.
    /// </summary>
    public async Task<WorkFolderResult> PickFolderAsync(string initialFolder)
    {
        ThreadGuard.ThrowIfNotMainThread();

        Intent intent = new(Intent.ActionOpenDocumentTree);
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

        return await Task.Run(async () =>
        {
            try
            {
                ContentResolver.TakePersistableUriPermission(
                    treeUri,
                    result!.Flags & (ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission));
            }
            catch (Java.Lang.SecurityException ex)
            {
                crashLogService.WriteSwallowedException(ex);
                return WorkFolderResult.NoAccess;
            }

            AndroidUri folderUri = GetTreeDocumentUri(treeUri);
            if (!await CanAccessFolderAsync(folderUri))
            {
                return WorkFolderResult.NoAccess;
            }

            FileReference folder = Locate(folderUri, GetName(folderUri, ""));
            return WorkFolderResult.Valid(new WorkFolder(folder.Id, folder.Name, treeUri.ToString()!)
            {
                Root = folder.Root,
                FolderPath = folder.FolderPath,
            });
        });
    }

    /// <summary>
    /// A file referred to through the grant of a folder is in it, which is told without asking the provider, so that a
    /// file that is gone, or cannot be reached now, is still found to be in its folder. Other files are looked for in
    /// the folders by the provider, or by their path on local storage.
    /// </summary>
    public Task<WorkFolder?> FindFolderAsync(string fileId) =>
        Task.Run(async () =>
        {
            AndroidUri fileUri = AndroidUri.Parse(fileId)!;
            List<WorkFolder> folders = [.. (await storage.LoadFoldersAsync()).Where(HasPersistedGrant)];
            if (DocumentsContract.IsTreeUri(fileUri) &&
                folders.FirstOrDefault(folder => IsGrantedThrough(folder, fileUri)) is { } grantedFolder)
            {
                return grantedFolder;
            }

            return folders
                .Where(folder => IsDescendant(folder, fileUri))
                .OrderByDescending(GetDocumentDepth)
                .FirstOrDefault();
        });

    // Through the grant of the folder, the provider may tell more of where the file is stored.
    public Task<FileReference> GetFileInFolderAsync(WorkFolder folder, FileReference file) =>
        Task.Run(() => Locate(
            DocumentsContract.BuildDocumentUriUsingTree(
                AndroidUri.Parse(folder.GrantId)!,
                DocumentsContract.GetDocumentId(AndroidUri.Parse(file.Id)!)!)!,
            file.Name));

    /// <summary>
    /// The parent of a file in a folder is found through the grant of the folder, and that of a file on local storage
    /// from its path. Other providers do not tell the parent of a file reached by itself.
    /// </summary>
    public Task<string> GetParentFolderAsync(string fileId) => Task.Run(() =>
    {
        try
        {
            AndroidUri fileUri = AndroidUri.Parse(fileId)!;
            if (DocumentsContract.IsTreeUri(fileUri) && FindParentInPath(fileUri) is { } parentDocumentId)
            {
                return DocumentsContract.BuildDocumentUriUsingTree(fileUri, parentDocumentId)!.ToString()!;
            }

            return fileUri.IsExternalStorage()
                ? DocumentsContract.BuildDocumentUri(
                    fileUri.Authority,
                    GetExternalStorageParentDocumentId(DocumentsContract.GetDocumentId(fileUri)!))!.ToString()!
                : "";
        }
        catch (Exception ex) when (ex.IsDocumentProviderFailure())
        {
            crashLogService.WriteSwallowedException(ex);
            return "";
        }
    });

    /// <summary>
    /// Only the document ids of local storage are paths. Other providers have opaque document ids, so only the name
    /// remembered is known without asking the provider.
    /// </summary>
    public IReadOnlyList<string> GetPathSegments(string id, string rememberedName)
    {
        AndroidUri uri = AndroidUri.Parse(id)!;
        if (!uri.IsExternalStorage())
        {
            return [rememberedName];
        }

        string[] segments = DocumentsContract.GetDocumentId(uri)!.Split([':', '/'], StringSplitOptions.RemoveEmptyEntries);
        return rememberedName.Length == 0 ||
            (segments.Length > 0 && string.Equals(segments[^1], rememberedName, StringComparison.OrdinalIgnoreCase))
            ? segments
            : [.. segments, rememberedName];
    }

    /// <summary>
    /// A document is the same whether it is referred to by itself or through the grant of a folder, so the provider and
    /// document id are compared.
    /// </summary>
    public Task<bool> IsSameFileAsync(string fileId, string otherFileId)
    {
        AndroidUri fileUri = AndroidUri.Parse(fileId)!;
        AndroidUri otherFileUri = AndroidUri.Parse(otherFileId)!;
        try
        {
            return Task.FromResult(fileUri.Authority == otherFileUri.Authority &&
                DocumentsContract.GetDocumentId(fileUri) == DocumentsContract.GetDocumentId(otherFileUri));
        }
        catch (Java.Lang.IllegalArgumentException ex)
        {
            // An id that is not a document uri.
            crashLogService.WriteSwallowedException(ex);
            return Task.FromResult(fileId == otherFileId);
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
        catch (Java.Lang.SecurityException ex)
        {
            crashLogService.WriteSwallowedException(ex);
            return Task.FromResult(HasFileGrant(fileId));
        }
    }

    // The provider and document id tell where the file is from the start.
    public Task<FileReference> LocateAgainAsync(FileReference file) => Task.FromResult(file);

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
        foreach (UriPermission permission in ContentResolver.PersistedUriPermissions)
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
            catch (Java.Lang.SecurityException ex)
            {
                // The grant is already gone.
                crashLogService.WriteSwallowedException(ex);
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

    public Task<IWritableFile> OpenWriteAsync(string fileId) =>
        Task.FromResult<IWritableFile>(new AndroidWritableFile(AndroidUri.Parse(fileId)!, crashLogService));

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

    public async Task<bool> FileExistsInFolderAsync(string folderId, string name) =>
        await AndroidUri.Parse(folderId)!.FindChildAsync(name) is not null;

    public async Task<IFileWriteSession> OpenWriteInFolderAsync(string folderId, string name, bool overwrite) =>
        await AndroidFileWriteSession.CreateAsync(
            AndroidUri.Parse(folderId)!,
            name,
            GetCreateDocumentType(name),
            overwrite);

    /// <summary>
    /// Storage such as Dropbox may never finish renaming a document, so the renaming is only waited for a while. A
    /// provider that gives the document another name than the one asked for has not done what was asked.
    /// </summary>
    public Task<string> RenameAsync(string fileId, string newName) =>
        Task.Run(async () =>
        {
            AndroidUri fileUri = AndroidUri.Parse(fileId)!;
            AndroidUri renamedUri;
            try
            {
                // A task of its own, so that it can be given up on.
                renamedUri = await Task.Run(() => fileUri.RenameDocument(newName)).WaitForProviderAsync(crashLogService);
            }
            catch (TimeoutException ex)
            {
                throw new IOException("The file could not be renamed in time.", ex);
            }

            if (renamedUri.GetDisplayName() != newName)
            {
                throw new IOException("The file was given another name than the one asked for.");
            }

            return renamedUri.ToString()!;
        });

    public async Task<FileReference?> SaveAsAsync(string fileName, Stream content)
    {
        ThreadGuard.ThrowIfNotMainThread();

        Intent intent = new(Intent.ActionCreateDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType(GetCreateDocumentType(fileName));
        intent.PutExtra(Intent.ExtraTitle, fileName);

        Intent? result = await StartPickerAsync(intent);
        AndroidUri? fileUri = result?.Data;
        if (fileUri is null)
        {
            return null;
        }

        return await Task.Run(async () =>
        {
            await using (Stream output = ContentResolver.OpenOutputStream(fileUri, "w")!)
            {
                await content.CopyToAsync(output);
            }

            return Locate(fileUri, GetName(fileUri, fileName));
        });
    }

    /// <summary>
    /// The provider creates the document, giving it another name when the one chosen is taken. The grant is not
    /// persisted, so the file is only reached while the app runs.
    /// </summary>
    public async Task<FileReference?> CreateFileAsync(string initialFolder, string fallbackName)
    {
        ThreadGuard.ThrowIfNotMainThread();

        Intent intent = new(Intent.ActionCreateDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType(GetCreateDocumentType(fallbackName));
        intent.PutExtra(Intent.ExtraTitle, fallbackName);
        if (initialFolder.Length > 0)
        {
            intent.PutExtra(DocumentsContract.ExtraInitialUri, AndroidUri.Parse(initialFolder));
        }

        Intent? result = await StartPickerAsync(intent);
        AndroidUri? fileUri = result?.Data;

        return fileUri is null ? null : await LocateAsync(fileUri, fallbackName);
    }

    // Android knows neither the encrypted type nor .axx, so a provider giving the file another name when the one chosen
    // is taken would put the suffix after .axx. As the generic type, which Android also gives .axx files, the extension
    // is kept, and existing .axx files can be picked to be replaced.
    private static string GetCreateDocumentType(string fileName) =>
        fileName.IsEncrypted()
            ? "application/octet-stream"
            : ContentTypeDetector.DetectContentType(fileName).OrAndroidContentType(fileName);

    private static Task<Intent?> StartPickerAsync(Intent intent) =>
        ((MainActivity)Platform.CurrentActivity!).StartDocumentPickerAsync(intent);

    // The provider is asked off the main thread, as for the writable file.
    private Task<FileReference> LocateAsync(AndroidUri uri, string fallbackName) =>
        Task.Run(() => Locate(uri, GetName(uri, fallbackName)));

    /// <summary>
    /// Tells where a document is stored: the root from its provider and document id, as the providers of the Android
    /// Open Source Project and of known apps define them, and the folders leading to it as far as the provider tells
    /// them. Other providers are told as <see cref="RootProvider.Other"/> until their authorities are verified and
    /// added here, since an app cannot ask a provider if its roots are local or in the cloud.
    /// </summary>
    private FileReference Locate(AndroidUri uri, string name)
    {
        ThreadGuard.ThrowIfMainThread();

        return new(uri.ToString()!, name) { Root = GetRoot(uri), FolderPath = GetFolderPath(uri) };
    }

    /// <summary>
    /// The path of the document, as document ids from the top of the tree grant it is referred to through. Returns
    /// null for providers that cannot tell it. A provider cannot be asked beforehand if it can, so one that answers
    /// that it does not, as Dropbox does, is not asked again while the app runs, and that answer is not logged.
    /// </summary>
    private IList<string>? TryFindDocumentPath(AndroidUri uri)
    {
        // The top of the tree is the whole path to itself, as the provider must tell it.
        if (DocumentsContract.IsTreeUri(uri) && DocumentsContract.GetDocumentId(uri) is { } documentId &&
            documentId == DocumentsContract.GetTreeDocumentId(uri))
        {
            return [documentId];
        }

        if (_findDocumentPathUnsupported.TryGetValue(uri.Authority!, out bool _))
        {
            return null;
        }

        try
        {
            return DocumentsContract.FindDocumentPath(ContentResolver, uri)?.GetPath();
        }
        catch (Java.Lang.UnsupportedOperationException)
        {
            _findDocumentPathUnsupported[uri.Authority!] = true;
            return null;
        }
        catch (Exception ex) when (ex.IsDocumentProviderFailure())
        {
            crashLogService.WriteSwallowedException(ex);
            return null;
        }
    }

    private static RootProvider GetRoot(AndroidUri uri)
    {
        string documentId = DocumentsContract.GetDocumentId(uri)!;
        return uri.Authority switch
        {
            "com.android.externalstorage.documents" => GetStorageRootId(documentId) is "primary" or "home"
                ? GetDeviceRoot()
                : RootProvider.RemovableStorage,
            "com.android.providers.downloads.documents" => RootProvider.Downloads,
            "com.android.providers.media.documents" => _mediaTypes.GetValueOrDefault(documentId.Split(':')[0], RootProvider.Other),
            "com.android.mtp.documents" => RootProvider.RemovableStorage,
            "com.google.android.apps.docs.storage" => RootProvider.GoogleDrive,
            "com.microsoft.skydrive.content.StorageAccessProvider" => RootProvider.OneDrive,
            "com.dropbox.product.android.dbapp.document_provider.documents" => RootProvider.Dropbox,
            _ => RootProvider.Other,
        };
    }

    /// <summary>
    /// The folders leading to the document. On local storage they are told by its document id, which is a path. Other
    /// providers only tell them through a tree grant, from the folder granted down to the document.
    /// </summary>
    private IReadOnlyList<string> GetFolderPath(AndroidUri uri)
    {
        if (!uri.IsExternalStorage())
        {
            return FindFolderNames(uri);
        }

        string documentId = DocumentsContract.GetDocumentId(uri)!;
        IEnumerable<string> folders = documentId[(documentId.IndexOf(':', 1) + 1)..]
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .SkipLast(1);

        // The Documents folder of the device, a root of its own up to Android 10.
        return GetStorageRootId(documentId) == "home" ? ["Documents", .. folders] : [.. folders];
    }

    private IReadOnlyList<string> FindFolderNames(AndroidUri uri)
    {
        if (!DocumentsContract.IsTreeUri(uri) || TryFindDocumentPath(uri) is not { Count: >= 2 } documentIds)
        {
            return [];
        }

        try
        {
            string[] names = documentIds
                .SkipLast(1)
                .Select(id => DocumentsContract.BuildDocumentUriUsingTree(uri, id)!.GetDisplayName() ?? "")
                .ToArray();
            if (names.Any(name => name.Length == 0))
            {
                return [];
            }

            return names;
        }
        catch (Java.Lang.Exception ex)
        {
            // The provider does not tell the name of a folder, such as when it cannot be reached now.
            crashLogService.WriteSwallowedException(ex);
            return [];
        }
    }

    private static string GetStorageRootId(string documentId) =>
        documentId[..Math.Max(documentId.IndexOf(':', 1), 0)];

    private static RootProvider GetDeviceRoot() =>
        DeviceInfo.Current.Idiom == DeviceIdiom.Phone
            ? RootProvider.AndroidPhone
            : DeviceInfo.Current.Idiom == DeviceIdiom.Tablet
                ? RootProvider.AndroidTablet
                : RootProvider.AndroidDevice;

    // The name is asked from the provider, which may not tell it, such as when it cannot be reached now.
    private string GetName(AndroidUri uri, string fallback)
    {
        try
        {
            return uri.GetDisplayName() ?? fallback;
        }
        catch (Java.Lang.Exception ex)
        {
            crashLogService.WriteSwallowedException(ex);
            return fallback;
        }
    }

    /// <summary>
    /// Searches the folders of the grant the file is referred to through, from its top, for the folder that lists the
    /// document of the file. Returns null only when the search completed without finding it. A folder that cannot be
    /// listed makes the search fail, since the file may be in it.
    /// </summary>
    private async Task<string?> SearchParentAsync(AndroidUri fileUri)
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
    private async Task<string?> SearchFolderAsync(AndroidUri fileUri, string folderDocumentId, string documentId)
    {
        List<string> childFolderIds = [];
        foreach (DocumentChild child in await fileUri.ListChildrenAsync(folderDocumentId))
        {
            if (child.Id == documentId)
            {
                return folderDocumentId;
            }

            if (child.MimeType == DocumentsContract.Document.MimeTypeDir)
            {
                childFolderIds.Add(child.Id);
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
    private bool MayContain(AndroidUri fileUri, string folderDocumentId)
    {
        try
        {
            return DocumentsContract.IsChildDocument(
                ContentResolver,
                DocumentsContract.BuildDocumentUriUsingTree(fileUri, folderDocumentId)!,
                fileUri);
        }
        catch (Java.Lang.UnsupportedOperationException ex)
        {
            crashLogService.WriteSwallowedException(ex);
            return true;
        }
    }

    /// <summary>
    /// Creates, writes, reads back and renames a file in the folder, and deletes it. Returns why the folder cannot be
    /// used, or empty when it can. The first step that fails is what is told, also when the file then cannot be
    /// deleted, which is logged.
    /// </summary>
    private async Task<string> ProbeAsync(AndroidUri folderUri)
    {
        ThreadGuard.ThrowIfMainThread();

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
        string status;
        try
        {
            status = await ProbeStepsAsync();
        }
        catch
        {
            await probeUri.DeleteDocumentAsync();
            throw;
        }

        bool isDeleted = await probeUri.DeleteDocumentAsync();
        return status.Length > 0 || isDeleted ? status : "The work folder deletion probe failed.";

        async Task<string> ProbeStepsAsync()
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

            Stream? input = await probeUri.WaitUntilReadableAsync()
                ? ContentResolver.OpenInputStream(probeUri)
                : null;
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
            try
            {
                probeUri = await Task.Run(() => probeUri.RenameDocument(renamedName))
                    .WaitForProviderAsync(crashLogService);
            }
            catch (TimeoutException)
            {
                return "The work folder rename probe did not finish in time.";
            }

            if (probeUri.GetDisplayName() != renamedName)
            {
                return "The work folder rename probe returned a different, or no name.";
            }

            return "";
        }
    }

    // Why the folder cannot be used is logged, as if it was an exception, for troubleshooting.
    private async Task<bool> CanAccessFolderAsync(AndroidUri folderUri)
    {
        try
        {
            string status = await ProbeAsync(folderUri);
            if (status.Length > 0)
            {
                crashLogService.WriteSwallowedException(new IOException(status));
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException || ex.IsDocumentProviderFailure())
        {
            crashLogService.WriteSwallowedException(ex);
            return false;
        }
    }

    private bool IsDescendant(WorkFolder folder, AndroidUri fileUri)
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
        catch (Exception ex) when (ex.IsDocumentProviderFailure())
        {
            crashLogService.WriteSwallowedException(ex);
        }

        if (!fileUri.IsExternalStorage())
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
    private string? FindParentInPath(AndroidUri fileUri) =>
        TryFindDocumentPath(fileUri) is { Count: >= 2 } documentIds
            ? documentIds[documentIds.Count - 2]
            : null;

    private int GetDocumentDepth(WorkFolder folder)
    {
        AndroidUri folderUri = GetFolderDocumentUri(folder);
        IList<string>? documentIds = TryFindDocumentPath(folderUri);
        return documentIds is not null
            ? documentIds.Count
            : folderUri.IsExternalStorage()
                ? DocumentsContract.GetDocumentId(folderUri)!
                    .Split([':', '/'], StringSplitOptions.RemoveEmptyEntries)
                    .Length
                : 0;
    }

    private static AndroidUri GetFolderDocumentUri(WorkFolder folder) =>
        DocumentsContract.BuildDocumentUriUsingTree(
            AndroidUri.Parse(folder.GrantId)!,
            DocumentsContract.GetDocumentId(AndroidUri.Parse(folder.Id)!)!)!;

    private static string GetExternalStorageParentDocumentId(string documentId)
    {
        int separatorIndex = documentId.LastIndexOf('/');
        return separatorIndex >= 0
            ? documentId[..separatorIndex]
            : documentId[..(documentId.IndexOf(':') + 1)];
    }
}
