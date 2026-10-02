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
using Xecrets.Core.Abstractions;
using Xecrets.Core.Models;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Texts;

namespace Xecrets.Mobile.Models.Services;

public sealed class EditSaveService(
    IPreviewService previewService,
    EncryptRequestFactory encryptRequestFactory,
    ICoreServices coreServices,
    IFileAccess fileAccess,
    FolderAccessWorkflow folderAccess,
    FileOperationWorkflow fileOperationWorkflow,
    IRecentFilesService recentFilesService,
    IFlowContext flowContext)
    : IEditSaveService
{
    public async Task SaveAsync(string text)
    {
        Func<Stream, Task> encryptTo = await PrepareAsync(text);

        // The source is reached through one of My folders, as it was when editing started.
        FileReference source = flowContext.Source!;
        string folderId = await fileAccess.GetFolderOfFileAsync(source.Id);
        string savedId = await fileAccess.WriteFileInFolderAsync(folderId, source.Name, overwrite: true, encryptTo);

        // The file is replaced, which on some storage gives it a new id, while the old id refers to the replaced
        // file. The saved file is the one saved over and listed from then on, in place of any entry for the
        // replaced file under any id, such as when it was viewed through a grant to the file itself.
        FileReference saved = source with { Id = savedId };
        flowContext.UpdateSource(saved);
        await recentFilesService.RemoveFileAsync(source.Id);
        await recentFilesService.AddFlowSourceAsync(RecentFileOperation.Edit);
    }

    public async Task SaveCopyAsync(string text)
    {
        Func<Stream, Task> encryptTo = await PrepareAsync(text);
        await using MemoryStream encrypted = new();
        await encryptTo(encrypted);
        encrypted.Position = 0;

        string fileName = previewService.Current.OriginalFileName.ToEncryptedName(string.Empty);
        FileReference? savedCopy = await fileOperationWorkflow.SaveAsAsync(fileName, encrypted);

        // The saved copy holds the edits, so it is the one saved over and listed from then on, if it was saved in
        // one of My folders. Otherwise it cannot be changed, and each save is a new copy.
        FileReference? reachable = savedCopy is null ? null : await folderAccess.FindInFoldersAsync(savedCopy);
        if (reachable is null)
        {
            return;
        }

        flowContext.UpdateSource(reachable);
        await recentFilesService.AddFlowSourceAsync(RecentFileOperation.Edit);
    }

    /// <summary>
    /// Writes the text to the decrypted file being previewed, and returns how to encrypt that file into a stream.
    /// </summary>
    private async Task<Func<Stream, Task>> PrepareAsync(string text)
    {
        IPreviewState state = previewService.Current;
        if (state.File is not null)
        {
            TryClearReadOnly(state.File.FilePath);
            await File.WriteAllTextAsync(state.File.FilePath, text);
            TryMakeReadOnly(state.File.FilePath);

            state.UpdateText(text);
            state.UpdateFileSize(new FileInfo(state.File.FilePath).Length);
        }

        EncryptRequest request = encryptRequestFactory.ForCurrentProfile(state.OriginalFileName);
        return async encrypted =>
        {
            await using FileStream cleartext = File.OpenRead(state.DecryptedPath);
            await coreServices.EncryptAsync(cleartext, encrypted, request);
        };
    }

    private static void TryClearReadOnly(string filePath)
    {
        try
        {
            File.SetAttributes(filePath, File.GetAttributes(filePath) & ~FileAttributes.ReadOnly);
        }
        catch
        {
            // Best effort. The file write will report the real failure if this matters.
        }
    }

    private static void TryMakeReadOnly(string filePath)
    {
        try
        {
            File.SetAttributes(filePath, File.GetAttributes(filePath) | FileAttributes.ReadOnly);
        }
        catch
        {
            // Best effort. Some mobile filesystems do not support this attribute.
        }
    }
}
