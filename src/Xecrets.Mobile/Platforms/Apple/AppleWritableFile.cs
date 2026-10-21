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
using System.Threading.Tasks;

using Foundation;

using Xecrets.Common.Models;
using Xecrets.Mobile.Models.Abstractions;

namespace Xecrets.Mobile.Platforms.Apple;

/// <summary>
/// A file accessed through a grant: that of one of My folders containing it, or, for a file in none of them, the url
/// a picker gave for it.
/// </summary>
internal sealed class AppleWritableFile(NSUrl url, NSUrl grant) : IWritableFile
{
    private string Path => url.Path!;

    public IDisposable AcquireAccess() => new AppleSecurityScope(grant);

    // Called while accessing the grant, as by the wiper.
    public Task<bool> CanWriteAsync() => Task.FromResult(
        NSFileManager.DefaultManager.IsReadableFile(Path) &&
        NSFileManager.DefaultManager.IsWritableFile(Path));

    /// <summary>
    /// Whether a file can be deleted is told by whether its folder can be written. For a file granted by itself, the
    /// folder is always reported as not writable, but the grant still allows deleting the file, as measured with all
    /// providers. Then whether the file itself can be written is checked instead. A file that is moved to the trash
    /// may not be on the device yet, as with OneDrive and Google Drive, and then tells nothing, so it is only seen
    /// whether it can be when it is.
    /// </summary>
    public Task<bool> CanDeleteAsync() => Task.FromResult(
        !IsDeletedByRemoving() ||
        (url.AbsoluteString == grant.AbsoluteString
            ? NSFileManager.DefaultManager.IsWritableFile(Path)
            : NSFileManager.DefaultManager.IsDeletableFile(Path)));

    public Task<long?> GetLengthAsync() => Task.FromResult<long?>(new FileInfo(Path).Length);

    /// <summary>
    /// The whole file is written within one coordinated write.
    /// </summary>
    public Task OverwriteAsync(Action<Stream> overwrite) =>
        url.WriteCoordinatedAsync(default, coordinatedUrl =>
        {
            using FileStream stream = new(coordinatedUrl.Path!, FileMode.Open, FileAccess.Write, FileShare.None);
            overwrite(stream);
        });

    /// <summary>
    /// Removes the other versions of the file that the storage keeps, such as iCloud does, and then the file itself,
    /// coordinated with any other process presenting it. An error may not pass through the native coordinator, so it is
    /// set as the result instead. Storage where removing the file was not verified to actually delete it is asked to
    /// move it to its trash instead. OneDrive and Google Drive report a removed file as deleted, but keep it,
    /// while they do delete a file moved to the trash.
    /// </summary>
    public async Task DeleteAsync()
    {
        if (!IsDeletedByRemoving())
        {
            if (!NSFileManager.DefaultManager.TrashItem(url, out NSUrl _, out NSError trashError))
            {
                throw new NSErrorException(trashError);
            }

            return;
        }

        TaskCompletionSource deleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        NSFileAccessIntent intent =
            NSFileAccessIntent.CreateWritingIntent(url, NSFileCoordinatorWritingOptions.ForDeleting);
        NSFileCoordinator coordinator = new();
        coordinator.CoordinateAccess([intent], new NSOperationQueue(), error =>
        {
            if (error != null!)
            {
                deleted.SetException(new NSErrorException(error));
                return;
            }

            // There are no other versions to remove for most storage, which is reported as an error, so we
            // ignore that.
            NSFileVersion.RemoveOtherVersions(intent.Url, out NSError _);

            if (!NSFileManager.DefaultManager.Remove(intent.Url, out NSError removeError))
            {
                deleted.SetException(new NSErrorException(removeError));
                return;
            }

            deleted.SetResult();
        });
        await deleted.Task;

        if (File.Exists(Path))
        {
            throw new IOException("The file could not be deleted.");
        }
    }

    // Checks if removing the file works, otherwise we need to fall back to trashing it. This is only
    // empirically verified, since the various implementations are inconsistent. These are the ones that
    // we have verified to delete the file when removing it with NSFileManager.DefaultManager.Remove().
    private bool IsDeletedByRemoving() => AppleFileLocation.Locate(url).Root is
        RootProvider.ICloudDrive or
        RootProvider.Dropbox or
        RootProvider.IPhone or
        RootProvider.IPad or
        RootProvider.Mac or
        RootProvider.None;
}
