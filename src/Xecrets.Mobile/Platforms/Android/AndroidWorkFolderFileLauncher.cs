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

using System.Runtime.Versioning;
using System.Threading.Tasks;

using Android.Content;

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Services;
using Xecrets.Mobile.Models.Utilities;

using AndroidUri = Android.Net.Uri;

using Platform = Microsoft.Maui.ApplicationModel.Platform;

namespace Xecrets.Mobile.Platforms.Android;

/// <summary>
/// Hands the document itself to other apps, rather than a copy, through the access granted to its known folder.
/// </summary>
[SupportedOSPlatform("android26.0")]
public class AndroidWorkFolderFileLauncher : IWorkFolderFileLauncher
{
    /// <summary>
    /// As a file manager does, views content the system Quick Viewer handles in it, and opens anything else in the
    /// app of the user's choice. That app is also allowed to write, so that an edited file is saved in place.
    /// </summary>
    public Task<bool> OpenAsync(WorkFolderFile file)
    {
        AndroidUri uri = AndroidUri.Parse(file.Id)!;
        string contentType = GetContentType(uri, file.FileName);

        if (contentType.IsQuickViewContentType())
        {
            Intent quickViewIntent = uri.CreateQuickViewIntent(contentType);
            if (quickViewIntent.HasExternalHandler())
            {
                quickViewIntent.StartQuickView();
                return Task.FromResult(true);
            }

            quickViewIntent.Dispose();
        }

        using Intent viewIntent = new(Intent.ActionView);
        viewIntent.SetDataAndType(uri, contentType);
        viewIntent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);

        return Task.FromResult(viewIntent.TryStartPreferredOrChooser(file.FileName));
    }

    public Task ShareAsync(WorkFolderFile file)
    {
        using Intent sendIntent = new(Intent.ActionSend);
        sendIntent.SetType(EncryptedFileType.ContentType);
        sendIntent.PutExtra(Intent.ExtraStream, AndroidUri.Parse(file.Id));
        sendIntent.AddFlags(ActivityFlags.GrantReadUriPermission);

        sendIntent.TryStartExternalChooser(file.FileName);
        return Task.CompletedTask;
    }

    // The document provider knows the type best, but reports the generic type for names it does not recognize,
    // in which case we fallback to our own content type detector.
    private static string GetContentType(AndroidUri uri, string fileName)
    {
        string? contentType = Platform.AppContext.ContentResolver!.GetType(uri);
        return string.IsNullOrEmpty(contentType) || contentType == "application/octet-stream"
            ? ContentTypeDetector.DetectContentType(fileName)
            : contentType;
    }
}
