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
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class FolderAccessWorkflowTests
{
    [Test]
    public async Task FileInOneOfMyFoldersIsUsedThroughItAndTheFolderIsMovedFirst()
    {
        TestApp app = new(usesFolderIds: true);
        app.AddFolder("other");
        WorkFolder docs = app.AddFolder("docs");
        app.Access.PickedFiles.Enqueue("docs/sub/a.txt");

        FileReference? file = await app.FolderAccess.PickWritableFileAsync(docs, FilePickerKind.Any);

        Assert.That(file, Is.EqualTo(new FileReference("grant:docs|docs/sub/a.txt", "a.txt")));
        Assert.That(app.Access.FilePickerStarts, Is.EqualTo(["docs"]));
        Assert.That(app.UserInterface.Confirmations, Is.Empty);
        Assert.That(app.FolderIds(), Is.EqualTo(["docs", "other"]));
    }

    [Test]
    public async Task InnermostOfMyFoldersIsUsed()
    {
        TestApp app = new(usesFolderIds: true);
        app.AddFolder("docs");
        app.AddFolder("docs/sub");
        app.Access.PickedFiles.Enqueue("docs/sub/a.txt");

        FileReference? file = await app.FolderAccess.PickWritableFileAsync(null, FilePickerKind.Any);

        Assert.That(file?.Id, Is.EqualTo("grant:docs/sub|docs/sub/a.txt"));
    }

    [Test]
    public async Task FileOutsideMyFoldersIsNotUsedWhenTheUserDeclinesToAddItsFolder()
    {
        TestApp app = new();
        app.Access.PickedFiles.Enqueue("downloads/a.txt");

        FileReference? file = await app.FolderAccess.PickWritableFileAsync(null, FilePickerKind.Any);

        Assert.That(file, Is.Null);
        Assert.That(app.UserInterface.Confirmations,
            Is.EqualTo([string.Format(MobileTexts.DialogTextAllowFolderAccessFormat, "a.txt")]));
        Assert.That(app.Access.FolderPickerStarts, Is.Empty);
    }

    /// <summary>
    /// The user picks the file again once its folder is added, starting in that folder.
    /// </summary>
    [Test]
    public async Task FileOutsideMyFoldersIsPickedAgainInTheFolderAdded()
    {
        TestApp app = new();
        app.Access.PickedFiles.Enqueue("downloads/a.txt");
        app.Access.PickedFiles.Enqueue("downloads/a.txt");
        app.Access.PickedFolders.Enqueue(FakeFileAccess.Folder("downloads"));
        app.UserInterface.Answers.Enqueue(true);

        FileReference? file = await app.FolderAccess.PickWritableFileAsync(null, FilePickerKind.Any);

        Assert.That(file?.Id, Is.EqualTo("downloads/a.txt"));
        Assert.That(app.Access.FolderPickerStarts, Is.EqualTo(["downloads"]));
        Assert.That(app.Access.FilePickerStarts, Is.EqualTo([string.Empty, "downloads"]));
        Assert.That(app.FolderIds(), Is.EqualTo(["downloads"]));
    }

    /// <summary>
    /// Locations that can never be added just keep the user asked, until the user cancels.
    /// </summary>
    [Test]
    public async Task FolderAddedThatDoesNotContainTheFileAsksAgainUntilTheUserCancels()
    {
        TestApp app = new();
        app.UserInterface.Answers.Enqueue(true);
        app.UserInterface.Answers.Enqueue(true);
        app.Access.PickedFolders.Enqueue(FakeFileAccess.Folder("elsewhere"));
        app.Access.PickedFolders.Enqueue(FakeFileAccess.Folder("elsewhere"));

        FileReference? file = await app.FolderAccess.EnsureFolderAccessAsync(new FileReference("images/a.jpg", "a.jpg"));

        Assert.That(file, Is.Null);
        Assert.That(app.UserInterface.Confirmations, Has.Count.EqualTo(3));
        Assert.That(app.Access.FolderPickerStarts, Is.EqualTo(["images", "images"]));
        Assert.That(app.Access.FilePickerStarts, Is.Empty);
    }

    [Test]
    public async Task CancelingTheFolderPickerEndsAskingForAccess()
    {
        TestApp app = new();
        app.UserInterface.Answers.Enqueue(true);
        app.Access.PickedFolders.Enqueue(WorkFolderResult.Canceled);

        FileReference? file = await app.FolderAccess.EnsureFolderAccessAsync(new FileReference("images/a.jpg", "a.jpg"));

        Assert.That(file, Is.Null);
        Assert.That(app.UserInterface.Confirmations, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task FolderWhoseGrantIsGoneIsAskedForAgain()
    {
        TestApp app = new(usesFolderIds: true);
        WorkFolder docs = app.AddFolder("docs");
        app.Access.RevokedFolderGrants.Add(docs.GrantId);
        app.UserInterface.Answers.Enqueue(true);
        app.Access.PickedFolders.Enqueue(WorkFolderResult.Valid(new WorkFolder("docs", "docs", "grant:docs-new")));

        FileReference? file = await app.FolderAccess.EnsureFolderAccessAsync(
            new FileReference("grant:docs|docs/a.txt", "a.txt"));

        Assert.That(file?.Id, Is.EqualTo("grant:docs-new|docs/a.txt"));
        Assert.That(app.Store.Folders.Select(folder => folder.GrantId), Is.EqualTo(["grant:docs-new"]));
    }

    [Test]
    public async Task FolderAddedAgainKeepsItsNameAndIsMovedFirst()
    {
        TestApp app = new();
        app.AddFolder("other");
        app.Store.Folders.Add(new WorkFolder("docs", "My documents", "grant:old"));
        app.Access.PickedFolders.Enqueue(WorkFolderResult.Valid(new WorkFolder("docs", "docs", "grant:new")));

        WorkFolder? folder = await app.FolderAccess.AddFolderAsync();

        Assert.That(folder, Is.EqualTo(new WorkFolder("docs", "My documents", "grant:new")));
        Assert.That(app.FolderIds(), Is.EqualTo(["docs", "other"]));
    }

    [Test]
    public async Task FolderThatCannotBeUsedIsReportedAndTheUserChoosesAgain()
    {
        TestApp app = new();
        app.Access.PickedFolders.Enqueue(WorkFolderResult.NoAccess);
        app.Access.PickedFolders.Enqueue(WorkFolderResult.NotFolder);
        app.Access.PickedFolders.Enqueue(WorkFolderResult.Canceled);

        WorkFolder? folder = await app.FolderAccess.AddFolderAsync();

        Assert.That(folder, Is.Null);
        Assert.That(app.UserInterface.Messages,
            Is.EqualTo([MobileTexts.DialogTextFolderNoAccess, MobileTexts.DialogTextSelectFolderFirst]));
        Assert.That(app.Store.Folders, Is.Empty);
    }
}
