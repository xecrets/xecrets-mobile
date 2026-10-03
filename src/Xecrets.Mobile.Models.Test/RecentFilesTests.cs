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
        TestUserDataStore store = new() { Files = InPlace("a.axx", "b.txt", "c.txt") };
        RecentFilesService service = new(SignedIn(store), new FlowContext());

        await service.AddAsync("b-txt.axx", RecentFileOperation.InPlace);

        Assert.That(Ids(await service.GetFilesAsync()), Is.EqualTo(["b-txt.axx", "a.axx", "b.txt", "c.txt"]));
    }

    [Test]
    public async Task RecordReplacesAnEarlierEntryForTheSameFileWithTheNewOperation()
    {
        TestUserDataStore store = new() { Files = InPlace("a.axx", "b-txt.axx", "b.txt") };
        RecentFilesService service = new(SignedIn(store), new FlowContext());

        await service.AddAsync("b-txt.axx", RecentFileOperation.View);

        Assert.That(
            (await service.GetFilesAsync()).Select(file => (file.Id, file.Operation)),
            Is.EqualTo([
                ("b-txt.axx", RecentFileOperation.View),
                ("a.axx", RecentFileOperation.InPlace),
                ("b.txt", RecentFileOperation.InPlace),
            ]));
    }

    [Test]
    public async Task RecordTransformCapsTheList()
    {
        TestUserDataStore store = new() { Files = InPlace([.. Enumerable.Range(0, 50).Select(i => $"{i}.txt")]) };
        RecentFilesService service = new(SignedIn(store), new FlowContext());

        await service.AddAsync("result.axx", RecentFileOperation.InPlace);

        IReadOnlyList<string> files = Ids(await service.GetFilesAsync());
        Assert.That(files, Has.Count.EqualTo(50));
        Assert.That(files[0], Is.EqualTo("result.axx"));
        Assert.That(files[^1], Is.EqualTo("48.txt"));
    }

    [Test]
    public async Task FlowSourceIsRecordedOnlyWhenThereIsOne()
    {
        TestUserDataStore store = new();
        FlowContext flow = new();
        RecentFilesService service = new(SignedIn(store), flow);

        flow.Begin(FlowOrigin.ReceivedFile, WorkFolderOperation.Decrypt);
        await service.AddFlowSourceAsync(RecentFileOperation.View);
        flow.Begin(FlowOrigin.Navigated, WorkFolderOperation.Decrypt, CreateFile("folder/secret.axx"));
        await service.AddFlowSourceAsync(RecentFileOperation.Edit);

        Assert.That(
            store.Files.Select(file => (file.Id, file.Operation)),
            Is.EqualTo([("folder/secret.axx", RecentFileOperation.Edit)]));
    }

    [Test]
    public async Task FlowSourceOutsideTheKnownFoldersIsNotRecorded()
    {
        TestUserDataStore store = new();
        FlowContext flow = new();
        flow.Begin(
            FlowOrigin.Navigated,
            WorkFolderOperation.Decrypt,
            CreateFile("cloud/secret.axx") with { IsInKnownWorkFolder = false });
        RecentFilesService service = new(SignedIn(store), flow);

        await service.AddFlowSourceAsync(RecentFileOperation.View);
        await service.AddSavedCopyAsync(CreateFile("folder/secret.txt"), RecentFileOperation.DecryptCopySaveAs);

        Assert.That(store.Files, Is.Empty);
    }

    [Test]
    public async Task SavedCopyRecordsTheSourceWithTheOperation()
    {
        TestUserDataStore store = new();
        FlowContext flow = new();
        flow.Begin(FlowOrigin.Navigated, WorkFolderOperation.Encrypt, CreateFile("folder/plain.txt"));
        RecentFilesService service = new(SignedIn(store), flow);

        await service.AddSavedCopyAsync(CreateFile("other/plain-txt.axx"), RecentFileOperation.EncryptCopySaveAs);

        Assert.That(
            store.Files.Select(file => (file.Id, file.Operation)),
            Is.EqualTo([("folder/plain.txt", RecentFileOperation.EncryptCopySaveAs)]));
    }

    [TestCase(true, new[] { "other/received-txt.axx" })]
    [TestCase(false, new string[0])]
    public async Task SavedCopyOfAReceivedFileIsRecordedInPlaceIfItCanBeOpenedAgain(bool isInKnownFolder, string[] expected)
    {
        TestUserDataStore store = new();
        FlowContext flow = new();
        flow.Begin(FlowOrigin.ReceivedFile, WorkFolderOperation.Encrypt);
        RecentFilesService service = new(SignedIn(store), flow);

        await service.AddSavedCopyAsync(
            CreateFile("other/received-txt.axx") with { IsInKnownWorkFolder = isInKnownFolder },
            RecentFileOperation.EncryptCopySaveAs);

        Assert.That(
            store.Files.Select(file => (file.Id, file.Operation)),
            Is.EqualTo(expected.Select(id => (id, RecentFileOperation.InPlace))));
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
            new TestFileWiper(),
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
            new TestFileWiper(),
            new TestUserInterfaceService { Confirmation = false });

        Assert.That(
            async () => await operations.EncryptAsync(CreateFile("folder/plain.txt")),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(recentFiles.Files, Is.Empty);
    }

    [TestCase([new[] { "a.axx", "b.txt" }])]
    [TestCase([new[] { "a.axx" }])]
    [TestCase([new string[0]])]
    public async Task DefaultFilterShowsAll(string[] files)
    {
        RecentFilesPageModel page = CreatePage(new TestRecentFilesService { Files = [.. files] }, new TestWorkFolderService());

        await page.LoadCommand.ExecuteAsync(null);

        Assert.That(page.SelectedState, Is.EqualTo(SelectedFileState.All));
        Assert.That(page.Files.Select(file => file.Id), Is.EqualTo(files));
    }

    [Test]
    public async Task MissingFilesAreRemovedAndInaccessibleFilesAreListed()
    {
        TestWorkFolderService folders = new();
        folders.Missing.Add("folder/gone.txt");
        folders.Inaccessible.Add("folder/locked.txt");
        TestRecentFilesService recentFiles = new() { Files = ["folder/gone.txt", "folder/locked.txt", "folder/open.txt"] };
        RecentFilesPageModel page = CreatePage(recentFiles, folders);

        await page.LoadCommand.ExecuteAsync(null);

        Assert.That(page.Files.Select(file => file.Id), Is.EqualTo(["folder/locked.txt", "folder/open.txt"]));
        Assert.That(recentFiles.Files, Is.EqualTo(["folder/locked.txt", "folder/open.txt"]));
    }

    [Test]
    public async Task MissingFileDoesNotReappearWhenItsFolderIsNoLongerKnown()
    {
        TestWorkFolderService folders = new();
        folders.Missing.Add("folder/gone.txt");
        TestRecentFilesService recentFiles = new() { Files = ["folder/gone.txt", "folder/open.txt"] };
        RecentFilesPageModel page = CreatePage(recentFiles, folders);
        await page.LoadCommand.ExecuteAsync(null);

        // With the folder removed from the known folders, the file can no longer be found to be missing.
        folders.Missing.Remove("folder/gone.txt");
        folders.Inaccessible.Add("folder/gone.txt");
        await page.ReloadCommand.ExecuteAsync(null);

        Assert.That(page.Files.Select(file => file.Id), Is.EqualTo(["folder/open.txt"]));
    }

    [Test]
    public async Task CompletedOperationIsShownInPlaceUntilTheFilterChanges()
    {
        TestRecentFilesService recentFiles = new() { Files = ["folder/one.txt", "folder/two.txt", "folder/old.axx"] };
        RecentFilesPageModel page = CreatePage(recentFiles, new TestWorkFolderService());
        await page.LoadCommand.ExecuteAsync(null);
        page.SelectedState = SelectedFileState.Decrypted;

        await page.ReverseCommand.ExecuteAsync(page.Files[1]);

        // The source of the operation is gone, so it is removed when the list is loaded again.
        Assert.That(recentFiles.Files, Is.EqualTo(["folder/two-txt.axx", "folder/one.txt", "folder/old.axx"]));
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

    [TestCase(RecentFileOperation.View)]
    [TestCase(RecentFileOperation.Edit)]
    public async Task OpenShowsAViewedFileAgainLikeADecryptedCopy(RecentFileOperation operation)
    {
        TestRecentFilesService recentFiles = new()
        {
            Entries = [new RecentFile { Id = "folder/secret.axx", Operation = operation }],
        };
        TestFileLauncher launcher = new();
        TestUserInterfaceService userInterface = new();
        TestPreviewService preview = new();
        FlowContext flow = new();
        RecentFilesPageModel page = CreatePage(
            recentFiles,
            new TestWorkFolderService(),
            userInterface,
            launcher,
            preview: preview,
            flowContext: flow);
        await page.LoadCommand.ExecuteAsync(null);

        await page.OpenCommand.ExecuteAsync(page.Files[0]);

        Assert.That(preview.Prepared, Is.EqualTo(["secret.axx"]));
        Assert.That(flow.Source?.Id, Is.EqualTo("folder/secret.axx"));
        Assert.That(userInterface.Destinations, Is.EqualTo([AppDestination.Preview]));
        Assert.That(launcher.Shared, Is.Empty);
    }

    [Test]
    public async Task OpenOfAViewedFileThatNeedsAPasswordAsksForIt()
    {
        TestRecentFilesService recentFiles = new()
        {
            Entries = [new RecentFile { Id = "folder/secret.axx", Operation = RecentFileOperation.View }],
        };
        TestUserInterfaceService userInterface = new();
        RecentFilesPageModel page = CreatePage(
            recentFiles,
            new TestWorkFolderService(),
            userInterface,
            preview: new TestPreviewService { NeedsPassword = true });
        await page.LoadCommand.ExecuteAsync(null);

        await page.OpenCommand.ExecuteAsync(page.Files[0]);

        Assert.That(userInterface.Destinations, Is.EqualTo([AppDestination.EnterPassword]));
    }

    [Test]
    public async Task OpenActsOnOtherFilesByTheirState()
    {
        TestRecentFilesService recentFiles = new()
        {
            Entries =
            [
                new RecentFile { Id = "folder/one.txt", Operation = RecentFileOperation.EncryptCopySaveAs },
                new RecentFile { Id = "folder/old.axx", Operation = RecentFileOperation.DecryptCopySendTo },
            ],
        };
        TestFileLauncher launcher = new();
        RecentFilesPageModel page = CreatePage(recentFiles, new TestWorkFolderService(), fileLauncher: launcher);
        await page.LoadCommand.ExecuteAsync(null);

        await page.OpenCommand.ExecuteAsync(page.Files[0]);
        await page.OpenCommand.ExecuteAsync(page.Files[1]);

        Assert.That(launcher.Opened, Is.EqualTo(["folder/one.txt"]));
        Assert.That(launcher.Shared, Is.EqualTo(["folder/old.axx"]));
    }

    [TestCase(
        SelectedFileState.All,
        new[]
        {
            "folder/one.txt", "folder/old.axx", "folder/viewed.axx", "folder/edited.axx", "folder/copied.txt",
            "folder/shared.axx",
        })]
    [TestCase(SelectedFileState.Decrypted, new[] { "folder/one.txt" })]
    [TestCase(SelectedFileState.Encrypted, new[] { "folder/old.axx" })]
    [TestCase(SelectedFileState.Viewed, new[] { "folder/viewed.axx", "folder/edited.axx" })]
    [TestCase(SelectedFileState.Other, new[] { "folder/copied.txt", "folder/shared.axx" })]
    public async Task FilterShowsTheFilesOfItsOperations(SelectedFileState state, string[] expected)
    {
        TestRecentFilesService recentFiles = new()
        {
            Entries =
            [
                new RecentFile { Id = "folder/one.txt", Operation = RecentFileOperation.InPlace },
                new RecentFile { Id = "folder/old.axx", Operation = RecentFileOperation.InPlace },
                new RecentFile { Id = "folder/viewed.axx", Operation = RecentFileOperation.View },
                new RecentFile { Id = "folder/edited.axx", Operation = RecentFileOperation.Edit },
                new RecentFile { Id = "folder/copied.txt", Operation = RecentFileOperation.EncryptCopySaveAs },
                new RecentFile { Id = "folder/shared.axx", Operation = RecentFileOperation.DecryptCopySendTo },
            ],
        };
        RecentFilesPageModel page = CreatePage(recentFiles, new TestWorkFolderService());
        await page.LoadCommand.ExecuteAsync(null);

        page.SelectedState = state;

        Assert.That(page.Files.Select(file => file.Id), Is.EqualTo(expected));
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
        recentFiles.Entries.Add(new RecentFile { Id = "folder/three.txt", Operation = RecentFileOperation.InPlace });

        await page.ReloadCommand.ExecuteAsync(null);

        Assert.That(page.SelectedState, Is.EqualTo(SelectedFileState.Decrypted));
        Assert.That(
            page.Files.Select(file => (file.Id, file.IsCompleted)),
            Is.EqualTo([("folder/one.txt", false), ("folder/three.txt", false)]));
    }

    [Test]
    public async Task AppearingAgainShowsAFileReplacedElsewhereFirst()
    {
        TestRecentFilesService recentFiles = new() { Files = ["folder/one.txt", "folder/two.txt", "folder/three.txt"] };
        RecentFilesPageModel page = CreatePage(recentFiles, new TestWorkFolderService());
        await page.AppearCommand.ExecuteAsync(null);
        recentFiles.Entries.RemoveAt(1);
        recentFiles.Entries.Insert(0, new RecentFile { Id = "folder/saved.txt", Operation = RecentFileOperation.Edit });

        await page.AppearCommand.ExecuteAsync(null);

        Assert.That(
            page.Files.Select(file => file.Id),
            Is.EqualTo(["folder/saved.txt", "folder/one.txt", "folder/three.txt"]));
    }

    [TestCase(SelectedFileState.Decrypted, "folder/new.txt", SelectedFileState.Decrypted, new[] { "folder/new.txt", "folder/one.txt" })]
    [TestCase(SelectedFileState.All, "folder/new.txt", SelectedFileState.Decrypted, new[] { "folder/new.txt", "folder/one.txt" })]
    [TestCase(SelectedFileState.Decrypted, "folder/new.axx", SelectedFileState.Encrypted, new[] { "folder/new.axx", "folder/old.axx" })]
    public async Task AddPicksAFileAndListsItFirstAmongFilesInItsState(
        SelectedFileState initialState, string picked, SelectedFileState expectedState, string[] expected)
    {
        TestRecentFilesService recentFiles = new() { Files = ["folder/one.txt", "folder/old.axx"] };
        TestWorkFolderService folders = new();
        folders.Picked.Enqueue(CreateFile(picked));
        RecentFilesPageModel page = CreatePage(recentFiles, folders);
        await page.LoadCommand.ExecuteAsync(null);
        page.SelectedState = initialState;

        await page.AddCommand.ExecuteAsync(null);

        Assert.That(page.SelectedState, Is.EqualTo(expectedState));
        Assert.That(page.Files.Select(file => file.Id), Is.EqualTo(expected));
        Assert.That(recentFiles.Entries[0].Operation, Is.EqualTo(RecentFileOperation.InPlace));
    }

    [Test]
    public async Task CancelledAddChangesNothing()
    {
        TestRecentFilesService recentFiles = new() { Files = ["folder/one.txt", "folder/old.axx"] };
        TestWorkFolderService folders = new();
        folders.Picked.Enqueue(null);
        RecentFilesPageModel page = CreatePage(recentFiles, folders);
        await page.LoadCommand.ExecuteAsync(null);

        await page.AddCommand.ExecuteAsync(null);

        Assert.That(recentFiles.Files, Is.EqualTo(["folder/one.txt", "folder/old.axx"]));
        Assert.That(page.SelectedState, Is.EqualTo(SelectedFileState.All));
        Assert.That(page.StatusText, Is.Empty);
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
    public async Task InaccessibleFileIsNotTransformedWhenTheUserDeclinesToAddItsFolder()
    {
        TestWorkFolderService folders = new();
        folders.Inaccessible.Add("folder/locked.txt");
        TestRecentFilesService recentFiles = new() { Files = ["folder/locked.txt"] };
        TestUserInterfaceService userInterface = new();
        RecentFilesPageModel page = CreatePage(recentFiles, folders, userInterface);
        await page.LoadCommand.ExecuteAsync(null);

        await page.ReverseCommand.ExecuteAsync(page.Files[0]);

        Assert.That(userInterface.Confirmations, Has.Count.EqualTo(1));
        Assert.That(folders.AddLocations, Is.Empty);
        Assert.That(userInterface.Messages, Is.Empty);
        Assert.That(recentFiles.Files, Is.EqualTo(["folder/locked.txt"]));
    }

    [Test]
    public async Task InaccessibleFileIsTransformedAfterItsFolderIsAdded()
    {
        TestWorkFolderService folders = new();
        folders.Inaccessible.Add("folder/locked.txt");
        TestRecentFilesService recentFiles = new() { Files = ["folder/locked.txt"] };
        TestUserInterfaceService userInterface = new() { Confirmation = true };
        RecentFilesPageModel page = CreatePage(recentFiles, folders, userInterface);
        await page.LoadCommand.ExecuteAsync(null);

        await page.ReverseCommand.ExecuteAsync(page.Files[0]);

        Assert.That(folders.AddLocations, Is.EqualTo(["folder"]));
        Assert.That(userInterface.Messages, Is.Empty);
        Assert.That(recentFiles.Files[0], Is.EqualTo("folder/locked-txt.axx"));
    }

    [Test]
    public async Task InaccessibleFileIsReportedWhenTheFolderAddedDoesNotContainIt()
    {
        TestWorkFolderService folders = new() { ChosenFolderId = "other" };
        folders.Inaccessible.Add("folder/locked.txt");
        TestRecentFilesService recentFiles = new() { Files = ["folder/locked.txt"] };
        TestUserInterfaceService userInterface = new() { Confirmation = true };
        RecentFilesPageModel page = CreatePage(recentFiles, folders, userInterface);
        await page.LoadCommand.ExecuteAsync(null);

        await page.OpenCommand.ExecuteAsync(page.Files[0]);

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
    [TestCase(SelectedFileState.Viewed, false, false)]
    [TestCase(SelectedFileState.Other, false, false)]
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
        TimeProvider? timeProvider = null,
        TestPreviewService? preview = null,
        FlowContext? flowContext = null)
    {
        userInterface ??= new TestUserInterfaceService();
        fileLauncher ??= new TestFileLauncher();
        WorkFolderWorkflow workflow = new(
            folders,
            new TestOperationService(recentFiles, folders),
            new TestFileOperations(),
            preview ?? new TestPreviewService(),
            flowContext ?? new FlowContext(),
            new TestCoreServices(),
            new TestFileWiper(),
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

    private static List<RecentFile> InPlace(params string[] ids) => [.. ids.Select(id => new RecentFile { Id = id, Operation = RecentFileOperation.InPlace })];

    private static List<string> Ids(IEnumerable<RecentFile> files) => [.. files.Select(file => file.Id)];

    private static WorkFolderFile CreateFile(string id)
    {
        string location = Path.GetDirectoryName(id)!.Replace('\\', '/');
        return new WorkFolderFile(id, Path.GetFileName(id), location, location, "grant", true, null!);
    }

    private sealed class TestUserDataStore : IUserDataStore
    {
        public List<RecentFile> Files { get; set; } = [];
        public UserId Id => throw new NotSupportedException();
        public Task<IPersistentData<RecentFiles>> LoadRecentFilesAsync() => throw new NotSupportedException();
        public Task<IPersistentData<RecentFileOperations>> LoadRecentFileOperationsAsync() =>
            Task.FromResult<IPersistentData<RecentFileOperations>>(new PersistentData<RecentFileOperations>(
                new RecentFileOperations { Files = [.. Files] },
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
        public List<RecentFile> Entries { get; set; } = [];

        // The ids in order, set as files encrypted or decrypted in place.
        public List<string> Files
        {
            get => Ids(Entries);
            init => Entries = InPlace([.. value]);
        }

        public Task<IReadOnlyList<RecentFile>> GetFilesAsync() => Task.FromResult<IReadOnlyList<RecentFile>>([.. Entries]);
        public Task AddAsync(string fileId, RecentFileOperation operation)
        {
            Entries = [new RecentFile { Id = fileId, Operation = operation }, .. Entries.Where(file => file.Id != fileId)];
            return Task.CompletedTask;
        }
        public Task AddFlowSourceAsync(RecentFileOperation operation) => throw new NotSupportedException();
        public Task AddSavedCopyAsync(WorkFolderFile savedCopy, RecentFileOperation operation) =>
            throw new NotSupportedException();
        public Task RemoveAsync(IReadOnlyCollection<string> fileIds)
        {
            Entries = [.. Entries.Where(file => !fileIds.Contains(file.Id))];
            return Task.CompletedTask;
        }
    }

    private sealed class TestPreviewService : IPreviewService
    {
        public bool NeedsPassword { get; init; }
        public List<string> Prepared { get; } = [];
        public IPreviewState Current => throw new NotSupportedException();
        public bool HasPendingPasswordRequest => NeedsPassword;
        public Task PrepareTextAsync(DecryptedFileInfo file, bool enableTextEditing) => throw new NotSupportedException();
        public Task<bool> PrepareAsync(DocumentPreviewFile encryptedFile, bool enableTextEditing)
        {
            Prepared.Add(encryptedFile.FileName);
            return Task.FromResult(!NeedsPassword);
        }
        public Task<PreviewPreparationStatus> PrepareImportedAsync(string encryptedFilePath) =>
            throw new NotSupportedException();
        public Task<PreviewPreparationStatus> PrepareWithPasswordAsync(string password) =>
            throw new NotSupportedException();
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
        public IReadOnlyList<string> GetFilePathSegments(string id, string? displayName = null) => id.Split('/');
        public Queue<WorkFolderFile?> Picked { get; } = new();
        public Task<IReadOnlyList<WorkFolder>> GetFoldersAsync() =>
            Task.FromResult<IReadOnlyList<WorkFolder>>([new WorkFolder("folder", "Folder", "grant")]);
        public string? GetFileLocationId(string fileId) =>
            fileId.Contains('/') ? fileId[..fileId.LastIndexOf('/')] : null;

        // The folder the user chooses when adding one, instead of the initial location, or null to cancel.
        public string? ChosenFolderId { get; set; } = string.Empty;
        public List<string?> AddLocations { get; } = [];
        public Task<WorkFolderResult> AddFolderAsync(string? initialLocationId = null)
        {
            AddLocations.Add(initialLocationId);
            if (ChosenFolderId is null)
            {
                return Task.FromResult(WorkFolderResult.Canceled);
            }

            string folderId = ChosenFolderId.Length > 0 ? ChosenFolderId : initialLocationId!;
            Inaccessible.RemoveWhere(fileId => GetFileLocationId(fileId) == folderId);
            return Task.FromResult(WorkFolderResult.Valid(new WorkFolder(folderId, "Added", "grant")));
        }
        public Task<WorkFolder> AddDiscoveredFolderAsync(WorkFolderFile file) => throw new NotSupportedException();
        public Task RemoveFolderAsync(WorkFolder folder) => throw new NotSupportedException();
        public Task RenameFolderAsync(WorkFolder folder, string displayName) => throw new NotSupportedException();
        public Task SaveFoldersAsync(IReadOnlyList<WorkFolder> folders) => Task.CompletedTask;
        public Task<WorkFolderFile?> PickFileAsync(WorkFolder? folder, FilePickerKind pickerKind) =>
            Task.FromResult(Picked.Dequeue());
        public Task<WorkFolderFile?> SaveFileAsync(WorkFolder? folder, string fileName, Stream content) =>
            throw new NotSupportedException();
    }

    private sealed class TestFileOperations : IWorkFolderFileOperations
    {
        public bool DestinationExists { get; init; }
        public Task<Stream> OpenReadAsync(WorkFolderFile file) =>
            Task.FromResult<Stream>(new MemoryStream(file.Id.EndsWith(".axx", StringComparison.Ordinal) ? [0xe0] : [0x00]));
        public Task<bool> DestinationExistsAsync(WorkFolderFile file, string name) => Task.FromResult(DestinationExists);
        public Task<string> WriteDestinationAsync(WorkFolderFile file, string name, bool overwrite, Func<Stream, Task> writer) =>
            Task.FromResult($"{file.LocationId}/{name}");
    }

    private sealed class TestFileWiper : IFileWiper
    {
        public Task<bool> CanWipeAsync(IPickedWritableFile file) => Task.FromResult(true);
        public Task<FileWipeStatus> WipeAsync(IPickedWritableFile file) => Task.FromResult(FileWipeStatus.Succeeded);
    }

    private sealed class TestOperationService(IRecentFilesService recentFiles, TestWorkFolderService folders)
        : IWorkFolderOperationService
    {
        public bool HasPendingPasswordRequest => false;
        public Task EncryptAsync(WorkFolderFile file)
        {
            folders.Missing.Add(file.Id);
            return recentFiles.AddAsync(
                $"{file.LocationId}/{Path.GetFileNameWithoutExtension(file.FileName)}-txt.axx",
                RecentFileOperation.InPlace);
        }
        public async Task<bool> DecryptWithKnownPasswordsAsync(WorkFolderFile file)
        {
            if (folders.NeedsPassword.Contains(file.Id))
            {
                return false;
            }

            folders.Missing.Add(file.Id);
            await recentFiles.AddAsync($"{file.LocationId}/decrypted.txt", RecentFileOperation.InPlace);
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
        public List<string> Confirmations { get; } = [];
        public Task<bool> DisplayConfirmationAsync(string message)
        {
            Confirmations.Add(message);
            return Task.FromResult(Confirmation);
        }
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
