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

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Models.Services;

/// <summary>
/// A save of a result in one of My folders is the only time a file of the user can be left changed in part, since it
/// is written in place, and the app may be ended while it is. The file saved in part is then removed when the user
/// next signs in, but only while the source is there, so that what the user had is never lost. Removing a result that
/// was saved after all is no loss either, since the source is only removed once the save is known to be done.
/// </summary>
internal sealed class InterruptedSaveService(
    ISaveInProgressStore store,
    IFileAccess fileAccess,
    ProfileSession profileSession,
    ICrashLogService crashLogService,
    IUserInterfaceService userInterfaceService) : IInterruptedSaveService
{
    public async Task<string> SaveAsync(SaveInProgress save, string completedFilePath, string? existingId)
    {
        await store.WriteSaveInProgressAsync(save with { UserId = CurrentUserId });
        try
        {
            return await fileAccess.SaveInFolderAsync(save.FolderId, save.Name, completedFilePath, existingId);
        }
        finally
        {
            // A save that fails removes what it saved in part itself.
            await store.ClearSaveInProgressAsync();
        }
    }

    /// <summary>
    /// A save of another profile is left for when it signs in, since it is through its My folders that the files are
    /// reached. A save that cannot be undone, such as when its folder or the source cannot be reached, is only told
    /// about, and not tried again.
    /// </summary>
    public async Task RecoverAsync()
    {
        if (await store.ReadSaveInProgressAsync() is not { } save || save.UserId != CurrentUserId)
        {
            return;
        }

        bool isChanged;
        try
        {
            isChanged = await UndoAsync(save);
        }
        catch (Exception ex)
        {
            crashLogService.WriteSwallowedException(ex);
            isChanged = true;
        }

        await store.ClearSaveInProgressAsync();
        if (isChanged)
        {
            await userInterfaceService.DisplayMessageAsync(
                string.Format(MobileTexts.DialogTextInterruptedSaveFormat, save.Name));
        }
    }

    private string CurrentUserId => profileSession.UserStore!.Id.Value;

    // Throws when the source cannot be reached, so that nothing is removed without it. Returns whether the user's files
    // may differ from what the user expects, which is not so when the save had not begun writing.
    private async Task<bool> UndoAsync(SaveInProgress save)
    {
        await fileAccess.CheckReachableAsync(save.SourceId);
        FolderListing listing = (await fileAccess.ListFolderAsync(save.FolderId, FolderScope.Folder)).Single();
        string? savedId = listing.IdsByName.GetValueOrDefault(save.Name);
        if (savedId is not null)
        {
            await RemoveAsync(savedId);
        }

        if (save.RestoresSource)
        {
            await fileAccess.RenameAsync(save.SourceId, save.Name);
            return true;
        }

        return savedId is not null;
    }

    private async Task RemoveAsync(string fileId)
    {
        IWritableFile file = await fileAccess.OpenWriteAsync(fileId);
        using IDisposable access = file.AcquireAccess();
        await file.DeleteAsync();
    }
}
