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

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Time.Testing;

using NUnit.Framework;

using Xecrets.Common.Abstractions;
using Xecrets.Common.Implementation;
using Xecrets.Common.Models;
using Xecrets.Core.Abstractions;
using Xecrets.Core.Models;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.PageModels;
using Xecrets.Mobile.Models.Services;
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class RecentFilesTests
{
    [Test]
    public async Task RecordTransformPutsResultAtTopAndKeepsSource()
    {
        TestUserDataStore store = new() { Files = ["a.axx", "b.txt", "c.txt"] };
        RecentFilesService service = new(SignedIn(store));

        await service.AddAsync("b-txt.axx");

        Assert.That(await service.GetFilesAsync(), Is.EqualTo(["b-txt.axx", "a.axx", "b.txt", "c.txt"]));
    }

    [Test]
    public async Task RecordTransformMovesExistingResultToTop()
    {
        TestUserDataStore store = new() { Files = ["a.axx", "b-txt.axx", "b.txt"] };
        RecentFilesService service = new(SignedIn(store));

        await service.AddAsync("b-txt.axx");

        Assert.That(await service.GetFilesAsync(), Is.EqualTo(["b-txt.axx", "a.axx", "b.txt"]));
    }

    [Test]
    public async Task RecordTransformCapsTheList()
    {
        TestUserDataStore store = new() { Files = [.. Enumerable.Range(0, 25).Select(i => $"{i}.txt")] };
        RecentFilesService service = new(SignedIn(store));

        await service.AddAsync("result.axx");

        IReadOnlyList<string> files = await service.GetFilesAsync();
        Assert.That(files, Has.Count.EqualTo(25));
        Assert.That(files[0], Is.EqualTo("result.axx"));
        Assert.That(files[^1], Is.EqualTo("23.txt"));
    }

    [Test]
    public async Task EncryptRecordsTheWrittenFile()
    {
        TestRecentFilesService recentFiles = new();
        WorkFolderOperationService operations = new(
            new TestCoreServices(),
            new TestProfileService(),
            recentFiles,
            new TestFileOperations(),
            new TestUserInterfaceService());

        await operations.EncryptAsync(CreateFile("folder/plain.txt"));

        Assert.That(recentFiles.Files, Is.EqualTo(["folder/plain-txt.axx"]));
    }

    [Test]
    public void DeclinedOverwriteRecordsNothing()
    {
        TestRecentFilesService recentFiles = new();
        WorkFolderOperationService operations = new(
            new TestCoreServices(),
            new TestProfileService(),
            recentFiles,
            new TestFileOperations { DestinationExists = true },
            new TestUserInterfaceService { Confirmation = false });

        Assert.That(
            async () => await operations.EncryptAsync(CreateFile("folder/plain.txt")),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(recentFiles.Files, Is.Empty);
    }

    [TestCase(new object[] { new[] { "a.axx", "b.txt" } })]
    [TestCase(new object[] { new[] { "a.axx" } })]
    [TestCase(new object[] { new string[0] })]
    public async Task DefaultFilterShowsAll(string[] files)
    {
        RecentFilesPageModel page = CreatePage(new TestRecentFilesService { Files = [.. files] }, new TestWorkFolderService());

        await page.LoadCommand.ExecuteAsync(null);

        Assert.That(page.SelectedState, Is.EqualTo(SelectedFileState.All));
        Assert.That(page.Files.Select(file => file.Id), Is.EqualTo(files));
    }

    [Test]
    public async Task MissingFilesAreSkippedAndInaccessibleFilesAreListed()
    {
        TestWorkFolderService folders = new();
        folders.Missing.Add("folder/gone.txt");
        folders.Inaccessible.Add("folder/locked.txt");
        RecentFilesPageModel page = CreatePage(
            new TestRecentFilesService { Files = ["folder/gone.txt", "folder/locked.txt", "folder/open.txt"] },
            folders);

        await page.LoadCommand.ExecuteAsync(null);

        Assert.That(page.Files.Select(file => file.Id), Is.EqualTo(["folder/locked.txt", "folder/open.txt"]));
    }

    [Test]
    public async Task CompletedOperationIsShownInPlaceUntilTheFilterChanges()
    {
        TestRecentFilesService recentFiles = new() { Files = ["folder/one.txt", "folder/two.txt", "folder/old.axx"] };
        RecentFilesPageModel page = CreatePage(recentFiles, new TestWorkFolderService());
        await page.LoadCommand.ExecuteAsync(null);
        page.SelectedState = SelectedFileState.Decrypted;

        await page.ReverseCommand.ExecuteAsync(page.Files[1]);

        Assert.That(recentFiles.Files, Is.EqualTo(["folder/two-txt.axx", "folder/one.txt", "folder/two.txt", "folder/old.axx"]));
        Assert.That(page.SelectedState, Is.EqualTo(SelectedFileState.Decrypted));
        Assert.That(
            page.Files.Select(file => (file.Id, file.IsCompleted)),
            Is.EqualTo([("folder/one.txt", false), ("folder/two-txt.axx", true)]));

        page.SelectedState = SelectedFileState.Encrypted;
        page.SelectedState = SelectedFileState.Decrypted;

        Assert.That(page.Files.Select(file => (file.Id, file.IsCompleted)), Is.EqualTo([("folder/one.txt", false)]));
    }

    [Test]
    public async Task OpenOpensADecryptedFileAndSharesAnEncryptedFile()
    {
        TestRecentFilesService recentFiles = new() { Files = ["folder/one.txt", "folder/old.axx"] };
        TestFileLauncher launcher = new();
        RecentFilesPageModel page = CreatePage(recentFiles, new TestWorkFolderService(), fileLauncher: launcher);
        await page.LoadCommand.ExecuteAsync(null);
        page.SelectedState = SelectedFileState.All;

        await page.OpenCommand.ExecuteAsync(page.Files[0]);
        await page.OpenCommand.ExecuteAsync(page.Files[1]);

        Assert.That(launcher.Opened, Is.EqualTo(["folder/one.txt"]));
        Assert.That(launcher.Shared, Is.EqualTo(["folder/old.axx"]));
    }

    [Test]
    public async Task OpenTellsTheUserWhenNoAppCanOpenTheFile()
    {
        TestUserInterfaceService userInterface = new();
        RecentFilesPageModel page = CreatePage(
            new TestRecentFilesService { Files = ["folder/one.txt"] },
            new TestWorkFolderService(),
            userInterface,
            new TestFileLauncher { CanOpen = false });
        await page.LoadCommand.ExecuteAsync(null);

        await page.OpenCommand.ExecuteAsync(page.Files[0]);

        Assert.That(userInterface.Messages, Is.EqualTo([MobileTexts.DialogTextNoAppToOpenFile]));
    }

    [Test]
    public async Task OpenOfAMissingFileIsReportedAndTheListReloaded()
    {
        TestWorkFolderService folders = new();
        TestUserInterfaceService userInterface = new();
        TestFileLauncher launcher = new();
        RecentFilesPageModel page = CreatePage(
            new TestRecentFilesService { Files = ["folder/gone.txt", "folder/one.txt"] },
            folders,
            userInterface,
            launcher);
        await page.LoadCommand.ExecuteAsync(null);
        folders.Missing.Add("folder/gone.txt");

        await page.OpenCommand.ExecuteAsync(page.Files[0]);

        Assert.That(userInterface.Messages, Is.EqualTo([MobileTexts.DialogTextRecentFileNotFound]));
        Assert.That(launcher.Opened, Is.Empty);
        Assert.That(page.Files.Select(file => file.Id), Is.EqualTo(["folder/one.txt"]));
    }

    [Test]
    public async Task OpenIgnoresACompletedRow()
    {
        TestFileLauncher launcher = new();
        RecentFilesPageModel page = CreatePage(
            new TestRecentFilesService { Files = ["folder/one.txt", "folder/two.txt"] },
            new TestWorkFolderService(),
            fileLauncher: launcher);
        await page.LoadCommand.ExecuteAsync(null);
        await page.ReverseCommand.ExecuteAsync(page.Files[1]);

        await page.OpenCommand.ExecuteAsync(page.Files[1]);

        Assert.That(page.Files[1].IsCompleted, Is.True);
        Assert.That(launcher.Opened, Is.Empty);
        Assert.That(launcher.Shared, Is.Empty);
    }

    [Test]
    public async Task RemoveTakesTheFileOffTheListOnly()
    {
        TestRecentFilesService recentFiles = new() { Files = ["folder/one.txt", "folder/two.txt", "folder/old.axx"] };
        RecentFilesPageModel page = CreatePage(recentFiles, new TestWorkFolderService());
        await page.LoadCommand.ExecuteAsync(null);
        page.SelectedState = SelectedFileState.Decrypted;

        await page.RemoveCommand.ExecuteAsync(page.Files[0]);

        Assert.That(recentFiles.Files, Is.EqualTo(["folder/two.txt", "folder/old.axx"]));
        Assert.That(page.Files.Select(file => file.Id), Is.EqualTo(["folder/two.txt"]));
    }

    [Test]
    public async Task ReloadShowsTheCurrentFilteredListAndKeepsTheFilter()
    {
        TestRecentFilesService recentFiles = new() { Files = ["folder/one.txt", "folder/two.txt", "folder/old.axx"] };
        RecentFilesPageModel page = CreatePage(recentFiles, new TestWorkFolderService());
        await page.LoadCommand.ExecuteAsync(null);
        page.SelectedState = SelectedFileState.Decrypted;
        await page.ReverseCommand.ExecuteAsync(page.Files[1]);
        recentFiles.Files.Add("folder/three.txt");

        await page.ReloadCommand.ExecuteAsync(null);

        Assert.That(page.SelectedState, Is.EqualTo(SelectedFileState.Decrypted));
        Assert.That(
            page.Files.Select(file => (file.Id, file.IsCompleted)),
            Is.EqualTo([("folder/one.txt", false), ("folder/three.txt", false)]));
    }

    [TestCase(SelectedFileState.Decrypted)]
    [TestCase(SelectedFileState.All)]
    public async Task ShowAddedSelectsTheStateAndListsTheAddedFileFirst(SelectedFileState initialState)
    {
        TestRecentFilesService recentFiles = new() { Files = ["folder/one.txt", "folder/old.axx"] };
        RecentFilesPageModel page = CreatePage(recentFiles, new TestWorkFolderService());
        await page.LoadCommand.ExecuteAsync(null);
        page.SelectedState = initialState;
        await recentFiles.AddAsync("folder/new.txt");

        await page.ShowAddedCommand.ExecuteAsync(SelectedFileState.Decrypted);

        Assert.That(page.SelectedState, Is.EqualTo(SelectedFileState.Decrypted));
        Assert.That(page.Files.Select(file => file.Id), Is.EqualTo(["folder/new.txt", "folder/one.txt"]));
    }

    [Test]
    public async Task AllShowsBothStatesWithTheCompletedResultOnlyInPlace()
    {
        TestRecentFilesService recentFiles = new() { Files = ["folder/one.txt", "folder/old.axx", "folder/two.txt"] };
        RecentFilesPageModel page = CreatePage(recentFiles, new TestWorkFolderService());
        await page.LoadCommand.ExecuteAsync(null);
        page.SelectedState = SelectedFileState.All;

        Assert.That(
            page.Files.Select(file => file.Id),
            Is.EqualTo(["folder/one.txt", "folder/old.axx", "folder/two.txt"]));

        await page.ReverseCommand.ExecuteAsync(page.Files[2]);

        Assert.That(
            page.Files.Select(file => (file.Id, file.IsCompleted)),
            Is.EqualTo([("folder/one.txt", false), ("folder/old.axx", false), ("folder/two-txt.axx", true)]));
    }

    [Test]
    public async Task CompletedOperationIsRefreshedAfterADelay()
    {
        TestRecentFilesService recentFiles = new() { Files = ["folder/one.txt", "folder/two.txt"] };
        FakeTimeProvider time = new();
        RecentFilesPageModel page = CreatePage(recentFiles, new TestWorkFolderService(), timeProvider: time);
        await page.LoadCommand.ExecuteAsync(null);
        await page.ReverseCommand.ExecuteAsync(page.Files[1]);

        Task refresh = page.RefreshAfterDelayCommand.ExecutionTask!;
        time.Advance(RecentFilesPageModel.RefreshDelay - TimeSpan.FromTicks(1));

        Assert.That(refresh.IsCompleted, Is.False);
        Assert.That(page.Files[1].IsCompleted, Is.True);

        time.Advance(TimeSpan.FromTicks(1));
        await refresh;

        Assert.That(
            page.Files.Select(file => (file.Id, file.IsCompleted)),
            Is.EqualTo([("folder/two-txt.axx", false), ("folder/one.txt", false)]));
    }

    [Test]
    public async Task AnotherOperationRestartsTheRefreshDelay()
    {
        TestRecentFilesService recentFiles = new() { Files = ["folder/one.txt", "folder/two.txt"] };
        FakeTimeProvider time = new();
        RecentFilesPageModel page = CreatePage(recentFiles, new TestWorkFolderService(), timeProvider: time);
        await page.LoadCommand.ExecuteAsync(null);
        TimeSpan half = RecentFilesPageModel.RefreshDelay / 2;

        await page.ReverseCommand.ExecuteAsync(page.Files[1]);
        Task first = page.RefreshAfterDelayCommand.ExecutionTask!;
        time.Advance(half);
        await page.ReverseCommand.ExecuteAsync(page.Files[0]);
        Task second = page.RefreshAfterDelayCommand.ExecutionTask!;

        time.Advance(half);

        // The first delay has passed, so the first refresh has ended, either canceled or refreshed.
        await first;

        Assert.That(second.IsCompleted, Is.False);
        Assert.That(page.Files.Select(file => file.IsCompleted), Is.EqualTo([true, true]));

        time.Advance(half);
        await second;

        Assert.That(page.Files.Select(file => file.IsCompleted), Is.EqualTo([false, false]));
    }

    [Test]
    public async Task InaccessibleFileIsReportedAndNotTransformed()
    {
        TestWorkFolderService folders = new();
        folders.Inaccessible.Add("folder/locked.txt");
        TestRecentFilesService recentFiles = new() { Files = ["folder/locked.txt"] };
        TestUserInterfaceService userInterface = new();
        RecentFilesPageModel page = CreatePage(recentFiles, folders, userInterface);
        await page.LoadCommand.ExecuteAsync(null);

        await page.ReverseCommand.ExecuteAsync(page.Files[0]);

        Assert.That(userInterface.Messages, Is.EqualTo([MobileTexts.DialogTextRecentFileNoAccess]));
        Assert.That(recentFiles.Files, Is.EqualTo(["folder/locked.txt"]));
    }

    [Test]
    public async Task ReverseAllTransformsEveryFileShown()
    {
        TestRecentFilesService recentFiles = new() { Files = ["folder/one.txt", "folder/old.axx", "folder/two.txt"] };
        RecentFilesPageModel page = CreatePage(recentFiles, new TestWorkFolderService());
        await page.LoadCommand.ExecuteAsync(null);
        page.SelectedState = SelectedFileState.Decrypted;

        await page.ReverseAllCommand.ExecuteAsync(null);

        Assert.That(
            recentFiles.Files.Take(2),
            Is.EquivalentTo(["folder/one-txt.axx", "folder/two-txt.axx"]));
        Assert.That(
            page.Files.Select(file => (file.Id, file.IsCompleted)),
            Is.EqualTo([("folder/one-txt.axx", true), ("folder/two-txt.axx", true)]));
    }

    [Test]
    public async Task ReverseAllStopsAtAFileThatNeedsAPassword()
    {
        TestWorkFolderService folders = new();
        folders.NeedsPassword.Add("folder/a.axx");
        TestRecentFilesService recentFiles = new() { Files = ["folder/a.axx", "folder/b.axx"] };
        TestUserInterfaceService userInterface = new();
        RecentFilesPageModel page = CreatePage(recentFiles, folders, userInterface);
        await page.LoadCommand.ExecuteAsync(null);
        page.SelectedState = SelectedFileState.Encrypted;

        await page.ReverseAllCommand.ExecuteAsync(null);

        Assert.That(userInterface.Destinations, Is.EqualTo([AppDestination.EnterPassword]));
        Assert.That(recentFiles.Files, Is.EqualTo(["folder/a.axx", "folder/b.axx"]));
        Assert.That(page.Files.Select(file => file.IsCompleted), Is.EqualTo([false, false]));
    }

    [TestCase(SelectedFileState.All, false, false)]
    [TestCase(SelectedFileState.Decrypted, true, true)]
    [TestCase(SelectedFileState.Encrypted, true, false)]
    public void ReverseAllIsShownForOneStateOnly(SelectedFileState state, bool visible, bool encrypts)
    {
        RecentFilesPageModel page = CreatePage(new TestRecentFilesService(), new TestWorkFolderService());

        page.SelectedState = state;

        Assert.That(page.IsReverseAllVisible, Is.EqualTo(visible));
        Assert.That(page.ReverseAllEncrypts, Is.EqualTo(encrypts));
    }

    private static RecentFilesPageModel CreatePage(
        TestRecentFilesService recentFiles,
        TestWorkFolderService folders,
        TestUserInterfaceService? userInterface = null,
        TestFileLauncher? fileLauncher = null,
        TimeProvider? timeProvider = null)
    {
        userInterface ??= new TestUserInterfaceService();
        fileLauncher ??= new TestFileLauncher();
        WorkFolderWorkflow workflow = new(
            folders,
            new TestOperationService(recentFiles, folders),
            new TestFileOperations(),
            new FlowContext(),
            new TestCoreServices(),
            userInterface);
        return new RecentFilesPageModel(
            recentFiles,
            folders,
            workflow,
            fileLauncher,
            userInterface,
            timeProvider ?? TimeProvider.System);
    }

    private static ProfileSession SignedIn(IUserDataStore store)
    {
        ProfileSession session = new();
        session.SignIn(null!, "password", null!, store);
        return session;
    }

    private static WorkFolderFile CreateFile(string id)
    {
        string location = Path.GetDirectoryName(id)!.Replace('\\', '/');
        return new WorkFolderFile(id, Path.GetFileName(id), location, location, "grant", true, null!);
    }

    private sealed class TestUserDataStore : IUserDataStore
    {
        public List<string> Files { get; set; } = [];
        public UserId Id => throw new NotSupportedException();
        public Task<IPersistentData<RecentFiles>> LoadRecentFilesAsync() =>
            Task.FromResult<IPersistentData<RecentFiles>>(new PersistentData<RecentFiles>(
                new RecentFiles { Files = [.. Files] },
                value =>
                {
                    Files = [.. value.Files];
                    return Task.FromResult(string.Empty);
                }));
        public Task<IPersistentData<UserSettings>> LoadSettingsAsync() => throw new NotSupportedException();
        public Task<IPersistentData<ExtraCredentials>> LoadExtraCredentialsAsync(IXecretsProtection protection) => throw new NotSupportedException();
        public Task<IPersistentData<PrivateKeyData>> LoadPrivateKeysAsync() => throw new NotSupportedException();
        public Task<IPersistentData<OpenFiles>> LoadOpenFilesAsync() => throw new NotSupportedException();
        public Task<IPersistentData<LicenseData>> LoadLicenseAsync() => throw new NotSupportedException();
        public Task<IPersistentData<WorkFolders>> LoadWorkFoldersAsync() => throw new NotSupportedException();
        public Task<IReadOnlyList<SignInKey>> GetSignInKeysAsync() => throw new NotSupportedException();
        public Task ReplaceSignInKeyAsync(SignInKey oldKey, SignInKey replacementKey, string email, string baseDisplayName) =>
            throw new NotSupportedException();
    }

    private sealed class TestRecentFilesService : IRecentFilesService
    {
        public List<string> Files { get; set; } = [];
        public Task<IReadOnlyList<string>> GetFilesAsync() => Task.FromResult<IReadOnlyList<string>>([.. Files]);
        public Task AddAsync(string fileId)
        {
            Files = [fileId, .. Files.Where(file => file != fileId)];
            return Task.CompletedTask;
        }
        public Task RemoveAsync(string fileId)
        {
            Files = [.. Files.Where(file => file != fileId)];
            return Task.CompletedTask;
        }
    }

    private sealed class TestWorkFolderService : IWorkFolderService
    {
        public HashSet<string> Missing { get; } = [];
        public HashSet<string> Inaccessible { get; } = [];
        public HashSet<string> NeedsPassword { get; } = [];
        public Task<WorkFolderFileResult> OpenFileAsync(string fileId) => Task.FromResult(
            Missing.Contains(fileId) ? WorkFolderFileResult.NotFound
            : Inaccessible.Contains(fileId) ? WorkFolderFileResult.NoAccess
            : WorkFolderFileResult.Valid(CreateFile(fileId)));
        public IReadOnlyList<string> GetFilePathSegments(string fileId) => fileId.Split('/');
        public Task<IReadOnlyList<WorkFolder>> GetFoldersAsync() => throw new NotSupportedException();
        public IReadOnlyList<string> GetPathSegments(WorkFolder folder) => throw new NotSupportedException();
        public Task<WorkFolderResult> AddFolderAsync(string? initialLocationId = null) => throw new NotSupportedException();
        public Task<WorkFolder> AddDiscoveredFolderAsync(WorkFolderFile file) => throw new NotSupportedException();
        public Task RemoveFolderAsync(WorkFolder folder) => throw new NotSupportedException();
        public Task RenameFolderAsync(WorkFolder folder, string displayName) => throw new NotSupportedException();
        public Task SaveFoldersAsync(IReadOnlyList<WorkFolder> folders) => throw new NotSupportedException();
        public Task<WorkFolderFile?> PickFileAsync(WorkFolder? folder, FilePickerKind pickerKind) => throw new NotSupportedException();
    }

    private sealed class TestFileOperations : IWorkFolderFileOperations
    {
        public bool DestinationExists { get; init; }
        public Task<Stream> OpenReadAsync(WorkFolderFile file) =>
            Task.FromResult<Stream>(new MemoryStream(file.Id.EndsWith(".axx", StringComparison.Ordinal) ? [0xe0] : [0x00]));
        public Task<bool> DestinationExistsAsync(WorkFolderFile file, string name) => Task.FromResult(DestinationExists);
        public Task<string> WriteDestinationAsync(WorkFolderFile file, string name, bool overwrite, Func<Stream, Task> writer) =>
            Task.FromResult($"{file.LocationId}/{name}");
        public Task DeleteAsync(WorkFolderFile file) => Task.CompletedTask;
    }

    private sealed class TestOperationService(IRecentFilesService recentFiles, TestWorkFolderService folders)
        : IWorkFolderOperationService
    {
        public bool HasPendingPasswordRequest => false;
        public Task EncryptAsync(WorkFolderFile file)
        {
            folders.Missing.Add(file.Id);
            return recentFiles.AddAsync($"{file.LocationId}/{Path.GetFileNameWithoutExtension(file.FileName)}-txt.axx");
        }
        public async Task<bool> DecryptWithKnownPasswordsAsync(WorkFolderFile file)
        {
            if (folders.NeedsPassword.Contains(file.Id))
            {
                return false;
            }

            folders.Missing.Add(file.Id);
            await recentFiles.AddAsync($"{file.LocationId}/decrypted.txt");
            return true;
        }
        public Task<bool> DecryptWithPasswordAsync(string password) => throw new NotSupportedException();
        public void CancelPasswordRequest() => throw new NotSupportedException();
    }

    private sealed class TestProfileService : IProfileService
    {
        public string CurrentEmail => throw new NotSupportedException();
        public bool IsAuthenticated => throw new NotSupportedException();
        public Identity GetIdentity() => new("password", []);
        public PublicKey GetPublicKey() => null!;
        public Task<bool> HasProfileAsync() => throw new NotSupportedException();
        public Task<SignInKey?> LoadProfileAsync() => throw new NotSupportedException();
        public Task<ProfileActionResult> CreateProfileAsync(string email, string password) => throw new NotSupportedException();
        public Task<ProfileActionResult> LoginAsync(string password) => throw new NotSupportedException();
        public void SignOut() => throw new NotSupportedException();
        public IReadOnlyList<PasswordUsage> GetExtraPasswords() => throw new NotSupportedException();
        public Task RecordExtraPasswordUseAsync(string password) => throw new NotSupportedException();
        public Task<bool> ShouldShowAsync(DontShowAgain notice) => throw new NotSupportedException();
        public Task SetDontShowAgainAsync(DontShowAgain notice) => throw new NotSupportedException();
    }

    private sealed class TestCoreServices : ICoreServices
    {
        public async Task<bool> IsEncryptedAsync(Func<Task<Stream>> openReadAsync)
        {
            await using Stream stream = await openReadAsync();
            return stream.ReadByte() == 0xe0;
        }

        public Task EncryptAsync(Stream cleartext, Stream encrypted, EncryptRequest request) => Task.CompletedTask;
        public Task<IDecryptionSession> OpenDecryptionAsync(Stream encrypted, DecryptRequest request) => throw new NotSupportedException();
        public Task<KeyPair> CreateKeyPairAsync(string email, string passphrase, DateTimeOffset createdUtc) => throw new NotSupportedException();
        public bool TryLoadKeyPair(ReadOnlyMemory<byte> encryptedKeyPair, IReadOnlyList<string> passphrases,
            [NotNullWhen(true)] out LoadedKeyPair? loadedKeyPair) => throw new NotSupportedException();
        public string ExportPublicKey(PublicKey publicKey) => throw new NotSupportedException();
        public PublicKey ImportPublicKey(string serializedPublicKey) => throw new NotSupportedException();
        public PrivateKeyImportResult ImportPrivateKeys(string serializedAccounts, PrivateKeyImportRequest request) => throw new NotSupportedException();
        public bool TryParseEmail(string email, [NotNullWhen(true)] out string? address) => throw new NotSupportedException();
    }

    private sealed class TestFileLauncher : IWorkFolderFileLauncher
    {
        public bool CanOpen { get; init; } = true;
        public List<string> Opened { get; } = [];
        public List<string> Shared { get; } = [];
        public Task<bool> OpenAsync(WorkFolderFile file)
        {
            Opened.Add(file.Id);
            return Task.FromResult(CanOpen);
        }
        public Task ShareAsync(WorkFolderFile file)
        {
            Shared.Add(file.Id);
            return Task.CompletedTask;
        }
    }

    private sealed class TestUserInterfaceService : IUserInterfaceService
    {
        public bool Confirmation { get; init; }
        public List<string> Messages { get; } = [];
        public List<AppDestination> Destinations { get; } = [];
        public bool IsShellAvailable => true;
        public bool CanProcessIncomingFiles => true;
        public bool CanReceiveIncomingFiles => true;
        public IReadOnlyDictionary<string, string> IconMap => throw new NotSupportedException();
        public Task InvokeOnMainThreadAsync(Func<Task> action) => action();
        public Task DisplayMessageAsync(string message)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
        public Task<bool> DisplayConfirmationAsync(string message) => Task.FromResult(Confirmation);
        public Task<string?> DisplayPromptAsync(string message, string initialValue) => throw new NotSupportedException();
        public Task DisplayTransientMessageAsync(string message) => Task.CompletedTask;
        public Task NavigateToAsync(AppDestination destination)
        {
            Destinations.Add(destination);
            return Task.CompletedTask;
        }
        public Task NavigateToAsync(AppDestination destination, object parameter) => throw new NotSupportedException();
        public Task GoBackAsync(object? parameter) => throw new NotSupportedException();
        public Task OpenBrowserAsync(string url) => throw new NotSupportedException();
        public Task SetClipboardTextAsync(string text) => throw new NotSupportedException();
    }
}
