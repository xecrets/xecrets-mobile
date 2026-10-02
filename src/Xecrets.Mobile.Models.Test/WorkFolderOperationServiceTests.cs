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

using NUnit.Framework;

using Xecrets.Common.Models;
using Xecrets.Core.Abstractions;
using Xecrets.Core.Models;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Services;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class WorkFolderOperationServiceTests
{
    private string _directory = string.Empty;

    [SetUp]
    public void SetUp() => _directory = Directory.CreateTempSubdirectory().FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    /// <summary>
    /// Storage such as Google Drive may still report a deleted file as existing, so the source must be removed from the
    /// recent files by the operation itself.
    /// </summary>
    [Test]
    public async Task DecryptInPlaceWipesSourceAndReplacesItInRecentFiles()
    {
        string sourcePath = Path.Combine(_directory, "input-txt.axx");
        await File.WriteAllTextAsync(sourcePath, "encrypted");
        WorkFolderFile source = new(sourcePath, "input-txt.axx", _directory, "Folder", "grant", true,
            new LocalWritableFile(sourcePath));
        TestRecentFilesService recentFiles = new() { Ids = ["other", sourcePath] };
        WorkFolderOperationService service = new(
            new TestCoreServices(),
            new TestProfileService(),
            recentFiles,
            new TestFileOperations(),
            new FileWiper(),
            new TestUserInterfaceService());

        bool decrypted = await service.DecryptWithKnownPasswordsAsync(source);

        Assert.That(decrypted, Is.True);
        Assert.That(Directory.GetFiles(_directory), Is.Empty);
        Assert.That(recentFiles.Ids, Is.EqualTo(new[] { $"{_directory}/input.txt", "other" }));
    }

    /// <summary>
    /// A failure to wipe the source is unexpected and reported, but the operation itself has succeeded, so the recent
    /// files still list the result instead of the source.
    /// </summary>
    [Test]
    public void FailedWipeIsReportedAndRecentFilesAreStillUpdated()
    {
        WorkFolderFile source = new("folder/input-txt.axx", "input-txt.axx", "folder", "Folder", "grant", true, null!);
        TestRecentFilesService recentFiles = new() { Ids = ["other", source.Id] };
        WorkFolderOperationService service = new(
            new TestCoreServices(),
            new TestProfileService(),
            recentFiles,
            new TestFileOperations { Content = "encrypted"u8.ToArray() },
            new FailingFileWiper(),
            new TestUserInterfaceService());

        Assert.That(async () => await service.DecryptWithKnownPasswordsAsync(source),
            Throws.TypeOf<IOException>().With.Message.EqualTo("Wipe failed."));
        Assert.That(recentFiles.Ids, Is.EqualTo(new[] { "folder/input.txt", "other" }));
    }

    private sealed class FailingFileWiper : IFileWiper
    {
        public Task<bool> CanWipeAsync(IPickedWritableFile file) => throw new NotSupportedException();
        public Task<FileWipeStatus> WipeAsync(IPickedWritableFile file) => throw new IOException("Wipe failed.");
    }

    private sealed class TestRecentFilesService : IRecentFilesService
    {
        public List<string> Ids { get; set; } = [];
        public Task<IReadOnlyList<RecentFile>> GetFilesAsync() => throw new NotSupportedException();
        public Task AddAsync(string fileId, RecentFileOperation operation)
        {
            Ids = [fileId, .. Ids.Where(id => id != fileId)];
            return Task.CompletedTask;
        }
        public Task AddFlowSourceAsync(RecentFileOperation operation) => throw new NotSupportedException();
        public Task AddSavedCopyAsync(WorkFolderFile savedCopy, RecentFileOperation operation) =>
            throw new NotSupportedException();
        public Task RemoveAsync(IReadOnlyCollection<string> fileIds)
        {
            Ids = [.. Ids.Where(id => !fileIds.Contains(id))];
            return Task.CompletedTask;
        }
    }

    private sealed class TestFileOperations : IWorkFolderFileOperations
    {
        // The content to read instead of the file itself, when set.
        public byte[]? Content { get; init; }
        public Task<Stream> OpenReadAsync(WorkFolderFile file) =>
            Task.FromResult(Content is not null ? new MemoryStream(Content) : (Stream)File.OpenRead(file.Id));
        public Task<bool> DestinationExistsAsync(WorkFolderFile file, string name) => Task.FromResult(false);
        public async Task<string> WriteDestinationAsync(WorkFolderFile file, string name, bool overwrite,
            Func<Stream, Task> writer)
        {
            await writer(Stream.Null);
            return $"{file.LocationId}/{name}";
        }
    }

    private sealed class TestCoreServices : ICoreServices
    {
        public Task<bool> IsEncryptedAsync(Func<Task<Stream>> openReadAsync) => throw new NotSupportedException();
        public Task EncryptAsync(Stream cleartext, Stream encrypted, EncryptRequest request) =>
            throw new NotSupportedException();
        public Task<IDecryptionSession> OpenDecryptionAsync(Stream encrypted, DecryptRequest request) =>
            Task.FromResult<IDecryptionSession>(new TestDecryptionSession());
        public Task<KeyPair> CreateKeyPairAsync(string email, string passphrase, DateTimeOffset createdUtc) =>
            throw new NotSupportedException();
        public bool TryLoadKeyPair(ReadOnlyMemory<byte> encryptedKeyPair, IReadOnlyList<string> passphrases,
            [NotNullWhen(true)] out LoadedKeyPair? loadedKeyPair) => throw new NotSupportedException();
        public string ExportPublicKey(PublicKey publicKey) => throw new NotSupportedException();
        public PublicKey? ImportPublicKey(string serializedPublicKey) => throw new NotSupportedException();
        public PrivateKeyImportResult ImportPrivateKeys(string serializedAccounts, PrivateKeyImportRequest request) =>
            throw new NotSupportedException();
        public bool TryParseEmail(string email, [NotNullWhen(true)] out string? address) =>
            throw new NotSupportedException();
    }

    private sealed class TestDecryptionSession : IDecryptionSession
    {
        public bool IsDecryptable => true;
        public string OriginalFileName => "input.txt";
        public DateTime CreationTimeUtc => DateTime.UnixEpoch;
        public DateTime LastAccessTimeUtc => DateTime.UnixEpoch;
        public DateTime LastWriteTimeUtc => DateTime.UnixEpoch;
        public EncryptedWithParameters EncryptedWithParameters => throw new NotSupportedException();
        public Task DecryptAsync(Stream cleartext) => Task.CompletedTask;
        public void Dispose() { }
    }

    private sealed class TestProfileService : IProfileService
    {
        public string CurrentEmail => throw new NotSupportedException();
        public bool IsAuthenticated => true;
        public Task<bool> HasProfileAsync() => throw new NotSupportedException();
        public Task<SignInKey?> LoadProfileAsync() => throw new NotSupportedException();
        public Task<ProfileActionResult> CreateProfileAsync(string email, string password) =>
            throw new NotSupportedException();
        public Task<ProfileActionResult> LoginAsync(string password) => throw new NotSupportedException();
        public void SignOut() => throw new NotSupportedException();
        public Identity GetIdentity() => new("password", []);
        public IReadOnlyList<PasswordUsage> GetExtraPasswords() => [];
        public Task RecordExtraPasswordUseAsync(string password) => throw new NotSupportedException();
        public PublicKey GetPublicKey() => throw new NotSupportedException();
        public Task<bool> ShouldShowAsync(DontShowAgain notice) => throw new NotSupportedException();
        public Task SetDontShowAgainAsync(DontShowAgain notice) => throw new NotSupportedException();
    }

    private sealed class TestUserInterfaceService : IUserInterfaceService
    {
        public bool IsShellAvailable => throw new NotSupportedException();
        public bool CanProcessIncomingFiles => throw new NotSupportedException();
        public bool CanReceiveIncomingFiles => throw new NotSupportedException();
        public IReadOnlyDictionary<string, string> IconMap => throw new NotSupportedException();
        public Task InvokeOnMainThreadAsync(Func<Task> action) => throw new NotSupportedException();
        public Task DisplayMessageAsync(string message) => throw new NotSupportedException();
        public Task<bool> DisplayConfirmationAsync(string message) => throw new NotSupportedException();
        public Task<string?> DisplayPromptAsync(string message, string initialValue) => throw new NotSupportedException();
        public Task DisplayTransientMessageAsync(string message) => throw new NotSupportedException();
        public Task NavigateToAsync(AppDestination destination) => throw new NotSupportedException();
        public Task NavigateToAsync(AppDestination destination, object parameter) => throw new NotSupportedException();
        public Task GoBackAsync(object? parameter) => throw new NotSupportedException();
        public Task OpenBrowserAsync(string url) => throw new NotSupportedException();
        public Task SetClipboardTextAsync(string text) => throw new NotSupportedException();
    }
}
