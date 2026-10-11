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
using System.Linq;

using Android.Content;
using Android.Provider;

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;

using Xecrets.Common.Models;
using Xecrets.Mobile.Models.Models;

using AndroidUri = Android.Net.Uri;

namespace Xecrets.Mobile.Platforms.Android;

/// <summary>
/// Tells where a document is stored: the root from its provider and document id, as the providers of the Android Open
/// Source Project and of known apps define them, and the folders leading to it as far as the provider tells them. Other
/// providers are told as <see cref="RootProvider.Other"/> until their authorities are verified and added here, since an
/// app cannot ask a provider if its roots are local or in the cloud.
/// </summary>
internal static class AndroidFileLocation
{
    public const string ExternalStorageAuthority = "com.android.externalstorage.documents";

    private const string _downloadsAuthority = "com.android.providers.downloads.documents";

    private const string _mediaAuthority = "com.android.providers.media.documents";

    // A camera or other device connected by USB.
    private const string _mtpAuthority = "com.android.mtp.documents";

    private const string _googleDriveAuthority = "com.google.android.apps.docs.storage";

    private const string _oneDriveAuthority = "com.microsoft.skydrive.content.StorageAccessProvider";

    private const string _dropboxAuthority = "com.dropbox.product.android.dbapp.document_provider.documents";

    // The storage of the device itself. Other volumes, such as SD cards, have the uuid of their file system as id.
    private const string _primaryRootId = "primary";

    // The Documents folder of the device, a root of its own up to Android 10.
    private const string _homeRootId = "home";

    private const string _homeFolderName = "Documents";

    // A single media item has the document id type:id.
    private static readonly Dictionary<string, RootProvider> _mediaTypes = new()
    {
        ["image"] = RootProvider.MediaImages,
        ["video"] = RootProvider.MediaVideos,
        ["audio"] = RootProvider.MediaAudio,
        ["document"] = RootProvider.MediaDocuments,
    };

    private static ContentResolver ContentResolver => Platform.AppContext.ContentResolver!;

    public static FileReference Locate(AndroidUri uri, string name) =>
        new(uri.ToString()!, name) { Root = GetRoot(uri), FolderPath = GetFolderPath(uri) };

    /// <summary>
    /// The path of the document, as document ids from the top of the tree grant it is referred to through. Returns null
    /// for providers that cannot tell it.
    /// </summary>
    public static IList<string>? TryFindDocumentPath(AndroidUri uri)
    {
        try
        {
            return DocumentsContract.FindDocumentPath(ContentResolver, uri)?.GetPath();
        }
        catch (Exception ex) when (ex.IsDocumentProviderFailure())
        {
            return null;
        }
    }

    private static RootProvider GetRoot(AndroidUri uri)
    {
        string documentId = DocumentsContract.GetDocumentId(uri)!;
        return uri.Authority switch
        {
            ExternalStorageAuthority => GetStorageRootId(documentId) is _primaryRootId or _homeRootId
                ? GetDeviceRoot()
                : RootProvider.RemovableStorage,
            _downloadsAuthority => RootProvider.Downloads,
            _mediaAuthority => _mediaTypes.GetValueOrDefault(documentId.Split(':')[0], RootProvider.Other),
            _mtpAuthority => RootProvider.RemovableStorage,
            _googleDriveAuthority => RootProvider.GoogleDrive,
            _oneDriveAuthority => RootProvider.OneDrive,
            _dropboxAuthority => RootProvider.Dropbox,
            _ => RootProvider.Other,
        };
    }

    /// <summary>
    /// The folders leading to the document. On local storage they are told by its document id, which is a path. Other
    /// providers only tell them through a tree grant, from the folder granted down to the document.
    /// </summary>
    private static IReadOnlyList<string> GetFolderPath(AndroidUri uri)
    {
        if (uri.Authority != ExternalStorageAuthority)
        {
            return FindFolderNames(uri);
        }

        string documentId = DocumentsContract.GetDocumentId(uri)!;
        string[] segments = documentId[(documentId.IndexOf(':', 1) + 1)..]
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        IEnumerable<string> folders = segments.SkipLast(1);

        return GetStorageRootId(documentId) == _homeRootId ? [_homeFolderName, .. folders] : [.. folders];
    }

    private static IReadOnlyList<string> FindFolderNames(AndroidUri uri)
    {
        if (!DocumentsContract.IsTreeUri(uri) || TryFindDocumentPath(uri) is not { Count: >= 2 } documentIds)
        {
            return [];
        }

        try
        {
            return
            [
                .. documentIds
                    .SkipLast(1)
                    .Select(id => DocumentsContract.BuildDocumentUriUsingTree(uri, id)!.GetDisplayName()),
            ];
        }
        catch (Java.Lang.Exception)
        {
            // The provider does not tell the name of a folder, such as when it cannot be reached now.
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
}
