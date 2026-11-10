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
public sealed class WorkFoldersPageTests
{
    [Test]
    public async Task OperationHintIsShownOnceAsATransientMessage()
    {
        using TestApp app = new();
        WorkFoldersPageModel page = app.CreateWorkFoldersPage();
        page.Initialize(WorkFolderIntent.Delete);

        await page.LoadCommand.ExecuteAsync(null);
        await page.LoadCommand.ExecuteAsync(null);

        Assert.That(app.UserInterface.TransientMessages, Is.EqualTo([MobileTexts.WorkFolderIntentDelete]));
        Assert.That(page.MessageText, Is.Empty);
    }

    [Test]
    public async Task FolderAddedForAnOperationHasTheFilePickedInItRightAway()
    {
        using TestApp app = new();
        app.AddFile("docs/a.txt");
        app.Access.PickedFolders.Enqueue(FakeFileAccess.Folder("docs"));
        app.Access.PickedFiles.Enqueue("docs/a.txt");
        app.UserInterface.Answers.Enqueue(true);
        WorkFoldersPageModel page = app.CreateWorkFoldersPage();
        page.Initialize(WorkFolderIntent.Delete);

        await page.AddCommand.ExecuteAsync(null);

        Assert.That(app.Access.FilePickerStarts, Is.EqualTo(["docs"]));
        Assert.That(app.Access.Files, Is.Empty);
    }

    [Test]
    public async Task FolderAddedWithoutAnOperationIsOnlyAdded()
    {
        using TestApp app = new();
        app.Access.PickedFolders.Enqueue(FakeFileAccess.Folder("docs"));
        WorkFoldersPageModel page = app.CreateWorkFoldersPage();

        await page.AddCommand.ExecuteAsync(null);

        Assert.That(page.Folders.Select(entry => entry.Folder.Id), Is.EqualTo(["docs"]));
        Assert.That(app.Access.FilePickerStarts, Is.Empty);
    }

    [Test]
    public async Task FoldersWithTheSameNameAreToldApartByTheirPath()
    {
        using TestApp app = new();
        WorkFoldersPageModel page = app.CreateWorkFoldersPage();
        foreach (string path in new[] { "a/docs", "b/docs", "c/notes" })
        {
            app.Access.PickedFolders.Enqueue(FakeFileAccess.Folder(path));
            await page.AddCommand.ExecuteAsync(null);
        }

        Assert.That(
            page.Folders.Select(entry => entry.ListDisplayName),
            Is.EquivalentTo(
            [
                $"a{Path.DirectorySeparatorChar}docs",
                $"b{Path.DirectorySeparatorChar}docs",
                "notes",
            ]));
    }

    [Test]
    public async Task FoldersWithTheSameNameAreToldApartByWhereTheyAreStored()
    {
        using TestApp app = new();
        WorkFoldersPageModel page = app.CreateWorkFoldersPage();
        (string Id, RootProvider Root)[] folders = [("x/Work", RootProvider.Dropbox), ("y/Work", RootProvider.ICloudDrive)];
        foreach ((string id, RootProvider root) in folders)
        {
            app.Access.PickedFolders.Enqueue(
                WorkFolderResult.Valid(new WorkFolder(id, "Work", $"grant:{id}") { Root = root }));
            await page.AddCommand.ExecuteAsync(null);
        }

        Assert.That(
            page.Folders.Select(entry => entry.ListDisplayName),
            Is.EquivalentTo(
            [
                $"Dropbox{Path.DirectorySeparatorChar}Work",
                $"iCloud{Path.DirectorySeparatorChar}Work",
            ]));
    }

    [TestCase(WorkFolderIntent.Auto, new[]
    {
        nameof(MobileTexts.MenuEditLabel),
        nameof(MobileTexts.MenuEncryptFolderFiles),
        nameof(MobileTexts.MenuEncryptFolderAndSubfolderFiles),
        nameof(MobileTexts.MenuDecryptFolderFiles),
        nameof(MobileTexts.MenuDecryptFolderAndSubfolderFiles),
        nameof(MobileTexts.MenuRemoveFromList),
    })]
    [TestCase(WorkFolderIntent.Delete, new[]
    {
        nameof(MobileTexts.MenuEditLabel),
        nameof(MobileTexts.MenuDeleteFolderFiles),
        nameof(MobileTexts.MenuDeleteFolderAndSubfolderFiles),
        nameof(MobileTexts.MenuRemoveFromList),
    })]
    [TestCase(WorkFolderIntent.Copy, new[]
    {
        nameof(MobileTexts.MenuEditLabel),
        nameof(MobileTexts.MenuRemoveFromList),
    })]
    [TestCase(WorkFolderIntent.Share, new[]
    {
        nameof(MobileTexts.MenuEditLabel),
        nameof(MobileTexts.MenuRemoveFromList),
    })]
    public async Task FolderMenuHasTheItemsForWhatThePageIsOpenedFor(WorkFolderIntent intent, string[] textNames)
    {
        using TestApp app = new();
        app.AddFolder("docs");
        WorkFoldersPageModel page = app.CreateWorkFoldersPage();
        page.Initialize(intent);

        await page.LoadCommand.ExecuteAsync(null);

        Assert.That(page.Folders.Single().MenuItems.Select(item => item.Text), Is.EqualTo(textNames.Select(MenuText)));
    }

    [Test]
    public async Task ChoosingToEncryptFolderFilesInTheMenuEncryptsThem()
    {
        using TestApp app = new();
        app.AddFolder("docs");
        app.AddFile("docs/a.txt");
        app.Access.PickedFolders.Enqueue(FakeFileAccess.Folder("docs"));
        app.UserInterface.Answers.Enqueue(true);
        WorkFoldersPageModel page = app.CreateWorkFoldersPage();
        await page.LoadCommand.ExecuteAsync(null);
        OverflowMenuItem item = page.Folders.Single().MenuItems
            .Single(menuItem => menuItem.Text == MobileTexts.MenuEncryptFolderFiles);

        await page.RunFolderFilesCommand.ExecuteAsync((FolderFilesRequest)item.Parameter);

        Assert.That(app.Access.Files.Keys.Single(), Does.EndWith(".axx"));
        Assert.That(page.IsBusy, Is.False);
    }

    [Test]
    public async Task ChoosingToRemoveTheFolderInTheMenuRemovesIt()
    {
        using TestApp app = new();
        app.AddFolder("docs");
        WorkFoldersPageModel page = app.CreateWorkFoldersPage();
        await page.LoadCommand.ExecuteAsync(null);
        OverflowMenuItem item = page.Folders.Single().MenuItems
            .Single(menuItem => menuItem.Text == MobileTexts.MenuRemoveFromList);

        item.Command.Execute(item.Parameter);

        Assert.That(page.Folders, Is.Empty);
        Assert.That(app.FolderIds(), Is.Empty);
    }

    private static string MenuText(string textName) => textName switch
    {
        nameof(MobileTexts.MenuEditLabel) => MobileTexts.MenuEditLabel,
        nameof(MobileTexts.MenuEncryptFolderFiles) => MobileTexts.MenuEncryptFolderFiles,
        nameof(MobileTexts.MenuEncryptFolderAndSubfolderFiles) => MobileTexts.MenuEncryptFolderAndSubfolderFiles,
        nameof(MobileTexts.MenuDecryptFolderFiles) => MobileTexts.MenuDecryptFolderFiles,
        nameof(MobileTexts.MenuDecryptFolderAndSubfolderFiles) => MobileTexts.MenuDecryptFolderAndSubfolderFiles,
        nameof(MobileTexts.MenuDeleteFolderFiles) => MobileTexts.MenuDeleteFolderFiles,
        nameof(MobileTexts.MenuDeleteFolderAndSubfolderFiles) => MobileTexts.MenuDeleteFolderAndSubfolderFiles,
        nameof(MobileTexts.MenuRemoveFromList) => MobileTexts.MenuRemoveFromList,
        _ => throw new ArgumentOutOfRangeException(nameof(textName), textName, null),
    };
}
