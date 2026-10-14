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
using System.Threading;
using System.Threading.Tasks;

using Foundation;

namespace Xecrets.Mobile.Platforms.Apple;

/// <summary>
/// A coordinated write of a file, which lasts until it is disposed, so that the file provider, such as Dropbox, does not
/// take the file, such as to upload it, while it is written. The accessor runs on a background queue, where it waits
/// until the write is disposed. Nothing else may coordinate access to the file meanwhile, such as to delete it, since
/// that waits for this write to end.
/// </summary>
internal sealed class AppleCoordinatedWrite : IDisposable
{
    private readonly ManualResetEventSlim _released;
    private bool _isDisposed;

    private AppleCoordinatedWrite(NSUrl url, ManualResetEventSlim released)
    {
        Url = url;
        _released = released;
    }

    /// <summary>
    /// The url to write the file at, as told by the coordinator.
    /// </summary>
    public NSUrl Url { get; }

    /// <summary>
    /// Begins the coordinated write once the coordinator allows it. An error may not pass through the native
    /// coordinator, so it is set as the result instead.
    /// </summary>
    public static Task<AppleCoordinatedWrite> BeginAsync(NSUrl fileUrl, NSFileCoordinatorWritingOptions options)
    {
        TaskCompletionSource<AppleCoordinatedWrite> begun = new(TaskCreationOptions.RunContinuationsAsynchronously);
        NSFileAccessIntent intent = NSFileAccessIntent.CreateWritingIntent(fileUrl, options);
        NSFileCoordinator coordinator = new();
        coordinator.CoordinateAccess([intent], new NSOperationQueue(), error =>
        {
            try
            {
                if (error != null!)
                {
                    begun.SetException(new NSErrorException(error));
                    return;
                }

                using ManualResetEventSlim released = new();
                begun.SetResult(new AppleCoordinatedWrite(intent.Url, released));
                released.Wait();
            }
            finally
            {
                // The coordinator is kept until the access it coordinates is done.
                GC.KeepAlive(coordinator);
            }
        });
        return begun.Task;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            _released.Set();
        }
    }
}
