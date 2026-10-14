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

internal sealed class EditSaveService(
    IPreviewService previewService,
    EncryptRequestFactory encryptRequestFactory,
    ICoreServices coreServices,
    IFileAccess fileAccess,
    FolderAccessWorkflow folderAccess,
    FileOperationWorkflow fileOperationWorkflow,
    IRecentFilesService recentFilesService,
    IFlowContext flowContext,
    ICrashLogService crashLogService)
    : IEditSaveService
{
    /// <summary>
    /// The source is kept under its backup name while the edits are saved under its own name, and is deleted once they
    /// are. Should saving fail, the source gets its own name back, so that only the edits are lost.
    /// </summary>
    public async Task<bool> SaveAsync(string text)
    {
        // The source is reached through one of My folders, as it was when editing started.
        FileReference source = flowContext.Source!;
        string folderId = await fileAccess.GetFolderOfFileAsync(source.Id);
        if (await fileAccess.FileExistsInFolderAsync(folderId, source.Name.ToBackupName()))
        {
            return false;
        }

        EncryptRequest request = await PrepareAsync(previewService.Current, text);
        string backupId = await fileAccess.RenameAsync(source.Id, source.Name.ToBackupName());
        string savedId;
        try
        {
            await using IFileWriteSession destination =
                await fileAccess.OpenWriteInFolderAsync(folderId, source.Name, overwrite: false);
            await EncryptPreparedAsync(previewService.Current, request, destination.OutputStream);
            savedId = await destination.CommitAsync();
        }
        catch
        {
            await TryRestoreAsync(backupId, source.Name);
            throw;
        }

        await TryDeleteBackupAsync(backupId);

        // The file is a new one, which on some storage gives it a new id, while the old id refers to the replaced
        // file. The saved file is the one saved over and listed from then on, in place of any entry for the
        // replaced file under any id, such as when it was viewed through a grant to the file itself.
        FileReference saved = source with { Id = savedId };
        flowContext.UpdateSource(saved);
        await recentFilesService.RemoveFileAsync(source.Id);
        await recentFilesService.AddFlowSourceAsync(RecentFileOperation.Edit);
        return true;
    }

    // The failure to save is what is reported, and the source is then left under its backup name.
    private async Task TryRestoreAsync(string backupId, string name)
    {
        try
        {
            await fileAccess.RenameAsync(backupId, name);
        }
        catch (Exception ex)
        {
            crashLogService.WriteSwallowedException(ex);
        }
    }

    // The edits are saved, so a backup that is left is only logged. It is encrypted, and named after the source.
    private async Task TryDeleteBackupAsync(string backupId)
    {
        try
        {
            IWritableFile backup = await fileAccess.OpenWriteAsync(backupId);
            using IDisposable access = backup.AcquireAccess();
            await backup.DeleteAsync();
        }
        catch (Exception ex)
        {
            crashLogService.WriteSwallowedException(ex);
        }
    }

    public async Task<bool> SaveCopyAsync(string text)
    {
        IPreviewState state = previewService.Current;
        EncryptRequest request = await PrepareAsync(state, text);
        await using MemoryStream encrypted = new();
        await EncryptPreparedAsync(state, request, encrypted);
        encrypted.Position = 0;

        string fileName = previewService.Current.OriginalFileName.ToEncryptedName(string.Empty);
        FileReference? savedCopy = await fileOperationWorkflow.SaveAsAsync(fileName, encrypted);

        if (savedCopy is null)
        {
            return false;
        }

        // The saved copy holds the edits, so it is the one saved over and listed from then on, if it was saved in
        // one of My folders. Otherwise it cannot be changed, and each save is a new copy.
        FileReference? reachable = await folderAccess.FindInFoldersAsync(savedCopy);
        if (reachable is null)
        {
            return true;
        }

        flowContext.UpdateSource(reachable);
        await recentFilesService.AddFlowSourceAsync(RecentFileOperation.Edit);
        return true;
    }

    /// <summary>
    /// Writes the text to the decrypted file being previewed, and prepares its encryption request.
    /// </summary>
    private async Task<EncryptRequest> PrepareAsync(IPreviewState state, string text)
    {
        if (state.File is not null)
        {
            TryClearReadOnly(state.File.FilePath);
            await File.WriteAllTextAsync(state.File.FilePath, text);
            TryMakeReadOnly(state.File.FilePath);

            state.UpdateText(text);
            state.UpdateFileSize(new FileInfo(state.File.FilePath).Length);
        }

        return encryptRequestFactory.ForCurrentProfile(state.OriginalFileName);
    }

    private async Task EncryptPreparedAsync(IPreviewState state, EncryptRequest request, Stream encrypted)
    {
        await using FileStream cleartext = File.OpenRead(state.DecryptedPath);
        await coreServices.EncryptAsync(cleartext, encrypted, request);
    }

    private void TryClearReadOnly(string filePath)
    {
        try
        {
            File.SetAttributes(filePath, File.GetAttributes(filePath) & ~FileAttributes.ReadOnly);
        }
        catch (Exception ex)
        {
            // Best effort. The file write will report the real failure if this matters.
            crashLogService.WriteSwallowedException(ex);
        }
    }

    private void TryMakeReadOnly(string filePath)
    {
        try
        {
            File.SetAttributes(filePath, File.GetAttributes(filePath) | FileAttributes.ReadOnly);
        }
        catch (Exception ex)
        {
            // Best effort. Some mobile filesystems do not support this attribute.
            crashLogService.WriteSwallowedException(ex);
        }
    }
}
