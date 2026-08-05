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
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Maui.Storage;

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Utilities;
using Xecrets.Texts;

namespace Xecrets.Mobile.Services;

public abstract class FileServiceBase : IFileService
{
    public abstract string PlatformId { get; }

    public string AppDataDirectory => FileSystem.AppDataDirectory;

    public string CacheDirectory => FileSystem.CacheDirectory;

    public async Task<PickedFile?> PickFileAsync(string pickerTitle, FilePickerKind pickerKind)
    {
        FileResult? file = await FilePicker.Default.PickAsync(
            new PickOptions
            {
                PickerTitle = pickerTitle,
                FileTypes = CreateFileTypes(pickerKind),
            });

        return file is null
            ? null
            : new PickedFile(file.FileName, file.FullPath, file.OpenReadAsync);
    }

    private static FilePickerFileType? CreateFileTypes(FilePickerKind pickerKind)
        => pickerKind switch
        {
            FilePickerKind.Any => null,
            FilePickerKind.Encrypted => new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.Android, ["application/vnd.xecrets-encrypted", "application/octet-stream"] },
                    { DevicePlatform.iOS, ["com.axantum.xecrets-file"] },
                    { DevicePlatform.MacCatalyst, ["com.axantum.xecrets-file"] },
                    { DevicePlatform.WinUI, [Extensions.EncryptedExtension] },
                }),
            _ => throw new ArgumentOutOfRangeException(nameof(pickerKind)),
        };

    public virtual async Task<bool> OpenInAsync(string filePath, string displayName, string contentType)
    {
        EnsureReadableFile(filePath);

        return await Launcher.Default.OpenAsync(
            new OpenFileRequest(
                displayName,
                string.IsNullOrWhiteSpace(contentType)
                    ? new ReadOnlyFile(filePath)
                    : new ReadOnlyFile(filePath, contentType)));
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

    public async Task<SaveFileResult> SaveAsAsync(Stream stream, string displayName, string originalSourcePath)
    {
        string initialDirectory = ResolveDefaultSaveLocation(originalSourcePath);
        FileSaverResult result = await FileSaver.Default.SaveAsync(
            initialDirectory,
            displayName,
            stream,
            CancellationToken.None);
        if (result.IsCancelled)
        {
            return new SaveFileResult(true, result.FilePath);
        }

        if (!result.IsSuccessful)
        {
            throw result.Exception ?? new InvalidOperationException(MobileTexts.DialogTextSaveAsFailed);
        }

        return new SaveFileResult(false, result.FilePath);
    }

    protected virtual string ResolveDefaultSaveLocation(string? originalFilePath)
    {
        if (!string.IsNullOrWhiteSpace(originalFilePath))
        {
            string? directory = Path.GetDirectoryName(originalFilePath);
            if (!string.IsNullOrWhiteSpace(directory) && !IsInternalTemporaryLocation(directory))
            {
                return directory;
            }
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    public virtual Task<bool> CanViewFileAsync(DecryptedFileInfo file) => Task.FromResult(false);

    public virtual async Task ViewFileAsync(DecryptedFileInfo file)
    {
        bool opened = await OpenInAsync(file.FilePath, file.DisplayName, string.Empty);
        if (!opened)
        {
            throw new InvalidOperationException(MobileTexts.DialogTextNoAppAvailable);
        }
    }

    // Default for platforms where an incoming file reference is a plain filesystem path (Windows, iOS,
    // MacCatalyst): Xecrets Ez hands off its own decrypted files under CacheDirectory/XecretsHandoff (see
    // TransientFileService.CreateHandoffPath), so a path there is always a file we created ourselves. Android
    // overrides this, since its incoming reference is a content Uri rather than a path.
    public virtual bool IsSelfHandoffReference(string reference) =>
        reference.StartsWith(Path.Combine(CacheDirectory, "XecretsHandoff"), StringComparison.OrdinalIgnoreCase);

    protected static void EnsureReadableFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            throw new FileNotFoundException(MobileTexts.DialogTextTemporaryFileUnavailable, filePath);
        }
    }

    private bool IsInternalTemporaryLocation(string directory)
        => directory.StartsWith(CacheDirectory, StringComparison.OrdinalIgnoreCase);
}
