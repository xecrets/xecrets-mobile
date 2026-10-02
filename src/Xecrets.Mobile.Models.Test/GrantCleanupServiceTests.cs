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
public sealed class GrantCleanupServiceTests
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

    /// <summary>
    /// The grants are held by the app, so a grant used by any profile on the device is kept. A folder grant is only
    /// held by My folders, and a grant to a file by itself only by a recent file that is only read.
    /// </summary>
    [Test]
    public async Task GrantsReferencedByAnyProfileAreKept()
    {
        MobileDataStore store = CreateStore();
        IUserDataStore first = await store.CreateUserAsync(NewUser("a@example.com", 1));
        IUserDataStore second = await store.CreateUserAsync(NewUser("b@example.com", 2));
        await SetFoldersAsync(first, new WorkFolder("docs", "docs", "grant:docs"));
        await SetFoldersAsync(second, new WorkFolder("photos", "photos", "grant:photos"));
        await SetRecentFilesAsync(first,
            new RecentFile { Id = "a.axx", Operation = RecentFileOperation.View },
            new RecentFile { Id = "docs/b.txt", Operation = RecentFileOperation.InPlace });
        await SetRecentFilesAsync(second,
            new RecentFile { Id = "c.txt", Operation = RecentFileOperation.EncryptCopySendTo },
            new RecentFile { Id = "docs/d.txt", Operation = RecentFileOperation.Edit },
            new RecentFile { Id = "e.txt", OperationName = "FutureOperation" });
        FakeFileAccess access = new(null!);

        await new GrantCleanupService(store, access, new TestCrashLogService()).RunAsync();

        Assert.That(access.Released!.FolderGrantIds, Is.EquivalentTo(["grant:docs", "grant:photos"]));
        Assert.That(access.Released.FileIds, Is.EquivalentTo(["a.axx", "c.txt", "e.txt"]));
    }

    [Test]
    public async Task AllGrantsAreReleasedWhenTheListsCannotBeLoaded()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(Path.Combine(_directory, "xecrets-data.json"), """{ "version": 999 }""");
        TestCrashLogService crashLog = new();
        FakeFileAccess access = new(null!);

        await new GrantCleanupService(CreateStore(), access, crashLog).RunAsync();

        Assert.That(access.Released!.FolderGrantIds, Is.Empty);
        Assert.That(access.Released.FileIds, Is.Empty);
        Assert.That(crashLog.Logged, Is.True);
    }

    private MobileDataStore CreateStore() =>
        new(new TestFileService(_directory), new TestCrashLogService(), TimeProvider.System, new ProtectedPayload());

    private static NewUserData NewUser(string email, byte marker) =>
        new(email, email, new SignInKey(email, DateTimeOffset.UtcNow, [marker]));

    private static async Task SetFoldersAsync(IUserDataStore user, params WorkFolder[] folders)
    {
        await using IEditScope<WorkFolders> scope = (await user.LoadWorkFoldersAsync()).BeginEdit();
        scope.Value.Folders = [.. folders];
    }

    private static async Task SetRecentFilesAsync(IUserDataStore user, params RecentFile[] files)
    {
        await using IEditScope<RecentFileOperations> scope = (await user.LoadRecentFileOperationsAsync()).BeginEdit();
        scope.Value.Files = [.. files];
    }

    private sealed class TestCrashLogService : ICrashLogService
    {
        public bool Logged { get; private set; }

        public bool HasPendingCrashLog => false;

        public void RegisterHandlers()
        {
        }

        public string ReadCurrent() => string.Empty;

        public void WriteCrashLog(string source, object? crash) => Logged = true;
    }

    private sealed class TestFileService(string directory) : IFileService
    {
        public string PlatformId => "test";
        public string AppDataDirectory => directory;
        public string CacheDirectory => directory;
        public Task<bool> OpenInAsync(string filePath, string displayName) => throw new NotSupportedException();
        public Task SendToAsync(string filePath, string displayName, string contentType) => throw new NotSupportedException();
        public Task<bool> CanViewFileAsync(DecryptedFileInfo file) => throw new NotSupportedException();
        public Task ViewFileAsync(DecryptedFileInfo file) => throw new NotSupportedException();
        public bool IsSelfHandoffReference(string reference) => false;
    }
}
