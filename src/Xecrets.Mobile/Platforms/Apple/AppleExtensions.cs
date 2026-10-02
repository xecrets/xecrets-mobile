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
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using CoreGraphics;

using Foundation;

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

using UniformTypeIdentifiers;

using UIKit;

namespace Xecrets.Mobile.Platforms.Apple;

public static class AppleExtensions
{
    private static string GrantDirectory => Path.Combine(FileSystem.AppDataDirectory, "WorkFolderGrants");

    public static async Task<NSUrl?> PickUrlAsync(this UTType contentType, NSUrl? initialUrl)
    {
        UIDocumentPickerViewController picker = new([contentType], false)
        {
            DirectoryUrl = initialUrl,
        };
        PickerDelegate pickerDelegate = new();
        picker.Delegate = pickerDelegate;
        await Platform.GetCurrentUIViewController()!.PresentViewControllerAsync(picker, true);
        return await pickerDelegate.Completion.Task;
    }

    /// <summary>
    /// Moves the file to where the user chooses, and returns the url it was moved to, which gives access to it, or null
    /// if the user cancels.
    /// </summary>
    public static async Task<NSUrl?> SaveUrlAsync(this NSUrl fileUrl, NSUrl? initialUrl)
    {
        UIDocumentPickerViewController picker = new([fileUrl], false)
        {
            DirectoryUrl = initialUrl,
        };
        PickerDelegate pickerDelegate = new();
        picker.Delegate = pickerDelegate;
        await Platform.GetCurrentUIViewController()!.PresentViewControllerAsync(picker, true);
        return await pickerDelegate.Completion.Task;
    }

    public static (double Left, double Right) GetWindowGaps(this VisualElement view)
    {
        UIView platformView = (UIView)view.Handler!.PlatformView!;
        CGRect frame = platformView.ConvertRectToView(platformView.Bounds, null);
        return (frame.Left, platformView.Window!.Bounds.Width - frame.Right);
    }

    extension(NSUrl url)
    {
        public async Task<T> WithAccessAsync<T>(Func<Task<T>> action)
        {
            bool isAccessing = url.StartAccessingSecurityScopedResource();
            try
            {
                return await action();
            }
            finally
            {
                if (isAccessing)
                {
                    url.StopAccessingSecurityScopedResource();
                }
            }
        }

        public async Task WithAccessAsync(Func<Task> action)
        {
            bool isAccessing = url.StartAccessingSecurityScopedResource();
            try
            {
                await action();
            }
            finally
            {
                if (isAccessing)
                {
                    url.StopAccessingSecurityScopedResource();
                }
            }
        }

        /// <summary>
        /// Opens the file for reading, holding the access to this url until the stream is disposed.
        /// </summary>
        public async Task<Stream> OpenScopedReadAsync(NSUrl fileUrl)
        {
            bool isAccessing = url.StartAccessingSecurityScopedResource();
            try
            {
                return new SecurityScopedStream(await OpenCoordinatedReadAsync(fileUrl), url, isAccessing);
            }
            catch
            {
                if (isAccessing)
                {
                    url.StopAccessingSecurityScopedResource();
                }

                throw;
            }
        }

        public void SaveGrant() => url.SaveGrant(url.AbsoluteString!);

        public void SaveGrant(string id)
        {
            Directory.CreateDirectory(GrantDirectory);
#if __MACCATALYST__
#pragma warning disable CA1416 // The Apple SDK declares this option available on Mac Catalyst 13.0 and later.
            NSUrlBookmarkCreationOptions options = NSUrlBookmarkCreationOptions.WithSecurityScope;
#pragma warning restore CA1416
#else
            NSUrlBookmarkCreationOptions options = default;
#endif
            NSData bookmark = url.CreateBookmarkData(
                options,
                [],
                null,
                out NSError? error);
            if (error is not null)
            {
                throw new NSErrorException(error);
            }

            File.WriteAllBytes(GetGrantPath(id), [.. bookmark]);
        }

    }

    internal static NSUrl ResolveGrant(string id)
    {
        NSData bookmark = NSData.FromArray(File.ReadAllBytes(GetGrantPath(id)));
#if __MACCATALYST__
#pragma warning disable CA1416 // The Apple SDK declares this option available on Mac Catalyst 13.0 and later.
        NSUrlBookmarkResolutionOptions options = NSUrlBookmarkResolutionOptions.WithSecurityScope;
#pragma warning restore CA1416
#else
        NSUrlBookmarkResolutionOptions options = default;
#endif
        NSUrl url = NSUrl.FromBookmarkData(
            bookmark,
            options,
            null,
            out bool isStale,
            out NSError? error);
        if (error is not null)
        {
            throw new NSErrorException(error);
        }

        if (isStale)
        {
            url.SaveGrant(id);
        }

        return url;
    }

    /// <summary>
    /// Deletes the bookmarks kept for any ids other than those given.
    /// </summary>
    internal static void ReleaseGrantsExcept(IEnumerable<string> ids)
    {
        if (!Directory.Exists(GrantDirectory))
        {
            return;
        }

        HashSet<string> kept = [.. ids.Select(GetGrantPath)];
        foreach (string path in Directory.EnumerateFiles(GrantDirectory).Where(path => !kept.Contains(path)))
        {
            File.Delete(path);
        }
    }

    internal static string GetGrantPath(string id)
    {
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)));
        return Path.Combine(GrantDirectory, key);
    }

    /// <summary>
    /// Opens the file for reading through a file coordinator, which has the file provider make it available first.
    /// Some providers, such as Google Drive, do not keep a file at its path until it is asked for this way. The wait,
    /// which may include a download, is asynchronous, and the accessor runs on a background queue with the url the
    /// file is at then. An open stream remains valid after the coordinated read has ended. An exception may not pass
    /// through the native coordinator, so it is caught in the accessor and set as the result instead.
    /// </summary>
    private static Task<Stream> OpenCoordinatedReadAsync(NSUrl fileUrl)
    {
        TaskCompletionSource<Stream> opened = new(TaskCreationOptions.RunContinuationsAsynchronously);
        NSFileAccessIntent intent = NSFileAccessIntent.CreateReadingIntent(fileUrl, default);
        NSFileCoordinator coordinator = new();
        coordinator.CoordinateAccess([intent], new NSOperationQueue(), error =>
        {
            try
            {
                if (error is not null)
                {
                    opened.SetException(new NSErrorException(error));
                    return;
                }

                opened.SetResult(File.OpenRead(intent.Url.Path!));
            }
            catch (Exception ex)
            {
                opened.SetException(ex);
            }
            finally
            {
                // The coordinator is kept until the access it coordinates is done.
                GC.KeepAlive(coordinator);
            }
        });
        return opened.Task;
    }

    internal static NSUrl? TryResolveGrant(string id)
    {
        try
        {
            return File.Exists(GetGrantPath(id)) ? ResolveGrant(id) : null;
        }
        catch (NSErrorException)
        {
            return null;
        }
    }

    private sealed class PickerDelegate : UIDocumentPickerDelegate
    {
        public TaskCompletionSource<NSUrl?> Completion { get; } = new();

        public override void DidPickDocument(UIDocumentPickerViewController controller, NSUrl[] urls) =>
            Completion.SetResult(urls[0]);

        public override void WasCancelled(UIDocumentPickerViewController controller) =>
            Completion.SetResult(null);
    }

    private sealed class SecurityScopedStream(Stream stream, NSUrl accessUrl, bool isAccessing) : Stream
    {
        private bool _isAccessing = isAccessing;

        public override bool CanRead => stream.CanRead;

        public override bool CanSeek => stream.CanSeek;

        public override bool CanWrite => stream.CanWrite;

        public override long Length => stream.Length;

        public override long Position
        {
            get => stream.Position;
            set => stream.Position = value;
        }

        public override void Flush() => stream.Flush();

        public override int Read(byte[] buffer, int offset, int count) => stream.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => stream.Seek(offset, origin);

        public override void SetLength(long value) => stream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) => stream.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    try
                    {
                        stream.Dispose();
                    }
                    finally
                    {
                        if (_isAccessing)
                        {
                            accessUrl.StopAccessingSecurityScopedResource();
                            _isAccessing = false;
                        }
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
