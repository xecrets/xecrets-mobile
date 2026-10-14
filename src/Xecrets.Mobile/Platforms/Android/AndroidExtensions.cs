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
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

using Android.Content;
using Android.Content.PM;
using Android.Database;
using Android.OS;
using Android.Provider;
using Android.Webkit;

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Services;

using AndroidUri = Android.Net.Uri;

using Platform = Microsoft.Maui.ApplicationModel.Platform;

namespace Xecrets.Mobile.Platforms.Android;

/// <summary>
/// Android platform specific extensions.
/// </summary>
[SupportedOSPlatform("android26.0")]
internal static class AndroidExtensions
{
    private static ContentResolver ContentResolver => Platform.CurrentActivity!.ContentResolver!;

    /// <summary>
    /// Tells if the exception is how a document provider reports that it does not support an operation, cannot find
    /// the document, or does not give access to it.
    /// Finding the path of a document that is not under a tree grant, such
    /// as a file picked by itself, requires MANAGE_DOCUMENTS, which only the system has.
    /// </summary>
    public static bool IsDocumentProviderFailure(this Exception exception) =>
        exception is Java.IO.FileNotFoundException or
        Java.Lang.IllegalArgumentException or
        Java.Lang.SecurityException or
        Java.Lang.UnsupportedOperationException;

    /// <summary>
    /// Waits for a call to a document provider for up to ten seconds, since storage such as Dropbox may never finish
    /// renaming a document it has only just created. The call itself cannot be canceled, so should it fail after that,
    /// it is logged, rather than left unobserved.
    /// </summary>
    /// <exception cref="TimeoutException">The call did not finish in time.</exception>
    public static async Task<T> WaitForProviderAsync<T>(this Task<T> call, ICrashLogService crashLogService)
    {
        try
        {
            return await call.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (TimeoutException) when (!call.IsCompleted)
        {
            _ = call.ContinueWith(
                task => crashLogService.WriteSwallowedException(task.Exception!.GetBaseException()),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
            throw;
        }
    }

    extension(string contentType)
    {
        // The system Quick Viewer (e.g. com.android.documentsui) commonly registers ACTION_QUICK_VIEW with
        // mimeType="*/*", so HasExternalHandler(quickViewIntent) matches every content type even though the
        // viewer only actually renders this narrower set - anything outside it launches fine but shows its
        // own "Unable to preview file" screen.
        public bool IsQuickViewContentType() =>
        contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) ||
        contentType.Equals("text/plain", StringComparison.OrdinalIgnoreCase) ||
        contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
        contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ||
        contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase);

        // Android's own table knows extensions that ContentTypeDetector does not, as file managers do.
        public string OrAndroidContentType(string fileName) =>
            contentType == ContentTypeDetector.UnknownContentType
                ? MimeTypeMap.Singleton!.GetMimeTypeFromExtension(Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant()) ?? contentType
                : contentType;
    }

    extension(AndroidUri uri)
    {
        public Intent CreateQuickViewIntent(string contentType)
        {
            Intent intent = new(Intent.ActionQuickView);
            intent.SetDataAndTypeAndNormalize(uri, contentType);

            // Google Drive's viewer honors EXTRA_QUICK_VIEW_FEATURES, which hides its "Insert" button, only for
            // ClipData uris.
            intent.ClipData = ClipData.NewRawUri(string.Empty, uri);
            intent.AddFlags(ActivityFlags.GrantReadUriPermission);
            return intent;
        }

        public bool IsExternalStorage() => uri.Authority == "com.android.externalstorage.documents";

        public Stream OpenInputStream() => ContentResolver.OpenInputStream(uri)!;

        public AndroidUri RenameDocument(string name) =>
            DocumentsContract.RenameDocument(ContentResolver, uri, name) ??
            throw new IOException("The document could not be renamed.");

        /// <summary>
        /// Deletes the document, off the main thread. Storage such as Dropbox may not yet delete a document it has only
        /// just created, so the deletion is retried for up to a second. Returns whether it was deleted.
        /// </summary>
        public Task<bool> DeleteDocumentAsync() =>
            RetryAsync(() => DocumentsContract.DeleteDocument(ContentResolver, uri), TimeSpan.FromSeconds(1));

        /// <summary>
        /// Waits, off the main thread, until the document can be opened for reading, for up to three seconds. Storage
        /// such as Dropbox does not yet know a document it has only just created, and then does not open it, may not
        /// delete it, and may never finish renaming it. Returns whether it could be opened.
        /// </summary>
        public Task<bool> WaitUntilReadableAsync() =>
            RetryAsync(() =>
                {
                    using Stream? input = ContentResolver.OpenInputStream(uri);
                    return input is not null;
                }, TimeSpan.FromSeconds(3));

        /// <summary>
        /// Finds the document with the name in the folder, once the provider has listed all of it.
        /// </summary>
        public async Task<AndroidUri?> FindChildAsync(string name) =>
            (await uri.ListChildrenAsync(DocumentsContract.GetDocumentId(uri)!))
                .FirstOrDefault(child => child.Name == name) is { } found
                ? DocumentsContract.BuildDocumentUriUsingTree(uri, found.Id)
                : null;

        /// <summary>
        /// Lists the documents in a folder, through the tree grant the uri is built on, off the main thread. A provider
        /// still loading the folder, such as from the cloud, tells so with the listing, and notifies when it has loaded,
        /// after which the folder is listed again, as the system document picker does. A provider that could not load
        /// it tells why, which is thrown, since the folder is then not known to be listed completely. There is no
        /// timeout, as reaching the storage is up to the provider.
        /// </summary>
        public Task<IReadOnlyList<DocumentChild>> ListChildrenAsync(string folderDocumentId) =>
            Task.Run(async () =>
            {
                AndroidUri childrenUri = DocumentsContract.BuildChildDocumentsUriUsingTree(uri, folderDocumentId)!;
                while (true)
                {
                    if (await ListChildrenAsync(childrenUri) is { } children)
                    {
                        return children;
                    }
                }
            });

        public string? GetDisplayName()
        {
            ThreadGuard.ThrowIfMainThread();

            using ICursor? cursor = ContentResolver.Query(
                uri,
                [DocumentsContract.Document.ColumnDisplayName],
                null, null, null)!;

            if (cursor is null)
            {
                return null;
            }

            cursor.MoveToFirst();
            string displayName = cursor.GetString(0)!;
            cursor.Close(); // Dispose does not call Java close()

            return displayName;
        }
    }

    extension(Intent quickViewIntent)
    {
        public void StartQuickView()
        {
            ComponentName? resolved = quickViewIntent.ResolveActivity(Platform.AppContext.PackageManager!);
            if (resolved != null)
            {
                quickViewIntent.SetPackage(resolved.PackageName);
            }

            // No features, so that the viewer offers no extra actions. Google Drive's viewer would show "Open with"
            // for FeatureView, which hands the file back to this app too.
            quickViewIntent.PutExtra(Intent.ExtraQuickViewFeatures, Array.Empty<string>());

            Platform.CurrentActivity!.StartActivity(quickViewIntent);
        }

        public bool HasExternalHandler() => quickViewIntent.GetExternalHandlers().Count > 0;

        public bool TryStartExternalChooser(string title)
        {
            if (!quickViewIntent.HasExternalHandler())
            {
                return false;
            }

            quickViewIntent.StartExternalChooser(title);
            return true;
        }

        /// <summary>
        /// Starts the app the user has chosen to always use for the intent, or the only app that handles it, as the
        /// system does when a file is tapped in a file manager. Otherwise, lets the user choose. The system's own
        /// chooser with "Always" cannot be used, since it would also offer Xecrets Ez.
        /// </summary>
        public bool TryStartPreferredOrChooser(string title)
        {
            List<ActivityInfo> handlers = quickViewIntent.GetExternalHandlers();
            if (handlers.Count == 0)
            {
                return false;
            }

            // Without a default, this resolves to the system's chooser activity, which is not one of the handlers.
            ActivityInfo? preferred = Platform.AppContext.PackageManager!
                .ResolveActivity(quickViewIntent, PackageInfoFlags.MatchDefaultOnly)?.ActivityInfo;
            ActivityInfo? target = handlers.Count == 1
                ? handlers[0]
                : handlers.FirstOrDefault(handler => handler.PackageName == preferred?.PackageName && handler.Name == preferred?.Name);
            if (target is null)
            {
                quickViewIntent.StartExternalChooser(title);
                return true;
            }

            quickViewIntent.SetClassName(target.PackageName!, target.Name!);
            Platform.CurrentActivity!.StartActivity(quickViewIntent);
            return true;
        }

        private List<ActivityInfo> GetExternalHandlers()
        {
            IList<ResolveInfo> activities = Platform.AppContext.PackageManager!.QueryIntentActivities(
                quickViewIntent,
                PackageInfoFlags.MatchDefaultOnly);
            return [.. activities
                .Select(activity => activity.ActivityInfo)
                .OfType<ActivityInfo>()
                .Where(activity => activity.PackageName != Platform.AppContext.PackageName)];
        }

        private void StartExternalChooser(string title)
        {
            Intent chooser = Intent.CreateChooser(quickViewIntent, title)!;
            chooser.PutParcelableArrayListExtra(
                Intent.ExtraExcludeComponents,
                [new ComponentName(Platform.AppContext, Java.Lang.Class.FromType(typeof(MainActivity)))]);
            Platform.CurrentActivity!.StartActivity(chooser);
        }
    }

    // Runs the attempt off the main thread until it succeeds, retrying it the given number of times, since storage such
    // as Dropbox may not yet handle a document it has only just created. Returns whether it succeeded.
    private static async Task<bool> RetryAsync(Func<bool> attempt, TimeSpan timeout)
    {
        while (true)
        {
            try
            {
                if (await Task.Run(attempt))
                {
                    return true;
                }
            }
            catch (Exception ex) when (ex is IOException || ex.IsDocumentProviderFailure())
            {
                // Retry
            }

            timeout -= TimeSpan.FromMilliseconds(250);
            if (timeout <= TimeSpan.Zero)
            {
                return false;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }
    }

    // Returns null when the provider is still loading the folder, after it has told that it has loaded, or after a
    // while.
    private static async Task<IReadOnlyList<DocumentChild>?> ListChildrenAsync(AndroidUri childrenUri)
    {
        ThreadGuard.ThrowIfMainThread();

        using ICursor cursor = ContentResolver.Query(
            childrenUri,
            [
                DocumentsContract.Document.ColumnDocumentId,
                DocumentsContract.Document.ColumnDisplayName,
                DocumentsContract.Document.ColumnMimeType,
            ],
            null, null, null) ?? throw new IOException("The folder could not be listed.");

        try
        {
            Bundle? extras = cursor.Extras;
            if (extras?.GetString(DocumentsContract.ExtraError) is { Length: > 0 } error)
            {
                throw new IOException(error);
            }

            if (extras?.GetBoolean(DocumentsContract.ExtraLoading) == true)
            {
                using ChangeObserver observer = new();
                cursor.RegisterContentObserver(observer);
                try
                {
                    await Task.WhenAny(observer.Changed, Task.Delay(TimeSpan.FromMilliseconds(250)));
                }
                finally
                {
                    cursor.UnregisterContentObserver(observer);
                }

                return null;
            }

            List<DocumentChild> children = [];
            while (cursor.MoveToNext())
            {
                children.Add(new DocumentChild(cursor.GetString(0)!, cursor.GetString(1) ?? "", cursor.GetString(2)));
            }

            return children;
        }
        finally
        {
            cursor.Close(); // Dispose does not call Java close()
        }
    }

    private sealed class ChangeObserver : ContentObserver
    {
        private readonly TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Without a handler, the change is told on a binder thread, which only completes the task.
        public ChangeObserver()
            : base(null)
        {
        }

        public Task Changed => _changed.Task;

        public override void OnChange(bool selfChange) => _changed.TrySetResult();
    }
}
