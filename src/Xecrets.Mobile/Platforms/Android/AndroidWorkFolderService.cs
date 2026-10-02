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
using Xecrets.Mobile.Services;
using Xecrets.Texts;

using AndroidUri = Android.Net.Uri;

namespace Xecrets.Mobile.Platforms.Android;

public sealed class AndroidWorkFolderService(
    WorkFolderStorage storage,
    IPickedWritableFileFactory pickedWritableFileFactory) : IWorkFolderService
{
    private const string _externalStorageAuthority = "com.android.externalstorage.documents";

    private static ContentResolver ContentResolver => Platform.CurrentActivity!.ContentResolver!;

    public async Task<IReadOnlyList<WorkFolder>> GetFoldersAsync() => await storage.LoadFoldersAsync();

    public async Task<WorkFolderResult> AddFolderAsync(string? initialLocationId = null)
    {
        Intent intent = new(Intent.ActionOpenDocumentTree);
        intent.AddFlags(ActivityFlags.GrantReadUriPermission |
                        ActivityFlags.GrantWriteUriPermission |
                        ActivityFlags.GrantPersistableUriPermission |
                        ActivityFlags.GrantPrefixUriPermission);
        if (initialLocationId is not null)
        {
            intent.PutExtra(DocumentsContract.ExtraInitialUri, AndroidUri.Parse(initialLocationId));
        }

        Intent? result = await ((MainActivity)Platform.CurrentActivity!).StartDocumentPickerAsync(intent);
        AndroidUri? uri = result?.Data;
        if (uri is null)
        {
            return WorkFolderResult.Canceled;
        }

        ActivityFlags grantFlags = result!.Flags &
            (ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
        try
        {
            ContentResolver.TakePersistableUriPermission(uri, grantFlags);
        }
        catch (Java.Lang.SecurityException)
        {
            return WorkFolderResult.NoAccess;
        }

        bool folderSaved = false;
        try
        {
            AndroidUri documentUri = GetTreeDocumentUri(uri);
            if (!await CanAccessFolderAsync(documentUri))
            {
                return WorkFolderResult.NoAccess;
            }

            WorkFolder folder = new(documentUri.ToString()!, documentUri.GetDisplayName(), uri.ToString()!);
            await storage.SaveFolderAsync(folder);
            folderSaved = true;
            return WorkFolderResult.Valid(folder);
        }
        finally
        {
            if (!folderSaved)
            {
                ContentResolver.ReleasePersistableUriPermission(uri, grantFlags);
            }
        }
    }

    public async Task<WorkFolder> AddDiscoveredFolderAsync(WorkFolderFile file)
    {
        WorkFolder folder = new(file.LocationId, file.LocationDisplayName, file.LocationGrantId);
        await storage.SaveFolderAsync(folder);
        return folder;
    }

    public async Task RemoveFolderAsync(WorkFolder folder)
    {
        ContentResolver.ReleasePersistableUriPermission(
            AndroidUri.Parse(folder.GrantId)!,
            ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
        await storage.SaveFoldersAsync((await storage.LoadFoldersAsync()).Where(item => item.Id != folder.Id));
    }

    public Task RenameFolderAsync(WorkFolder folder, string displayName) =>
        storage.RenameFolderAsync(folder, displayName);

    public Task SaveFoldersAsync(IReadOnlyList<WorkFolder> folders) => storage.SaveFoldersAsync(folders);

    public async Task<WorkFolderFile?> PickFileAsync(WorkFolder? folder, FilePickerKind pickerKind)
    {
        Intent intent = new(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("*/*");
        if (pickerKind == FilePickerKind.Encrypted)
        {
            intent.PutExtra(
                Intent.ExtraMimeTypes,
                [EncryptedFileType.ContentType, "application/octet-stream"]);
        }
        if (folder is not null)
        {
            intent.PutExtra(DocumentsContract.ExtraInitialUri, AndroidUri.Parse(folder.Id));
        }
        intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);

        Intent? result = await ((MainActivity)Platform.CurrentActivity!).StartDocumentPickerAsync(intent);
        AndroidUri? fileUri = result?.Data;
        if (fileUri is null)
        {
            return null;
        }

        return CreateFile(fileUri, await FindAccessFolderAsync(fileUri));
    }

    public async Task<WorkFolderFile?> SaveFileAsync(WorkFolder? folder, string fileName, Stream content)
    {
        Intent intent = new(Intent.ActionCreateDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType(fileName.IsEncrypted()
            ? EncryptedFileType.ContentType
            : ContentTypeDetector.DetectContentType(fileName));
        intent.PutExtra(Intent.ExtraTitle, fileName);
        if (folder is not null)
        {
            intent.PutExtra(DocumentsContract.ExtraInitialUri, AndroidUri.Parse(folder.Id));
        }
        intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);

        Intent? result = await ((MainActivity)Platform.CurrentActivity!).StartDocumentPickerAsync(intent);
        AndroidUri? fileUri = result?.Data;
        if (fileUri is null)
        {
            return null;
        }

        await using (Stream output = ContentResolver.OpenOutputStream(fileUri, "w")!)
        {
            await content.CopyToAsync(output);
        }

        return CreateFile(fileUri, await FindAccessFolderAsync(fileUri));
    }

    /// <summary>
    /// The id of a file in a known folder is built on the tree grant of the folder, while a file in an unknown
    /// folder has the plain document uri the picker returned. The latter can be opened once its folder is added.
    /// </summary>
    public async Task<WorkFolderFileResult> OpenFileAsync(string fileId)
    {
        AndroidUri fileUri = AndroidUri.Parse(fileId)!;
        WorkFolder? accessFolder = DocumentsContract.IsTreeUri(fileUri)
            ? (await storage.LoadFoldersAsync()).FirstOrDefault(folder => IsGrantedThrough(folder, fileUri))
            : await FindAccessFolderAsync(fileUri);
        if (accessFolder is null || !HasPersistedGrant(accessFolder))
        {
            return WorkFolderFileResult.NoAccess;
        }

        try
        {
            using ICursor? cursor = ContentResolver.Query(
                GetAccessFileUri(fileUri, accessFolder),
                [DocumentsContract.Document.ColumnDocumentId],
                null, null, null);

            bool exists = cursor?.MoveToFirst() == true;
            cursor?.Close(); // Dispose does not call Java close()
            if (!exists)
            {
                return WorkFolderFileResult.NotFound;
            }
        }
        catch (Java.Lang.SecurityException)
        {
            return WorkFolderFileResult.NoAccess;
        }
        catch (Exception ex) when (IsUnsupportedDocumentProviderOperation(ex))
        {
            return WorkFolderFileResult.NotFound;
        }

        return WorkFolderFileResult.Valid(CreateFile(fileUri, accessFolder));
    }

    public IReadOnlyList<string> GetFilePathSegments(string id, string? displayName = null)
    {
        AndroidUri uri = AndroidUri.Parse(id)!;
        if (uri.Authority != _externalStorageAuthority && displayName is not null)
        {
            return [displayName];
        }

        string documentId = DocumentsContract.GetDocumentId(uri)!;
        if (uri.Authority == _externalStorageAuthority)
        {
            string[] segments = documentId.Split([':', '/'], StringSplitOptions.RemoveEmptyEntries);
            return displayName is null ||
                (segments.Length > 0 && string.Equals(segments[^1], displayName, StringComparison.OrdinalIgnoreCase))
                ? segments
                : [.. segments, displayName];
        }

        // Other providers have opaque document ids, so the name can only be had from the provider itself.
        try
        {
            return [uri.GetDisplayName()];
        }
        catch (Java.Lang.SecurityException)
        {
            return [GetExternalStorageDocumentDisplayName(documentId)];
        }
        catch (Exception ex) when (IsUnsupportedDocumentProviderOperation(ex))
        {
            return [GetExternalStorageDocumentDisplayName(documentId)];
        }
    }

    // Only the document ids of local storage are paths, from which the folder can be worked out without access.
    public string? GetFileLocationId(string fileId)
    {
        AndroidUri uri = AndroidUri.Parse(fileId)!;
        return uri.Authority == _externalStorageAuthority
            ? DocumentsContract.BuildDocumentUri(
                uri.Authority,
                GetExternalStorageParentDocumentId(DocumentsContract.GetDocumentId(uri)!))!.ToString()
            : null;
    }

    /// <summary>
    /// Finds the innermost known folder that contains a file.
    /// </summary>
    private async Task<WorkFolder?> FindAccessFolderAsync(AndroidUri fileUri) =>
        (await storage.LoadFoldersAsync())
            .Where(item => IsDescendant(item, fileUri))
            .OrderByDescending(GetDocumentDepth)
            .FirstOrDefault();

    private static AndroidUri GetAccessFileUri(AndroidUri fileUri, WorkFolder? accessFolder) =>
        accessFolder is null
            ? fileUri
            : DocumentsContract.BuildDocumentUriUsingTree(
                AndroidUri.Parse(accessFolder.GrantId)!,
                DocumentsContract.GetDocumentId(fileUri)!)!;

    private WorkFolderFile CreateFile(AndroidUri fileUri, WorkFolder? accessFolder)
    {
        AndroidUri accessFileUri = GetAccessFileUri(fileUri, accessFolder);
        string? parentDocumentId = ResolveParentDocumentId(accessFileUri);
        if (parentDocumentId is null)
        {
            // The file can still be read, but without its folder it cannot be used through a known folder.
            return new WorkFolderFile(
                fileUri.ToString()!,
                fileUri.GetDisplayName(),
                string.Empty,
                string.Empty,
                string.Empty,
                false,
                pickedWritableFileFactory.Create(fileUri));
        }

        AndroidUri locationUri = accessFolder is null
            ? DocumentsContract.BuildDocumentUri(accessFileUri.Authority!, parentDocumentId)!
            : DocumentsContract.BuildDocumentUriUsingTree(
                AndroidUri.Parse(accessFolder.GrantId)!,
                parentDocumentId)!;
        string locationId = locationUri.ToString()!;
        bool isInKnownFolder = accessFolder is not null;

        return new WorkFolderFile(
            accessFileUri.ToString()!,
            accessFileUri.GetDisplayName(),
            locationId,
            accessFolder is not null
                ? locationUri.GetDisplayName()
                : accessFileUri.Authority == _externalStorageAuthority
                    ? GetExternalStorageDocumentDisplayName(parentDocumentId)
                    : string.Empty,
            accessFolder?.GrantId ?? string.Empty,
            isInKnownFolder,
            pickedWritableFileFactory.Create(accessFileUri));
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
        catch (IOException)
        {
            return false;
        }
        catch (Java.IO.FileNotFoundException)
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
            if (DocumentsContract.IsChildDocument(ContentResolver, folderUri, fileUri))
            {
                return true;
            }
        }
        catch (Exception ex) when (IsUnsupportedDocumentProviderOperation(ex))
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
    /// Tells if the file uri is built on the persisted tree grant of the folder, which is what gives access to it.
    /// Folders discovered below a granted folder share the grant of that folder.
    /// </summary>
    private static bool IsGrantedThrough(WorkFolder folder, AndroidUri fileUri)
    {
        AndroidUri grantUri = AndroidUri.Parse(folder.GrantId)!;
        return grantUri.Authority == fileUri.Authority &&
            DocumentsContract.IsTreeUri(fileUri) &&
            DocumentsContract.GetTreeDocumentId(grantUri) == DocumentsContract.GetTreeDocumentId(fileUri);
    }

    private static bool HasPersistedGrant(WorkFolder folder) =>
        ContentResolver.PersistedUriPermissions.Any(permission =>
            permission.Uri?.ToString() == folder.GrantId &&
            permission.IsReadPermission &&
            permission.IsWritePermission);

    private static AndroidUri GetTreeDocumentUri(AndroidUri treeUri) =>
        DocumentsContract.BuildDocumentUriUsingTree(treeUri, DocumentsContract.GetTreeDocumentId(treeUri)!)!;

    // Returns null for providers whose document ids are not paths and that cannot find the path of a document,
    // such as some cloud providers.
    private static string? ResolveParentDocumentId(AndroidUri fileUri)
    {
        IList<string>? documentIds = TryFindDocumentPath(fileUri);
        if (documentIds is { Count: >= 2 })
        {
            return documentIds[documentIds.Count - 2];
        }

        return fileUri.Authority == _externalStorageAuthority
            ? GetExternalStorageParentDocumentId(DocumentsContract.GetDocumentId(fileUri)!)
            : null;
    }

    private int GetDocumentDepth(WorkFolder folder)
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

    private static string GetExternalStorageDocumentDisplayName(string documentId)
    {
        int separatorIndex = documentId.LastIndexOf('/');
        return separatorIndex >= 0 ? documentId[(separatorIndex + 1)..] : documentId;
    }
}
