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

using Android.Content;
using Android.Provider;

using Xecrets.Mobile.Models.Abstractions;

using AndroidUri = Android.Net.Uri;
using Platform = Microsoft.Maui.ApplicationModel.Platform;

namespace Xecrets.Mobile.Platforms.Android;

/// <summary>
/// Writes the destination document in place. The provider is called off the main thread, as for the writable file.
/// </summary>
[SupportedOSPlatform("android26.0")]
internal sealed class AndroidFileWriteSession : IFileWriteSession
{
    private readonly AndroidUri _fileUri;
    private bool _isCommitted;

    private static ContentResolver ContentResolver => Platform.CurrentActivity!.ContentResolver!;

    private AndroidFileWriteSession(AndroidUri fileUri, Stream outputStream)
    {
        _fileUri = fileUri;
        OutputStream = outputStream;
    }

    public Stream OutputStream { get; }

    /// <summary>
    /// Opens an existing document with the name to replace it, when allowed, and otherwise creates it. A document that
    /// was created, but could not be opened, is removed. Storage such as Dropbox does not truncate when only opened for
    /// writing, so it is asked to.
    /// </summary>
    public static Task<AndroidFileWriteSession> CreateAsync(
        AndroidUri folderUri,
        string name,
        string contentType,
        bool overwrite) =>
        Task.Run(async () =>
        {
            AndroidUri? existingUri = overwrite ? await folderUri.FindChildAsync(name) : null;
            AndroidUri fileUri = existingUri ?? await CreateDocumentAsync(folderUri, name, contentType);
            try
            {
                return new AndroidFileWriteSession(fileUri, ContentResolver.OpenOutputStream(fileUri, "wt")!);
            }
            catch
            {
                if (existingUri is null)
                {
                    await fileUri.DeleteDocumentAsync();
                }

                throw;
            }
        });

    public async Task<string> CommitAsync()
    {
        await Task.Run(OutputStream.Close);
        _isCommitted = true;
        return _fileUri.ToString()!;
    }

    public ValueTask DisposeAsync() =>
        new(Task.Run(async () =>
        {
            try
            {
                await OutputStream.DisposeAsync();
            }
            finally
            {
                if (!_isCommitted)
                {
                    await _fileUri.DeleteDocumentAsync();
                }
            }
        }));

    // The provider gives the document another name when the one asked for is taken, which is then not the file asked
    // for.
    private static async Task<AndroidUri> CreateDocumentAsync(AndroidUri folderUri, string name, string contentType)
    {
        ThreadGuard.ThrowIfMainThread();

        AndroidUri fileUri =
            DocumentsContract.CreateDocument(ContentResolver, folderUri, contentType, name) ??
            throw new IOException("The file could not be created.");
        if (fileUri.GetDisplayName() != name)
        {
            await fileUri.DeleteDocumentAsync();
            throw new IOException("The destination file name is already in use or could not be determined.");
        }

        return fileUri;
    }
}
