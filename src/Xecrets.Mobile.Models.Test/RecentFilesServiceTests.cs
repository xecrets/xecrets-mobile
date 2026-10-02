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

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class RecentFilesServiceTests
{
    [Test]
    public async Task AddedFileIsListedFirstInPlaceOfAnEntryForTheSameFile()
    {
        TestApp app = new();
        app.AddRecent("a.txt", RecentFileOperation.InPlace);
        app.AddRecent("b.txt", RecentFileOperation.View);

        await app.Recent.AddAsync(new FileReference("b.txt", "b.txt"), RecentFileOperation.Edit);

        Assert.That(app.Store.Files.Select(file => (file.Id, file.Operation, file.Name)),
            Is.EqualTo(
            [
                ("b.txt", RecentFileOperation.Edit, (string?)"b.txt"),
                ("a.txt", RecentFileOperation.InPlace, null),
            ]));
    }

    [Test]
    public async Task ListIsLimitedToTheMostRecentFiles()
    {
        TestApp app = new();
        for (int i = 0; i < 60; i++)
        {
            await app.Recent.AddAsync(new FileReference($"{i}.txt", $"{i}.txt"), RecentFileOperation.InPlace);
        }

        Assert.That(app.Store.Files, Has.Count.EqualTo(50));
        Assert.That(app.Store.Files[0].Id, Is.EqualTo("59.txt"));
    }

    [Test]
    public async Task FileOnlyReadKeepsTheAccessToIt()
    {
        TestApp app = new();

        bool isAdded = await app.Recent.AddAsync(new FileReference("a.axx", "a.axx"), RecentFileOperation.View);

        Assert.That(isAdded, Is.True);
        Assert.That(app.Access.FileGrants, Is.EqualTo(["a.axx"]));
    }

    [Test]
    public async Task FileOnlyReadIsNotListedWhenTheAccessToItCannotBeKept()
    {
        TestApp app = new();
        app.Access.CanKeepFileGrants = false;

        bool isAdded = await app.Recent.AddAsync(
            new FileReference("a.txt", "a.txt"),
            RecentFileOperation.EncryptCopySaveAs);

        Assert.That(isAdded, Is.False);
        Assert.That(app.Store.Files, Is.Empty);
    }

    /// <summary>
    /// A changed file is reached through one of My folders, so only reading it, such as viewing it after editing it,
    /// keeps it listed as changed, rather than as depending on access to it alone that it may not have.
    /// </summary>
    [Test]
    public async Task ChangedFileThenOnlyReadStaysListedAsChangedAndIsMovedFirst()
    {
        TestApp app = new(usesFolderIds: true);
        app.AddRecent("b.axx", RecentFileOperation.View, "b.axx");
        app.AddRecent("grant:docs|docs/a.txt", RecentFileOperation.Edit, "a.txt");

        bool isAdded = await app.Recent.AddAsync(
            new FileReference("grant:docs|docs/a.txt", "a.txt"),
            RecentFileOperation.View);

        Assert.That(isAdded, Is.True);
        Assert.That(app.Store.Files.Select(file => (file.Id, file.Operation)),
            Is.EqualTo(
            [
                ("grant:docs|docs/a.txt", RecentFileOperation.Edit),
                ("b.axx", RecentFileOperation.View),
            ]));
        Assert.That(app.Access.FileGrants, Is.Empty);
    }

    [Test]
    public async Task FileReachedThroughAFolderIsNotListedAsOnlyRead()
    {
        TestApp app = new(usesFolderIds: true);

        bool isAdded = await app.Recent.AddAsync(
            new FileReference("grant:docs|docs/a.txt", "a.txt"),
            RecentFileOperation.DecryptCopySendTo);

        Assert.That(isAdded, Is.False);
        Assert.That(app.Store.Files, Is.Empty);
    }

    [Test]
    public async Task ChangedFileIsListedWithoutAccessToItAlone()
    {
        TestApp app = new();

        await app.Recent.AddAsync(new FileReference("docs/a.txt", "a.txt"), RecentFileOperation.InPlace);
        await app.Recent.AddAsync(new FileReference("docs/b.txt", "b.txt"), RecentFileOperation.Edit);

        Assert.That(app.RecentIds(), Is.EqualTo(["docs/b.txt", "docs/a.txt"]));
        Assert.That(app.Access.FileGrants, Is.Empty);
    }

    [Test]
    public async Task FlowSourceIsListedWhenThereIsOne()
    {
        TestApp app = new();
        await app.Recent.AddFlowSourceAsync(RecentFileOperation.DecryptCopySendTo);
        app.Flow.Begin(FlowOrigin.Navigated, WorkFolderOperation.Decrypt, new FileReference("a.axx", "a.axx"));

        await app.Recent.AddFlowSourceAsync(RecentFileOperation.DecryptCopySendTo);

        Assert.That(app.RecentIds(), Is.EqualTo(["a.axx"]));
    }

    [Test]
    public async Task ReplacedIdKeepsItsPlaceOperationAndName()
    {
        TestApp app = new();
        app.Store.Files =
        [
            new RecentFile { Id = "b.txt", Operation = RecentFileOperation.View },
            new RecentFile { Id = "old/a.txt", OperationName = "FutureOperation", Name = "a.txt" },
            new RecentFile { Id = "new/a.txt", Operation = RecentFileOperation.InPlace },
        ];

        await app.Recent.ReplaceIdAsync("old/a.txt", "new/a.txt");

        Assert.That(app.Store.Files.Select(file => (file.Id, file.OperationName, file.Name)),
            Is.EqualTo(
            [
                ("b.txt", nameof(RecentFileOperation.View), (string?)null),
                ("new/a.txt", "FutureOperation", "a.txt"),
            ]));
    }
}
