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

using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using System.Threading.Tasks;

using Android.Content;

using AndroidX.Core.Content;

using Xecrets.Core.Public;
using Xecrets.Mobile.Models;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Services;
using Xecrets.Mobile.Services;

using AndroidFile = Java.IO.File;
using AndroidUri = Android.Net.Uri;

using Platform = Microsoft.Maui.ApplicationModel.Platform;

namespace Xecrets.Mobile.Platforms.Android;

/// <summary>
/// The file service of Android, which hands files to other apps through content uris of a file provider.
/// </summary>
[SupportedOSPlatform("android26.0")]
internal class AndroidFileService : FileServiceBase
{
    /// <inheritdoc/>
    public override string PlatformId => "android";

    /// <inheritdoc/>
    /// <remarks>
    /// The viewer is only the Quick Viewer of the device, since handing a file to an installed app is what
    /// <see cref="OpenInAsync"/> does.
    /// </remarks>
    public override Task<bool> CanViewFileAsync(DecryptedFileInfo file)
    {
        if (TryGetQuickViewIntent(file, out Intent? quickViewIntent))
        {
            quickViewIntent.Dispose();
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Returns when the viewer is started. It reads the file through the grant, so the file is still in use.
    /// </remarks>
    public override Task<bool> ViewFileAsync(DecryptedFileInfo file)
    {
        ThreadGuard.ThrowIfNotMainThread();

        CreateQuickViewIntent(file, GetContentType(file)).StartQuickView();
        return Task.FromResult(true);
    }

    private static AndroidUri GetUri(string filePath) =>
        FileProvider.GetUriForFile(Platform.AppContext, QuickViewFileProvider.Authority, new AndroidFile(filePath))!;

    // Google Drive's video player reads the uri with a query appended, which only a prefix grant covers.
    private static Intent CreateQuickViewIntent(DecryptedFileInfo file, string contentType)
    {
        Intent intent = GetUri(file.FilePath).CreateQuickViewIntent(contentType);
        intent.AddFlags(ActivityFlags.GrantPrefixUriPermission);
        return intent;
    }

    private static string GetContentType(DecryptedFileInfo file) => file.ContentType.OrAndroidContentType(file.OriginalFileName);

    private static bool TryGetQuickViewIntent(DecryptedFileInfo file, [NotNullWhen(true)] out Intent? quickViewIntent)
    {
        quickViewIntent = null;

        string contentType = GetContentType(file);
        if (!contentType.IsQuickViewContentType())
        {
            return false;
        }

        Intent intent = CreateQuickViewIntent(file, contentType);
        if (intent.HasExternalHandler())
        {
            quickViewIntent = intent;
            return true;
        }

        intent.Dispose();
        return false;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The file is opened with a chooser of the apps that can view it.
    /// </remarks>
    public override Task<bool> OpenInAsync(string filePath, string displayName)
    {
        ThreadGuard.ThrowIfNotMainThread();

        (AndroidUri uri, string resolvedContentType) = PrepareHandoff(filePath, displayName, string.Empty);

        using Intent viewIntent = new(Intent.ActionView);
        viewIntent.SetDataAndType(uri, resolvedContentType);
        viewIntent.AddFlags(ActivityFlags.GrantReadUriPermission);

        return Task.FromResult(viewIntent.TryStartExternalChooser(displayName));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Returns when the chooser is started, and the chosen app reads the file through the grant, so the file is still
    /// in use.
    /// </remarks>
    public override Task<bool> SendToAsync(string filePath, string displayName, string contentType)
    {
        ThreadGuard.ThrowIfNotMainThread();

        (AndroidUri uri, string resolvedContentType) = PrepareHandoff(filePath, displayName, contentType);

        using Intent sendIntent = new(Intent.ActionSend);
        sendIntent.SetType(resolvedContentType);
        sendIntent.PutExtra(Intent.ExtraStream, uri);
        sendIntent.AddFlags(ActivityFlags.GrantReadUriPermission);

        return Task.FromResult(sendIntent.TryStartExternalChooser(displayName));
    }

    private static (AndroidUri Uri, string ContentType) PrepareHandoff(
        string filePath,
        string displayName,
        string contentType)
    {
        EnsureReadableFile(filePath);

        string resolvedContentType = (string.IsNullOrWhiteSpace(contentType)
            ? displayName.DetectContentType()
            : contentType).OrAndroidContentType(displayName);
        AndroidUri uri = FileProvider.GetUriForFile(
            Platform.AppContext,
            $"{Platform.AppContext.PackageName}.fileProvider",
            new AndroidFile(filePath))!;

        return (uri, resolvedContentType);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The reference is a content uri, which is one the app handed off when it is of one of the file providers of the
    /// app.
    /// </remarks>
    public override bool IsSelfHandoffReference(string reference) =>
        AndroidUri.Parse(reference)?.Authority is string authority &&
        (authority == $"{Platform.AppContext.PackageName}.fileProvider" || authority == QuickViewFileProvider.Authority);
}
