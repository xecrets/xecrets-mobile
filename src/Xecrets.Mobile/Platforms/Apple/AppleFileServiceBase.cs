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

using Foundation;

using UniformTypeIdentifiers;

using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Utilities;
using Xecrets.Mobile.Services;

namespace Xecrets.Mobile.Platforms.Apple;

public abstract class AppleFileServiceBase : FileServiceBase
{
    public override async Task<PickedWritableFile?> PickWritableFileAsync(string pickerTitle, FilePickerKind pickerKind)
    {
        UTType contentType = pickerKind == FilePickerKind.Encrypted
            ? UTType.CreateExportedType(EncryptedFileType.UniformTypeIdentifier)
            : UTTypes.Data;
        NSUrl? selectedUrl = await contentType.PickUrlAsync(null);
        if (selectedUrl is null)
        {
            return null;
        }

        NSUrl fileUrl = selectedUrl;
        return new PickedWritableFile(
            fileUrl.LastPathComponent!,
            action => WithAccessAsync(fileUrl, action),
            () => Task.FromResult(IsWritable(fileUrl)),
            () => Task.FromResult(IsWritable(fileUrl)),
            () => Task.FromResult(new FileInfo(fileUrl.Path!).Length),
            () => Task.FromResult<Stream>(new FileStream(fileUrl.Path!, FileMode.Open, FileAccess.Write, FileShare.None)),
            name =>
            {
                try
                {
                    string path = Path.Combine(fileUrl.RemoveLastPathComponent().Path!, name);
                    File.Move(fileUrl.Path!, path);
                    fileUrl = NSUrl.FromFilename(path);
                    return Task.FromResult(true);
                }
                catch (IOException)
                {
                    return Task.FromResult(false);
                }
                catch (UnauthorizedAccessException)
                {
                    return Task.FromResult(false);
                }
            },
            () =>
            {
                File.Delete(fileUrl.Path!);
                return Task.CompletedTask;
            });
    }

    private static async Task WithAccessAsync(NSUrl url, Func<Task> action)
    {
        bool isAccessing = url.StartAccessingSecurityScopedResource();
        if (!isAccessing)
        {
            throw new UnauthorizedAccessException("The selected file could not be accessed.");
        }

        try
        {
            await action();
        }
        finally
        {
            url.StopAccessingSecurityScopedResource();
        }
    }

    private static bool IsWritable(NSUrl url) =>
        url.TryGetResource(NSUrl.IsWritableKey, out NSObject value, out NSError _) && ((NSNumber)value).BoolValue;
}
