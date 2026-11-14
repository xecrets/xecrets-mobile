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

using Xecrets.Common.Models;
using Xecrets.Core.Models;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Services;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class TransientFileServiceTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

    private string _cacheDirectory = "";

    private FakeEzLogging _log = new();

    [SetUp]
    public void SetUp()
    {
        _cacheDirectory = Directory.CreateTempSubdirectory().FullName;
        _log = new FakeEzLogging();
    }

    // A wipe removes the cache directory itself once it is empty.
    [TearDown]
    public void TearDown()
    {
        if (!Directory.Exists(_cacheDirectory))
        {
            return;
        }

        foreach (string file in Directory.GetFiles(_cacheDirectory, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_cacheDirectory, recursive: true);
    }

    [Test]
    public async Task ReleasedFileIsWiped()
    {
        TransientFileService transient = CreateService(_ => Task.CompletedTask);
        ITransientFile file = transient.CreateEncryptedOutputFile("a.txt.axx");
        await File.WriteAllTextAsync(file.FilePath, "encrypted");

        file.Dispose();
        await WhenSweptAsync(transient);

        Assert.That(Directory.Exists(Path.GetDirectoryName(file.FilePath)), Is.False);
    }

    [Test]
    public async Task FileInUseIsNotSwept()
    {
        TransientFileService transient = CreateService(_ => Task.CompletedTask);
        ITransientFile file = transient.CreateIncomingFile("a.txt");
        await File.WriteAllTextAsync(file.FilePath, "received");
        Directory.SetLastWriteTimeUtc(Path.GetDirectoryName(file.FilePath)!, DateTime.UtcNow.AddDays(-1));

        await transient.SweepAsync(TimeSpan.Zero).WaitAsync(_timeout);

        Assert.That(File.Exists(file.FilePath), Is.True);
    }

    [Test]
    public async Task HandedOffFileIsKeptUntilReleasedLongerAgoThanTheMaxAge()
    {
        TransientFileService transient = CreateService(_ => Task.CompletedTask);
        ITransientFile file = transient.CreateHandoffFile("a.txt");
        await File.WriteAllTextAsync(file.FilePath, "decrypted");
        file.MarkHandedOff();
        file.Dispose();

        await transient.SweepAsync(TimeSpan.FromHours(1)).WaitAsync(_timeout);
        Assert.That(File.Exists(file.FilePath), Is.True);

        string directory = Path.GetDirectoryName(file.FilePath)!;
        Directory.SetLastWriteTimeUtc(directory, DateTime.UtcNow.AddHours(-2));
        await transient.SweepAsync(TimeSpan.FromHours(1)).WaitAsync(_timeout);
        Assert.That(Directory.Exists(directory), Is.False);
    }

    /// <summary>
    /// A file may be handed off long after it was made, such as when a preview has been open for a while.
    /// </summary>
    [Test]
    public async Task AgeOfHandedOffFileStartsWhenItIsReleased()
    {
        TransientFileService transient = CreateService(_ => Task.CompletedTask);
        ITransientFile file = transient.CreateHandoffFile("a.txt");
        await File.WriteAllTextAsync(file.FilePath, "decrypted");
        Directory.SetLastWriteTimeUtc(Path.GetDirectoryName(file.FilePath)!, DateTime.UtcNow.AddHours(-2));
        file.MarkHandedOff();
        file.Dispose();

        await transient.SweepAsync(TimeSpan.FromHours(1)).WaitAsync(_timeout);

        Assert.That(File.Exists(file.FilePath), Is.True);
    }

    /// <summary>
    /// The folders that the files are made in are left during use, since a new file may be made in one at any time.
    /// </summary>
    [Test]
    public async Task SweepDuringUseKeepsTheFoldersTheFilesAreMadeIn()
    {
        TransientFileService transient = CreateService(_ => Task.CompletedTask);
        ITransientFile file = transient.CreateIncomingFile("a.txt");
        await File.WriteAllTextAsync(file.FilePath, "received");
        file.Dispose();

        await transient.SweepAsync(TimeSpan.Zero).WaitAsync(_timeout);

        Assert.That(Directory.Exists(Path.GetDirectoryName(file.FilePath)), Is.False);
        Assert.That(Directory.Exists(Path.Combine(_cacheDirectory, "XecretsHandoff", "incoming")), Is.True);
    }

    [Test]
    public async Task SweepAllReleasesFilesInUseAndWipesEverything()
    {
        TransientFileService transient = CreateService(_ => Task.CompletedTask);
        ITransientFile file = transient.CreateHandoffFile("a.txt");
        await File.WriteAllTextAsync(file.FilePath, "decrypted");

        await transient.SweepAllAsync().WaitAsync(_timeout);

        Assert.That(Directory.Exists(_cacheDirectory), Is.False);
    }

    [Test]
    public async Task FileReleasedAfterSweepAllIsGone()
    {
        TransientFileService transient = CreateService(_ => Task.CompletedTask);
        ITransientFile file = transient.CreateHandoffFile("a.txt");
        await File.WriteAllTextAsync(file.FilePath, "decrypted");
        file.MarkHandedOff();
        await transient.SweepAllAsync().WaitAsync(_timeout);

        file.Dispose();
        await WhenSweptAsync(transient);

        Assert.That(Directory.Exists(_cacheDirectory), Is.False);
    }

    [Test]
    public async Task SweepsAreDoneOneAtATimeInTheOrderAsked()
    {
        TaskCompletionSource wipeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseWipe = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TransientFileService transient = CreateService(async _ =>
        {
            wipeStarted.TrySetResult();
            await releaseWipe.Task;
        });
        ITransientFile first = transient.CreateHandoffFile("first.txt");
        await File.WriteAllTextAsync(first.FilePath, "first");
        ITransientFile second = transient.CreateHandoffFile("second.txt");
        await File.WriteAllTextAsync(second.FilePath, "second");

        first.Dispose();
        await wipeStarted.Task.WaitAsync(_timeout);
        second.Dispose();
        Task later = transient.SweepAsync(TimeSpan.Zero);
        Assert.That(later.IsCompleted, Is.False);

        releaseWipe.SetResult();
        await later.WaitAsync(_timeout);
        Assert.That(File.Exists(first.FilePath), Is.False);
        Assert.That(File.Exists(second.FilePath), Is.False);
    }

    [Test]
    public async Task FileBeingReceivedIsNotSwept()
    {
        TaskCompletionSource copying = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseCopy = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TransientFileService transient = CreateService(_ => Task.CompletedTask);
        IncomingFileService incoming = CreateIncomingService();
        string path = "";
        Task receive = incoming.ReceiveAsync(new TestIncomingFileSource(async () =>
        {
            ITransientFile file = transient.CreateIncomingFile("new.txt");
            path = file.FilePath;
            await File.WriteAllTextAsync(path, "new");
            copying.SetResult();
            await releaseCopy.Task;
            return new IncomingFileInfo(file, "new.txt", "text/plain");
        }));
        await copying.Task.WaitAsync(_timeout);

        await transient.SweepAsync(TimeSpan.Zero).WaitAsync(_timeout);
        Assert.That(File.Exists(path), Is.True);

        releaseCopy.SetResult();
        await receive.WaitAsync(_timeout);
        Assert.That(File.Exists(path), Is.True);
    }

    [Test]
    public async Task IncomingFailureReleasesGate()
    {
        TransientFileService transient = CreateService(_ => Task.CompletedTask);
        IncomingFileService incoming = CreateIncomingService();
        await Assert.ThatAsync(async () => await incoming.ReceiveAsync(new TestIncomingFileSource(async () =>
        {
            await Task.Yield();
            throw new IOException("Copy failed.");
        })), Throws.TypeOf<IOException>());

        bool imported = false;
        await incoming.ReceiveAsync(new TestIncomingFileSource(() =>
        {
            imported = true;
            return Task.FromResult(new IncomingFileInfo(transient.CreateIncomingFile("new.txt"), "new.txt", "text/plain"));
        })).WaitAsync(_timeout);
        Assert.That(imported, Is.True);
    }

    [Test]
    public async Task WipeFailureContinuesWithTheOtherFiles()
    {
        int wipeCount = 0;
        TransientFileService transient = CreateService(async _ =>
        {
            Interlocked.Increment(ref wipeCount);
            await Task.Yield();
            throw new IOException("Wipe failed.");
        });
        await File.WriteAllTextAsync(transient.CreateIncomingFile("first.txt").FilePath, "first");
        await File.WriteAllTextAsync(transient.CreateIncomingFile("second.txt").FilePath, "second");
        await transient.SweepAllAsync().WaitAsync(_timeout);
        Assert.That(wipeCount, Is.EqualTo(2));

        // Even though overwrite itself failed for both files, they must still have been removed - the wipe
        // always tries to delete, not just when overwrite succeeded.
        Assert.That(Directory.Exists(Path.Combine(_cacheDirectory, "XecretsHandoff")), Is.False);
    }

    [Test]
    public async Task WipeClearsAllCacheEntries()
    {
        string directory = Path.Combine(_cacheDirectory, "legacy-cache");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(_cacheDirectory, "legacy-file"), "legacy");
        await File.WriteAllTextAsync(Path.Combine(directory, "legacy-file"), "legacy");
        TransientFileService transient = CreateService(_ => Task.CompletedTask);

        await transient.SweepAllAsync().WaitAsync(_timeout);

        Assert.That(File.Exists(Path.Combine(_cacheDirectory, "legacy-file")), Is.False);
        Assert.That(Directory.Exists(directory), Is.False);
    }

    [Test]
    public async Task NothingIsCreatedInTheCacheUntilNeeded()
    {
        Directory.Delete(_cacheDirectory);
        TransientFileService transient = CreateService(_ => Task.CompletedTask);

        await transient.SweepAllAsync().WaitAsync(_timeout);

        Assert.That(Directory.Exists(_cacheDirectory), Is.False);
    }

    [Test]
    public async Task WipeRemovesReadOnlyFiles()
    {
        TransientFileService transient = CreateService(_ => Task.CompletedTask);
        string path = transient.CreateHandoffFile("opened.txt").FilePath;
        await File.WriteAllTextAsync(path, "decrypted");
        File.SetAttributes(path, FileAttributes.ReadOnly);

        await transient.SweepAllAsync().WaitAsync(_timeout);

        Assert.That(Directory.Exists(_cacheDirectory), Is.False);
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task IncomingFileWaitingForSignInIsNotSwept(bool pending, bool fail)
    {
        TaskCompletionSource releaseNavigation = new();
        TaskCompletionSource navigationStarted = new();
        TransientFileService transient = CreateService(_ => Task.CompletedTask);
        TestUserInterfaceService userInterface = new()
        {
            CanProcessIncomingFiles = !pending,
            CanReceiveIncomingFiles = true,
            IsShellAvailable = true,
            NavigateAsync = async () =>
            {
                navigationStarted.SetResult();
                await releaseNavigation.Task;
                if (fail)
                {
                    throw new IOException("Navigation failed.");
                }
            }
        };
        IncomingFileService incoming = new(new TestProfileService(), null!, null!, null!, null!, new FakeCrashLogService(), userInterface);
        Task processing = incoming.ReceiveAsync(new TestIncomingFileSource(async () =>
        {
            ITransientFile file = transient.CreateIncomingFile("new.txt");
            await File.WriteAllTextAsync(file.FilePath, "new");
            return new IncomingFileInfo(file, "new.txt", "text/plain");
        }));
        if (pending)
        {
            await processing;
            processing = incoming.ProcessPendingAsync();
        }
        await navigationStarted.Task.WaitAsync(_timeout);

        // The received file is waiting for the user to sign in, so a sweep leaves it.
        await transient.SweepAsync(TimeSpan.Zero).WaitAsync(_timeout);
        Assert.That(Directory.GetFiles(_cacheDirectory, "*", SearchOption.AllDirectories), Has.Length.EqualTo(1));

        releaseNavigation.SetResult();
        if (fail)
        {
            await Assert.ThatAsync(async () => await processing.WaitAsync(_timeout), Throws.TypeOf<IOException>());
        }
        else
        {
            await processing.WaitAsync(_timeout);
        }

        await transient.SweepAllAsync().WaitAsync(_timeout);
        Assert.That(Directory.Exists(Path.Combine(_cacheDirectory, "XecretsHandoff")), Is.False);
    }

    [Test]
    public async Task SweepIsLoggedWhenQueuedStartedAndEndedWithWhatItWiped()
    {
        TransientFileService transient = CreateService(_ => Task.CompletedTask);
        ITransientFile file = transient.CreateHandoffFile("secret.txt");
        await File.WriteAllTextAsync(file.FilePath, "decrypted");
        file.MarkHandedOff();
        file.Dispose();
        string folder = Path.GetRelativePath(_cacheDirectory, Path.GetDirectoryName(file.FilePath)!);

        await transient.SweepAsync(TimeSpan.Zero).WaitAsync(_timeout);

        Assert.That(_log.Entries, Is.EqualTo(new[]
        {
            (EzLogLevel.Debug, "Sweep of XecretsHandoff queued, aged files, max age 00:00:00"),
            (EzLogLevel.Debug, "Sweep of XecretsHandoff started, aged files, max age 00:00:00"),
            (EzLogLevel.Information, $"Wiped {Path.Combine(folder, "se***.txt")}"),
            (EzLogLevel.Debug, $"Removed folder {folder}"),
            (EzLogLevel.Debug, "Sweep of XecretsHandoff ended, 1 wiped, 0 deleted"),
        }));
    }

    [Test]
    public async Task SweepOfAReleasedFileIsLoggedWithItsCause()
    {
        TransientFileService transient = CreateService(_ => Task.CompletedTask);
        ITransientFile file = transient.CreateEncryptedOutputFile("secret.txt.axx");
        await File.WriteAllTextAsync(file.FilePath, "encrypted");
        string folder = Path.GetRelativePath(_cacheDirectory, Path.GetDirectoryName(file.FilePath)!);

        file.Dispose();
        await WhenSweptAsync(transient);

        Assert.That(_log.Texts, Does.Contain($"Sweep of {folder} queued, released file, max age 00:00:00"));
        Assert.That(_log.Texts, Does.Contain($"Wiped {Path.Combine(folder, "se***.axx")}"));
    }

    [Test]
    public async Task SweepAllIsLoggedWithoutTheNamesOfTheFiles()
    {
        TransientFileService transient = CreateService(_ => Task.CompletedTask);
        await File.WriteAllTextAsync(transient.CreateIncomingFile("secret.txt").FilePath, "received");
        await File.WriteAllTextAsync(transient.CreateHandoffFile("private.txt").FilePath, "decrypted");

        await transient.SweepAllAsync().WaitAsync(_timeout);

        Assert.That(_log.Texts, Does.Contain("Sweep of [cache…] started, all files, max age 00:00:00"));
        Assert.That(_log.Texts, Does.Contain("Sweep of [cache…] ended, 2 wiped, 0 deleted"));
        Assert.That(_log.Texts, Does.Contain("Removed folder [cache…]"));
        Assert.That(_log.Texts, Has.None.Contains("secret"));
        Assert.That(_log.Texts, Has.None.Contains("private"));
    }

    // Sweeps are done in the order asked for, so one asked for now is done once those asked for before it are. With
    // the largest age, it wipes nothing itself.
    private static Task WhenSweptAsync(TransientFileService transient) =>
        transient.SweepAsync(TimeSpan.MaxValue).WaitAsync(_timeout);

    private TransientFileService CreateService(Func<IWritableFile, Task> onOverwriteAsync)
        => new(new TestFileService(_cacheDirectory), new TestFileWiper(onOverwriteAsync), new FakeCrashLogService(), _log);

    private static IncomingFileService CreateIncomingService()
        => new(null!, null!, null!, null!, null!, new FakeCrashLogService(), new TestUserInterfaceService { CanReceiveIncomingFiles = true });

    // The real wiper, calling the action when a file is opened to be overwritten, so that a failure of the action is
    // a failure during the wipe.
    private sealed class TestFileWiper(Func<IWritableFile, Task> onOverwriteAsync) : IFileWiper
    {
        private readonly FileWiper _wiper = new(new FakeCrashLogService());

        public Task<bool> CanWipeAsync(IWritableFile file) => _wiper.CanWipeAsync(file);

        public Task<FileWipeStatus> WipeAsync(IWritableFile file, bool isOnDevice) =>
            _wiper.WipeAsync(new OverwriteHookFile(file, onOverwriteAsync), isOnDevice);
    }

    private sealed class OverwriteHookFile(IWritableFile file, Func<IWritableFile, Task> onOverwriteAsync)
        : IWritableFile
    {
        public IDisposable AcquireAccess() => file.AcquireAccess();
        public Task<bool> CanWriteAsync() => file.CanWriteAsync();
        public Task<bool> CanDeleteAsync() => file.CanDeleteAsync();
        public Task<long?> GetLengthAsync() => file.GetLengthAsync();
        public async Task OverwriteAsync(Action<Stream> overwrite)
        {
            await onOverwriteAsync(file);
            await file.OverwriteAsync(overwrite);
        }
        public Task DeleteAsync() => file.DeleteAsync();
    }

    private sealed class TestUserInterfaceService : IUserInterfaceService
    {
        public bool IsShellAvailable { get; init; }
        public bool CanProcessIncomingFiles { get; init; }
        public bool CanReceiveIncomingFiles { get; init; }
        public string CurrentLocation => "";
        public bool IsNavigating => false;
        public IReadOnlyDictionary<string, string> IconMap => throw new NotSupportedException();
        public Func<Task> NavigateAsync { get; init; } = () => throw new NotSupportedException();
        public Task InvokeOnMainThreadAsync(Func<Task> action) => action();
        public Task DisplayMessageAsync(string message) => throw new NotSupportedException();
        public Task<bool> DisplayConfirmationAsync(string message, string accept, string cancel) => throw new NotSupportedException();
        public Task<string?> DisplayPromptAsync(string message, string initialValue) => throw new NotSupportedException();
        public Task<SaveChangesChoice> DisplaySaveChangesAsync(string message) => throw new NotSupportedException();
        public Task DisplayTransientMessageAsync(string message) => throw new NotSupportedException();
        public Task NavigateToAsync(AppDestination destination, object? parameter) =>
            parameter is null ? NavigateAsync() : throw new NotSupportedException();
        public Task NavigateReplacingAsync(AppDestination destination, object? parameter) =>
            throw new NotSupportedException();
        public Task GoBackAsync(object? parameter) => throw new NotSupportedException();
        public Task OpenBrowserAsync(string url) => throw new NotSupportedException();

        public Task SetSensitiveClipboardTextAsync(string text, bool confirm) => throw new NotSupportedException();
    }

    private sealed class TestProfileService : IProfileService
    {
        public string CurrentEmail => throw new NotSupportedException();
        public string CurrentPassword => throw new NotSupportedException();
        public bool IsAuthenticated => false;
        public Task<bool> HasProfileAsync() => Task.FromResult(true);
        public Task<SignInKey?> LoadProfileAsync() => throw new NotSupportedException();
        public Task<ProfileActionResult> CreateProfileAsync(string email, string password) => throw new NotSupportedException();
        public Task<ProfileActionResult> ChangeProfileAsync(string email, string password) => throw new NotSupportedException();
        public Task<ProfileActionResult> LoginAsync(string password) => throw new NotSupportedException();
        public Task ResetProfileAsync(string password) => throw new NotSupportedException();
        public void SignOut() => throw new NotSupportedException();
        public Identity GetIdentity() => throw new NotSupportedException();
        public IReadOnlyList<PasswordUsage> GetExtraPasswords() => throw new NotSupportedException();
        public Task RecordExtraPasswordUseAsync(string password) => throw new NotSupportedException();
        public Task AddExtraPasswordAsync(string password) => throw new NotSupportedException();
        public Task RemoveExtraPasswordAsync(string password) => throw new NotSupportedException();
        public PublicKey GetPublicKey() => throw new NotSupportedException();
        public Task<bool> ShouldShowAsync(DontShowAgain notice) => throw new NotSupportedException();
        public Task SetDontShowAgainAsync(DontShowAgain notice) => throw new NotSupportedException();
        public Task<TextEditor> GetTextEditorSettingsAsync() => throw new NotSupportedException();
        public Task SetTextEditorFontSizeAsync(double fontSize) => throw new NotSupportedException();
        public Task SetTextEditorProportionalFontAsync(bool proportionalFont) => throw new NotSupportedException();
    }

    private sealed class TestFileService(string cacheDirectory) : IFileService
    {
        public string CacheDirectory => cacheDirectory;
        public string AppDataDirectory => throw new NotSupportedException();
        public string PlatformId => throw new NotSupportedException();
        public Task<bool> OpenInAsync(string filePath, string displayName) => throw new NotSupportedException();
        public Task<bool> SendToAsync(string filePath, string displayName, string contentType) => throw new NotSupportedException();
        public Task<bool> CanViewFileAsync(DecryptedFileInfo file) => throw new NotSupportedException();
        public Task<bool> ViewFileAsync(DecryptedFileInfo file) => throw new NotSupportedException();
        public bool IsSelfHandoffReference(string reference) => throw new NotSupportedException();
    }
}
