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

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Database;
using Android.OS;
using Android.Provider;

using AndroidUri = global::Android.Net.Uri;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Services;
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Platforms.Android;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, Exported = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter([Intent.ActionSend], Categories = [Intent.CategoryDefault], DataMimeType = "text/plain")]
[IntentFilter([Intent.ActionSend], Categories = [Intent.CategoryDefault], DataMimeType = "application/octet-stream")]
[IntentFilter([Intent.ActionSend], Categories = [Intent.CategoryDefault], DataMimeType = "application/vnd.xecrets-encrypted")]
[IntentFilter([Intent.ActionSend], Categories = [Intent.CategoryDefault], DataMimeType = "*/*")]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataMimeType = "text/plain", DataScheme = "content")]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataMimeType = "application/octet-stream", DataScheme = "content")]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataMimeType = "application/vnd.xecrets-encrypted", DataScheme = "content")]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataMimeType = "*/*", DataScheme = "content")]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataMimeType = "text/plain", DataScheme = "file")]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataMimeType = "application/octet-stream", DataScheme = "file")]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataMimeType = "application/vnd.xecrets-encrypted", DataScheme = "file")]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataMimeType = "*/*", DataScheme = "file")]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // https://wagenheimer.com/blog/dont-let-android-15-break-your-maui-app-the-3-step-edge-to-edge-fix
        base.OnCreate(savedInstanceState);
        _ = HandleIncomingIntentAsync(Intent!);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        Intent = intent;
        _ = HandleIncomingIntentAsync(intent!);
    }

    private async Task HandleIncomingIntentAsync(Intent intent)
    {
        AndroidUri? uri = GetIncomingUri(intent);
        ContentResolver contentResolver = ContentResolver!;
        ITransientFileService transientFileStore = MauiProgram.Services!.GetRequiredService<ITransientFileService>();
        IIncomingFileService incomingFileService = MauiProgram.Services!.GetRequiredService<IIncomingFileService>();

        if (uri is null)
        {
            await HandleIncomingTextAsync(intent, transientFileStore, incomingFileService);
            return;
        }

        IFileService fileService = MauiProgram.Services!.GetRequiredService<IFileService>();
        if (fileService.IsSelfHandoffReference(uri.ToString()!))
        {
            IUserInterfaceService userInterfaceService = MauiProgram.Services!.GetRequiredService<IUserInterfaceService>();
            await userInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextSelfHandoffRejected);
            return;
        }

        string displayName = GetDisplayName(uri);
        string contentType = contentResolver.GetType(uri) ?? ContentTypeDetector.DetectContentType(displayName);
        string incomingPath = transientFileStore.CreateIncomingPath(displayName);

        Stream? inputStream = contentResolver.OpenInputStream(uri);
        if (inputStream is null)
        {
            return;
        }

        await using (inputStream)
        await using (FileStream outputStream = File.Open(incomingPath, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            await inputStream.CopyToAsync(outputStream);
        }

        await incomingFileService.ReceiveAsync(new IncomingFileInfo(incomingPath, displayName, contentType));
    }

    private static async Task HandleIncomingTextAsync(
        Intent intent,
        ITransientFileService transientFileStore,
        IIncomingFileService incomingFileService)
    {
        if (intent.Action != Intent.ActionSend)
        {
            return;
        }

        string? text = intent.GetStringExtra(Intent.ExtraText);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        const string displayName = "received.txt";
        string incomingPath = transientFileStore.CreateIncomingPath(displayName);
        await File.WriteAllTextAsync(incomingPath, text);
        await incomingFileService.ReceiveAsync(new IncomingFileInfo(incomingPath, displayName, "text/plain"));
    }

    private static AndroidUri? GetIncomingUri(Intent intent) => intent.Action == Intent.ActionView
            ? intent.Data
            : intent.Action == Intent.ActionSend
                ? GetStreamExtra(intent)
                : null;

    // Bundle.Get(string) is obsolete since API 33 in favor of the type-safe overload, but the type-safe overload
    // does not exist below API 33, and this app's minimum supported API level is 29.
    private static AndroidUri? GetStreamExtra(Intent intent) => OperatingSystem.IsAndroidVersionAtLeast(33)
            ? intent.GetParcelableExtra(Intent.ExtraStream, Java.Lang.Class.FromType(typeof(AndroidUri))) as AndroidUri
            : intent.Extras?.Get(Intent.ExtraStream) as AndroidUri;

    private string GetDisplayName(AndroidUri uri)
    {
        if (string.Equals(uri.Scheme, ContentResolver.SchemeContent, StringComparison.OrdinalIgnoreCase))
        {
            using ICursor? cursor = ContentResolver!.Query(uri, [IOpenableColumns.DisplayName], null, null, null);
            if (cursor is not null && cursor.MoveToFirst())
            {
                int columnIndex = cursor.GetColumnIndex(IOpenableColumns.DisplayName);
                if (columnIndex >= 0)
                {
                    string? displayName = cursor.GetString(columnIndex);
                    if (!string.IsNullOrWhiteSpace(displayName))
                    {
                        return displayName;
                    }
                }
            }
        }

        return Path.GetFileName(uri.Path) is { Length: > 0 } fileName
            ? fileName
            : "received.txt";
    }
}
