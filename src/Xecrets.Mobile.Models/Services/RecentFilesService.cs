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

using Xecrets.Common.Abstractions;
using Xecrets.Common.Models;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Models.Services;

internal sealed class RecentFilesService(
    ProfileSession profileSession,
    IFlowContext flowContext,
    IFileAccess fileAccess)
    : IRecentFilesService
{
    private const int _maxRecentListLength = 50;

    public async Task<IReadOnlyList<RecentFile>> GetFilesAsync() =>
        [.. (await profileSession.UserStore!.LoadRecentFileOperationsAsync()).Value.Files];

    /// <summary>
    /// A file only read is listed with the access to it alone, which is kept for as long as it is listed. The grants
    /// no longer listed are released when the app starts, or the user signs out.
    /// <para>
    /// A file listed as changed is reached through one of My folders, which may not give access to it alone, so it
    /// stays listed as changed when it is then only read, and is just moved first.
    /// </para>
    /// <para>
    /// The same file may be referred to by different ids, such as by itself or through one of My folders, and is only
    /// listed once.
    /// </para>
    /// </summary>
    public async Task<bool> AddAsync(FileReference file, RecentFileOperation operation)
    {
        if (!operation.IsWriteClass())
        {
            RecentFile? listed = await FindListedAsync(file.Id);
            if (listed is not null && listed.Operation.IsWriteClass())
            {
                await EditFilesAsync(async files => [listed, .. await RemoveFileAsync(files, file.Id)]);
                return true;
            }

            if (!await fileAccess.KeepFileGrantAsync(file.Id))
            {
                return false;
            }
        }

        FileReference located = await fileAccess.LocateAgainAsync(file);
        await EditFilesAsync(async files =>
        [
            located.ToRecentFile(operation),
            .. (await RemoveFileAsync(files, file.Id)).Take(_maxRecentListLength - 1),
        ]);
        return true;
    }

    public async Task AddFlowSourceAsync(RecentFileOperation operation)
    {
        if (flowContext.Source is { } source)
        {
            await AddAsync(source, operation);
        }
    }

    public Task RemoveAsync(string fileId) =>
        EditFilesAsync(files => Task.FromResult<List<RecentFile>>([.. files.Where(file => file.Id != fileId)]));

    public Task RemoveFileAsync(string fileId) =>
        EditFilesAsync(files => RemoveFileAsync(files, fileId));

    private async Task<RecentFile?> FindListedAsync(string fileId)
    {
        foreach (RecentFile file in await GetFilesAsync())
        {
            if (await IsSameFileAsync(file.Id, fileId))
            {
                return file;
            }
        }

        return null;
    }

    private async Task<List<RecentFile>> RemoveFileAsync(List<RecentFile> files, string fileId)
    {
        List<RecentFile> kept = [];
        foreach (RecentFile file in files)
        {
            if (!await IsSameFileAsync(file.Id, fileId))
            {
                kept.Add(file);
            }
        }

        return kept;
    }

    // An id that cannot be made sense of, such as from an earlier version, is not taken to be the same file.
    private async Task<bool> IsSameFileAsync(string listedId, string fileId)
    {
        try
        {
            return await fileAccess.IsSameFileAsync(listedId, fileId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return listedId == fileId;
        }
    }

    // The file may already be listed by the new id, and is then only kept where it is listed first.
    public Task ReplaceIdAsync(string fileId, string newFileId) =>
        EditFilesAsync(files => Task.FromResult<List<RecentFile>>(
        [
            .. files
                .Select(file => file.Id == fileId
                    ? file with { Id = newFileId }
                    : file)
                .DistinctBy(file => file.Id),
        ]));

    private async Task EditFilesAsync(Func<List<RecentFile>, Task<List<RecentFile>>> edit)
    {
        await using IEditScope<RecentFileOperations> scope =
            (await profileSession.UserStore!.LoadRecentFileOperationsAsync()).BeginEdit();
        scope.Value.Files = await edit(scope.Value.Files);
    }
}
