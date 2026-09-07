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
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;

using Android.Database;
using Android.Provider;

using Xecrets.Mobile.Models.Abstractions;

using AndroidUri = Android.Net.Uri;

using Platform = Microsoft.Maui.ApplicationModel.Platform;

namespace Xecrets.Mobile.Platforms.Android;

[SupportedOSPlatform("android26.0")]
internal sealed class AndroidPickedWritableFile(AndroidUri uri) : IPickedWritableFile
{
    // A rename gives the document a new Uri, so the target of subsequent access/write/delete calls has
    // to track it - hence a mutable field rather than the constructor parameter directly.
    private AndroidUri _fileUri = uri;

    public Task<T> WithAccessAsync<T>(Func<Task<T>> action) => action();

    public Task<bool> CanWriteAsync() => Task.FromResult(Supports(_fileUri, DocumentContractFlags.SupportsWrite));

    public Task<bool> CanDeleteAsync() => Task.FromResult(Supports(_fileUri, DocumentContractFlags.SupportsDelete));

    public Task<long> GetLengthAsync() => Task.FromResult(GetLength(_fileUri));

    public Task<Stream> OpenWriteAsync() =>
        Task.FromResult(Platform.AppContext.ContentResolver!.OpenOutputStream(_fileUri, "w")!);

    public Task RenameIfPossibleAsync(string newFileName)
    {
        if (!Supports(_fileUri, DocumentContractFlags.SupportsRename))
        {
            return Task.CompletedTask;
        }

        AndroidUri? renamedUri = DocumentsContract.RenameDocument(Platform.AppContext.ContentResolver!, _fileUri, newFileName);
        if (renamedUri is null)
        {
            return Task.CompletedTask;
        }

        _fileUri = renamedUri;
        return Task.CompletedTask;
    }

    public Task DeleteAsync() =>
        DocumentsContract.DeleteDocument(Platform.AppContext.ContentResolver!, _fileUri)
            ? Task.CompletedTask
            : throw new IOException("The selected file could not be deleted.");

    private static long GetLength(AndroidUri uri)
    {
        using ICursor? cursor = Platform.AppContext.ContentResolver!.Query(uri, [IOpenableColumns.Size], null, null, null);
        if (cursor is null || !cursor.MoveToFirst())
        {
            throw new IOException("The selected file size could not be determined.");
        }

        int columnIndex = cursor.GetColumnIndex(IOpenableColumns.Size);
        if (columnIndex < 0 || cursor.IsNull(columnIndex))
        {
            throw new IOException("The selected file size could not be determined.");
        }

        return cursor.GetLong(columnIndex);
    }

    private static bool Supports(AndroidUri uri, DocumentContractFlags capability)
    {
        using ICursor? cursor = Platform.AppContext.ContentResolver!.Query(
            uri,
            [DocumentsContract.Document.ColumnFlags],
            null,
            null,
            null);
        if (cursor is null || !cursor.MoveToFirst())
        {
            return false;
        }

        int columnIndex = cursor.GetColumnIndex(DocumentsContract.Document.ColumnFlags);

        // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
        return columnIndex >= 0 && (((DocumentContractFlags)cursor.GetLong(columnIndex) & capability) == capability);
    }
}
