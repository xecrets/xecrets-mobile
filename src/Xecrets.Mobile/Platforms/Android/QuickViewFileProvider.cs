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
using System.Linq;
using System.Runtime.Versioning;

using Android.App;
using Android.Content;
using Android.Database;
using Android.Provider;

using AndroidX.Core.Content;

using AndroidUri = Android.Net.Uri;

using Platform = Microsoft.Maui.ApplicationModel.Platform;

namespace Xecrets.Mobile.Platforms.Android;

/// <summary>
/// Provides decrypted files to the Quick Viewer. Google Drive's viewer only honors EXTRA_QUICK_VIEW_FEATURES for
/// ClipData uris, and for those it needs the document columns that the plain FileProvider does not return, or it
/// shows a blank page.
/// </summary>
[SupportedOSPlatform("android26.0")]
[ContentProvider(["${applicationId}.viewProvider"], Exported = false, GrantUriPermissions = true)]
[MetaData("android.support.FILE_PROVIDER_PATHS", Resource = "@xml/quick_view_file_paths")]
public class QuickViewFileProvider : FileProvider
{
    public static string Authority => $"{Platform.AppContext.PackageName}.viewProvider";

    private static readonly string[] _defaultColumns =
    [
        DocumentsContract.Document.ColumnDocumentId,
        DocumentsContract.Document.ColumnDisplayName,
        DocumentsContract.Document.ColumnMimeType,
        DocumentsContract.Document.ColumnSize,
        DocumentsContract.Document.ColumnFlags,
    ];

    public override ICursor Query(
        AndroidUri? uri,
        string[]? projection,
        string? selection,
        string[]? selectionArgs,
        string? sortOrder)
    {
        // Like FileProvider, leave out the columns it does not know.
        string[] columns = projection?.Intersect(_defaultColumns).ToArray() ?? _defaultColumns;
        string displayName;
        long size;
        using (ICursor fileCursor = base.Query(
            uri,
            [IOpenableColumns.DisplayName, IOpenableColumns.Size],
            selection,
            selectionArgs,
            sortOrder)!)
        {

            fileCursor.MoveToFirst();
            displayName = fileCursor.GetString(0)!;
            size = fileCursor.GetLong(1);

            fileCursor.Close(); // Dispose does not call Java close()
        }

        // Flags stay 0: SUPPORTS_WRITE and SUPPORTS_DELETE would enable more actions in Drive's viewer.
        MatrixCursor cursor = new(columns, 1);
        cursor.AddRow([.. columns.Select<string, Java.Lang.Object>(column => column switch
        {
            DocumentsContract.Document.ColumnDocumentId => new Java.Lang.String(uri!.LastPathSegment!),
            DocumentsContract.Document.ColumnDisplayName => new Java.Lang.String(displayName),
            DocumentsContract.Document.ColumnMimeType => new Java.Lang.String(GetType(uri)!),
            DocumentsContract.Document.ColumnSize => Java.Lang.Long.ValueOf(size),
            DocumentsContract.Document.ColumnFlags => Java.Lang.Integer.ValueOf(0),
            _ => throw new ArgumentOutOfRangeException(nameof(projection), column, null),
        })]);

        return cursor;
    }
}
