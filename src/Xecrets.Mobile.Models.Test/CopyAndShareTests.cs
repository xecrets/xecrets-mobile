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
using Xecrets.Mobile.Models.Services;
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class CopyAndShareTests
{
    [Test]
    public async Task CopyOfAPlainFileIsEncryptedAndTheFileIsLeftAsItIs()
    {
        TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/a.txt");
        app.Access.PickedFiles.Enqueue("docs/a.txt");

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Copy, docs);

        Assert.That(app.UserInterface.Destinations, Is.EqualTo([AppDestination.EncryptResult]));
        Assert.That(((EncryptionPreparationResult)app.UserInterface.Payloads.Single()).IsForPassword, Is.False);
        Assert.That(app.Access.Files.Keys, Is.EqualTo(["docs/a.txt"]));
        Assert.That(app.Flow.Origin, Is.EqualTo(FlowOrigin.Home));
    }

    [Test]
    public async Task CopyOfAnEncryptedFileIsPreviewedAndTheFileIsLeftAsItIs()
    {
        TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/x.axx", "ENC:a.txt\ntext");
        app.Access.PickedFiles.Enqueue("docs/x.axx");

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Copy, docs);

        Assert.That(app.UserInterface.Destinations, Is.EqualTo([AppDestination.Preview]));
        Assert.That(app.Access.Files.Keys, Is.EqualTo(["docs/x.axx"]));
        Assert.That(app.Flow.Origin, Is.EqualTo(FlowOrigin.Home));
    }

    [Test]
    public async Task CopyOfAFileNamedEncryptedButNotEncryptedIsNotPreviewed()
    {
        TestApp app = new();
        app.AddFile("docs/x.axx");
        app.Access.PickedFiles.Enqueue("docs/x.axx");

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Copy, null);

        Assert.That(app.UserInterface.Destinations, Is.Empty);
        Assert.That(app.UserInterface.TransientMessages, Is.EqualTo([MobileTexts.DialogTextNotEncrypted]));
    }

    [TestCase(WorkFolderIntent.Copy)]
    [TestCase(WorkFolderIntent.Share)]
    public async Task CopyDoesNotMoveTheFolderOfTheFileToTheTop(WorkFolderIntent intent)
    {
        TestApp app = new();
        app.AddFolder("docs");
        WorkFolder other = app.AddFolder("other");
        app.AddFile("other/a.txt");
        app.Access.PickedFiles.Enqueue("other/a.txt");

        await app.Operations.PickAndRunAsync(intent, other);

        Assert.That(app.FolderIds(), Is.EqualTo(["docs", "other"]));
    }

    [Test]
    public async Task SharingAFileAsksForThePasswordForIt()
    {
        TestApp app = new();
        app.AddFile("docs/a.txt");
        app.Access.PickedFiles.Enqueue("docs/a.txt");

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Share, null);

        Assert.That(app.UserInterface.Destinations, Is.EqualTo([AppDestination.EncryptToShare]));
        Assert.That(((FileReference)app.UserInterface.Payloads.Single()).Id, Is.EqualTo("docs/a.txt"));
        Assert.That(app.Flow.Origin, Is.EqualTo(FlowOrigin.Home));
    }

    [TestCase("docs/x.axx", "text")]
    [TestCase("docs/a.txt", "ENC:a.txt\ntext")]
    public async Task SharingAnEncryptedFileIsRefusedBeforeThePasswordIsAskedFor(string path, string content)
    {
        TestApp app = new();
        app.AddFile(path, content);
        app.Access.PickedFiles.Enqueue(path);

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Share, null);

        Assert.That(app.UserInterface.Destinations, Is.Empty);
        Assert.That(app.UserInterface.TransientMessages, Is.EqualTo([MobileTexts.DialogTextAlreadyEncrypted]));
    }

    [Test]
    public async Task SharePasswordEncryptsACopyOfTheChosenFile()
    {
        TestApp app = new();
        app.AddFile("docs/a.txt");
        app.Flow.Begin(FlowOrigin.Home, new FileReference("docs/a.txt", "a.txt"));
        EncryptToSharePageModel page = new(
            new FakeProfileService(),
            app.Access,
            app.Encryption,
            app.Flow,
            app.CrashLog,
            app.UserInterface);
        page.Initialize(new FileReference("docs/a.txt", "a.txt"));
        page.Password = "shared";

        await page.SubmitCommand.ExecuteAsync(null);

        Assert.That(app.UserInterface.Destinations, Is.EqualTo([AppDestination.EncryptResult]));
        Assert.That(((EncryptionPreparationResult)app.UserInterface.Payloads.Single()).IsForPassword, Is.True);
        Assert.That(page.Password, Is.Empty);
        Assert.That(app.Access.Files.Keys, Is.EqualTo(["docs/a.txt"]));
        Assert.That(page.Breadcrumb, Is.EqualTo(string.Join(
            MobileTexts.BreadcrumbSeparator,
            MobileTexts.BreadcrumbHome,
            MobileTexts.BreadcrumbEncryptToShare,
            MobileTexts.BreadcrumbPassword)));
    }
}
