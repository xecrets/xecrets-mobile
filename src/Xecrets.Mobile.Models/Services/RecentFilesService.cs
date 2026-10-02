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

public sealed class RecentFilesService(ProfileSession profileSession, IFlowContext flowContext) : IRecentFilesService
{
    private const int _maxRecentListLength = 50;

    public async Task<IReadOnlyList<RecentFile>> GetFilesAsync() =>
        [.. (await profileSession.UserStore!.LoadRecentFileOperationsAsync()).Value.Files];

    public async Task AddAsync(string fileId, RecentFileOperation operation)
    {
        await using IEditScope<RecentFileOperations> scope =
            (await profileSession.UserStore!.LoadRecentFileOperationsAsync()).BeginEdit();
        scope.Value.Files =
        [
            new RecentFile { Id = fileId, Operation = operation },
            .. scope.Value.Files
                .Where(file => file.Id != fileId)
                .Take(_maxRecentListLength - 1),
        ];
    }

    // A source outside the known folders cannot be opened again, so it is not listed.
    public Task AddFlowSourceAsync(RecentFileOperation operation) =>
        flowContext.Source is { IsInKnownWorkFolder: true } source
            ? AddAsync(source.Id, operation)
            : Task.CompletedTask;

    public Task AddSavedCopyAsync(WorkFolderFile savedCopy, RecentFileOperation operation) =>
        flowContext.Source is not null ? AddFlowSourceAsync(operation)
        : savedCopy.IsInKnownWorkFolder ? AddAsync(savedCopy.Id, RecentFileOperation.InPlace)
        : Task.CompletedTask;

    public async Task RemoveAsync(IReadOnlyCollection<string> fileIds)
    {
        await using IEditScope<RecentFileOperations> scope =
            (await profileSession.UserStore!.LoadRecentFileOperationsAsync()).BeginEdit();
        scope.Value.Files = [.. scope.Value.Files.Where(file => !fileIds.Contains(file.Id))];
    }
}
