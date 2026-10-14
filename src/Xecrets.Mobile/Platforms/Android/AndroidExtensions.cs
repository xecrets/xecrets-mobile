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
    // Retried 4 times, 250 ms apart, which is up to a second.
    private const int _deleteRetries = 4;

    // Retried 12 times, 250 ms apart, which is up to three seconds.
    private const int _readableRetries = 12;

    private static readonly TimeSpan _retryInterval = TimeSpan.FromMilliseconds(250);

    private static readonly TimeSpan _providerTimeout = TimeSpan.FromSeconds(10);

    // A provider tells when it has loaded a folder, but not if it does so before being listened to, so a folder still
    // loading is also listed again after a while.
    private static readonly TimeSpan _loadingRecheckInterval = TimeSpan.FromSeconds(1);

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

    // The system Quick Viewer (e.g. com.android.documentsui) commonly registers ACTION_QUICK_VIEW with
    // mimeType="*/*", so HasExternalHandler(quickViewIntent) matches every content type even though the
    // viewer only actually renders this narrower set - anything outside it launches fine but shows its
    // own "Unable to preview file" screen.
    public static bool IsQuickViewContentType(this string contentType) =>
        contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) ||
        contentType.Equals("text/plain", StringComparison.OrdinalIgnoreCase) ||
        contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
        contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ||
        contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase);

    public static bool IsExternalStorage(this string? authority) => authority == "com.android.externalstorage.documents";

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
            return await call.WaitAsync(_providerTimeout);
        }
        catch (TimeoutException)
        {
            _ = call.ContinueWith(
                task => crashLogService.WriteSwallowedException(task.Exception!.InnerException!),
                TaskContinuationOptions.OnlyOnFaulted);
            throw;
        }
    }

    // Android's own table knows extensions that ContentTypeDetector does not, as file managers do.
    public static string OrAndroidContentType(this string contentType, string fileName) =>
        contentType == ContentTypeDetector.UnknownContentType
            ? MimeTypeMap.Singleton!.GetMimeTypeFromExtension(Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant()) ?? contentType
            : contentType;

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

        public Stream OpenInputStream() => ContentResolver.OpenInputStream(uri)!;

        public AndroidUri RenameDocument(string name) =>
            DocumentsContract.RenameDocument(ContentResolver, uri, name) ??
            throw new IOException("The document could not be renamed.");

        /// <summary>
        /// Deletes the document, off the main thread. Storage such as Dropbox may not yet delete a document it has only
        /// just created, so the deletion is retried for up to a second, after which the failure is thrown.
        /// </summary>
        public async Task DeleteDocumentAsync()
        {
            for (int retry = 0; ; retry++)
            {
                try
                {
                    if (await Task.Run(() => DocumentsContract.DeleteDocument(ContentResolver, uri)))
                    {
                        return;
                    }

                    throw new IOException("The file could not be deleted.");
                }
                catch (Exception ex) when (retry < _deleteRetries && (ex is IOException || ex.IsDocumentProviderFailure()))
                {
                    // Retried below.
                }

                await Task.Delay(_retryInterval);
            }
        }

        /// <summary>
        /// Waits, off the main thread, until the document can be opened for reading, for up to three seconds. Storage
        /// such as Dropbox does not yet know a document it has only just created, and then does not open it, may not
        /// delete it, and may never finish renaming it. Returns whether it could be opened.
        /// </summary>
        public async Task<bool> WaitUntilReadableAsync()
        {
            for (int retry = 0; ; retry++)
            {
                try
                {
                    Stream? input = await Task.Run(() => ContentResolver.OpenInputStream(uri));
                    if (input is not null)
                    {
                        await input.DisposeAsync();
                        return true;
                    }
                }
                catch (Exception ex) when (ex is IOException || ex.IsDocumentProviderFailure())
                {
                    // Not known yet, retried below.
                }

                if (retry == _readableRetries)
                {
                    return false;
                }

                await Task.Delay(_retryInterval);
            }
        }

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
        public async Task<IReadOnlyList<DocumentChild>> ListChildrenAsync(string folderDocumentId)
        {
            AndroidUri childrenUri = DocumentsContract.BuildChildDocumentsUriUsingTree(uri, folderDocumentId)!;
            while (true)
            {
                if (await TryListChildrenAsync(childrenUri) is { } children)
                {
                    return children;
                }
            }
        }
    }

    // Returns null when the provider is still loading the folder, after it has told that it has loaded, or after a
    // while.
    private static async Task<IReadOnlyList<DocumentChild>?> TryListChildrenAsync(AndroidUri childrenUri)
    {
        using ICursor cursor = await Task.Run(() => ContentResolver.Query(
            childrenUri,
            [
                DocumentsContract.Document.ColumnDocumentId,
                DocumentsContract.Document.ColumnDisplayName,
                DocumentsContract.Document.ColumnMimeType,
            ],
            null, null, null)) ?? throw new IOException("The folder could not be listed.");
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
                    await Task.WhenAny(observer.Changed, Task.Delay(_loadingRecheckInterval));
                }
                finally
                {
                    cursor.UnregisterContentObserver(observer);
                }

                return null;
            }

            return await Task.Run(() =>
            {
                List<DocumentChild> children = [];
                while (cursor.MoveToNext())
                {
                    children.Add(new DocumentChild(cursor.GetString(0)!, cursor.GetString(1) ?? "", cursor.GetString(2)));
                }

                return children;
            });
        }
        finally
        {
            await Task.Run(cursor.Close); // Dispose does not call Java close()
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

    extension(AndroidUri uri)
    {

        public string? GetDisplayName()
        {
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
}
