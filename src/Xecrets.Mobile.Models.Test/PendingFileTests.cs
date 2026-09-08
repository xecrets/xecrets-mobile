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
using Xecrets.Mobile.Models.PageModels;
using Xecrets.Mobile.Models.Services;
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class PendingFileTests
{
    private string _directory = string.Empty;

    [SetUp]
    public void SetUp() => _directory = Directory.CreateTempSubdirectory().FullName;

    [TearDown]
    public void TearDown()
    {
        foreach (string file in Directory.GetFiles(_directory, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_directory, recursive: true);
    }

    [TestCase("input.txt", false)]
    [TestCase("input.txt", true)]
    [TestCase("input.axx", false)]
    [TestCase("input.axx", true)]
    public async Task WipedIncomingFileIsDiscardedWithoutAuthenticationOrProcessing(string name, bool authenticated)
    {
        TransientFileService transient = CreateTransientService();
        TestUserInterfaceService userInterface = new() { CanReceiveIncomingFiles = true };
        TestProfileService profile = new() { IsAuthenticated = authenticated };
        IncomingFileService incoming = new(profile, transient, null!, null!, null!, userInterface);
        string path = transient.CreateIncomingPath(name);
        await incoming.ReceiveAsync(async () =>
        {
            await File.WriteAllTextAsync(path, "input");
            return new IncomingFileInfo(path, name, "application/octet-stream");
        });

        await transient.MaybeWipeTrackedFilesAsync();
        await incoming.ProcessPendingAsync();

        // Recreating the file must not revive a request that was already discarded.
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "replacement");
        await incoming.ProcessPendingAsync();

        Assert.That(userInterface.Destinations, Is.Empty);
        Assert.That(userInterface.Messages, Is.Empty);
    }

    [Test]
    public async Task OnlyFirstIncomingFileIsAcceptedWhileAuthenticationIsPending()
    {
        TransientFileService transient = CreateTransientService();
        TestUserInterfaceService userInterface = new() { CanProcessIncomingFiles = true, CanReceiveIncomingFiles = true };
        IncomingFileService incoming = new(new TestProfileService(), transient, null!, null!, null!, userInterface);
        int receivedCount = 0;

        Task Receive(string name) => incoming.ReceiveAsync(async () =>
        {
            receivedCount++;
            string path = transient.CreateIncomingPath(name);
            await File.WriteAllTextAsync(path, name);
            return new IncomingFileInfo(path, name, "text/plain");
        });

        await Task.WhenAll(Receive("first.txt"), Receive("second.txt"));

        Assert.That(receivedCount, Is.EqualTo(1));
        Assert.That(userInterface.Destinations, Is.EqualTo(new[] { AppDestination.Login }));
    }

    [Test]
    public async Task IncomingFileIsDiscardedWhileAnotherWorkflowIsActive()
    {
        TransientFileService transient = CreateTransientService();
        TestUserInterfaceService userInterface = new();
        IncomingFileService incoming = new(new TestProfileService(), transient, null!, null!, null!, userInterface);
        int receivedCount = 0;

        await incoming.ReceiveAsync(() =>
        {
            receivedCount++;
            return Task.FromResult(new IncomingFileInfo("unused", "file.txt", "text/plain"));
        });

        Assert.That(receivedCount, Is.Zero);
        Assert.That(userInterface.Destinations, Is.Empty);
    }

    [Test]
    public async Task PendingIncomingMessageDisplaysOnceWithoutAuthentication()
    {
        TransientFileService transient = CreateTransientService();
        TestUserInterfaceService userInterface = new() { CanReceiveIncomingFiles = true };
        IncomingFileService incoming = new(new TestProfileService(), transient, null!, null!, null!, userInterface);

        await incoming.ReceiveMessageAsync(MobileTexts.DialogTextIncomingFileAccessDenied);
        await incoming.ProcessPendingAsync();
        await incoming.ProcessPendingAsync();

        Assert.That(userInterface.Messages, Is.EqualTo(new[] { MobileTexts.DialogTextIncomingFileAccessDenied }));
        Assert.That(userInterface.Destinations, Is.Empty);
    }

    [TestCase("input.txt", AppDestination.EncryptResult)]
    [TestCase("input.axx", AppDestination.Preview)]
    public async Task ExistingIncomingFileProcessesAfterSignIn(string name, AppDestination destination)
    {
        TransientFileService transient = CreateTransientService();
        TestUserInterfaceService userInterface = new() { CanProcessIncomingFiles = true, CanReceiveIncomingFiles = true };
        TestProfileService profile = new();
        TestCoreServices core = new();
        PreviewService preview = new(core, profile, transient, new PreviewState(), new DecryptionPasswordRequestState());
        IncomingFileService incoming = new(profile, transient, preview,
            new EncryptionPreparationService(profile, transient, core), new FlowContext(), userInterface);
        await incoming.ReceiveAsync(async () =>
        {
            string path = transient.CreateIncomingPath(name);
            await File.WriteAllTextAsync(path, "input");
            return new IncomingFileInfo(path, name, "application/octet-stream");
        });
        Assert.That(userInterface.Destinations, Is.EqualTo(new[] { AppDestination.Login }));

        profile.IsAuthenticated = true;
        await incoming.ProcessPendingAsync();
        await incoming.ProcessPendingAsync();

        Assert.That(userInterface.Destinations, Is.EqualTo(new[] { AppDestination.Login, destination }));
        Assert.That(userInterface.Messages, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task MissingPasswordInputCancelsAndClosesPage(bool deleteDirectory)
    {
        TransientFileService transient = CreateTransientService();
        string path = transient.CreateEncryptedInputPath("input.axx");
        await File.WriteAllTextAsync(path, "input");
        DecryptionPasswordRequestState request = new();
        request.Set(path, "original", true);
        if (deleteDirectory)
        {
            await transient.MaybeWipeTrackedFilesAsync();
        }
        else
        {
            File.Delete(path);
        }

        PreviewService preview = new(null!, null!, transient, new PreviewState(), request);
        TestUserInterfaceService userInterface = new();
        EnterPasswordPageModel page = new(preview, new TestWorkFolderOperationService(), null!, userInterface)
        {
            Password = "password",
        };
        await page.SubmitCommand.ExecuteAsync(null);

        Assert.That(request.HasPendingRequest, Is.False);
        Assert.That(request.SourcePath, Is.Empty);
        Assert.That(page.Password, Is.Empty);
        Assert.That(page.ErrorText, Is.Empty);
        Assert.That(page.IsBusy, Is.False);
        Assert.That(userInterface.BackCount, Is.EqualTo(1));
        Assert.That(userInterface.Destinations, Is.Empty);
        Assert.That(userInterface.Messages, Is.Empty);
        Assert.That(await preview.PrepareWithPasswordAsync("password"), Is.EqualTo(PreviewPreparationStatus.Cancelled));
    }

    [Test]
    public async Task WrongPasswordKeepsRequestAndCorrectPasswordCompletesIt()
    {
        TransientFileService transient = CreateTransientService();
        string path = transient.CreateEncryptedInputPath("input.axx");
        await File.WriteAllTextAsync(path, "input");
        DecryptionPasswordRequestState request = new();
        TestProfileService profile = new();
        PreviewState state = new();
        PreviewService preview = new(new TestCoreServices(), profile, transient, state, request);
        Assert.That(await preview.PrepareImportedAsync(path), Is.EqualTo(PreviewPreparationStatus.WrongPassword));

        TestUserInterfaceService userInterface = new();
        EnterPasswordPageModel page = new(preview, new TestWorkFolderOperationService(), null!, userInterface)
        {
            Password = "wrong",
        };
        await page.SubmitCommand.ExecuteAsync(null);
        Assert.That(request.HasPendingRequest, Is.True);
        Assert.That(page.ErrorText, Is.EqualTo(MobileTexts.DialogTextWrongPasswordOpen));
        Assert.That(userInterface.BackCount, Is.Zero);

        page.Password = "password";
        await page.SubmitCommand.ExecuteAsync(null);
        Assert.That(request.HasPendingRequest, Is.False);
        Assert.That(page.Password, Is.Empty);
        Assert.That(page.ErrorText, Is.Empty);
        Assert.That(state.Text, Is.EqualTo("decrypted"));
        Assert.That(profile.RecordedPasswords, Is.EqualTo(new[] { "password" }));
        Assert.That(userInterface.Destinations, Is.EqualTo(new[] { AppDestination.Preview }));
    }

    [Test]
    public async Task MissingImportedInputDoesNotCreatePasswordRequest()
    {
        TestProfileService profile = new();
        DecryptionPasswordRequestState request = new();
        request.Set("old", "old", true);
        PreviewService preview = new(null!, profile, null!, new PreviewState(), request);

        Assert.That(await preview.PrepareImportedAsync(Path.Combine(_directory, "missing.axx")),
            Is.EqualTo(PreviewPreparationStatus.Cancelled));
        Assert.That(request.HasPendingRequest, Is.False);
    }

    [Test]
    public async Task UnreadableImportedInputIsNotTreatedAsMissing()
    {
        PreviewService preview = new(null!, new TestProfileService(), null!,
            new PreviewState(), new DecryptionPasswordRequestState());

        await Assert.ThatAsync(async () => await preview.PrepareImportedAsync(_directory),
            Throws.TypeOf<UnauthorizedAccessException>());
    }

    [Test]
    public async Task MissingOutputDirectoryIsNotTreatedAsMissingInput()
    {
        TransientFileService transient = CreateTransientService();
        string path = transient.CreateEncryptedInputPath("input.axx");
        await File.WriteAllTextAsync(path, "input");
        DecryptionPasswordRequestState request = new();
        request.Set(path, string.Empty, false);
        PreviewService preview = new(new TestCoreServices(new DirectoryNotFoundException("output failed")),
            new TestProfileService(), transient, new PreviewState(), request);

        await Assert.ThatAsync(async () => await preview.PrepareWithPasswordAsync("password"),
            Throws.TypeOf<DirectoryNotFoundException>().With.Message.EqualTo("output failed"));
        Assert.That(request.HasPendingRequest, Is.True);
    }

    [Test]
    public async Task ExitClearsPasswordRequestBeforeWiping()
    {
        DecryptionPasswordRequestState request = new();
        bool pendingWhenWiped = true;
        TransientFileService transient = new(new TestFileService(_directory),
            new TestFileWiper(() => pendingWhenWiped = request.HasPendingRequest));
        string path = transient.CreateEncryptedInputPath("input.axx");
        await File.WriteAllTextAsync(path, "input");
        request.Set(path, "original", true);
        TestProfileService profile = new() { IsAuthenticated = true };
        PreviewService preview = new(null!, profile, transient, new PreviewState(), request);
        TestUserInterfaceService userInterface = new();
        SessionExitService exit = new(preview, request, transient, profile, userInterface);

        await exit.ExitAsync();

        Assert.That(request.HasPendingRequest, Is.False);
        Assert.That(pendingWhenWiped, Is.False);
        Assert.That(File.Exists(path), Is.False);
        Assert.That(profile.IsAuthenticated, Is.False);
        Assert.That(userInterface.Destinations, Is.EqualTo(new[] { AppDestination.Login }));
        Assert.That(await preview.PrepareWithPasswordAsync("password"), Is.EqualTo(PreviewPreparationStatus.Cancelled));
    }

    private TransientFileService CreateTransientService() => new(new TestFileService(_directory), new TestFileWiper());

    private sealed class TestUserInterfaceService : IUserInterfaceService
    {
        public bool IsShellAvailable => true;
        public bool CanProcessIncomingFiles { get; init; }
        public bool CanReceiveIncomingFiles { get; init; }
        public List<AppDestination> Destinations { get; } = [];
        public List<string> Messages { get; } = [];
        public int BackCount { get; private set; }
        public Task InvokeOnMainThreadAsync(Func<Task> action) => action();
        public Task DisplayMessageAsync(string message)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
        public Task<bool> DisplayConfirmationAsync(string message) => throw new NotSupportedException();
        public Task<string?> DisplayPromptAsync(string message, string initialValue) => throw new NotSupportedException();
        public Task DisplayTransientMessageAsync(string message) => DisplayMessageAsync(message);
        public Task NavigateToAsync(AppDestination destination)
        {
            Destinations.Add(destination);
            return Task.CompletedTask;
        }
        public Task NavigateToAsync(AppDestination destination, object parameter) => NavigateToAsync(destination);
        public Task GoBackAsync()
        {
            BackCount++;
            return Task.CompletedTask;
        }
        public Task OpenBrowserAsync(string url) => throw new NotSupportedException();
    }

    private sealed class TestProfileService : IProfileService
    {
        public string CurrentEmail => "test@example.com";
        public bool IsAuthenticated { get; set; }
        public List<string> RecordedPasswords { get; } = [];
        public Task<bool> HasProfileAsync() => Task.FromResult(true);
        public Task<SignInKey?> LoadProfileAsync() => throw new NotSupportedException();
        public Task<ProfileActionResult> CreateProfileAsync(string email, string password) => throw new NotSupportedException();
        public Task<ProfileActionResult> LoginAsync(string password) => throw new NotSupportedException();
        public void SignOut() => IsAuthenticated = false;
        public Identity GetIdentity() => new(IsAuthenticated ? "password" : "profile", []);
        public IReadOnlyList<PasswordUsage> GetExtraPasswords() => [];
        public Task RecordExtraPasswordUseAsync(string password)
        {
            RecordedPasswords.Add(password);
            return Task.CompletedTask;
        }
        public PublicKey GetPublicKey() => null!;
    }

    private sealed class TestCoreServices(Exception? decryptionFailure = null) : ICoreServices
    {
        public Task EncryptAsync(Stream cleartext, Stream encrypted, EncryptRequest request) => cleartext.CopyToAsync(encrypted);
        public Task<IDecryptionSession> OpenDecryptionAsync(Stream encrypted, DecryptRequest request) =>
            Task.FromResult<IDecryptionSession>(new TestDecryptionSession(request.Identities[0].Passphrase == "password", decryptionFailure));
        public Task<KeyPair> CreateKeyPairAsync(string email, string passphrase, DateTimeOffset createdUtc) => throw new NotSupportedException();
        public bool TryLoadKeyPair(ReadOnlyMemory<byte> encryptedKeyPair, IReadOnlyList<string> passphrases,
            [NotNullWhen(true)] out LoadedKeyPair? loadedKeyPair) => throw new NotSupportedException();
        public string ExportPublicKey(PublicKey publicKey) => throw new NotSupportedException();
        public PublicKey? ImportPublicKey(string serializedPublicKey) => throw new NotSupportedException();
        public PrivateKeyImportResult ImportPrivateKeys(string serializedAccounts, PrivateKeyImportRequest request) => throw new NotSupportedException();
        public bool TryParseEmail(string email, [NotNullWhen(true)] out string? address) => throw new NotSupportedException();
    }

    private sealed class TestDecryptionSession(bool isDecryptable, Exception? failure) : IDecryptionSession
    {
        public bool IsDecryptable => isDecryptable;
        public string OriginalFileName => "input.txt";
        public DateTime CreationTimeUtc => DateTime.UnixEpoch;
        public DateTime LastAccessTimeUtc => DateTime.UnixEpoch;
        public DateTime LastWriteTimeUtc => DateTime.UnixEpoch;
        public EncryptedWithParameters EncryptedWithParameters => throw new NotSupportedException();
        public async Task DecryptAsync(Stream cleartext)
        {
            if (failure is not null)
            {
                throw failure;
            }

            await cleartext.WriteAsync(new ReadOnlyMemory<byte>([.. "decrypted"u8]));
        }
        public void Dispose() { }
    }

    private sealed class TestWorkFolderOperationService : IWorkFolderOperationService
    {
        public bool HasPendingPasswordRequest => false;
        public Task EncryptAsync(WorkFolderFile file) => throw new NotSupportedException();
        public Task<bool> DecryptWithKnownPasswordsAsync(WorkFolderFile file) => throw new NotSupportedException();
        public Task<bool> DecryptWithPasswordAsync(string password) => throw new NotSupportedException();
        public void CancelPasswordRequest() => throw new NotSupportedException();
    }

    private sealed class TestFileWiper(Action? onWipe = null) : IFileWiper
    {
        public Task<FileWipeStatus> WipeAsync(IPickedWritableFile file) => throw new NotSupportedException();
        public Task OverwriteAsync(Stream stream, long length)
        {
            onWipe?.Invoke();
            return Task.CompletedTask;
        }
    }

    private sealed class TestFileService(string directory) : IFileService
    {
        public string CacheDirectory => directory;
        public string AppDataDirectory => throw new NotSupportedException();
        public string PlatformId => throw new NotSupportedException();
        public Task<PickedFile?> PickFileAsync(string pickerTitle, FilePickerKind pickerKind) => throw new NotSupportedException();
        public Task<IPickedWritableFile?> PickWritableFileAsync(string pickerTitle, FilePickerKind pickerKind) => throw new NotSupportedException();
        public Task<bool> OpenInAsync(string filePath, string displayName) => throw new NotSupportedException();
        public Task SendToAsync(string filePath, string displayName, string contentType) => throw new NotSupportedException();
        public Task<SaveFileResult> SaveAsAsync(Stream stream, string displayName, string originalSourcePath) => throw new NotSupportedException();
        public Task<bool> CanViewFileAsync(DecryptedFileInfo file) => throw new NotSupportedException();
        public Task ViewFileAsync(DecryptedFileInfo file) => throw new NotSupportedException();
        public bool IsSelfHandoffReference(string reference) => throw new NotSupportedException();
    }
}
