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
using Xecrets.Texts;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class WorkFolderOperationServiceTests
{
    /// <summary>
    /// Storage such as Google Drive may still report a deleted file as existing, so the source must be removed from the
    /// recent files by the operation itself.
    /// </summary>
    [Test]
    public async Task EncryptWritesResultNextToSourceWipesSourceAndReplacesItInRecentFiles()
    {
        TestApp app = new();
        app.AddFile("docs/a.txt");
        app.AddRecent("docs/a.txt", RecentFileOperation.InPlace, "a.txt");
        app.AddRecent("other.txt", RecentFileOperation.InPlace);
        string encryptedName = "a.txt".ToEncryptedName(string.Empty);

        FileReference result = await app.OperationService.EncryptAsync(new FileReference("docs/a.txt", "a.txt"));

        Assert.That(result, Is.EqualTo(new FileReference($"docs/{encryptedName}", encryptedName)));
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo([$"docs/{encryptedName}"]));
        Assert.That(app.Wiper.Wiped, Is.EqualTo(["docs/a.txt"]));
        Assert.That(app.Store.Files.Select(file => (file.Id, file.Name)),
            Is.EqualTo([($"docs/{encryptedName}", (string?)encryptedName), ("other.txt", null)]));
    }

    /// <summary>
    /// The same file may be listed under several ids, such as when viewed after being picked by itself, and none of
    /// them refers to a file that exists after the operation.
    /// </summary>
    [Test]
    public async Task EncryptRemovesTheSourceUnderAnyIdItIsListedBy()
    {
        TestApp app = new(usesFolderIds: true);
        app.AddFile("docs/a.txt");
        app.AddRecent("grant:docs|docs/a.txt", RecentFileOperation.InPlace, "a.txt");
        app.AddRecent("docs/a.txt", RecentFileOperation.EncryptCopySaveAs, "a.txt");
        app.AddRecent("docs/b.txt", RecentFileOperation.InPlace, "b.txt");
        string encryptedName = "a.txt".ToEncryptedName(string.Empty);

        await app.OperationService.EncryptAsync(new FileReference("grant:docs|docs/a.txt", "a.txt"));

        Assert.That(app.RecentIds(), Is.EqualTo([$"grant:docs|docs/{encryptedName}", "docs/b.txt"]));
    }

    [Test]
    public async Task DecryptWritesTheOriginalFileNextToTheSource()
    {
        TestApp app = new(usesFolderIds: true);
        app.Access.Files["docs/x.axx"] = "ENC:a.txt\ntext"u8.ToArray();

        FileReference? result = await app.OperationService.DecryptWithKnownPasswordsAsync(
            new FileReference("grant:docs|docs/x.axx", "x.axx"));

        Assert.That(result, Is.EqualTo(new FileReference("grant:docs|docs/a.txt", "a.txt")));
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["docs/a.txt"]));
        Assert.That(app.RecentIds(), Is.EqualTo(["grant:docs|docs/a.txt"]));
    }

    [Test]
    public async Task FileThatCannotBeDecryptedIsKeptForAPassword()
    {
        TestApp app = new();
        app.AddFile("docs/x.axx", "not encrypted");

        FileReference? result = await app.OperationService.DecryptWithKnownPasswordsAsync(
            new FileReference("docs/x.axx", "x.axx"));

        Assert.That(result, Is.Null);
        Assert.That(app.OperationService.HasPendingPasswordRequest, Is.True);
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["docs/x.axx"]));
    }

    [Test]
    public async Task FileKeptForAPasswordIsDecryptedWithIt()
    {
        TestApp app = new();
        app.Access.Files["docs/x.axx"] = "ENC:a.txt\ntext"u8.ToArray();
        app.Core.Password = "other";
        await app.OperationService.DecryptWithKnownPasswordsAsync(new FileReference("docs/x.axx", "x.axx"));

        FileReference? wrong = await app.OperationService.DecryptWithPasswordAsync("wrong");
        FileReference? result = await app.OperationService.DecryptWithPasswordAsync("other");

        Assert.That(wrong, Is.Null);
        Assert.That(result, Is.EqualTo(new FileReference("docs/a.txt", "a.txt")));
        Assert.That(app.OperationService.HasPendingPasswordRequest, Is.False);
    }

    /// <summary>
    /// A failure to wipe the source is unexpected and reported, but the operation itself has succeeded, so the recent
    /// files still list the result instead of the source.
    /// </summary>
    [Test]
    public void FailedWipeIsReportedAndRecentFilesAreStillUpdated()
    {
        TestApp app = new();
        app.Access.Files["docs/x.axx"] = "ENC:a.txt\ntext"u8.ToArray();
        app.AddRecent("docs/x.axx", RecentFileOperation.InPlace);
        app.Wiper.Fails = true;

        Assert.That(async () => await app.OperationService.DecryptWithKnownPasswordsAsync(
                new FileReference("docs/x.axx", "x.axx")),
            Throws.TypeOf<IOException>().With.Message.EqualTo("Wipe failed."));
        Assert.That(app.RecentIds(), Is.EqualTo(["docs/a.txt"]));
    }

    /// <summary>
    /// Finding the folder of a file may take a search of the folders on some storage, so it is done once.
    /// </summary>
    [Test]
    public async Task FolderOfTheFileIsFoundOnceForAnOperation()
    {
        TestApp app = new();
        string encryptedName = "a.txt".ToEncryptedName(string.Empty);
        app.AddFile("docs/a.txt");
        app.AddFile($"docs/{encryptedName}", "existing");
        app.UserInterface.Answers.Enqueue(true);

        await app.OperationService.EncryptAsync(new FileReference("docs/a.txt", "a.txt"));

        Assert.That(app.Access.FolderLookupCount, Is.EqualTo(1));
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo([$"docs/{encryptedName}"]));
    }

    [Test]
    public void ExistingResultIsOnlyOverwrittenWhenTheUserConfirms()
    {
        TestApp app = new();
        string encryptedName = "a.txt".ToEncryptedName(string.Empty);
        app.AddFile("docs/a.txt");
        app.AddFile($"docs/{encryptedName}", "existing");

        Assert.That(async () => await app.OperationService.EncryptAsync(new FileReference("docs/a.txt", "a.txt")),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(app.UserInterface.Confirmations,
            Is.EqualTo([string.Format(MobileTexts.DialogTextConfirmOverwrite, encryptedName)]));
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["docs/a.txt", $"docs/{encryptedName}"]));
    }
}
