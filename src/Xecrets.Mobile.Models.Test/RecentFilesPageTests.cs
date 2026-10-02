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
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.PageModels;
using Xecrets.Mobile.Models.Utilities;
using Xecrets.Texts;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class RecentFilesPageTests
{
    /// <summary>
    /// Files are only checked when used, so that files that are offline or gone do not slow down the list.
    /// </summary>
    [Test]
    public async Task ListIsShownWithoutAccessingTheFiles()
    {
        TestApp app = new();
        app.AddRecent("docs/a.txt", RecentFileOperation.InPlace);
        app.AddRecent("gone.axx", RecentFileOperation.View);
        RecentFilesPageModel page = app.CreateRecentFilesPage();

        await page.LoadCommand.ExecuteAsync(null);

        Assert.That(page.Files.Select(file => file.Id), Is.EqualTo(["docs/a.txt", "gone.axx"]));
        Assert.That(app.Access.ReadCount, Is.Zero);
    }

    [Test]
    public async Task FileWhoseIdCannotBeReadIsShownByItsRememberedName()
    {
        TestApp app = new();
        app.AddRecent("bad:1", RecentFileOperation.View, "a.axx");
        app.AddRecent("bad:2", RecentFileOperation.View);
        RecentFilesPageModel page = app.CreateRecentFilesPage();

        await page.LoadCommand.ExecuteAsync(null);

        Assert.That(page.Files.Select(file => file.DisplayPath),
            Is.EqualTo(["a.axx", MobileTexts.RecentFileNameUnknown]));
        Assert.That(page.Files[0].IsEncrypted, Is.True);
    }

    [Test]
    public async Task ViewedFileIsShownAgainThroughTheAccessToItAlone()
    {
        TestApp app = new();
        app.AddFile("downloads/a.axx");
        app.Access.FileGrants.Add("downloads/a.axx");
        app.AddRecent("downloads/a.axx", RecentFileOperation.View, "a.axx");
        RecentFilesPageModel page = await LoadedPageAsync(app);

        await page.OpenCommand.ExecuteAsync(page.Files[0]);

        Assert.That(app.Preview.Prepared, Is.EqualTo(["a.axx"]));
        Assert.That(app.UserInterface.Confirmations, Is.Empty);
    }

    [Test]
    public async Task FileOnlyReadWithoutAccessIsOfferedToBeRemoved()
    {
        TestApp app = new();
        app.AddFile("downloads/a.axx");
        app.AddRecent("downloads/a.axx", RecentFileOperation.View, "a.axx");
        app.UserInterface.Answers.Enqueue(true);
        RecentFilesPageModel page = await LoadedPageAsync(app);

        await page.OpenCommand.ExecuteAsync(page.Files[0]);

        Assert.That(app.UserInterface.Confirmations, Is.EqualTo([MobileTexts.DialogTextRecentFileNotAccessible]));
        Assert.That(app.Store.Files, Is.Empty);
        Assert.That(page.Files, Is.Empty);
        Assert.That(app.Preview.Prepared, Is.Empty);
    }

    /// <summary>
    /// A file that cannot be reached may only be offline for now, so it is up to the user whether to remove it.
    /// </summary>
    [Test]
    public async Task FileThatCannotBeReachedIsKeptWhenTheUserSaysSo()
    {
        TestApp app = new();
        app.AddFolder("docs");
        app.AddFile("docs/a.txt");
        app.Access.Offline.Add("docs/a.txt");
        app.AddRecent("docs/a.txt", RecentFileOperation.InPlace, "a.txt");
        RecentFilesPageModel page = await LoadedPageAsync(app);

        await page.OpenCommand.ExecuteAsync(page.Files[0]);

        Assert.That(app.UserInterface.Confirmations, Is.EqualTo([MobileTexts.DialogTextRecentFileNotAccessible]));
        Assert.That(app.RecentIds(), Is.EqualTo(["docs/a.txt"]));
        Assert.That(app.Launcher.Opened, Is.Empty);
    }

    [Test]
    public async Task DecryptedFileIsOpenedWhereItIsAndMayBeChangedThere()
    {
        TestApp app = new();
        app.AddFolder("docs");
        app.AddFile("docs/a.txt");
        app.AddRecent("docs/a.txt", RecentFileOperation.InPlace, "a.txt");
        RecentFilesPageModel page = await LoadedPageAsync(app);

        await page.OpenCommand.ExecuteAsync(page.Files[0]);

        Assert.That(app.Launcher.Opened, Is.EqualTo([("docs/a.txt", true)]));
    }

    [Test]
    public async Task FileOnlyReadIsOpenedWithoutAllowingChanges()
    {
        TestApp app = new();
        app.AddFile("downloads/a.txt");
        app.Access.FileGrants.Add("downloads/a.txt");
        app.AddRecent("downloads/a.txt", RecentFileOperation.EncryptCopySaveAs, "a.txt");
        RecentFilesPageModel page = await LoadedPageAsync(app);

        await page.OpenCommand.ExecuteAsync(page.Files[0]);

        Assert.That(app.Launcher.Opened, Is.EqualTo([("downloads/a.txt", false)]));
    }

    /// <summary>
    /// A file changed where it is needs one of My folders, which the user is asked to add without picking the file
    /// again, and the file is listed as reached through it from then on.
    /// </summary>
    [Test]
    public async Task ChangedFileOutsideMyFoldersIsUsedOnceItsFolderIsAdded()
    {
        TestApp app = new(usesFolderIds: true);
        app.AddFile("docs/a.txt");
        app.AddRecent("grant:gone|docs/a.txt", RecentFileOperation.InPlace, "a.txt");
        app.UserInterface.Answers.Enqueue(true);
        app.Access.PickedFolders.Enqueue(FakeFileAccess.Folder("docs"));
        RecentFilesPageModel page = await LoadedPageAsync(app);

        await page.OpenCommand.ExecuteAsync(page.Files[0]);

        Assert.That(app.Access.FolderPickerStarts, Is.EqualTo(["docs"]));
        Assert.That(app.Access.FilePickerStarts, Is.Empty);
        Assert.That(app.RecentIds(), Is.EqualTo(["grant:docs|docs/a.txt"]));
        Assert.That(app.Launcher.Opened, Is.EqualTo([("grant:docs|docs/a.txt", true)]));
    }

    [Test]
    public async Task ChangedFileIsNotUsedWhenTheUserDeclinesToAddItsFolder()
    {
        TestApp app = new();
        app.AddFile("docs/a.txt");
        app.AddRecent("docs/a.txt", RecentFileOperation.InPlace, "a.txt");
        RecentFilesPageModel page = await LoadedPageAsync(app);

        await page.OpenCommand.ExecuteAsync(page.Files[0]);

        Assert.That(app.UserInterface.Confirmations,
            Is.EqualTo([string.Format(MobileTexts.DialogTextAllowFolderAccessFormat, "a.txt")]));
        Assert.That(app.Launcher.Opened, Is.Empty);
        Assert.That(app.RecentIds(), Is.EqualTo(["docs/a.txt"]));
    }

    [Test]
    public async Task EncryptingFromTheListShowsTheResultFirst()
    {
        TestApp app = new();
        app.AddFolder("docs");
        app.AddFile("docs/a.txt");
        app.AddRecent("docs/b.txt", RecentFileOperation.InPlace, "b.txt");
        app.AddRecent("docs/a.txt", RecentFileOperation.InPlace, "a.txt");
        RecentFilesPageModel page = await LoadedPageAsync(app);
        string encryptedName = "a.txt".ToEncryptedName(string.Empty);

        await page.ReverseCommand.ExecuteAsync(page.Files[1]);

        Assert.That(app.RecentIds(), Is.EqualTo([$"docs/{encryptedName}", "docs/b.txt"]));
        Assert.That(page.Files.Select(file => file.Id), Is.EqualTo([$"docs/{encryptedName}", "docs/b.txt"]));
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo([$"docs/{encryptedName}"]));
    }

    [Test]
    public async Task DecryptingWithAPasswordFromTheListShowsTheResultWhenReturning()
    {
        TestApp app = new();
        string encryptedName = "a.txt".ToEncryptedName(string.Empty);
        app.AddFolder("docs");
        app.AddFile($"docs/{encryptedName}", "ENC:a.txt\ntext");
        app.AddRecent("docs/b.txt", RecentFileOperation.InPlace, "b.txt");
        app.AddRecent($"docs/{encryptedName}", RecentFileOperation.InPlace, encryptedName);
        app.Core.Password = "other";
        RecentFilesPageModel page = await LoadedPageAsync(app);

        await page.ReverseCommand.ExecuteAsync(page.Files[1]);
        await app.OperationService.DecryptWithPasswordAsync("other");
        app.OperationService.CancelPasswordRequest();
        await page.LoadCommand.ExecuteAsync(null);

        Assert.That(app.RecentIds(), Is.EqualTo(["docs/a.txt", "docs/b.txt"]));
        Assert.That(page.Files.Select(file => file.Id), Is.EqualTo(["docs/a.txt", "docs/b.txt"]));
    }

    [Test]
    public async Task DecryptionWaitingForAPasswordThatIsNotGivenLeavesTheSource()
    {
        TestApp app = new();
        string encryptedName = "a.txt".ToEncryptedName(string.Empty);
        app.AddFolder("docs");
        app.AddFile($"docs/{encryptedName}", "ENC:a.txt\ntext");
        app.AddRecent($"docs/{encryptedName}", RecentFileOperation.InPlace, encryptedName);
        app.Core.Password = "other";
        RecentFilesPageModel page = await LoadedPageAsync(app);

        await page.ReverseCommand.ExecuteAsync(page.Files[0]);
        app.OperationService.CancelPasswordRequest();
        await page.LoadCommand.ExecuteAsync(null);

        Assert.That(page.Files.Select(file => file.Id), Is.EqualTo([$"docs/{encryptedName}"]));
    }

    /// <summary>
    /// A file that nothing is done with, such as one already encrypted, does not stop the others.
    /// </summary>
    [Test]
    public async Task FileNotChangedIsLeftAndTheOthersAreStillChanged()
    {
        TestApp app = new();
        string encryptedName = "b.txt".ToEncryptedName(string.Empty);
        app.AddFolder("docs");
        app.AddFile("docs/a.txt", "ENC:a.txt\ntext");
        app.AddFile("docs/b.txt");
        app.AddRecent("docs/a.txt", RecentFileOperation.InPlace, "a.txt");
        app.AddRecent("docs/b.txt", RecentFileOperation.InPlace, "b.txt");
        RecentFilesPageModel page = await LoadedPageAsync(app);
        page.SelectedState = SelectedFileState.Decrypted;

        await page.ReverseAllCommand.ExecuteAsync(null);

        Assert.That(app.UserInterface.TransientMessages, Does.Contain(MobileTexts.DialogTextAlreadyEncrypted));
        Assert.That(app.RecentIds(), Is.EqualTo([$"docs/{encryptedName}", "docs/a.txt"]));
        Assert.That(page.Files.Select(file => file.Id), Is.EqualTo(["docs/a.txt"]));
    }

    /// <summary>
    /// Only a search of the folders that completes can tell that the file is not there.
    /// </summary>
    [Test]
    public async Task FileNotFoundInItsFolderIsOfferedToBeRemoved()
    {
        TestApp app = new();
        app.AddFolder("docs");
        app.AddFile("docs/a.txt");
        app.AddRecent("docs/a.txt", RecentFileOperation.InPlace, "a.txt");
        app.Access.FolderLookupFailure = new FileNotAccessibleException();
        RecentFilesPageModel page = await LoadedPageAsync(app);

        await page.ReverseCommand.ExecuteAsync(page.Files[0]);

        Assert.That(app.UserInterface.Confirmations, Is.EqualTo([MobileTexts.DialogTextRecentFileNotAccessible]));
        Assert.That(page.StatusText, Is.Empty);
    }

    [Test]
    public async Task FolderThatCannotBeSearchedIsReportedAsAnError()
    {
        TestApp app = new();
        app.AddFolder("docs");
        app.AddFile("docs/a.txt");
        app.AddRecent("docs/a.txt", RecentFileOperation.InPlace, "a.txt");
        app.Access.FolderLookupFailure = new IOException("The folder of the file could not be searched.");
        RecentFilesPageModel page = await LoadedPageAsync(app);

        await page.ReverseCommand.ExecuteAsync(page.Files[0]);

        Assert.That(app.UserInterface.Confirmations, Is.Empty);
        Assert.That(page.StatusText, Does.Contain("The folder of the file could not be searched."));
        Assert.That(app.RecentIds(), Is.EqualTo(["docs/a.txt"]));
    }

    [Test]
    public async Task RemovingAnEntryKeepsOtherEntriesForTheSameFile()
    {
        TestApp app = new(usesFolderIds: true);
        app.AddRecent("grant:docs|docs/a.txt", RecentFileOperation.InPlace, "a.txt");
        app.AddRecent("docs/a.txt", RecentFileOperation.EncryptCopySaveAs, "a.txt");
        RecentFilesPageModel page = await LoadedPageAsync(app);

        await page.RemoveCommand.ExecuteAsync(page.Files[0]);

        Assert.That(app.RecentIds(), Is.EqualTo(["docs/a.txt"]));
    }

    [Test]
    public async Task FileListedByAnUnknownOperationIsNotShown()
    {
        TestApp app = new();
        app.AddRecent("docs/a.txt", RecentFileOperation.InPlace);
        app.AddRecent("future.txt", RecentFileOperation.Unknown, "future.txt");

        RecentFilesPageModel page = await LoadedPageAsync(app);

        Assert.That(page.Files.Select(file => file.Id), Is.EqualTo(["docs/a.txt"]));
    }

    private static async Task<RecentFilesPageModel> LoadedPageAsync(TestApp app)
    {
        RecentFilesPageModel page = app.CreateRecentFilesPage();
        await page.LoadCommand.ExecuteAsync(null);
        return page;
    }
}
