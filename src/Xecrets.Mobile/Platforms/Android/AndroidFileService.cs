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

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Services;
using Xecrets.Mobile.Models.Utilities;
using Xecrets.Mobile.Services;

using AndroidFile = Java.IO.File;
using AndroidUri = Android.Net.Uri;

using Platform = Microsoft.Maui.ApplicationModel.Platform;

namespace Xecrets.Mobile.Platforms.Android;

[SupportedOSPlatform("android26.0")]
public class AndroidFileService(IPickedWritableFileFactory pickedWritableFileFactory) : FileServiceBase
{
    public override string PlatformId => "android";

    public override async Task<IPickedWritableFile?> PickWritableFileAsync(string pickerTitle, FilePickerKind pickerKind)
    {
        Intent intent = new(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("*/*");
        if (pickerKind == FilePickerKind.Encrypted)
        {
            intent.PutExtra(Intent.ExtraMimeTypes, [EncryptedFileType.ContentType, "application/octet-stream"]);
        }
        intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);

        Intent? result = await ((MainActivity)Platform.CurrentActivity!).StartDocumentPickerAsync(intent);
        AndroidUri? selectedUri = result?.Data;
        if (selectedUri is null)
        {
            return null;
        }

        return pickedWritableFileFactory.Create(selectedUri);
    }

    // View is only ever the on-device Quick Viewer - handing a file to a real installed app is what
    // OpenInAsync/the "Open In..." button already does, so View must not overlap with it.
    public override Task<bool> CanViewFileAsync(DecryptedFileInfo file)
    {
        if (TryGetQuickViewIntent(file, out Intent? quickViewIntent))
        {
            quickViewIntent.Dispose();
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public override Task ViewFileAsync(DecryptedFileInfo file)
    {
        GetUri(file.FilePath).CreateQuickViewIntent(file.ContentType).StartQuickView();
        return Task.CompletedTask;
    }

    private static AndroidUri GetUri(string filePath) =>
        FileProvider.GetUriForFile(Platform.AppContext, $"{Platform.AppContext.PackageName}.fileProvider", new AndroidFile(filePath))!;

    private static bool TryGetQuickViewIntent(DecryptedFileInfo file, [NotNullWhen(true)] out Intent? quickViewIntent)
    {
        quickViewIntent = null;

        if (!file.ContentType.IsQuickViewContentType())
        {
            return false;
        }

        Intent intent = GetUri(file.FilePath).CreateQuickViewIntent(file.ContentType);
        if (intent.HasExternalHandler())
        {
            quickViewIntent = intent;
            return true;
        }

        intent.Dispose();
        return false;
    }

    public override Task<bool> OpenInAsync(string filePath, string displayName)
    {
        (AndroidUri uri, string resolvedContentType) = PrepareHandoff(filePath, displayName, string.Empty);

        using Intent viewIntent = new(Intent.ActionView);
        viewIntent.SetDataAndType(uri, resolvedContentType);
        viewIntent.AddFlags(ActivityFlags.GrantReadUriPermission);

        return Task.FromResult(viewIntent.TryStartExternalChooser(displayName));
    }

    public override Task SendToAsync(string filePath, string displayName, string contentType)
    {
        (AndroidUri uri, string resolvedContentType) = PrepareHandoff(filePath, displayName, contentType);

        using Intent sendIntent = new(Intent.ActionSend);
        sendIntent.SetType(resolvedContentType);
        sendIntent.PutExtra(Intent.ExtraStream, uri);
        sendIntent.AddFlags(ActivityFlags.GrantReadUriPermission);

        sendIntent.TryStartExternalChooser(displayName);
        return Task.CompletedTask;
    }

    private static (AndroidUri Uri, string ContentType) PrepareHandoff(
        string filePath,
        string displayName,
        string contentType)
    {
        EnsureReadableFile(filePath);

        string resolvedContentType = string.IsNullOrWhiteSpace(contentType)
            ? ContentTypeDetector.DetectContentType(displayName)
            : contentType;
        AndroidUri uri = FileProvider.GetUriForFile(
            Platform.AppContext,
            $"{Platform.AppContext.PackageName}.fileProvider",
            new AndroidFile(filePath))!;

        return (uri, resolvedContentType);
    }

    public override bool IsSelfHandoffReference(string reference) =>
        AndroidUri.Parse(reference)?.Authority == $"{Platform.AppContext.PackageName}.fileProvider";
}
