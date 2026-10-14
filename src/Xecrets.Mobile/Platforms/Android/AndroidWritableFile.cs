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

using Android.Content;
using Android.Content.PM;
using Android.Database;
using Android.OS;
using Android.Provider;

using Microsoft.Win32.SafeHandles;

using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Services;

using AndroidUri = Android.Net.Uri;

using Platform = Microsoft.Maui.ApplicationModel.Platform;

namespace Xecrets.Mobile.Platforms.Android;

/// <summary>
/// The provider is called off the main thread, since the StrictMode policy of the calling thread goes with each call
/// to it. A provider that reaches the network for a call, such as Dropbox when deleting, would otherwise fail with
/// NetworkOnMainThreadException.
/// </summary>
[SupportedOSPlatform("android26.0")]
internal sealed class AndroidWritableFile(AndroidUri uri, ICrashLogService crashLogService) : IWritableFile
{
    private readonly AndroidUri _fileUri = uri;

    public string Id => _fileUri.ToString()!;

    public IDisposable AcquireAccess() => new EmptyAccessScope();

    // A virtual document, such as a Google Docs document, has no contents of its own that can be written.
    public Task<bool> CanWriteAsync() => Task.Run(() =>
        IsGranted(_fileUri) &&
        GetFlags(_fileUri) is { } flags &&
        flags.HasFlag(DocumentContractFlags.SupportsWrite) &&
        !flags.HasFlag(DocumentContractFlags.VirtualDocument));

    public Task<bool> CanDeleteAsync() => Task.Run(() =>
        IsGranted(_fileUri) &&
        GetFlags(_fileUri) is { } flags &&
        flags.HasFlag(DocumentContractFlags.SupportsDelete));

    public Task<long?> GetLengthAsync() => Task.Run(() => GetLength(_fileUri));

    public Task<Stream> OpenWriteAsync() => Task.Run(() => OpenWrite(_fileUri));

    public Stream OpenWrite(AndroidUri fileUri)
    {
        bool isProbablyLocal =
            fileUri.Authority is
                "com.android.externalstorage.documents" or
                "com.android.providers.downloads.documents" or
                "com.android.providers.media.documents";

        ParcelFileDescriptor? pfd = null;

        if (isProbablyLocal)
        {
            try
            {
                pfd = Platform.AppContext.ContentResolver!.OpenFileDescriptor(fileUri, "rw");
            }
            catch (Exception ex)
            {
                // Ignore and try the next method.
                crashLogService.WriteSwallowedException(ex);
            }
        }

        if (pfd is null)
        {
            return Platform.AppContext.ContentResolver!.OpenOutputStream(_fileUri, "wt")!;
        }

        try
        {
            int fd = pfd.DetachFd();

            SafeFileHandle handle = new(fd, ownsHandle: true);

            return new FileStream(handle, FileAccess.ReadWrite);
        }
        finally
        {
            pfd.Close();
        }
    }

    public Task DeleteAsync() => _fileUri.DeleteDocumentAsync();

    // A provider need not tell the size, such as for a virtual document.
    private static long? GetLength(AndroidUri uri)
    {
        using ICursor? cursor = Platform.AppContext.ContentResolver!.Query(uri, [IOpenableColumns.Size], null, null, null);
        if (cursor is null || !cursor.MoveToFirst())
        {
            return null;
        }

        int columnIndex = cursor.GetColumnIndex(IOpenableColumns.Size);
        if (columnIndex < 0 || cursor.IsNull(columnIndex))
        {
            return null;
        }

        return cursor.GetLong(columnIndex);
    }

    // Both reading and writing are granted for a file the picker gave, as for one reached through a folder.
    private static bool IsGranted(AndroidUri uri) =>
        Platform.AppContext.CheckCallingOrSelfUriPermission(
            uri,
            ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission) == Permission.Granted;

    // Returns null when the provider does not tell.
    private static DocumentContractFlags? GetFlags(AndroidUri uri)
    {
        using ICursor? cursor = Platform.AppContext.ContentResolver!.Query(
            uri,
            [DocumentsContract.Document.ColumnFlags],
            null,
            null,
            null);
        if (cursor is null || !cursor.MoveToFirst())
        {
            return null;
        }

        int columnIndex = cursor.GetColumnIndex(DocumentsContract.Document.ColumnFlags);
        return columnIndex >= 0 ? (DocumentContractFlags)cursor.GetLong(columnIndex) : null;
    }
}
