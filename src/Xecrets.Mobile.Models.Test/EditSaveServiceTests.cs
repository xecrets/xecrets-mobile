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

using System.Text;

using NUnit.Framework;

using Xecrets.Common.Models;
using Xecrets.Core.Models;
using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class EditSaveServiceTests
{
    [Test]
    public async Task SaveEncryptsTheEditedTextOverTheSourceAndListsItInPlaceOfAnyEntryForTheFile()
    {
        using TestApp app = CreateEditing("text", usesFolderIds: true);
        app.AddFile("docs/x.axx", "ENC:a.txt\ntext");
        app.AddRecent("docs/x.axx", RecentFileOperation.InPlace, "x.axx");
        FileReference source = new("grant:docs|docs/x.axx", "x.axx");
        app.Flow.Begin(FlowOrigin.Home, source);

        await app.EditSave.SaveAsync("edited");

        Assert.That(Encoding.UTF8.GetString(app.Access.Files["docs/x.axx"]), Is.EqualTo("ENC:a.txt\nedited"));
        Assert.That(app.Preview.Current.Text, Is.EqualTo("edited"));
        Assert.That(app.Flow.Source, Is.EqualTo(source));
        Assert.That(app.Store.Files.Select(file => (file.Id, file.Operation)),
            Is.EqualTo([("grant:docs|docs/x.axx", RecentFileOperation.Edit)]));
        Assert.That(app.Transient.RemainingFiles(), Is.Empty);
    }

    [Test]
    public async Task SaveLeavesNoBackupOfTheSource()
    {
        using TestApp app = CreateEditing("text");
        app.AddFile("docs/x.axx", "ENC:a.txt\ntext");
        app.Flow.Begin(FlowOrigin.Home, new FileReference("docs/x.axx", "x.axx"));

        Assert.That(await app.EditSave.SaveAsync("edited"), Is.True);

        Assert.That(app.Access.Files.Keys, Is.EqualTo(["docs/x.axx"]));
    }

    [Test]
    public async Task SaveIsNotDoneWhenTheBackupNameIsInUse()
    {
        using TestApp app = CreateEditing("text");
        app.AddFile("docs/x.axx", "ENC:a.txt\ntext");
        app.AddFile("docs/x-axx.bak", "other");
        app.Flow.Begin(FlowOrigin.Home, new FileReference("docs/x.axx", "x.axx"));

        Assert.That(await app.EditSave.SaveAsync("edited"), Is.False);

        Assert.That(Encoding.UTF8.GetString(app.Access.Files["docs/x.axx"]), Is.EqualTo("ENC:a.txt\ntext"));
        Assert.That(Encoding.UTF8.GetString(app.Access.Files["docs/x-axx.bak"]), Is.EqualTo("other"));
    }

    /// <summary>
    /// Only the edits are lost, since the source is kept under its backup name while saving.
    /// </summary>
    [Test]
    public void FailedSaveKeepsTheSource()
    {
        using TestApp app = CreateEditing("text");
        app.AddFile("docs/x.axx", "ENC:a.txt\ntext");
        app.Access.FailingWrites.Add("docs/x.axx");
        app.Flow.Begin(FlowOrigin.Home, new FileReference("docs/x.axx", "x.axx"));

        Assert.That(async () => await app.EditSave.SaveAsync("edited"), Throws.TypeOf<IOException>());

        Assert.That(app.Access.Files.Keys, Is.EqualTo(["docs/x.axx"]));
        Assert.That(Encoding.UTF8.GetString(app.Access.Files["docs/x.axx"]), Is.EqualTo("ENC:a.txt\ntext"));
        Assert.That(app.Transient.RemainingFiles(), Is.Empty);
    }

    [Test]
    public void SaveIsNotDoneWhenTheSourceCannotBeKeptUnderItsBackupName()
    {
        using TestApp app = CreateEditing("text");
        app.AddFile("docs/x.axx", "ENC:a.txt\ntext");
        app.Access.FailingRenames.Add("docs/x.axx");
        app.Flow.Begin(FlowOrigin.Home, new FileReference("docs/x.axx", "x.axx"));

        Assert.That(async () => await app.EditSave.SaveAsync("edited"), Throws.TypeOf<IOException>());

        Assert.That(app.Access.Files.Keys, Is.EqualTo(["docs/x.axx"]));
        Assert.That(Encoding.UTF8.GetString(app.Access.Files["docs/x.axx"]), Is.EqualTo("ENC:a.txt\ntext"));
    }

    [Test]
    public async Task CopySavedInMyFoldersBecomesTheSource()
    {
        using TestApp app = CreateEditing("text");
        app.AddFolder("docs");
        app.Access.PickedFiles.Enqueue("docs/copy.axx");

        await app.EditSave.SaveCopyAsync("edited");

        Assert.That(Encoding.UTF8.GetString(app.Access.Files["docs/copy.axx"]), Is.EqualTo("ENC:a.txt\nedited"));
        Assert.That(app.Flow.Source, Is.EqualTo(new FileReference("docs/copy.axx", "copy.axx")));
        Assert.That(app.Store.Files.Select(file => (file.Id, file.Operation)),
            Is.EqualTo([("docs/copy.axx", RecentFileOperation.Edit)]));
    }

    [Test]
    public async Task CopySavedOutsideMyFoldersLeavesTheSourceUnchanged()
    {
        using TestApp app = CreateEditing("text");
        app.AddFolder("docs");
        app.Access.PickedFiles.Enqueue("downloads/copy.axx");

        await app.EditSave.SaveCopyAsync("edited");

        Assert.That(Encoding.UTF8.GetString(app.Access.Files["downloads/copy.axx"]), Is.EqualTo("ENC:a.txt\nedited"));
        Assert.That(app.Flow.Source, Is.Null);
        Assert.That(app.Store.Files, Is.Empty);
    }

    [Test]
    public async Task SaveEncryptsTheEditedTextForAllTheSourceIsEncryptedFor()
    {
        PublicKey other = new("other@example.com", "other");
        PublicKey masterKey = new("", "master");
        using TestApp app = CreateEditing("text", new EncryptedWithParameters("password", [other], [masterKey]));
        app.AddFile("docs/x.axx", "ENC:a.txt\ntext");
        app.Flow.Begin(FlowOrigin.Home, new FileReference("docs/x.axx", "x.axx"));

        await app.EditSave.SaveAsync("edited");

        EncryptRequest request = app.Core.EncryptRequests.Single();
        Assert.That(request.Passphrase, Is.EqualTo(app.Profile.GetIdentity().Passphrase));
        Assert.That(request.Recipients, Is.EqualTo([other, app.Profile.GetPublicKey()]));
        Assert.That(request.MasterKeys, Is.EqualTo([masterKey]));
    }

    [Test]
    public async Task CopyEncryptsTheEditedTextForAllTheSourceIsEncryptedFor()
    {
        PublicKey other = new("other@example.com", "other");
        PublicKey masterKey = new("", "master");
        using TestApp app = CreateEditing("text", new EncryptedWithParameters("password", [other], [masterKey]));
        app.AddFolder("docs");
        app.Access.PickedFiles.Enqueue("docs/copy.axx");

        await app.EditSave.SaveCopyAsync("edited");

        EncryptRequest request = app.Core.EncryptRequests.Single();
        Assert.That(request.Recipients, Is.EqualTo([other, app.Profile.GetPublicKey()]));
        Assert.That(request.MasterKeys, Is.EqualTo([masterKey]));
    }

    /// <summary>
    /// The text was just changed, so only the creation time of the original is kept.
    /// </summary>
    [Test]
    public async Task SaveKeepsTheCreationTimeOfTheOriginal()
    {
        using TestApp app = CreateEditing("text");
        DateTime created = new(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        app.Preview.Current.SetText(
            app.Preview.Current.File with { OriginalTimes = new FileTimes(created, created, created) },
            "text"u8.ToArray(),
            EncryptedWithParameters.Empty);
        app.AddFile("docs/x.axx", "ENC:a.txt\ntext");
        app.Flow.Begin(FlowOrigin.Home, new FileReference("docs/x.axx", "x.axx"));
        DateTime before = DateTime.UtcNow;

        await app.EditSave.SaveAsync("edited");

        EncryptRequest request = app.Core.EncryptRequests.Single();
        Assert.That(request.CreationTimeUtc, Is.EqualTo(created));
        Assert.That(request.LastWriteTimeUtc, Is.InRange(before, DateTime.UtcNow));
        Assert.That(request.LastAccessTimeUtc, Is.InRange(before, DateTime.UtcNow));
    }

    [Test]
    public async Task SaveDoesNotAddTheProfileToTheSourceRecipientsAgain()
    {
        using TestApp app = CreateEditing("text");
        app.Preview.Current.SetText(
            app.Preview.Current.File,
            "text"u8.ToArray(),
            new EncryptedWithParameters("password", [app.Profile.GetPublicKey()], []));
        app.AddFile("docs/x.axx", "ENC:a.txt\ntext");
        app.Flow.Begin(FlowOrigin.Home, new FileReference("docs/x.axx", "x.axx"));

        await app.EditSave.SaveAsync("edited");

        Assert.That(app.Core.EncryptRequests.Single().Recipients, Is.EqualTo([app.Profile.GetPublicKey()]));
    }

    /// <summary>
    /// A file the text was handed over in before it was edited no longer holds what is shown.
    /// </summary>
    [Test]
    public async Task SaveWipesTheFileTheTextWasHandedOverIn()
    {
        using TestApp app = CreateEditing("text");
        string decryptedPath = app.Transient.CreateHandoffPath("a.txt");
        await File.WriteAllTextAsync(decryptedPath, "text");
        app.Preview.Current.SetDecryptedPath(decryptedPath);
        app.AddFile("docs/x.axx", "ENC:a.txt\ntext");
        app.Flow.Begin(FlowOrigin.Home, new FileReference("docs/x.axx", "x.axx"));

        await app.EditSave.SaveAsync("edited");

        Assert.That(app.Preview.Current.File.FilePath, Is.Empty);
        Assert.That(app.Preview.Current.File.FilePath, Is.Empty);
        Assert.That(app.Transient.RemainingFiles(), Is.Empty);
    }

    [Test]
    public void FailedSaveKeepsTheTextShown()
    {
        using TestApp app = CreateEditing("text");
        app.AddFile("docs/x.axx", "ENC:a.txt\ntext");
        app.Access.FailingWrites.Add("docs/x.axx");
        app.Flow.Begin(FlowOrigin.Home, new FileReference("docs/x.axx", "x.axx"));

        Assert.That(async () => await app.EditSave.SaveAsync("edited"), Throws.TypeOf<IOException>());

        Assert.That(app.Preview.Current.Text, Is.EqualTo("text"));
    }

    [Test]
    public async Task SavedTextIsShownWithItsSizeAsUtf8()
    {
        using TestApp app = CreateEditing("text");
        app.AddFile("docs/x.axx", "ENC:a.txt\ntext");
        app.Flow.Begin(FlowOrigin.Home, new FileReference("docs/x.axx", "x.axx"));

        await app.EditSave.SaveAsync("Räksmörgås");

        Assert.That(app.Preview.Current.Text, Is.EqualTo("Räksmörgås"));
        Assert.That(app.Preview.Current.File.FileSize, Is.EqualTo(Encoding.UTF8.GetByteCount("Räksmörgås")));
    }

    [Test]
    public async Task CopyKeepsTheCreationTimeOfTheOriginal()
    {
        using TestApp app = CreateEditing("text");
        DateTime created = new(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        app.Preview.Current.SetText(
            app.Preview.Current.File with { OriginalTimes = new FileTimes(created, created, created) },
            "text"u8.ToArray(),
            EncryptedWithParameters.Empty);
        app.AddFolder("docs");
        app.Access.PickedFiles.Enqueue("docs/copy.axx");

        await app.EditSave.SaveCopyAsync("edited");

        Assert.That(app.Core.EncryptRequests.Single().CreationTimeUtc, Is.EqualTo(created));
        Assert.That(app.Preview.Current.Text, Is.EqualTo("edited"));
        Assert.That(app.Transient.RemainingFiles(), Is.Empty);
    }

    [Test]
    public async Task CopyThatIsNotSavedKeepsTheTextShown()
    {
        using TestApp app = CreateEditing("text");
        app.Access.PickedFiles.Enqueue(null);

        Assert.That(await app.EditSave.SaveCopyAsync("edited"), Is.False);

        Assert.That(app.Preview.Current.Text, Is.EqualTo("text"));
    }

    private TestApp CreateEditing(string text, bool usesFolderIds = false) =>
        CreateEditing(text, EncryptedWithParameters.Empty, usesFolderIds);

    private TestApp CreateEditing(string text, EncryptedWithParameters encryptedWith, bool usesFolderIds = false)
    {
        TestApp app = new(usesFolderIds);
        app.Preview.Current.SetText(
            new DecryptedFileInfo("", "a.txt", "text/plain", text.Length, PreviewKind.Text, FileTimes.UtcNow),
            Encoding.UTF8.GetBytes(text),
            encryptedWith);
        return app;
    }
}
