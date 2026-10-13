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
using Xecrets.Mobile.Models.Models;

using AndroidUri = Android.Net.Uri;
using Platform = Microsoft.Maui.ApplicationModel.Platform;

namespace Xecrets.Mobile.Platforms.Android;

[SupportedOSPlatform("android26.0")]
public sealed class AndroidFileWriteSession : IFileWriteSession
{
    private readonly AndroidUri _folderUri;
    private readonly AndroidUri _temporaryUri;
    private readonly string _name;
    private readonly bool _overwrite;
    private readonly IFileWiper _fileWiper;
    private bool _destinationCommitted;

    private static ContentResolver ContentResolver => Platform.CurrentActivity!.ContentResolver!;

    public AndroidFileWriteSession(AndroidUri folderUri, string name, bool overwrite, IFileWiper fileWiper)
    {
        _folderUri = folderUri;
        _name = name;
        _overwrite = overwrite;
        _fileWiper = fileWiper;
        _temporaryUri = DocumentsContract.CreateDocument( ContentResolver, folderUri, "application/octet-stream", $".xecrets-{Guid.NewGuid():N}.tmp")!;

        try
        {
            OutputStream = ContentResolver.OpenOutputStream(_temporaryUri, "w")!;
        }
        catch
        {
            DocumentsContract.DeleteDocument(ContentResolver, _temporaryUri);
            throw;
        }
    }

    public Stream OutputStream { get; }

    public async Task<string> CommitAsync()
    {
        OutputStream.Close();

        AndroidUri? backupUri = _overwrite
            ? _folderUri.FindChild(_name)!.RenameDocument($".xecrets-{Guid.NewGuid():N}.bak")
            : null;
        AndroidUri renamedUri;

        try
        {
            renamedUri = _temporaryUri.RenameDocument(_name);
            if (renamedUri.GetDisplayName() != _name)
            {
                throw new IOException("The destination file name is already in use.");
            }

            _destinationCommitted = true;
        }
        catch
        {
            if (backupUri is not null)
            {
                _ = backupUri.RenameDocument(_name);
            }

            throw;
        }

        if (backupUri is not null &&
            await _fileWiper.WipeAsync(new AndroidWritableFile(backupUri)) == FileWipeStatus.InsufficientRights)
        {
            throw new IOException("The replaced destination file could not be removed.");
        }

        return renamedUri.ToString()!;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await OutputStream.DisposeAsync();
        }
        finally
        {
            // A failure to wipe the backup must not remove the replacement already put in place.
            if (!_destinationCommitted)
            {
                DocumentsContract.DeleteDocument(ContentResolver, _temporaryUri);
            }
        }
    }
}
