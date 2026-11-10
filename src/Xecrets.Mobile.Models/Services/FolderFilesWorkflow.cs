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

using Xecrets.Common.Models;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Utilities;
using Xecrets.Texts;

namespace Xecrets.Mobile.Models.Services;

/// <summary>
/// Encrypts, decrypts or deletes the files of a folder the user picks, one at a time, as if each was chosen by itself.
/// The folder is reached through My folders, as a file is. The folders are listed once, and the user confirms how many
/// files and how much it is, unless there are none, which the summary then tells. The listing is used throughout the
/// operation, so that no folder is listed again. A file whose contents show it is not what the operation is for, such
/// as one already encrypted, is passed over. The first file that is not done stops the others, with what the user is
/// told for a file by itself. A file that needs a password the app does not know is done once the user enters one that
/// decrypts it.
/// </summary>
public sealed class FolderFilesWorkflow(
    IFileAccess fileAccess,
    FolderAccessWorkflow folderAccess,
    FileOperationWorkflow fileOperations,
    IUserInterfaceService userInterfaceService)
{
    public async Task RunAsync(FolderFilesRequest request)
    {
        WorkFolder? folder = await folderAccess.PickWritableFolderAsync(
            request.StartFolder.Id,
            GetPickerKind(request.Operation));
        if (folder is null)
        {
            return;
        }

        IReadOnlyList<FolderListing> listings = await fileAccess.ListFolderAsync(folder.Id, request.Scope);
        FolderFile[] files =
        [
            .. listings
                .SelectMany(listing => listing.Files)
                .Where(folderFile => IsIncluded(request.Operation, folderFile.File)),
        ];
        if (files.Length > 0 && !await ConfirmAsync(request.Operation, files))
        {
            return;
        }

        await fileOperations.RunAsync(
            [.. files.Select(folderFile => folderFile.File)],
            request.Operation,
            FlowOrigin.MyFolders,
            isElsewhere: false,
            new FolderContents(fileAccess, listings),
            new FolderRunCompletionReporter(
                userInterfaceService,
                GetCountFormat(request.Operation)));
    }

    private Task<bool> ConfirmAsync(FileOperation operation, FolderFile[] files)
    {
        string countAndSize = string.Format(
            MobileTexts.DialogTextFileCountAndSizeFormat,
            files.Length.ToCountText(),
            files.Sum(folderFile => folderFile.Size).ToSizeText());
        string confirmation = $"{GetConfirmationText(operation)} {countAndSize}";
        // Also where other storage may keep versions or a trash, since the files are still gone from the folder.
        if (operation == FileOperation.Delete)
        {
            confirmation = $"{confirmation} {MobileTexts.MessageTextConfirmWipe}";
        }

        return userInterfaceService.DisplayConfirmationAsync(
            confirmation,
            MobileTexts.ButtonOk,
            MobileTexts.LabelCancel);
    }

    // As for a file chosen by itself, its name tells if it is encrypted.
    private static bool IsIncluded(FileOperation operation, FileReference file) => operation switch
    {
        FileOperation.Encrypt => !file.Name.IsNameEncrypted(),
        FileOperation.Decrypt => file.Name.IsNameEncrypted(),
        FileOperation.Delete => true,
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, @"Unknown folder files operation."),
    };

    // Only encrypted files are decrypted, so only they are shown where the files are shown while picking the folder.
    private static FilePickerKind GetPickerKind(FileOperation operation) => operation switch
    {
        FileOperation.Encrypt => FilePickerKind.Any,
        FileOperation.Decrypt => FilePickerKind.Encrypted,
        FileOperation.Delete => FilePickerKind.Any,
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, @"Unknown folder files operation."),
    };

    private static string GetConfirmationText(FileOperation operation) => operation switch
    {
        FileOperation.Encrypt => MobileTexts.DialogTextConfirmEncryptFiles,
        FileOperation.Decrypt => MobileTexts.DialogTextConfirmDecryptFiles,
        FileOperation.Delete => MobileTexts.DialogTextConfirmDeleteFiles,
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, @"Unknown folder files operation."),
    };

    private static string GetCountFormat(FileOperation operation) => operation switch
    {
        FileOperation.Encrypt => MobileTexts.DialogTextFilesEncryptedFormat,
        FileOperation.Decrypt => MobileTexts.DialogTextFilesDecryptedFormat,
        FileOperation.Delete => MobileTexts.DialogTextFilesDeletedFormat,
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, @"Unknown folder files operation."),
    };
}
