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

using System.Text;

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
    IInterruptedSaveService interruptedSaves,
    FolderAccessWorkflow folderAccess,
    FileOperationWorkflow fileOperationWorkflow,
    IRecentFilesService recentFilesService,
    ITransientFileService transientFileService,
    IFlowContext flowContext,
    ICrashLogService crashLogService)
    : IEditSaveService
{
    /// <summary>
    /// The edits are encrypted to a temporary file first, which is wiped once saved, or not. The source is then kept
    /// under its backup name while the edits are saved under its own name, and is deleted once they are. Should saving
    /// fail, the source gets its own name back, so that only the edits are lost, which is also done when the user next
    /// signs in, should the app be ended while saving.
    /// </summary>
    public async Task<bool> SaveAsync(string text)
    {
        // The source is reached through one of My folders, as it was when editing started.
        FileReference source = flowContext.Source!;
        ListedFolder folder = await new FolderContents(fileAccess, []).GetFolderOfAsync(source);
        if (folder.FindId(source.Name.ToBackupName()) is not null)
        {
            return false;
        }

        byte[] plaintext = new UTF8Encoding(false).GetBytes(text);
        ITransientFile encrypted = await EncryptToTransientAsync(previewService.Current, plaintext);
        string savedId;
        try
        {
            string backupId = await fileAccess.RenameAsync(source.Id, source.Name.ToBackupName());
            try
            {
                savedId = await interruptedSaves.SaveAsync(
                    new SaveInProgress
                    {
                        FolderId = folder.Id,
                        Name = source.Name,
                        SourceId = backupId,
                        RestoresSource = true,
                    },
                    encrypted.FilePath,
                    existingId: null);
            }
            catch
            {
                await TryRestoreAsync(backupId, source.Name);
                throw;
            }

            await TryDeleteBackupAsync(backupId);
        }
        finally
        {
            encrypted.Dispose();
        }

        previewService.Current.UpdateText(plaintext);

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
        byte[] plaintext = new UTF8Encoding(false).GetBytes(text);
        ITransientFile encrypted = await EncryptToTransientAsync(previewService.Current, plaintext);
        FileReference? savedCopy;
        try
        {
            string fileName = previewService.Current.File.OriginalFileName.ToEncryptedName("");
            savedCopy = await fileOperationWorkflow.SaveAsAsync(fileName, encrypted.FilePath, FileTimes.UtcNow);
        }
        finally
        {
            encrypted.Dispose();
        }

        if (savedCopy is null)
        {
            return false;
        }

        previewService.Current.UpdateText(plaintext);

        // The saved copy holds the edits, so it is the one saved over and listed from then on, if it was saved in
        // one of My folders. Otherwise, it cannot be changed, and each save is a new copy.
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
    /// Encrypts the text from memory to a temporary file. The text is encrypted for all the file being previewed was
    /// encrypted for, and keeps the creation time of the original.
    /// </summary>
    private async Task<ITransientFile> EncryptToTransientAsync(IPreviewState state, byte[] plaintext)
    {
        EncryptRequest request = encryptRequestFactory.Create(state.File.OriginalFileName, state.EncryptedWith) with
        {
            CreationTimeUtc = state.File.OriginalTimes.CreationTimeUtc,
        };
        ITransientFile encryptedFile =
            transientFileService.CreateEncryptedOutputFile(state.File.OriginalFileName.ToEncryptedName(""));
        try
        {
            using MemoryStream cleartext = new(plaintext);
            await using FileStream encrypted = File.Create(encryptedFile.FilePath);
            await coreServices.EncryptAsync(cleartext, encrypted, request);
        }
        catch
        {
            // What was encrypted before the operation was canceled, or failed, is not left until the cache is cleaned.
            encryptedFile.Dispose();
            throw;
        }

        return encryptedFile;
    }
}
