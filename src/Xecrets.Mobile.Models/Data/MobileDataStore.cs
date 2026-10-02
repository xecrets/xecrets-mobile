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

using Xecrets.Common;
using Xecrets.Common.Abstractions;
using Xecrets.Common.Implementation;
using Xecrets.Common.Models;
using Xecrets.Mobile.Models.Abstractions;

namespace Xecrets.Mobile.Models.Data;

public sealed class MobileDataStore(IFileService fileService, ICrashLogService crashLogService, TimeProvider timeProvider, IProtectedPayload protectedPayload) : IXecretsDataStore
{
    internal const string DataFileName = "xecrets-data.json";

    private const string _extraCredentialsPayloadName = "extraCredentials";

    private readonly SemaphoreSlim _access = new(1, 1);

    private string DataPath => Path.Combine(fileService.AppDataDirectory, DataFileName);

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
            await SaveAsync(document);
        }
        finally
        {
            _access.Release();
        }
    }

    public async Task<IPersistentData<ApplicationSettings>> OpenApplicationSettingsAsync() =>
        ApplicationSettingsData(await ReadAsync(document => document.ApplicationSettings));

    // Reads without _access, since a synchronous wait on it could deadlock. SaveAsync replaces the file atomically, so
    // the read sees either the previous or the new document in full. On Windows, a save that overlaps this read fails,
    // since a file that is open cannot be replaced there. This is in practice not an issue though as this is only called once on startup.
    public IPersistentData<ApplicationSettings> OpenApplicationSettings() =>
        ApplicationSettingsData(Validate(JsonFile.Load<ApplicationData>(DataPath, OnInvalidJson)).ApplicationSettings);

    private PersistentData<ApplicationSettings> ApplicationSettingsData(ApplicationSettings settings) =>
        new(settings, async value =>
        {
            await UpdateAsync(document => document.ApplicationSettings = value);
            return JsonFile.Serialize(value);
        });

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

    public async Task<IUserDataStore> OpenUserAsync(UserId userId)
    {
        await ReadAsync(document => FindUser(document, userId));
        return new MobileUserDataStore(this, userId, timeProvider, protectedPayload);
    }

    public async Task<ApplicationConfigurationPackage> ExportApplicationConfigurationAsync()
    {
        ApplicationSettings settings = await ReadAsync(document => document.ApplicationSettings);
        settings.DeviceSettings = new DeviceSettings();
        return new ApplicationConfigurationPackage { Settings = settings };
    }

    public Task ImportApplicationConfigurationAsync(ApplicationConfigurationPackage package)
    {
        ValidateVersion("application configuration package", package.Version,
            ApplicationConfigurationPackage.SupportedVersion);
        return UpdateAsync(document =>
        {
            document.ApplicationSettings = package.Settings;
        });
    }

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

    public Task ResetApplicationConfigurationAsync() =>
        UpdateAsync(document => document.ApplicationSettings = new ApplicationSettings());

    public Task ResetUserAsync(UserId userId) =>
        UpdateAsync(document => document.Users.Remove(FindUser(document, userId)));

    public async Task ResetStoreAsync()
    {
        await _access.WaitAsync();
        try
        {
            if (File.Exists(DataPath))
            {
                File.Delete(DataPath);
            }
        }
        finally
        {
            _access.Release();
        }
    }

    private async Task<ApplicationData> LoadAsync() =>
        MigrateRecentFiles(Validate(await JsonFile.LoadAsync<ApplicationData>(DataPath, OnInvalidJson)));

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

    // The legacy 'recentFiles' only holds plain strings, which is all that earlier versions can read. Recent files are
    // now kept in 'recentFileOperations', and the legacy list is merged into it and then cleared, so that it is no
    // longer written. Earlier versions then just see an empty list of recent files.
    private static ApplicationData MigrateRecentFiles(ApplicationData document)
    {
        foreach (LocalProfileData user in document.Users)
        {
            if (user.RecentFiles is null)
            {
                continue;
            }

            HashSet<string> ids = [.. user.RecentFileOperations.Select(file => file.Id)];
            user.RecentFileOperations.AddRange(user.RecentFiles
                .Where(ids.Add)
                .Select(id => new RecentFile { Id = id, Operation = RecentFileOperation.InPlace }));
            user.RecentFiles = null!;
        }

        return document;
    }

    private async Task SaveAsync(ApplicationData document)
    {
        Directory.CreateDirectory(fileService.AppDataDirectory);
        string temporaryPath = $"{DataPath}.tmp";

        try
        {
            string serialized = JsonFile.Serialize(document);
            await File.WriteAllTextAsync(temporaryPath, serialized);
            File.Move(temporaryPath, DataPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    internal Task<LocalProfileData> ReadUserAsync(UserId userId) =>
        ReadAsync(document => FindUser(document, userId));

    internal Task UpdateUserAsync(UserId userId, Action<LocalProfileData> update) =>
        UpdateAsync(document => update(FindUser(document, userId)));

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
