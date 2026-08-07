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

using Android.App;
using Android.Content;
using Android.Database;
using Android.Provider;

using Microsoft.Maui.ApplicationModel;

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Utilities;
using Xecrets.Mobile.Services;

using AndroidUri = Android.Net.Uri;

namespace Xecrets.Mobile.Platforms.Android;

public sealed class AndroidWorkFolderService(WorkFolderStorage storage) : IWorkFolderService
{
    private const string _externalStorageAuthority = "com.android.externalstorage.documents";

    private ContentResolver ContentResolver => Platform.CurrentActivity!.ContentResolver!;

    public async Task<IReadOnlyList<WorkFolder>> GetFoldersAsync() => await storage.LoadFoldersAsync();

    public IReadOnlyList<string> GetPathSegments(WorkFolder folder)
    {
        AndroidUri uri = AndroidUri.Parse(folder.Id)!;
        if (uri.Authority != _externalStorageAuthority)
        {
            return [folder.DisplayName];
        }

        string documentId = DocumentsContract.GetDocumentId(uri)!;
        return WorkFolderStorage.BuildPathSegments(documentId, folder, ':', '/');
    }

    public async Task<WorkFolder?> AddFolderAsync(string? initialLocationId = null)
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
            return null;
        }

        ActivityFlags grantFlags = result!.Flags &
            (ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
        ContentResolver.TakePersistableUriPermission(uri, grantFlags);
        bool folderSaved = false;
        try
        {
            AndroidUri documentUri = GetTreeDocumentUri(uri);
            await ProbeAsync(documentUri);
            WorkFolder folder = new(documentUri.ToString()!, GetDisplayName(documentUri), uri.ToString()!);
            await storage.SaveFolderAsync(folder);
            folderSaved = true;
            return folder;
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
        storage.SaveFolders((await storage.LoadFoldersAsync()).Where(item => item.Id != folder.Id));
    }

    public Task RenameFolderAsync(WorkFolder folder, string displayName) =>
        storage.RenameFolderAsync(folder, displayName);

    public Task SaveFolderOrderAsync(IReadOnlyList<WorkFolder> folders)
    {
        storage.SaveFolders(folders);
        return Task.CompletedTask;
    }

    public async Task<WorkFolderFile?> PickFileAsync(WorkFolder folder, FilePickerKind pickerKind)
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
        intent.PutExtra(DocumentsContract.ExtraInitialUri, AndroidUri.Parse(folder.Id));
        intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);

        Intent? result = await ((MainActivity)Platform.CurrentActivity!).StartDocumentPickerAsync(intent);
        AndroidUri? fileUri = result?.Data;
        if (fileUri is null)
        {
            return null;
        }

        WorkFolder? accessFolder = (await storage.LoadFoldersAsync())
            .Where(item => IsDescendant(item, fileUri))
            .OrderByDescending(GetDocumentDepth)
            .FirstOrDefault();
        AndroidUri accessFileUri = accessFolder is null
            ? fileUri
            : DocumentsContract.BuildDocumentUriUsingTree(
                AndroidUri.Parse(accessFolder.GrantId)!,
                DocumentsContract.GetDocumentId(fileUri)!)!;
        string parentDocumentId = ResolveParentDocumentId(accessFileUri);
        AndroidUri locationUri = accessFolder is null
            ? DocumentsContract.BuildDocumentUri(accessFileUri.Authority!, parentDocumentId)!
            : DocumentsContract.BuildDocumentUriUsingTree(
                AndroidUri.Parse(accessFolder.GrantId)!,
                parentDocumentId)!;
        string locationId = locationUri.ToString()!;
        bool isInKnownFolder = accessFolder is not null;

        return new WorkFolderFile(
            GetDisplayName(accessFileUri),
            locationId,
            accessFolder is not null
                ? GetDisplayName(locationUri)
                : accessFileUri.Authority == _externalStorageAuthority
                    ? GetExternalStorageDocumentDisplayName(parentDocumentId)
                    : string.Empty,
            accessFolder?.GrantId ?? string.Empty,
            isInKnownFolder,
            () => Task.FromResult<Stream>(ContentResolver.OpenInputStream(accessFileUri)!),
            name => Task.FromResult(FindChild(locationUri, name) is not null),
            (name, overwrite, writer) => WriteDocumentAsync(locationUri, name, overwrite, writer),
            () => DeleteDocumentAsync(accessFileUri));
    }

    private async Task ProbeAsync(AndroidUri folderUri)
    {
        string name = $".xecrets-probe-{Guid.NewGuid():N}";
        AndroidUri probeUri = DocumentsContract.CreateDocument(
            ContentResolver,
            folderUri,
            "application/octet-stream",
            name)!;
        try
        {
            byte[] expected = Encoding.UTF8.GetBytes(name);
            await using (Stream output = ContentResolver.OpenOutputStream(probeUri, "w")!)
            {
                await output.WriteAsync(expected);
            }

            await using Stream input = ContentResolver.OpenInputStream(probeUri)!;
            byte[] actual = new byte[expected.Length];
            await input.ReadExactlyAsync(actual);
            if (!actual.AsSpan().SequenceEqual(expected))
            {
                throw new IOException("The work folder read probe returned different data.");
            }

            string renamedName = $".xecrets-probe-{Guid.NewGuid():N}";
            probeUri = RenameDocument(probeUri, renamedName);
            if (GetDisplayName(probeUri) != renamedName)
            {
                throw new IOException("The work folder rename probe returned a different name.");
            }
        }
        finally
        {
            if (!DocumentsContract.DeleteDocument(ContentResolver, probeUri))
            {
                throw new IOException("The work folder deletion probe failed.");
            }
        }
    }

    private async Task WriteDocumentAsync(
        AndroidUri folderUri,
        string name,
        bool overwrite,
        Func<Stream, Task> writer)
    {
        string temporaryName = $".xecrets-{Guid.NewGuid():N}.tmp";
        AndroidUri temporaryUri = DocumentsContract.CreateDocument(
            ContentResolver,
            folderUri,
            "application/octet-stream",
            temporaryName)!;
        bool destinationCommitted = false;
        try
        {
            await using (Stream output = ContentResolver.OpenOutputStream(temporaryUri, "w")!)
            {
                await writer(output);
            }

            AndroidUri? backupUri = null;
            if (overwrite)
            {
                backupUri = RenameDocument(FindChild(folderUri, name)!, $".xecrets-{Guid.NewGuid():N}.bak");
            }

            try
            {
                AndroidUri renamedUri = RenameDocument(temporaryUri, name);
                if (GetDisplayName(renamedUri) != name)
                {
                    throw new IOException("The destination file name is already in use.");
                }

                destinationCommitted = true;
            }
            catch
            {
                if (backupUri is not null)
                {
                    _ = RenameDocument(backupUri, name);
                }

                throw;
            }

            if (backupUri is not null && !DocumentsContract.DeleteDocument(ContentResolver, backupUri))
            {
                throw new IOException("The replaced destination file could not be removed.");
            }
        }
        catch
        {
            if (!destinationCommitted)
            {
                DocumentsContract.DeleteDocument(ContentResolver, temporaryUri);
            }

            throw;
        }
    }

    private Task DeleteDocumentAsync(AndroidUri uri)
    {
        if (!DocumentsContract.DeleteDocument(ContentResolver, uri))
        {
            throw new IOException("The source file could not be deleted.");
        }
        return Task.CompletedTask;
    }

    private AndroidUri RenameDocument(AndroidUri uri, string name) =>
        DocumentsContract.RenameDocument(ContentResolver, uri, name) ??
        throw new IOException("The document could not be renamed.");

    private AndroidUri? FindChild(AndroidUri folderUri, string name)
    {
        string documentId = DocumentsContract.GetDocumentId(folderUri)!;
        AndroidUri childrenUri = DocumentsContract.BuildChildDocumentsUriUsingTree(folderUri, documentId)!;
        using ICursor cursor = ContentResolver.Query(
            childrenUri,
            [DocumentsContract.Document.ColumnDocumentId, DocumentsContract.Document.ColumnDisplayName],
            null,
            null,
            null)!;
        while (cursor.MoveToNext())
        {
            if (cursor.GetString(1) == name)
            {
                return DocumentsContract.BuildDocumentUriUsingTree(folderUri, cursor.GetString(0)!);
            }
        }

        return null;
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

    private static AndroidUri GetTreeDocumentUri(AndroidUri treeUri) =>
        DocumentsContract.BuildDocumentUriUsingTree(treeUri, DocumentsContract.GetTreeDocumentId(treeUri)!)!;

    private string ResolveParentDocumentId(AndroidUri fileUri)
    {
        IList<string>? documentIds = TryFindDocumentPath(fileUri);
        if (documentIds is { Count: >= 2 })
        {
            return documentIds[documentIds.Count - 2];
        }

        if (fileUri.Authority == _externalStorageAuthority)
        {
            return GetExternalStorageParentDocumentId(DocumentsContract.GetDocumentId(fileUri)!);
        }

        throw new IOException("This location is not currently supported.");
    }

    private int GetDocumentDepth(WorkFolder folder)
    {
        AndroidUri folderUri = GetFolderDocumentUri(folder);
        IList<string>? documentIds = TryFindDocumentPath(folderUri);
        if (documentIds is not null)
        {
            return documentIds.Count;
        }

        return folderUri.Authority == _externalStorageAuthority
            ? DocumentsContract.GetDocumentId(folderUri)!
                .Split([':', '/'], StringSplitOptions.RemoveEmptyEntries)
                .Length
            : 0;
    }

    private static AndroidUri GetFolderDocumentUri(WorkFolder folder) =>
        DocumentsContract.BuildDocumentUriUsingTree(
            AndroidUri.Parse(folder.GrantId)!,
            DocumentsContract.GetDocumentId(AndroidUri.Parse(folder.Id)!)!)!;

    private IList<string>? TryFindDocumentPath(AndroidUri uri)
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

    private string GetDisplayName(AndroidUri uri)
    {
        using ICursor cursor = ContentResolver.Query(
            uri,
            [DocumentsContract.Document.ColumnDisplayName],
            null,
            null,
            null)!;
        cursor.MoveToFirst();
        return cursor.GetString(0)!;
    }
}
