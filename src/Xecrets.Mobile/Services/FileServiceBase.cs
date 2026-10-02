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

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Services;

public abstract class FileServiceBase : IFileService
{
    public abstract string PlatformId { get; }

    public string AppDataDirectory => FileSystem.AppDataDirectory;

    public string CacheDirectory => Path.Combine(FileSystem.CacheDirectory, "XecretsEz");

    public virtual async Task<bool> OpenInAsync(string filePath, string displayName)
    {
        EnsureReadableFile(filePath);

        return await Launcher.Default.OpenAsync(new OpenFileRequest(displayName, new ReadOnlyFile(filePath)));
    }

    public virtual async Task SendToAsync(string filePath, string displayName, string contentType)
    {
        EnsureReadableFile(filePath);

        ShareFile shareFile = string.IsNullOrWhiteSpace(contentType)
            ? new ShareFile(filePath)
            : new ShareFile(filePath, contentType);

        await Share.Default.RequestAsync(
            new ShareFileRequest
            {
                Title = displayName,
                File = shareFile,
            });
    }

    public virtual Task<bool> CanViewFileAsync(DecryptedFileInfo file) => Task.FromResult(false);

    public virtual async Task ViewFileAsync(DecryptedFileInfo file) => await OpenInAsync(file.FilePath, file.DisplayName);

    // Default for platforms where an incoming file reference is a plain filesystem path (Windows, iOS,
    // MacCatalyst): Xecrets Ez hands off its own decrypted files under CacheDirectory/XecretsHandoff (see
    // TransientFileService.CreateHandoffPath), so a path there is always a file we created ourselves. Android
    // overrides this, since its incoming reference is a content Uri rather than a path.
    public virtual bool IsSelfHandoffReference(string reference) =>
        reference.StartsWith(Path.Combine(CacheDirectory, "XecretsHandoff"), StringComparison.OrdinalIgnoreCase);

    protected static void EnsureReadableFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(@"The file was not found.", filePath);
        }
    }
}
