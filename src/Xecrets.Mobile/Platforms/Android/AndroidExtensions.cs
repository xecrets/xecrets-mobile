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
using Android.Provider;

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

    public static Intent CreateQuickViewIntent(this AndroidUri uri, string contentType)
    {
        Intent intent = new(Intent.ActionQuickView);
        intent.SetDataAndTypeAndNormalize(uri, contentType);
        intent.AddFlags(ActivityFlags.GrantReadUriPermission);
        return intent;
    }

    extension(AndroidUri uri)
    {
        /// <summary>
        /// Writes a document with the given name in the folder through a temporary document, and returns the uri of the
        /// written document.
        /// </summary>
        public async Task<string> WriteDocumentAsync(string name, bool overwrite, Func<Stream, Task> writer)
        {
            string temporaryName = $".xecrets-{Guid.NewGuid():N}.tmp";
            AndroidUri temporaryUri = DocumentsContract.CreateDocument(
                ContentResolver,
                uri,
                "application/octet-stream",
                temporaryName)!;
            bool destinationCommitted = false;
            try
            {
                await using (Stream output = ContentResolver.OpenOutputStream(temporaryUri, "w")!)
                {
                    await writer(output);
                }

                AndroidUri? backupUri = overwrite
                    ? uri.FindChild(name)!.RenameDocument($".xecrets-{Guid.NewGuid():N}.bak")
                    : null;
                AndroidUri renamedUri;
                try
                {
                    renamedUri = temporaryUri.RenameDocument(name);
                    if (renamedUri.GetDisplayName() != name)
                    {
                        throw new IOException("The destination file name is already in use.");
                    }

                    destinationCommitted = true;
                }
                catch
                {
                    if (backupUri is not null)
                    {
                        _ = backupUri.RenameDocument(name);
                    }

                    throw;
                }

                if (backupUri is not null && !DocumentsContract.DeleteDocument(ContentResolver, backupUri))
                {
                    throw new IOException("The replaced destination file could not be removed.");
                }

                return renamedUri.ToString()!;
            }
            catch
            {
                if (!destinationCommitted)
                {
                    DocumentsContract.DeleteDocument(ContentResolver, temporaryUri);
                }

                throw;
            }
        }

        public Stream OpenInputStream() => ContentResolver.OpenInputStream(uri)!;

        public void DeleteDocument()
        {
            if (!DocumentsContract.DeleteDocument(ContentResolver, uri))
            {
                throw new IOException("The source file could not be deleted.");
            }
        }

        public AndroidUri RenameDocument(string name) =>
            DocumentsContract.RenameDocument(ContentResolver, uri, name) ??
            throw new IOException("The document could not be renamed.");

        public AndroidUri? FindChild(string name)
        {
            string documentId = DocumentsContract.GetDocumentId(uri)!;
            AndroidUri childrenUri = DocumentsContract.BuildChildDocumentsUriUsingTree(uri, documentId)!;
            using ICursor cursor = ContentResolver.Query(
                childrenUri,
                [DocumentsContract.Document.ColumnDocumentId, DocumentsContract.Document.ColumnDisplayName],
                null,
                null,
                null)!;

            AndroidUri? child = null;
            while (child is null && cursor.MoveToNext())
            {
                if (cursor.GetString(1) == name)
                {
                    child = DocumentsContract.BuildDocumentUriUsingTree(uri, cursor.GetString(0)!);
                }
            }

            cursor.Close(); // Dispose does not call Java close()
            return child;
        }

        public string GetDisplayName()
        {
            using ICursor cursor = ContentResolver.Query(
                uri,
                [DocumentsContract.Document.ColumnDisplayName],
                null, null, null)!;
            cursor.MoveToFirst();
            string displayName = cursor.GetString(0)!;
            cursor.Close();

            return displayName; // Dispose does not call Java close()
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
            quickViewIntent.PutExtra(Intent.ExtraQuickViewFeatures, [QuickViewConstants.FeatureView]);

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
