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
public sealed class LocalProfileDeletionServiceTests
{
    private string _directory = null!;

    [SetUp]
    public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), $"xecrets-mobile-test-{Guid.NewGuid():N}");

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    [Test]
    public async Task AllLocalDataIsDeletedAndNothingIsLeft()
    {
        IFileService fileService = new TestFileService(Path.Combine(_directory, "data"), Path.Combine(_directory, "cache", "XecretsEz"));
        FakeCrashLogService crashLog = new();
        FakeEzLogging log = new();
        MobileDataStore store = new(fileService, crashLog, log, TimeProvider.System, new ProtectedPayload());
        MobileDeviceSettings deviceSettings = new(store);
        TransientFileService transient = new(fileService, new FileWiper(crashLog), crashLog, log);

        IUserDataStore user = await store.CreateUserAsync(
            new NewUserData("me@example.com", "me@example.com", new SignInKey("me@example.com", DateTimeOffset.UtcNow, [1])));
        await using (IEditScope<WorkFolders> scope = (await user.LoadWorkFoldersAsync()).BeginEdit())
        {
            scope.Value.Folders = [new WorkFolder("docs", "docs", "grant:docs")];
        }

        await using (IEditScope<ApplicationSettings> settings = (await store.OpenApplicationSettingsAsync()).BeginEdit())
        {
            settings.Value.Theme = nameof(ThemePreference.Dark);
        }

        deviceSettings["key"] = "value";
        await File.WriteAllTextAsync(transient.CreateHandoffFile("secret.txt").FilePath, "secret");
        Directory.CreateDirectory(Path.Combine(fileService.DeviceDirectory, "WorkFolderGrants"));

        FakeProfileService profile = new();
        FakeFileAccess access = new(null!);
        ScriptedUserInterface userInterface = new();
        LocalProfileDeletionService service = new(
            new FakePreviewService(),
            new DecryptionPasswordRequestState(),
            transient,
            new GrantCleanupService(store, access, crashLog),
            store,
            deviceSettings,
            fileService,
            crashLog,
            log,
            profile,
            userInterface);

        await service.DeleteAsync();

        Assert.That(Directory.EnumerateFileSystemEntries(fileService.AppDataDirectory), Is.Empty);
        Assert.That(Directory.EnumerateFileSystemEntries(Path.Combine(_directory, "cache")), Is.Empty);
        Assert.That(deviceSettings["key"], Is.Empty);
        Assert.That(access.Released!.FolderGrantIds, Is.Empty);
        Assert.That(access.Released.FileIds, Is.Empty);
        Assert.That(profile.IsSignedOut, Is.True);
        Assert.That(crashLog.IsDeleted, Is.True);
        Assert.That(log.IsDeleted, Is.True);
        Assert.That(userInterface.Destinations, Is.EqualTo([AppDestination.Startup]));

        // As when the startup page is shown again, which reads the theme.
        Assert.That((await store.OpenApplicationSettingsAsync()).Value.Theme, Is.Not.EqualTo(nameof(ThemePreference.Dark)));
        Assert.That(Directory.EnumerateFileSystemEntries(fileService.AppDataDirectory), Is.Empty);
    }

    private sealed class TestFileService(string appDataDirectory, string cacheDirectory) : IFileService
    {
        public string PlatformId => "test";
        public string AppDataDirectory => appDataDirectory;
        public string CacheDirectory => cacheDirectory;
        public Task<bool> OpenInAsync(string filePath, string displayName) => throw new NotSupportedException();
        public Task<bool> SendToAsync(string filePath, string displayName, string contentType) => throw new NotSupportedException();
        public Task<bool> CanViewFileAsync(DecryptedFileInfo file) => throw new NotSupportedException();
        public Task<bool> ViewFileAsync(DecryptedFileInfo file) => throw new NotSupportedException();
        public bool IsSelfHandoffReference(string reference) => false;
    }
}
