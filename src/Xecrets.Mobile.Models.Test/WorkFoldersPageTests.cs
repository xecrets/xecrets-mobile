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
        TestApp app = new();
        WorkFoldersPageModel page = app.CreateWorkFoldersPage();
        page.Initialize(WorkFolderIntent.Encrypt);

        await page.LoadCommand.ExecuteAsync(null);
        await page.LoadCommand.ExecuteAsync(null);

        Assert.That(app.UserInterface.TransientMessages, Is.EqualTo([MobileTexts.WorkFolderIntentEncrypt]));
        Assert.That(page.MessageText, Is.Empty);
    }

    [Test]
    public async Task FolderAddedForAnOperationHasTheFilePickedInItRightAway()
    {
        TestApp app = new();
        app.AddFile("docs/a.txt");
        app.Access.PickedFolders.Enqueue(FakeFileAccess.Folder("docs"));
        app.Access.PickedFiles.Enqueue("docs/a.txt");
        WorkFoldersPageModel page = app.CreateWorkFoldersPage();
        page.Initialize(WorkFolderIntent.Encrypt);

        await page.AddCommand.ExecuteAsync(null);

        Assert.That(app.Access.FilePickerStarts, Is.EqualTo(["docs"]));
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo([$"docs/{"a.txt".ToEncryptedName(string.Empty)}"]));
    }

    [Test]
    public async Task FolderAddedWithoutAnOperationIsOnlyAdded()
    {
        TestApp app = new();
        app.Access.PickedFolders.Enqueue(FakeFileAccess.Folder("docs"));
        WorkFoldersPageModel page = app.CreateWorkFoldersPage();

        await page.AddCommand.ExecuteAsync(null);

        Assert.That(page.Folders.Select(entry => entry.Folder.Id), Is.EqualTo(["docs"]));
        Assert.That(app.Access.FilePickerStarts, Is.Empty);
    }

    [Test]
    public async Task FoldersWithTheSameNameAreToldApartByTheirPath()
    {
        TestApp app = new();
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
}
