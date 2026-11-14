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

using System.Text.Json;

using Xecrets.Common;
using Xecrets.Common.Abstractions;
using Xecrets.Common.Implementation;
using Xecrets.Common.Models;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Models.Data;

/// <summary>
/// Keeps the app data, the device-specific settings and the save in progress in JSON files of their own, each replaced
/// as a whole when written. App data or device-specific settings that are not valid JSON are read as new ones, after
/// a crash log is written.
/// </summary>
public sealed class MobileDataStore(
    IFileService fileService,
    ICrashLogService crashLogService,
    IEzLogging log,
    TimeProvider timeProvider,
    IProtectedPayload protectedPayload)
    : IXecretsDataStore, IDeviceSettingsStore, ILocalProfileStore, ISaveInProgressStore
{
    private const string _extraCredentialsPayloadName = "extraCredentials";

    private readonly SemaphoreSlim _access = new(1, 1);

    #region IXecretsDataStore

    /// <inheritdoc/>
    public async Task<IPersistentData<ApplicationSettings>> OpenApplicationSettingsAsync() =>
        ApplicationSettingsData(await ReadAsync(document => document.ApplicationSettings));

    /// <inheritdoc/>
    /// <remarks>
    /// Reads without waiting for other reads and writes to end, since a synchronous wait for them could deadlock. A
    /// write replaces the file as a whole, so the read sees either the previous or the new document in full, except on
    /// Windows, where a write that overlaps the read fails, since a file that is open cannot be replaced there.
    /// </remarks>
    public IPersistentData<ApplicationSettings> OpenApplicationSettings() =>
        ApplicationSettingsData(Validate(JsonFile.Load<ApplicationData>(fileService.DataFilePath, OnInvalidJson)).ApplicationSettings);

    /// <inheritdoc/>
    /// <remarks>
    /// Profiles are listed in the order they were created. A display name that is used by an earlier profile gets the
    /// number of its occurrence, such as "Name (2)".
    /// </remarks>
    public async Task<IReadOnlyList<UserSummary>> GetUsersAsync()
    {
        List<LocalProfileData> users = await ReadAsync(document => document.Users.OrderBy(user => user.CreationOrder).ToList());
        Dictionary<string, int> occurrences = new(StringComparer.OrdinalIgnoreCase);
        List<UserSummary> summaries = [];
        foreach (LocalProfileData user in users)
        {
            int occurrence = occurrences.GetValueOrDefault(user.BaseDisplayName) + 1;
            occurrences[user.BaseDisplayName] = occurrence;
            string displayName = occurrence == 1 ? user.BaseDisplayName : $"{user.BaseDisplayName} ({occurrence})";
            summaries.Add(new UserSummary(new UserId(user.Id), user.Email, user.BaseDisplayName, displayName));
        }

        return summaries;
    }

    /// <inheritdoc/>
    public async Task<IUserDataStore> CreateUserAsync(NewUserData user)
    {
        UserId id = new(Guid.NewGuid().ToString("N"));
        await UpdateAsync(document => document.Users.Add(new LocalProfileData
        {
            Id = id.Value,
            Email = user.Email,
            BaseDisplayName = user.BaseDisplayName,
            CreationOrder = document.Users.Count == 0 ? 1 : document.Users.Max(existing => existing.CreationOrder) + 1,
            Settings = new UserSettings { UserDisplayName = user.BaseDisplayName },
            SignInKeys = [user.SignInKey],
        }));
        return new MobileUserDataStore(this, id, timeProvider, protectedPayload);
    }

    /// <inheritdoc/>
    public async Task<IUserDataStore> OpenUserAsync(UserId userId)
    {
        await ReadAsync(document => FindUser(document, userId));
        return new MobileUserDataStore(this, userId, timeProvider, protectedPayload);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The device-specific settings are not exported.
    /// </remarks>
    public async Task<ApplicationConfigurationPackage> ExportApplicationConfigurationAsync() =>
        new() { Settings = await ReadAsync(document => document.ApplicationSettings) };

    /// <inheritdoc/>
    /// <remarks>
    /// The device-specific settings, which are not exported, are cleared.
    /// </remarks>
    public async Task ImportApplicationConfigurationAsync(ApplicationConfigurationPackage package)
    {
        ValidateVersion("application configuration package", package.Version,
            ApplicationConfigurationPackage.SupportedVersion);
        await UpdateAsync(document => document.ApplicationSettings = package.Settings);
        await ClearDeviceSettingsAsync();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The settings of the profile that only apply to it on this device are not exported. The extra credentials are
    /// exported as they are protected in the store, so <paramref name="protection"/> is not used.
    /// </remarks>
    public async Task<UserDataPackage> ExportUserAsync(UserId userId, IXecretsProtection protection)
    {
        LocalProfileData user = await ReadAsync(document => FindUser(document, userId));
        return new UserDataPackage
        {
            Email = user.Email,
            BaseDisplayName = user.BaseDisplayName,
            Settings = user.Settings,
            SignInKeys = [.. user.SignInKeys],
            ProtectedExtraCredentials = user.ProtectedPayloads.GetValueOrDefault(_extraCredentialsPayloadName) ?? [],
            PrivateKeys = user.PrivateKeys,
            License = user.License,
        };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The package is imported as a new profile. The extra credentials are imported as they are protected in the
    /// package, so <paramref name="protection"/> is not used.
    /// </remarks>
    public async Task<IUserDataStore> ImportUserAsync(UserDataPackage package, IXecretsProtection protection)
    {
        ValidateVersion("user data package", package.Version, UserDataPackage.SupportedVersion);
        if (package.SignInKeys.Count == 0)
        {
            throw new XecretsDataFormatException(
                "user data package",
                "At least one sign-in key is required.",
                UserDataPackage.SupportedVersion,
                package.Version);
        }
        IUserDataStore userStore = await CreateUserAsync(new NewUserData(
            package.Email,
            package.BaseDisplayName,
            package.SignInKeys.First()));
        await UpdateAsync(document =>
        {
            LocalProfileData user = FindUser(document, userStore.Id);
            user.Settings = package.Settings;
            user.SignInKeys = [.. package.SignInKeys];
            user.License = package.License;
            if (package.ProtectedExtraCredentials.Length > 0)
            {
                user.ProtectedPayloads[_extraCredentialsPayloadName] = package.ProtectedExtraCredentials;
            }
            user.PrivateKeys = package.PrivateKeys;
        });
        return userStore;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The device-specific settings are cleared too.
    /// </remarks>
    public async Task ResetApplicationConfigurationAsync()
    {
        await UpdateAsync(document => document.ApplicationSettings = new ApplicationSettings());
        await ClearDeviceSettingsAsync();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Removes the profile, together with its settings that only apply to it on this device and any save in progress
    /// of it.
    /// </remarks>
    public async Task ResetUserAsync(UserId userId)
    {
        await UpdateAsync(document => document.Users.Remove(FindUser(document, userId)));
        await UpdateDeviceAsync(settings => settings.Profiles.Remove(userId.Value));
        if (await ReadSaveInProgressAsync() is { } save && save.UserId == userId.Value)
        {
            await ClearSaveInProgressAsync();
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Deletes the files of the app data, the device-specific settings and the save in progress.
    /// </remarks>
    public async Task ResetStoreAsync()
    {
        await _access.WaitAsync();
        try
        {
            string[] paths = [fileService.DataFilePath, fileService.DeviceSettingsFilePath, fileService.SaveInProgressFilePath];
            foreach (string path in paths.Where(File.Exists))
            {
                Delete(path);
            }
        }
        finally
        {
            _access.Release();
        }
    }

    #endregion IXecretsDataStore

    #region IDeviceSettingsStore

    /// <inheritdoc/>
    public Task<GrantReferences> GetGrantReferencesAsync() =>
        ReadDeviceAsync(settings => new GrantReferences(
            settings.Profiles.Values
                .SelectMany(profile => profile.WorkFolders.Folders)
                .Select(folder => folder.GrantId)
                .ToHashSet(),
            settings.Profiles.Values
                .SelectMany(profile => profile.RecentFiles)
                .Where(file => !file.Operation.IsWriteClass())
                .Select(file => file.Id)
                .ToHashSet()));

    /// <inheritdoc/>
    public Task<DeviceSettings> ReadDeviceSettingsAsync() => ReadDeviceAsync(settings => settings);

    /// <inheritdoc/>
    public async Task ReplaceDeviceSettingsAsync(DeviceSettings settings)
    {
        await _access.WaitAsync();
        try
        {
            await SaveAsync(fileService.DeviceSettingsFilePath, settings);
        }
        finally
        {
            _access.Release();
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Reads without waiting for other reads and writes to end, as <see cref="OpenApplicationSettings"/> does.
    /// </remarks>
    public Dictionary<string, string> ReadDeviceValues() =>
        JsonFile.Load<DeviceSettings>(fileService.DeviceSettingsFilePath, OnInvalidDeviceJson).Values;

    /// <inheritdoc/>
    public Task UpdateDeviceValuesAsync(Action<Dictionary<string, string>> update) =>
        UpdateDeviceAsync(settings => update(settings.Values));

    #endregion IDeviceSettingsStore

    #region ILocalProfileStore

    /// <inheritdoc/>
    public Task<LocalProfileData> ReadUserAsync(UserId userId) =>
        ReadAsync(document => FindUser(document, userId));

    /// <inheritdoc/>
    public Task UpdateUserAsync(UserId userId, Action<LocalProfileData> update) =>
        UpdateAsync(document => update(FindUser(document, userId)));

    /// <inheritdoc/>
    public Task<ProfileDeviceSettings> ReadProfileDeviceSettingsAsync(UserId userId) =>
        ReadDeviceAsync(settings => settings.Profiles.GetValueOrDefault(userId.Value) ?? new ProfileDeviceSettings());

    /// <inheritdoc/>
    public Task UpdateProfileDeviceSettingsAsync(UserId userId, Action<ProfileDeviceSettings> update) =>
        UpdateDeviceAsync(deviceSettings =>
        {
            Dictionary<string, ProfileDeviceSettings> profiles = deviceSettings.Profiles;
            if (!profiles.TryGetValue(userId.Value, out ProfileDeviceSettings? settings))
            {
                profiles[userId.Value] = settings = new ProfileDeviceSettings();
            }

            update(settings);
        });

    #endregion ILocalProfileStore

    #region ISaveInProgressStore

    /// <inheritdoc/>
    public async Task<SaveInProgress?> ReadSaveInProgressAsync()
    {
        await _access.WaitAsync();
        try
        {
            return File.Exists(fileService.SaveInProgressFilePath)
                ? JsonSerializer.Deserialize(
                    await File.ReadAllBytesAsync(fileService.SaveInProgressFilePath),
                    MobileJsonContext.Default.SaveInProgress)
                : null;
        }
        finally
        {
            _access.Release();
        }
    }

    /// <inheritdoc/>
    public async Task WriteSaveInProgressAsync(SaveInProgress save)
    {
        await _access.WaitAsync();
        try
        {
            await SaveTextAsync(
                fileService.SaveInProgressFilePath,
                JsonSerializer.Serialize(save, MobileJsonContext.Default.SaveInProgress));
        }
        finally
        {
            _access.Release();
        }
    }

    /// <inheritdoc/>
    public async Task ClearSaveInProgressAsync()
    {
        await _access.WaitAsync();
        try
        {
            Delete(fileService.SaveInProgressFilePath);
        }
        finally
        {
            _access.Release();
        }
    }

    #endregion ISaveInProgressStore

    private async Task<TResult> ReadAsync<TResult>(Func<ApplicationData, TResult> read)
    {
        await _access.WaitAsync();
        try
        {
            return read(await LoadAsync());
        }
        finally
        {
            _access.Release();
        }
    }

    private async Task UpdateAsync(Action<ApplicationData> update)
    {
        await _access.WaitAsync();
        try
        {
            ApplicationData document = await LoadAsync();
            update(document);
            await SaveAsync(fileService.DataFilePath, document);
        }
        finally
        {
            _access.Release();
        }
    }

    private async Task<TResult> ReadDeviceAsync<TResult>(Func<DeviceSettings, TResult> read)
    {
        await _access.WaitAsync();
        try
        {
            return read(await LoadDeviceAsync());
        }
        finally
        {
            _access.Release();
        }
    }

    private async Task UpdateDeviceAsync(Action<DeviceSettings> update)
    {
        await _access.WaitAsync();
        try
        {
            DeviceSettings settings = await LoadDeviceAsync();
            update(settings);
            await SaveAsync(fileService.DeviceSettingsFilePath, settings);
        }
        finally
        {
            _access.Release();
        }
    }

    private PersistentData<ApplicationSettings> ApplicationSettingsData(ApplicationSettings settings) =>
        new(settings, async value =>
        {
            await UpdateAsync(document => document.ApplicationSettings = value);
            return JsonFile.Serialize(value);
        });

    // Keeps what tells which version and device the settings belong to.
    private Task ClearDeviceSettingsAsync() =>
        UpdateDeviceAsync(settings =>
        {
            settings.Values = [];
            settings.Profiles = [];
        });

    private async Task<ApplicationData> LoadAsync() =>
        Validate(await JsonFile.LoadAsync<ApplicationData>(fileService.DataFilePath, OnInvalidJson));

    private Task<DeviceSettings> LoadDeviceAsync() =>
        JsonFile.LoadAsync<DeviceSettings>(fileService.DeviceSettingsFilePath, OnInvalidDeviceJson);

    private void OnInvalidDeviceJson(Exception exception) =>
        crashLogService.WriteCrashLog("Device settings were invalid JSON; resetting to new settings.", exception);

    private void OnInvalidJson(Exception exception) =>
        crashLogService.WriteCrashLog("Mobile data was invalid JSON; resetting to a new document.", exception);

    private static ApplicationData Validate(ApplicationData document)
    {
        if (document.Version != ApplicationData.SupportedVersion)
        {
            throw new XecretsDataFormatException(
                "Mobile data",
                $"Version {document.Version} is not supported.",
                ApplicationData.SupportedVersion,
                document.Version);
        }

        return document;
    }

    private Task SaveAsync<T>(string path, T document) => SaveTextAsync(path, JsonFile.Serialize(document));

    // The file is replaced as a whole, so that it is never read written in part.
    private async Task SaveTextAsync(string path, string serialized)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporaryPath = $"{path}.tmp";

        try
        {
            await File.WriteAllTextAsync(temporaryPath, serialized);
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                Delete(temporaryPath);
            }
        }
    }

    private void Delete(string path)
    {
        File.Delete(path);
        log.Information($"Deleted {Path.GetRelativePath(fileService.AppDataDirectory, path)}");
    }

    private static LocalProfileData FindUser(ApplicationData document, UserId userId) =>
        document.Users.Single(user => user.Id == userId.Value);

    private static void ValidateVersion(string kind, int encountered, int supported)
    {
        if (encountered != supported)
        {
            throw new XecretsDataFormatException(kind, $"Version {encountered} is not supported.", supported, encountered);
        }
    }
}
