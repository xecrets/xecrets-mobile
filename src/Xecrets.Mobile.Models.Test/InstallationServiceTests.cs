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

using System.Text.Json.Nodes;

using NUnit.Framework;

using Xecrets.Common.Abstractions;
using Xecrets.Common.Implementation;
using Xecrets.Common.Models;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Data;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Services;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class InstallationServiceTests
{
    private string _directory = null!;
    private TestFileService _fileService = null!;
    private TestDeviceIdentity _deviceIdentity = null!;
    private MobileDataStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"xecrets-mobile-test-{Guid.NewGuid():N}");
        _fileService = new TestFileService(_directory);
        _deviceIdentity = new TestDeviceIdentity { DeviceId = "device" };
        _store = new MobileDataStore(_fileService, new TestCrashLogService(), TimeProvider.System, new ProtectedPayload());
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    [Test]
    public async Task FreshStartExcludesTheDeviceDirectoryFromBackupAndWritesNewSettings()
    {
        await NewService().EnsureAsync();

        DeviceSettings settings = ReadSettings();
        Assert.That(_fileService.ExcludedFromBackup, Is.EqualTo(new[] { DeviceDirectory }));
        Assert.That(settings.Version, Is.EqualTo(InstallationService.DeviceSettingsVersion));
        Assert.That(settings.DeviceIdSalt, Is.Not.Empty);
        Assert.That(settings.DeviceId, Is.Not.Empty.And.Not.Contain("device"));
    }

    [Test]
    public async Task SaltDiffersBetweenInstallations()
    {
        await NewService().EnsureAsync();
        DeviceSettings first = ReadSettings();
        File.Delete(SettingsPath);

        await NewService().EnsureAsync();
        DeviceSettings second = ReadSettings();

        Assert.That(second.DeviceIdSalt, Is.Not.EqualTo(first.DeviceIdSalt));
        Assert.That(second.DeviceId, Is.Not.EqualTo(first.DeviceId));
    }

    [Test]
    public async Task NormalStartKeepsTheDeviceSettings()
    {
        IUserDataStore user = await StartWithDeviceSettingsAsync();
        string before = await File.ReadAllTextAsync(SettingsPath);

        await NewService().EnsureAsync();

        Assert.That(await File.ReadAllTextAsync(SettingsPath), Is.EqualTo(before));
        Assert.That((await user.LoadWorkFoldersAsync()).Value.Folders, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task RestoredDataWithoutTheDeviceDirectoryGetsNewSettingsAndKeepsTheProfiles()
    {
        IUserDataStore user = await StartWithDeviceSettingsAsync();
        Directory.Delete(DeviceDirectory, true);

        await NewService().EnsureAsync();

        await AssertDeviceSettingsResetAsync(user);
    }

    [Test]
    public async Task SettingsFromAnotherDeviceAreReset()
    {
        IUserDataStore user = await StartWithDeviceSettingsAsync();
        _deviceIdentity.DeviceId = "other-device";

        await NewService().EnsureAsync();

        await AssertDeviceSettingsResetAsync(user);
    }

    [Test]
    public async Task SettingsOfAnotherVersionAreReset()
    {
        IUserDataStore user = await StartWithDeviceSettingsAsync();
        RewriteSettings(settings => settings["version"] = InstallationService.DeviceSettingsVersion + 1);

        await NewService().EnsureAsync();

        Assert.That(ReadSettings().Version, Is.EqualTo(InstallationService.DeviceSettingsVersion));
        await AssertDeviceSettingsResetAsync(user);
    }

    [Test]
    public async Task UnknownDeviceKeepsTheDeviceSettings()
    {
        IUserDataStore user = await StartWithDeviceSettingsAsync();
        string before = await File.ReadAllTextAsync(SettingsPath);
        _deviceIdentity.DeviceId = string.Empty;

        await NewService().EnsureAsync();

        Assert.That(await File.ReadAllTextAsync(SettingsPath), Is.EqualTo(before));
        Assert.That((await user.LoadWorkFoldersAsync()).Value.Folders, Has.Count.EqualTo(1));
    }

    private InstallationService NewService() => new(_store, _fileService, _deviceIdentity);

    private async Task<IUserDataStore> StartWithDeviceSettingsAsync()
    {
        await NewService().EnsureAsync();
        await _store.UpdateDeviceValuesAsync(values => values["key"] = "local");
        IUserDataStore user = await _store.CreateUserAsync(
            new NewUserData("a@example.com", "A", new SignInKey("a@example.com", DateTimeOffset.UtcNow, [1])));
        await using (IEditScope<WorkFolders> workFolders = (await user.LoadWorkFoldersAsync()).BeginEdit())
        {
            workFolders.Value.Folders.Add(new WorkFolder("folder", "Folder", "grant"));
        }

        return user;
    }

    private async Task AssertDeviceSettingsResetAsync(IUserDataStore user)
    {
        Assert.That(ReadSettings().Version, Is.EqualTo(InstallationService.DeviceSettingsVersion));
        Assert.That(_store.ReadDeviceValues(), Is.Empty);
        Assert.That((await user.LoadWorkFoldersAsync()).Value.Folders, Is.Empty);
        Assert.That(await _store.GetUsersAsync(), Has.Count.EqualTo(1));
    }

    private string DeviceDirectory => Path.Combine(_directory, IFileService.DeviceDirectoryName);

    private string SettingsPath => Path.Combine(DeviceDirectory, MobileDataStore.DeviceSettingsFileName);

    private DeviceSettings ReadSettings() => JsonFile.Load<DeviceSettings>(SettingsPath, exception => throw exception);

    private void RewriteSettings(Action<JsonObject> rewrite)
    {
        JsonObject settings = JsonNode.Parse(File.ReadAllText(SettingsPath))!.AsObject();
        rewrite(settings);
        File.WriteAllText(SettingsPath, settings.ToJsonString());
    }

    private sealed class TestDeviceIdentity : IDeviceIdentity
    {
        public string DeviceId { get; set; } = string.Empty;
    }

    private sealed class TestCrashLogService : ICrashLogService
    {
        public bool HasPendingCrashLog => false;

        public void RegisterHandlers()
        {
        }

        public string ReadCurrent() => string.Empty;

        public void WriteCrashLog(string source, object? crash)
        {
        }
    }

    private sealed class TestFileService(string directory) : IFileService
    {
        public List<string> ExcludedFromBackup { get; } = [];
        public string PlatformId => "test";
        public string AppDataDirectory => directory;
        public string CacheDirectory => directory;
        public Task<bool> OpenInAsync(string filePath, string displayName) => throw new NotSupportedException();
        public Task SendToAsync(string filePath, string displayName, string contentType) => throw new NotSupportedException();
        public Task<bool> CanViewFileAsync(DecryptedFileInfo file) => throw new NotSupportedException();
        public Task ViewFileAsync(DecryptedFileInfo file) => throw new NotSupportedException();
        public bool IsSelfHandoffReference(string reference) => false;
        public void ExcludeFromBackup(string path) => ExcludedFromBackup.Add(path);
    }
}
