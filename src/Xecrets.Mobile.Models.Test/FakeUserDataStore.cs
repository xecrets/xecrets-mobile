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
using Xecrets.Common.Implementation;
using Xecrets.Common.Models;

namespace Xecrets.Mobile.Models.Test;

internal sealed class FakeUserDataStore : IUserDataStore
{
    public List<RecentFile> Files { get; set; } = [];

    public List<WorkFolder> Folders { get; set; } = [];

    public UserId Id => throw new NotSupportedException();

    public Task<IPersistentData<RecentFileOperations>> LoadRecentFileOperationsAsync() =>
        Task.FromResult<IPersistentData<RecentFileOperations>>(new PersistentData<RecentFileOperations>(
            new RecentFileOperations { Files = [.. Files] },
            value =>
            {
                Files = [.. value.Files];
                return Task.FromResult(string.Empty);
            }));

    public Task<IPersistentData<WorkFolders>> LoadWorkFoldersAsync() =>
        Task.FromResult<IPersistentData<WorkFolders>>(new PersistentData<WorkFolders>(
            new WorkFolders { Folders = [.. Folders] },
            value =>
            {
                Folders = [.. value.Folders];
                return Task.FromResult(string.Empty);
            }));

    public Task<IPersistentData<RecentFiles>> LoadRecentFilesAsync() => throw new NotSupportedException();
    public Task<IPersistentData<UserSettings>> LoadSettingsAsync() => throw new NotSupportedException();
    public Task<IPersistentData<ExtraCredentials>> LoadExtraCredentialsAsync(IXecretsProtection protection) =>
        throw new NotSupportedException();
    public Task<IPersistentData<PrivateKeyData>> LoadPrivateKeysAsync() => throw new NotSupportedException();
    public Task<IPersistentData<OpenFiles>> LoadOpenFilesAsync() => throw new NotSupportedException();
    public Task<IPersistentData<LicenseData>> LoadLicenseAsync() => throw new NotSupportedException();
    public Task<IReadOnlyList<SignInKey>> GetSignInKeysAsync() => throw new NotSupportedException();
    public Task ReplaceSignInKeyAsync(SignInKey oldKey, SignInKey replacementKey, string email, string baseDisplayName) =>
        throw new NotSupportedException();
}
