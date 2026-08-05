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
using System.Runtime.Versioning;
using System.Threading.Tasks;

using Android.Content;
using Android.Content.PM;
using Android.OS;

using AndroidX.Core.Content;

using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Services;

using Xecrets.Mobile.Services;

using AndroidFile = Java.IO.File;
using AndroidUri = Android.Net.Uri;
using Platform = Microsoft.Maui.ApplicationModel.Platform;

namespace Xecrets.Mobile.Platforms.Android;

[SupportedOSPlatform("android24.0")]
public class AndroidFileService : FileServiceBase
{
    public override string PlatformId => "android";

    public override Task<bool> CanViewFileAsync(DecryptedFileInfo file)
    {
        if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(false);
        }

        Context context = Platform.AppContext;
        AndroidUri uri = FileProvider.GetUriForFile(
            context,
            $"{context.PackageName}.fileProvider",
            new AndroidFile(file.FilePath))!;
        using Intent intent = new(Intent.ActionView);
        intent.SetDataAndType(uri, file.ContentType);
        intent.AddFlags(ActivityFlags.GrantReadUriPermission);

        return Task.FromResult(HasExternalHandler(context, intent));
    }

    public override Task<bool> OpenInAsync(string filePath, string displayName, string contentType)
    {
        (Context context, AndroidUri uri, string resolvedContentType) = PrepareHandoff(filePath, displayName, contentType);

        using Intent viewIntent = new(Intent.ActionView);
        viewIntent.SetDataAndType(uri, resolvedContentType);
        viewIntent.AddFlags(ActivityFlags.GrantReadUriPermission);

        return Task.FromResult(TryStartExternalChooser(context, viewIntent, displayName));
    }

    public override Task SendToAsync(string filePath, string displayName, string contentType)
    {
        (Context context, AndroidUri uri, string resolvedContentType) = PrepareHandoff(filePath, displayName, contentType);

        using Intent sendIntent = new(Intent.ActionSend);
        sendIntent.SetType(resolvedContentType);
        sendIntent.PutExtra(Intent.ExtraStream, uri);
        sendIntent.AddFlags(ActivityFlags.GrantReadUriPermission);

        TryStartExternalChooser(context, sendIntent, displayName);
        return Task.CompletedTask;
    }

    private static (Context Context, AndroidUri Uri, string ContentType) PrepareHandoff(
        string filePath,
        string displayName,
        string contentType)
    {
        EnsureReadableFile(filePath);

        Context context = Platform.AppContext;
        string resolvedContentType = string.IsNullOrWhiteSpace(contentType)
            ? ContentTypeDetector.DetectContentType(displayName)
            : contentType;
        AndroidUri uri = FileProvider.GetUriForFile(
            context,
            $"{context.PackageName}.fileProvider",
            new AndroidFile(filePath))!;

        return (context, uri, resolvedContentType);
    }

    // Xecrets Ez hands off its own decrypted files via this same FileProvider authority (see
    // TransientFileService.CreateHandoffPath), so an incoming content Uri served by our own authority is always a file
    // we created ourselves, never a genuine share from another app - our FileProvider is not exported, so no other app
    // can mint a working Uri against it.
    public override bool IsSelfHandoffReference(string reference) =>
        AndroidUri.Parse(reference)?.Authority == $"{Platform.AppContext.PackageName}.fileProvider";

    // Xecrets Ez's own manifest intent-filters (content scheme, any mime type) make it a valid resolver for its own
    // outgoing View/Send requests, on top of any genuine external app. Excluding our own package here, and from the
    // chooser below, is what stops Xecrets from ever opening a file it just handed to "another app" back on itself.
    private static bool HasExternalHandler(Context context, Intent intent)
    {
        IList<ResolveInfo> activities = context.PackageManager!.QueryIntentActivities(
            intent,
            PackageInfoFlags.MatchDefaultOnly);
        foreach (ResolveInfo activity in activities)
        {
            if (activity.ActivityInfo?.PackageName != context.PackageName)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryStartExternalChooser(Context context, Intent intent, string title)
    {
        if (!HasExternalHandler(context, intent))
        {
            return false;
        }

        Intent chooser = Intent.CreateChooser(intent, title)!;
        chooser.PutParcelableArrayListExtra(
            Intent.ExtraExcludeComponents,
            new List<IParcelable> { new ComponentName(context, Java.Lang.Class.FromType(typeof(MainActivity))) });
        Platform.CurrentActivity!.StartActivity(chooser);
        return true;
    }
}
