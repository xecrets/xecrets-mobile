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
    private string _cacheDirectory = string.Empty;

    [SetUp]
    public void SetUp() => _cacheDirectory = Directory.CreateTempSubdirectory().FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(_cacheDirectory, recursive: true);

    [Test]
    public async Task SuspendedWipeReturnsControlAndExcludesIncomingCopy()
    {
        TaskCompletionSource releaseWipe = new();
        int wipeCount = 0;
        TransientFileService transient = CreateService(async (_, _) =>
        {
            wipeCount++;
            await releaseWipe.Task.WaitAsync(TimeSpan.FromSeconds(5));
        });
        await File.WriteAllTextAsync(transient.CreateIncomingPath("first.txt"), "first");
        await File.WriteAllTextAsync(transient.CreateIncomingPath("second.txt"), "second");
        IncomingFileService incoming = CreateIncomingService(transient);
        bool copyStarted = false;

        Task cleanup = transient.MaybeWipeTrackedFilesAsync();
        Task receive = incoming.ReceiveAsync(async () =>
        {
            copyStarted = true;
            string path = transient.CreateIncomingPath("new.txt");
            await File.WriteAllTextAsync(path, "new");
            return new IncomingFileInfo(path, "new.txt", "text/plain");
        });
        try
        {
            Assert.That(cleanup.IsCompleted, Is.False);
            Assert.That(wipeCount, Is.EqualTo(1));
            Assert.That(copyStarted, Is.False);
        }
        finally
        {
            releaseWipe.SetResult();
        }
        await Task.WhenAll(cleanup, receive).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(wipeCount, Is.EqualTo(2));
        Assert.That(Directory.GetFiles(_cacheDirectory, "*", SearchOption.AllDirectories), Has.Length.EqualTo(1));
    }

    [Test]
    public async Task WipeSkipsWithoutBlockingWhileIncomingCopyHoldsGateThenRunsOnceItIsFree()
    {
        TaskCompletionSource releaseCopy = new();
        bool wiped = false;
        TransientFileService transient = CreateService((_, _) =>
        {
            wiped = true;
            return Task.CompletedTask;
        });
        IncomingFileService incoming = CreateIncomingService(transient);
        Task receive = incoming.ReceiveAsync(async () =>
        {
            string path = transient.CreateIncomingPath("new.txt");
            await File.WriteAllTextAsync(path, "new");
            await releaseCopy.Task;
            return new IncomingFileInfo(path, "new.txt", "text/plain");
        });

        // The gate is held by the in-flight receive; wipe must not wait for it - it skips immediately,
        // leaving cleanup for another opportunity, so it can never end up deadlocked behind a caller that
        // holds the gate across navigation.
        await transient.MaybeWipeTrackedFilesAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(wiped, Is.False);

        releaseCopy.SetResult();
        await receive.WaitAsync(TimeSpan.FromSeconds(5));

        // Once the gate is free again, a wipe actually runs.
        await transient.MaybeWipeTrackedFilesAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(wiped, Is.True);
        Assert.That(Directory.Exists(Path.Combine(_cacheDirectory, "XecretsHandoff")), Is.False);
    }

    [Test]
    public async Task IncomingFailureReleasesGate()
    {
        TransientFileService transient = CreateService((_, _) => Task.CompletedTask);
        IncomingFileService incoming = CreateIncomingService(transient);
        await Assert.ThatAsync(async () => await incoming.ReceiveAsync(async () =>
        {
            await Task.Yield();
            throw new IOException("Copy failed.");
        }), Throws.TypeOf<IOException>());
        await transient.MaybeWipeTrackedFilesAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task WipeFailureContinuesAndReleasesGate()
    {
        int wipeCount = 0;
        TransientFileService transient = CreateService(async (_, _) =>
        {
            wipeCount++;
            await Task.Yield();
            throw new IOException("Wipe failed.");
        });
        await File.WriteAllTextAsync(transient.CreateIncomingPath("first.txt"), "first");
        await File.WriteAllTextAsync(transient.CreateIncomingPath("second.txt"), "second");
        await transient.MaybeWipeTrackedFilesAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(wipeCount, Is.EqualTo(2));

        // Even though the overwrite itself failed for both files, they must still have been removed -
        // cleanup always tries to delete, not just when the overwrite succeeded.
        Assert.That(Directory.Exists(Path.Combine(_cacheDirectory, "XecretsHandoff")), Is.False);

        await transient.RunExclusiveAsync(() => Task.CompletedTask).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task IncomingProcessingHoldsGateAcrossNavigationAndWipeSkipsMeanwhile(bool pending, bool fail)
    {
        TaskCompletionSource releaseNavigation = new();
        TaskCompletionSource navigationStarted = new();
        TransientFileService transient = CreateService((_, _) => Task.CompletedTask);
        TestUserInterfaceService userInterface = new()
        {
            CanProcessIncomingFiles = !pending,
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
        IncomingFileService incoming = new(new TestProfileService(), transient, null!, null!, null!, userInterface);
        Task processing = incoming.ReceiveAsync(async () =>
        {
            string path = transient.CreateIncomingPath("new.txt");
            await File.WriteAllTextAsync(path, "new");
            return new IncomingFileInfo(path, "new.txt", "text/plain");
        });
        if (pending)
        {
            await processing;
            processing = incoming.ProcessPendingAsync();
        }
        await navigationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // The gate is held across the still-in-flight navigation. A concurrent wipe must not block waiting
        // for it - it skips immediately (proven here by the tracked directory surviving the call) rather
        // than risk deadlocking behind a caller that will not release the gate until navigation completes.
        await transient.MaybeWipeTrackedFilesAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(Directory.Exists(Path.Combine(_cacheDirectory, "XecretsHandoff")), Is.True);

        releaseNavigation.SetResult();
        if (fail)
        {
            await Assert.ThatAsync(async () => await processing.WaitAsync(TimeSpan.FromSeconds(5)), Throws.TypeOf<IOException>());
        }
        else
        {
            await processing.WaitAsync(TimeSpan.FromSeconds(5));
        }

        // Now that processing has finished and released the gate, a wipe actually runs.
        await transient.MaybeWipeTrackedFilesAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(Directory.Exists(Path.Combine(_cacheDirectory, "XecretsHandoff")), Is.False);
    }

    private TransientFileService CreateService(Func<Stream, long, Task> overwriteAsync)
        => new(new TestFileService(_cacheDirectory), new TestFileWiper(overwriteAsync));

    private static IncomingFileService CreateIncomingService(ITransientFileService transient)
        => new(null!, transient, null!, null!, null!, new TestUserInterfaceService());

    private sealed class TestFileWiper(Func<Stream, long, Task> overwriteAsync) : IFileWiper
    {
        public Task<FileWipeStatus> WipeAsync(IPickedWritableFile file) => throw new NotSupportedException();
        public Task OverwriteAsync(Stream stream, long length) => overwriteAsync(stream, length);
    }

    private sealed class TestUserInterfaceService : IUserInterfaceService
    {
        public bool IsShellAvailable { get; init; }
        public bool CanProcessIncomingFiles { get; init; }
        public Func<Task> NavigateAsync { get; init; } = () => throw new NotSupportedException();
        public Task InvokeOnMainThreadAsync(Func<Task> action) => action();
        public Task DisplayMessageAsync(string message) => throw new NotSupportedException();
        public Task<bool> DisplayConfirmationAsync(string message) => throw new NotSupportedException();
        public Task<string?> DisplayPromptAsync(string message, string initialValue) => throw new NotSupportedException();
        public Task DisplayTransientMessageAsync(string message) => throw new NotSupportedException();
        public Task NavigateToAsync(AppDestination destination) => NavigateAsync();
        public Task NavigateToAsync(AppDestination destination, object parameter) => throw new NotSupportedException();
        public Task GoBackAsync() => throw new NotSupportedException();
        public Task OpenBrowserAsync(string url) => throw new NotSupportedException();
    }

    private sealed class TestProfileService : IProfileService
    {
        public string CurrentEmail => throw new NotSupportedException();
        public bool IsAuthenticated => false;
        public Task<bool> HasProfileAsync() => Task.FromResult(true);
        public Task<SignInKey?> LoadProfileAsync() => throw new NotSupportedException();
        public Task<ProfileActionResult> CreateProfileAsync(string email, string password) => throw new NotSupportedException();
        public Task<ProfileActionResult> LoginAsync(string password) => throw new NotSupportedException();
        public void SignOut() => throw new NotSupportedException();
        public Identity GetIdentity() => throw new NotSupportedException();
        public IReadOnlyList<PasswordUsage> GetExtraPasswords() => throw new NotSupportedException();
        public Task RecordExtraPasswordUseAsync(string password) => throw new NotSupportedException();
        public PublicKey GetPublicKey() => throw new NotSupportedException();
    }

    private sealed class TestFileService(string cacheDirectory) : IFileService
    {
        public string CacheDirectory => cacheDirectory;
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
