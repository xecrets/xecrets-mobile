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

public sealed class RecentFilesService(
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
    /// </summary>
    public async Task<bool> AddAsync(FileReference file, RecentFileOperation operation)
    {
        if (!operation.IsWriteClass())
        {
            RecentFile? listed = (await GetFilesAsync()).FirstOrDefault(item => item.Id == file.Id);
            if (listed is not null && listed.Operation.IsWriteClass())
            {
                await EditFilesAsync(files => [listed, .. files.Where(item => item.Id != file.Id)]);
                return true;
            }

            if (!await fileAccess.KeepFileGrantAsync(file.Id))
            {
                return false;
            }
        }

        await EditFilesAsync(files =>
        [
            new RecentFile
            {
                Id = file.Id,
                Operation = operation,
                Name = file.Name.Length > 0 ? file.Name : null,
            },
            .. files
                .Where(item => item.Id != file.Id)
                .Take(_maxRecentListLength - 1),
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
        EditFilesAsync(files => [.. files.Where(file => file.Id != fileId)]);

    public Task RemoveFileAsync(string fileId) =>
        EditFilesAsync(files => [.. files.Where(file => !IsSameFile(file.Id, fileId))]);

    // An id that cannot be made sense of, such as from an earlier version, is not taken to be the same file.
    private bool IsSameFile(string listedId, string fileId)
    {
        try
        {
            return fileAccess.IsSameFile(listedId, fileId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return listedId == fileId;
        }
    }

    // The operation name is copied as persisted, so that a name not known by this version survives. The file may
    // already be listed by the new id, and is then only kept where it is listed first.
    public Task ReplaceIdAsync(string fileId, string newFileId) =>
        EditFilesAsync(files =>
        [
            .. files
                .Select(file => file.Id == fileId
                    ? new RecentFile
                    {
                        Id = newFileId,
                        OperationName = file.OperationName,
                        Name = file.Name,
                        FolderName = file.FolderName,
                    }
                    : file)
                .DistinctBy(file => file.Id),
        ]);

    private async Task EditFilesAsync(Func<List<RecentFile>, List<RecentFile>> edit)
    {
        await using IEditScope<RecentFileOperations> scope =
            (await profileSession.UserStore!.LoadRecentFileOperationsAsync()).BeginEdit();
        scope.Value.Files = edit(scope.Value.Files);
    }
}
