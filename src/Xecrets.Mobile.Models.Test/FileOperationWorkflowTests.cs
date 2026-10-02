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
public sealed class FileOperationWorkflowTests
{
    [Test]
    public async Task SecureDeleteWipesTheFileAndRemovesItFromRecentFiles()
    {
        TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/a.txt");
        app.AddRecent("docs/a.txt", RecentFileOperation.InPlace);
        app.Access.PickedFiles.Enqueue("docs/a.txt");
        app.UserInterface.Answers.Enqueue(true);

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Delete, docs);

        Assert.That(app.UserInterface.Confirmations, Is.EqualTo([MobileTexts.MessageTextConfirmWipe]));
        Assert.That(app.Access.Files, Is.Empty);
        Assert.That(app.Store.Files, Is.Empty);
    }

    [Test]
    public async Task SecureDeleteRemovesTheFileUnderAnyIdItIsListedBy()
    {
        TestApp app = new(usesFolderIds: true);
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/a.txt");
        app.AddRecent("docs/a.txt", RecentFileOperation.View);
        app.AddRecent("bad:1", RecentFileOperation.View);
        app.Access.PickedFiles.Enqueue("docs/a.txt");
        app.UserInterface.Answers.Enqueue(true);

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Delete, docs);

        Assert.That(app.RecentIds(), Is.EqualTo(["bad:1"]));
    }

    [Test]
    public async Task SecureDeleteDoesNothingUnlessConfirmed()
    {
        TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/a.txt");
        app.Access.PickedFiles.Enqueue("docs/a.txt");

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Delete, docs);

        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["docs/a.txt"]));
    }

    [Test]
    public async Task DecryptOfFileThatIsNotEncryptedOnlyTellsTheUser()
    {
        TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/a.txt");
        app.Access.PickedFiles.Enqueue("docs/a.txt");

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Decrypt, docs);

        Assert.That(app.UserInterface.TransientMessages, Is.EqualTo([MobileTexts.DialogTextNotEncrypted]));
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["docs/a.txt"]));
    }

    [Test]
    public async Task FileIsDecryptedOrEncryptedByItsContentsByDefault()
    {
        TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.Access.Files["docs/x.axx"] = "ENC:a.txt\ntext"u8.ToArray();
        app.Access.PickedFiles.Enqueue("docs/x.axx");

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Auto, docs);

        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["docs/a.txt"]));
        Assert.That(app.UserInterface.TransientMessages, Is.EqualTo([MobileTexts.DialogTextFileDecrypted]));
    }
}
