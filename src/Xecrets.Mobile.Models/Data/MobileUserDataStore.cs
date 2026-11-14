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
using Xecrets.Mobile.Models.Abstractions;

namespace Xecrets.Mobile.Models.Data;

/// <summary>
/// The data store of a local profile, kept by an <see cref="ILocalProfileStore"/>. My folders and the recent files only
/// apply to the profile on this device, and are kept with its device-specific settings.
/// </summary>
/// <param name="store">Keeps the data of the profile.</param>
/// <param name="id">The id of the profile.</param>
/// <param name="timeProvider">Tells when the extra credentials are written.</param>
/// <param name="protectedPayload">Protects the extra credentials.</param>
internal sealed class MobileUserDataStore(ILocalProfileStore store, UserId id, TimeProvider timeProvider, IProtectedPayload protectedPayload) : IUserDataStore
{
    private const string _extraCredentialsPayloadName = "extraCredentials";

    /// <inheritdoc/>
    public UserId Id { get; } = id;

    /// <inheritdoc/>
    public async Task<IPersistentData<UserSettings>> LoadSettingsAsync()
    {
        UserSettings settings = (await store.ReadUserAsync(Id)).Settings;
        return new PersistentData<UserSettings>(settings, async value =>
        {
            await store.UpdateUserAsync(Id, user => user.Settings = value);
            return JsonFile.Serialize(value);
        });
    }

    /// <inheritdoc/>
    /// <remarks>
    /// When saved, duplicate passwords are removed, and the time of writing is set.
    /// </remarks>
    public async Task<IPersistentData<ExtraCredentials>> LoadExtraCredentialsAsync(IXecretsProtection protection)
    {
        byte[]? protectedBytes = (await store.ReadUserAsync(Id)).ProtectedPayloads.GetValueOrDefault(_extraCredentialsPayloadName);
        ExtraCredentials credentials = await protectedPayload.Unprotect<ExtraCredentials>(protectedBytes, protection);

        return new PersistentData<ExtraCredentials>(credentials, async current =>
        {
            current.Passwords = [.. current.Passwords.DistinctBy(password => password.Password)];
            current.LastWriteUtc = timeProvider.GetUtcNow().UtcDateTime;
            byte[] encoded = await protectedPayload.Protect(current, protection);

            await store.UpdateUserAsync(Id, stored => stored.ProtectedPayloads[_extraCredentialsPayloadName] = encoded);
            return JsonFile.Serialize(current);
        });
    }

    /// <inheritdoc/>
    public async Task<IPersistentData<PrivateKeyData>> LoadPrivateKeysAsync()
    {
        PrivateKeyData privateKeys = (await store.ReadUserAsync(Id)).PrivateKeys;
        return new PersistentData<PrivateKeyData>(privateKeys, async value =>
            {
                await store.UpdateUserAsync(Id, user => user.PrivateKeys = value);
                return JsonFile.Serialize(value);
            });
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The app keeps no open files.
    /// </remarks>
    public Task<IPersistentData<OpenFiles>> LoadOpenFilesAsync()
        => throw new NotSupportedException();

    /// <inheritdoc/>
    /// <remarks>
    /// The app keeps its recent files as <see cref="RecentFileOperations"/>.
    /// </remarks>
    public Task<IPersistentData<RecentFiles>> LoadRecentFilesAsync()
        => throw new NotSupportedException();

    /// <inheritdoc/>
    /// <remarks>
    /// The recent files are kept with the device-specific settings of the profile.
    /// </remarks>
    public async Task<IPersistentData<RecentFileOperations>> LoadRecentFileOperationsAsync()
    {
        RecentFileOperations recentFiles = new() { Files = [.. (await store.ReadProfileDeviceSettingsAsync(Id)).RecentFiles] };
        return new PersistentData<RecentFileOperations>(recentFiles,
            async value =>
            {
                await store.UpdateProfileDeviceSettingsAsync(Id, settings => settings.RecentFiles = [.. value.Files]);
                return JsonFile.Serialize(value);
            });
    }

    /// <inheritdoc/>
    public async Task<IPersistentData<LicenseData>> LoadLicenseAsync()
    {
        LicenseData license = (await store.ReadUserAsync(Id)).License;
        return new PersistentData<LicenseData>(license,
            async value =>
            {
                await store.UpdateUserAsync(Id, user => user.License = value);
                return JsonFile.Serialize(value);
            });
    }

    /// <inheritdoc/>
    /// <remarks>
    /// My folders are kept with the device-specific settings of the profile.
    /// </remarks>
    public async Task<IPersistentData<WorkFolders>> LoadWorkFoldersAsync()
    {
        WorkFolders workFolders = (await store.ReadProfileDeviceSettingsAsync(Id)).WorkFolders;
        return new PersistentData<WorkFolders>(workFolders,
            async value =>
            {
                await store.UpdateProfileDeviceSettingsAsync(Id, settings => settings.WorkFolders = value);
                return JsonFile.Serialize(value);
            });
    }

    /// <inheritdoc/>
    public async Task<IPersistentData<ProfileDeviceSettings>> LoadDeviceSettingsAsync()
    {
        ProfileDeviceSettings deviceSettings = await store.ReadProfileDeviceSettingsAsync(Id);
        return new PersistentData<ProfileDeviceSettings>(deviceSettings,
            async value =>
            {
                await store.UpdateProfileDeviceSettingsAsync(Id, settings =>
                {
                    settings.WorkFolders = value.WorkFolders;
                    settings.RecentFiles = value.RecentFiles;
                    settings.DontShowAgain = value.DontShowAgain;
                });
                return JsonFile.Serialize(value);
            });
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SignInKey>> GetSignInKeysAsync() =>
        [.. (await store.ReadUserAsync(Id)).SignInKeys];

    /// <inheritdoc/>
    /// <remarks>
    /// The display name of the user settings is replaced too.
    /// </remarks>
    public Task ReplaceSignInKeyAsync(
        SignInKey oldKey,
        SignInKey replacementKey,
        string email,
        string baseDisplayName) =>
        store.UpdateUserAsync(Id, user =>
        {
            int index = user.SignInKeys.FindIndex(key => key.ProtectedBytes.AsSpan().SequenceEqual(oldKey.ProtectedBytes));
            user.SignInKeys[index] = replacementKey;
            user.Email = email;
            user.BaseDisplayName = baseDisplayName;
            user.Settings.UserDisplayName = baseDisplayName;
        });
}
