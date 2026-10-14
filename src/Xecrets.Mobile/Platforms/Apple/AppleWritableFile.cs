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

using Xecrets.Mobile.Models.Abstractions;

namespace Xecrets.Mobile.Platforms.Apple;

/// <summary>
/// A file accessed through a grant: that of one of My folders containing it, or, for a file in none of them, the url
/// a picker gave for it.
/// </summary>
internal sealed class AppleWritableFile(NSUrl url, NSUrl grant) : IWritableFile
{
    private readonly NSUrl _fileUrl = url;

    public string Id => _fileUrl.AbsoluteString!;

    public IDisposable AcquireAccess() => new AppleSecurityScope(grant);

    // Called while accessing the grant, as by the wiper.
    public Task<bool> CanWriteAsync() => Task.FromResult(
        NSFileManager.DefaultManager.IsReadableFile(_fileUrl.Path!) &&
        NSFileManager.DefaultManager.IsWritableFile(_fileUrl.Path!));

    /// <summary>
    /// Whether a file can be deleted is told by whether its folder can be written. For a file granted by itself, the
    /// folder is always reported as not writable, but the grant still allows deleting the file, as measured with all
    /// providers. Then whether the file itself can be written is checked instead.
    /// </summary>
    public Task<bool> CanDeleteAsync() => Task.FromResult(
        _fileUrl.AbsoluteString == grant.AbsoluteString
            ? NSFileManager.DefaultManager.IsWritableFile(_fileUrl.Path!)
            : NSFileManager.DefaultManager.IsDeletableFile(_fileUrl.Path!));

    public Task<long?> GetLengthAsync() => Task.FromResult<long?>(new FileInfo(_fileUrl.Path!).Length);

    /// <summary>
    /// The file is written within a coordinated write, which lasts until the stream is disposed.
    /// </summary>
    public async Task<Stream> OpenWriteAsync()
    {
        AppleCoordinatedWrite coordination = await AppleCoordinatedWrite.BeginAsync(_fileUrl, default);
        try
        {
            return new OwningStream(
                new FileStream(coordination.Url.Path!, FileMode.Open, FileAccess.Write, FileShare.None),
                coordination);
        }
        catch
        {
            coordination.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Removes the other versions of the file that the storage keeps, such as iCloud does, and then the file itself,
    /// coordinated with any other process presenting it. An error may not pass through the native coordinator, so it is
    /// set as the result instead.
    /// </summary>
    public async Task DeleteAsync()
    {
        TaskCompletionSource deleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        NSFileAccessIntent intent =
            NSFileAccessIntent.CreateWritingIntent(_fileUrl, NSFileCoordinatorWritingOptions.ForDeleting);
        NSFileCoordinator coordinator = new();
        coordinator.CoordinateAccess([intent], new NSOperationQueue(), error =>
        {
            try
            {
                if (error != null!)
                {
                    deleted.SetException(new NSErrorException(error));
                    return;
                }

                // There are no other versions to remove for most storage, which is reported as an error.
                NSFileVersion.RemoveOtherVersions(intent.Url, out NSError _);
                if (!NSFileManager.DefaultManager.Remove(intent.Url, out NSError removeError))
                {
                    deleted.SetException(new NSErrorException(removeError));
                    return;
                }

                deleted.SetResult();
            }
            finally
            {
                // The coordinator is kept until the access it coordinates is done.
                GC.KeepAlive(coordinator);
            }
        });
        await deleted.Task;

        if (File.Exists(_fileUrl.Path!))
        {
            throw new IOException("The file could not be deleted.");
        }
    }
}
