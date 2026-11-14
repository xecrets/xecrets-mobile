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
public sealed class FolderFilesWorkflowTests
{
    [Test]
    public async Task EncryptCountsAndEncryptsOnlyTheFilesNotEncrypted()
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/a.txt", "text");
        app.AddFile("docs/b.txt", "hello");
        app.AddFile("docs/x.axx", "ENC:x.txt\ntext");
        app.UserInterface.Answers.Enqueue(true);

        await PickAndRunAsync(app, docs, FileOperation.Encrypt, FolderScope.Folder);

        Assert.That(
            app.UserInterface.Confirmations,
            Is.EqualTo(
            [
                Confirmation(MobileTexts.DialogTextConfirmEncryptFiles, 2, 9L),
            ]));
        Assert.That(app.Access.Files.Keys, Has.Count.EqualTo(3).And.All.EndsWith(".axx"));
        Assert.That(
            app.UserInterface.TransientMessages,
            Is.EqualTo([$"{MobileTexts.DialogTextDone} {string.Format(MobileTexts.DialogTextFilesEncryptedFormat, 2.ToCountText())}"]));
    }

    [Test]
    public async Task DecryptCountsAndDecryptsOnlyTheEncryptedFiles()
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/a.txt", "text");
        app.AddFile("docs/x.axx", "ENC:x.txt\ntext");
        app.UserInterface.Answers.Enqueue(true);

        await PickAndRunAsync(app, docs, FileOperation.Decrypt, FolderScope.Folder);

        Assert.That(
            app.UserInterface.Confirmations,
            Is.EqualTo(
            [
                Confirmation(MobileTexts.DialogTextConfirmDecryptFiles, 1, 14L),
            ]));
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["docs/a.txt", "docs/x.txt"]));
        Assert.That(
            app.UserInterface.TransientMessages,
            Is.EqualTo([$"{MobileTexts.DialogTextDone} {string.Format(MobileTexts.DialogTextFilesDecryptedFormat, 1.ToCountText())}"]));
    }

    /// <summary>
    /// A provider may make a name unique with a number after the extension, which still tells the file is encrypted.
    /// </summary>
    [Test]
    public async Task EncryptLeavesFilesNamedEncryptedWithANumberAfterTheExtension()
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/a.txt", "text");
        app.AddFile("docs/x-txt.axx (1)", "ENC:x.txt\ntext");
        app.UserInterface.Answers.Enqueue(true);

        await PickAndRunAsync(app, docs, FileOperation.Encrypt, FolderScope.Folder);

        Assert.That(
            app.UserInterface.Confirmations,
            Is.EqualTo([Confirmation(MobileTexts.DialogTextConfirmEncryptFiles, 1, 4L)]));
        Assert.That(
            app.Access.Files.Keys,
            Is.EquivalentTo([$"docs/{"a.txt".ToEncryptedName("")}", "docs/x-txt.axx (1)"]));
    }

    [Test]
    public async Task DecryptIncludesFilesNamedEncryptedWithANumberAfterTheExtension()
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/a.txt", "text");
        app.AddFile("docs/x-txt.axx (1)", "ENC:x.txt\ntext");
        app.UserInterface.Answers.Enqueue(true);

        await PickAndRunAsync(app, docs, FileOperation.Decrypt, FolderScope.Folder);

        Assert.That(
            app.UserInterface.Confirmations,
            Is.EqualTo([Confirmation(MobileTexts.DialogTextConfirmDecryptFiles, 1, 14L)]));
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["docs/a.txt", "docs/x.txt"]));
        Assert.That(
            app.UserInterface.TransientMessages,
            Is.EqualTo([$"{MobileTexts.DialogTextDone} {string.Format(MobileTexts.DialogTextFilesDecryptedFormat, 1.ToCountText())}"]));
    }

    [Test]
    public async Task NothingIsDoneUnlessConfirmed()
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/a.txt");

        await PickAndRunAsync(app, docs, FileOperation.Encrypt, FolderScope.Folder);

        Assert.That(app.UserInterface.Confirmations, Has.Count.EqualTo(1));
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["docs/a.txt"]));
        Assert.That(app.UserInterface.TransientMessages, Is.Empty);
    }

    [TestCase(FolderScope.Folder, new[] { "docs/sub/b.txt" })]
    [TestCase(FolderScope.FolderAndSubfolders, new string[0])]
    public async Task FilesInSubfoldersAreOnlyIncludedWhenChosen(FolderScope scope, string[] left)
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/a.txt");
        app.AddFile("docs/sub/b.txt");
        app.AddFile("other/c.txt");
        app.UserInterface.Answers.Enqueue(true);

        await PickAndRunAsync(app, docs, FileOperation.Delete, scope);

        Assert.That(app.Access.Files.Keys, Is.EquivalentTo([.. left, "other/c.txt"]));
    }

    /// <summary>
    /// A folder without files to change is not confirmed, and the summary tells that none were done.
    /// </summary>
    [TestCase(FileOperation.Encrypt, nameof(MobileTexts.DialogTextFilesEncryptedFormat))]
    [TestCase(FileOperation.Decrypt, nameof(MobileTexts.DialogTextFilesDecryptedFormat))]
    [TestCase(FileOperation.Delete, nameof(MobileTexts.DialogTextFilesDeletedFormat))]
    public async Task UserIsToldWhenThereAreNoFiles(FileOperation operation, string countFormatName)
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        if (operation == FileOperation.Decrypt)
        {
            app.AddFile("docs/a.txt");
        }
        else if (operation == FileOperation.Encrypt)
        {
            app.AddFile("docs/x.axx", "ENC:x.txt\ntext");
        }

        await PickAndRunAsync(app, docs, operation, FolderScope.Folder);

        string countFormat = countFormatName switch
        {
            nameof(MobileTexts.DialogTextFilesEncryptedFormat) => MobileTexts.DialogTextFilesEncryptedFormat,
            nameof(MobileTexts.DialogTextFilesDecryptedFormat) => MobileTexts.DialogTextFilesDecryptedFormat,
            nameof(MobileTexts.DialogTextFilesDeletedFormat) => MobileTexts.DialogTextFilesDeletedFormat,
            _ => throw new ArgumentOutOfRangeException(nameof(countFormatName), countFormatName, null),
        };
        Assert.That(app.UserInterface.Confirmations, Is.Empty);
        Assert.That(
            app.UserInterface.TransientMessages,
            Is.EqualTo([$"{MobileTexts.DialogTextDone} {string.Format(countFormat, 0.ToCountText())}"]));
    }

    [Test]
    public async Task FirstFileNotDoneStopsTheOthersWithWhatTheUserIsToldForTheFileByItself()
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/a.txt");
        app.AddFile("docs/b.txt");
        app.Wiper.Denied.Add("docs/a.txt");
        app.UserInterface.Answers.Enqueue(true);

        await PickAndRunAsync(app, docs, FileOperation.Encrypt, FolderScope.Folder);

        Assert.That(app.UserInterface.Messages, Is.EqualTo([MobileTexts.DialogTextInsufficientRights]));
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["docs/a.txt", "docs/b.txt"]));
    }

    [Test]
    public async Task FileFoundEncryptedIsPassedOverAndTheOthersAreEncrypted()
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/a.txt", "ENC:x.txt\ntext");
        app.AddFile("docs/b.txt");
        app.UserInterface.Answers.Enqueue(true);

        await PickAndRunAsync(app, docs, FileOperation.Encrypt, FolderScope.Folder);

        Assert.That(app.UserInterface.Messages, Is.Empty);
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["docs/a.txt", $"docs/{"b.txt".ToEncryptedName("")}"]));
        Assert.That(
            app.UserInterface.TransientMessages,
            Is.EqualTo([$"{MobileTexts.DialogTextDone} {string.Format(MobileTexts.DialogTextFilesEncryptedFormat, 1.ToCountText())}"]));
    }

    [Test]
    public async Task FileNamedEncryptedButNotEncryptedIsPassedOverAndTheOthersAreDecrypted()
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/x.axx", "not encrypted");
        app.AddFile("docs/y.axx", "ENC:y.txt\ntext");
        app.UserInterface.Answers.Enqueue(true);

        await PickAndRunAsync(app, docs, FileOperation.Decrypt, FolderScope.Folder);

        Assert.That(app.UserInterface.Messages, Is.Empty);
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["docs/x.axx", "docs/y.txt"]));
        Assert.That(
            app.UserInterface.TransientMessages,
            Is.EqualTo([$"{MobileTexts.DialogTextDone} {string.Format(MobileTexts.DialogTextFilesDecryptedFormat, 1.ToCountText())}"]));
    }

    [Test]
    public async Task FileNeedingAnotherPasswordStopsTheOthersWhenTheUserLeavesWithoutOne()
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/x.axx", "ENC:x.txt\ntext");
        app.AddFile("docs/y.axx", "ENC:y.txt\ntext");
        app.Core.Password = "other";
        app.UserInterface.Answers.Enqueue(true);
        Task run = PickAndRunAsync(app, docs, FileOperation.Decrypt, FolderScope.Folder);

        app.CreateEnterPasswordPage().CancelWorkFolderRequest();
        await run;

        Assert.That(app.UserInterface.Destinations, Is.EqualTo([AppDestination.EnterPassword]));
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["docs/x.axx", "docs/y.axx"]));
        Assert.That(app.UserInterface.TransientMessages, Is.Empty);
    }

    [Test]
    public async Task PasswordPageLeftBeforeItIsShownLeavesTheFile()
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/x.axx", "ENC:x.txt\ntext");
        app.Core.Password = "other";
        app.UserInterface.Answers.Enqueue(true);
        app.UserInterface.Navigating = _ => app.CreateEnterPasswordPage().CancelWorkFolderRequest();

        await PickAndRunAsync(app, docs, FileOperation.Decrypt, FolderScope.Folder);

        Assert.That(app.UserInterface.Destinations, Is.EqualTo([AppDestination.EnterPassword]));
        Assert.That(app.Access.Files.Keys, Is.EqualTo(["docs/x.axx"]));
        Assert.That(app.UserInterface.TransientMessages, Is.Empty);
    }

    [Test]
    public async Task FilesNeedingDifferentPasswordsAreDoneWhenTheUserEntersEach()
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/x.axx", "ENC:x.txt\ntext");
        app.AddFile("docs/y.axx", "ENC:y.txt\ntext");
        app.Core.PasswordsByName["x.txt"] = "first";
        app.Core.PasswordsByName["y.txt"] = "second";
        app.UserInterface.Answers.Enqueue(true);
        using SemaphoreSlim shown = new(0);
        app.UserInterface.Navigating = _ => shown.Release();

        Task run = PickAndRunAsync(app, docs, FileOperation.Decrypt, FolderScope.Folder);
        await EnterPasswordWhenShownAsync(app, shown, "first");
        await EnterPasswordWhenShownAsync(app, shown, "second");
        await run.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.That(
            app.UserInterface.Destinations,
            Is.EqualTo([AppDestination.EnterPassword, AppDestination.EnterPassword]));
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["docs/x.txt", "docs/y.txt"]));
        Assert.That(
            app.UserInterface.TransientMessages,
            Is.EqualTo([$"{MobileTexts.DialogTextDone} {string.Format(MobileTexts.DialogTextFilesDecryptedFormat, 2.ToCountText())}"]));
    }

    [Test]
    public async Task FileNeedingAnotherPasswordIsDoneAndTheOthersFollowWhenTheUserEntersOne()
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/x.axx", "ENC:x.txt\ntext");
        app.AddFile("docs/y.axx", "ENC:y.txt\ntext");
        app.Core.Password = "other";
        app.UserInterface.Answers.Enqueue(true);
        Task run = PickAndRunAsync(app, docs, FileOperation.Decrypt, FolderScope.Folder);
        EnterPasswordPageModel passwordPage = app.CreateEnterPasswordPage();
        passwordPage.Initialize(new DecryptionPasswordsPayload(DecryptionPasswordsIntent.Add, "x.axx"));
        passwordPage.Password = "other";

        await passwordPage.SubmitCommand.ExecuteAsync(null);
        // As the profile does, the password that decrypted the file is kept, so the other files need no password.
        app.Profile.ExtraPasswords.Add(new PasswordUsage { Password = "other" });
        passwordPage.CancelWorkFolderRequest();
        await run;

        Assert.That(app.UserInterface.Destinations, Is.EqualTo([AppDestination.EnterPassword]));
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["docs/x.txt", "docs/y.txt"]));
        Assert.That(
            app.UserInterface.TransientMessages,
            Is.EqualTo([$"{MobileTexts.DialogTextDone} {string.Format(MobileTexts.DialogTextFilesDecryptedFormat, 2.ToCountText())}"]));
    }

    [Test]
    public async Task EncryptedAndDecryptedFilesAreNotAddedToRecentFiles()
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/a.txt");
        app.AddFile("docs/x.axx", "ENC:x.txt\ntext");
        app.UserInterface.Answers.Enqueue(true);
        app.UserInterface.Answers.Enqueue(true);

        await PickAndRunAsync(app, docs, FileOperation.Encrypt, FolderScope.Folder);
        await PickAndRunAsync(app, docs, FileOperation.Decrypt, FolderScope.Folder);

        Assert.That(app.Store.Files, Is.Empty);
    }

    [Test]
    public async Task DeleteIsConfirmedOnceWithTheWarningThatItCannotBeUndone()
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs") with { Root = RootProvider.AndroidPhone };
        app.AddFile("docs/a.txt", "text");
        app.AddFile("docs/b.txt", "text");
        app.AddRecent("docs/a.txt", RecentFileOperation.InPlace);
        app.UserInterface.Answers.Enqueue(true);

        await PickAndRunAsync(app, docs, FileOperation.Delete, FolderScope.Folder);

        string question =
            Confirmation(MobileTexts.DialogTextConfirmDeleteFiles, 2, 8L);
        Assert.That(app.UserInterface.Confirmations, Is.EqualTo([$"{question} {MobileTexts.MessageTextConfirmWipe}"]));
        Assert.That(app.Access.Files, Is.Empty);
        Assert.That(app.Store.Files, Is.Empty);
        Assert.That(
            app.UserInterface.TransientMessages,
            Is.EqualTo([$"{MobileTexts.DialogTextDone} {string.Format(MobileTexts.DialogTextFilesDeletedFormat, 2.ToCountText())}"]));
    }

    [Test]
    public async Task DeleteWhereTheFilesMayBeRecoveredHasTheWarningToo()
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs") with { Root = RootProvider.Dropbox };
        app.AddFile("docs/a.txt", "text");
        app.UserInterface.Answers.Enqueue(true);

        await PickAndRunAsync(app, docs, FileOperation.Delete, FolderScope.Folder);

        string question =
            Confirmation(MobileTexts.DialogTextConfirmDeleteFiles, 1, 4L);
        Assert.That(app.UserInterface.Confirmations, Is.EqualTo([$"{question} {MobileTexts.MessageTextConfirmWipe}"]));
        Assert.That(app.Access.Files, Is.Empty);
    }

    [Test]
    public async Task PickerStartsInTheFolderWhoseMenuIsUsed()
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/a.txt");

        await PickAndRunAsync(app, docs, FileOperation.Encrypt, FolderScope.Folder);

        Assert.That(app.Access.FolderPickerStarts, Is.EqualTo(["docs"]));
    }

    [Test]
    public async Task FolderInOneOfMyFoldersIsUsedWithoutAskingForAccess()
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/sub/a.txt", "text");
        app.Access.PickedFolders.Enqueue(FakeFileAccess.Folder("docs/sub"));
        app.UserInterface.Answers.Enqueue(true);

        await app.FolderFiles.RunAsync(new FolderFilesRequest(docs, FileOperation.Encrypt, FolderScope.Folder));

        Assert.That(
            app.UserInterface.Confirmations,
            Is.EqualTo(
            [
                Confirmation(MobileTexts.DialogTextConfirmEncryptFiles, 1, 4L),
            ]));
        Assert.That(app.Access.Files.Keys.Single(), Does.StartWith("docs/sub/").And.EndWith(".axx"));
        Assert.That(app.FolderIds(), Is.EqualTo(["docs"]));
    }

    [Test]
    public async Task FolderInNoneOfMyFoldersIsAddedFirstWhenAccessIsAllowed()
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("other/a.txt", "text");
        app.Access.PickedFolders.Enqueue(FakeFileAccess.Folder("other"));
        app.UserInterface.Answers.Enqueue(true);
        app.UserInterface.Answers.Enqueue(true);

        await app.FolderFiles.RunAsync(new FolderFilesRequest(docs, FileOperation.Encrypt, FolderScope.Folder));

        Assert.That(
            app.UserInterface.Confirmations,
            Is.EqualTo(
            [
                MobileTexts.DialogTextAllowAccessToFolder,
                Confirmation(MobileTexts.DialogTextConfirmEncryptFiles, 1, 4L),
            ]));
        Assert.That(app.FolderIds(), Is.EqualTo(["other", "docs"]));
        Assert.That(app.Access.Files.Keys.Single(), Does.StartWith("other/").And.EndWith(".axx"));
    }

    [Test]
    public async Task FolderInNoneOfMyFoldersIsNotUsedWhenAccessIsNotAllowed()
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("other/a.txt");
        app.Access.PickedFolders.Enqueue(FakeFileAccess.Folder("other"));
        app.UserInterface.Answers.Enqueue(false);

        await app.FolderFiles.RunAsync(new FolderFilesRequest(docs, FileOperation.Encrypt, FolderScope.Folder));

        Assert.That(
            app.UserInterface.Confirmations,
            Is.EqualTo([MobileTexts.DialogTextAllowAccessToFolder]));
        Assert.That(app.FolderIds(), Is.EqualTo(["docs"]));
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["other/a.txt"]));
    }

    [Test]
    public async Task NothingIsDoneWhenPickingIsCanceled()
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/a.txt");
        app.Access.PickedFolders.Enqueue(WorkFolderResult.Canceled);

        await app.FolderFiles.RunAsync(new FolderFilesRequest(docs, FileOperation.Encrypt, FolderScope.Folder));

        Assert.That(app.UserInterface.Confirmations, Is.Empty);
        Assert.That(app.UserInterface.TransientMessages, Is.Empty);
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["docs/a.txt"]));
    }

    [Test]
    public async Task EachFolderIsListedOnceForTheWholeOperation()
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/a.txt");
        app.AddFile("docs/b.txt");
        app.AddFile("docs/sub/c.txt");
        app.UserInterface.Answers.Enqueue(true);

        await PickAndRunAsync(app, docs, FileOperation.Encrypt, FolderScope.FolderAndSubfolders);

        Assert.That(app.Access.FolderListingCount, Is.EqualTo(1));
        Assert.That(app.Access.FolderLookupCount, Is.Zero);
        Assert.That(app.Access.Files.Keys, Has.Count.EqualTo(3).And.All.EndsWith(".axx"));
    }

    [Test]
    public async Task ExistingResultInTheFolderIsOnlyOverwrittenWhenTheUserConfirms()
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        string encryptedName = "a.txt".ToEncryptedName("");
        app.AddFile("docs/a.txt", "text");
        app.AddFile($"docs/{encryptedName}", "ENC:old.txt\nold");
        app.UserInterface.Answers.Enqueue(true);
        app.UserInterface.Answers.Enqueue(true);

        await PickAndRunAsync(app, docs, FileOperation.Encrypt, FolderScope.Folder);

        Assert.That(
            app.UserInterface.Confirmations[1],
            Is.EqualTo(string.Format(MobileTexts.DialogTextConfirmOverwrite, encryptedName)));
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo([$"docs/{encryptedName}"]));
    }

    [Test]
    public async Task FileWhoseResultWouldReplaceAnotherIsPassedOverWhenTheUserDeclines()
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        string encryptedName = "a.txt".ToEncryptedName("");
        app.AddFile("docs/a.txt", "text");
        app.AddFile("docs/b.txt", "text");
        app.AddFile($"docs/{encryptedName}", "ENC:old.txt\nold");
        app.UserInterface.Answers.Enqueue(true);
        app.UserInterface.Answers.Enqueue(false);

        await PickAndRunAsync(app, docs, FileOperation.Encrypt, FolderScope.Folder);

        Assert.That(
            app.UserInterface.Confirmations[1],
            Is.EqualTo(string.Format(MobileTexts.DialogTextConfirmOverwrite, encryptedName)));
        Assert.That(
            app.Access.Files.Keys,
            Is.EquivalentTo(["docs/a.txt", $"docs/{encryptedName}", $"docs/{"b.txt".ToEncryptedName("")}"]));
        Assert.That(app.Access.Files[$"docs/{encryptedName}"], Is.EqualTo("ENC:old.txt\nold"u8.ToArray()));
        Assert.That(
            app.UserInterface.TransientMessages,
            Is.EqualTo([$"{MobileTexts.DialogTextDone} {string.Format(MobileTexts.DialogTextFilesEncryptedFormat, 1.ToCountText())}"]));
    }

    [Test]
    public async Task FileDecryptedToTheNameOfOneDecryptedBeforeIsPassedOverWhenTheUserDeclines()
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/x.axx", "ENC:a.txt\nfirst");
        app.AddFile("docs/y.axx", "ENC:a.txt\nsecond");
        app.AddFile("docs/z.axx", "ENC:z.txt\nthird");
        app.UserInterface.Answers.Enqueue(true);
        app.UserInterface.Answers.Enqueue(false);

        await PickAndRunAsync(app, docs, FileOperation.Decrypt, FolderScope.Folder);

        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["docs/a.txt", "docs/y.axx", "docs/z.txt"]));
        Assert.That(app.Access.Files["docs/a.txt"], Is.EqualTo("first"u8.ToArray()));
        Assert.That(
            app.UserInterface.TransientMessages,
            Is.EqualTo([$"{MobileTexts.DialogTextDone} {string.Format(MobileTexts.DialogTextFilesDecryptedFormat, 2.ToCountText())}"]));
    }

    /// <summary>
    /// What a file is saved as is known for the files that follow, without listing the folder again.
    /// </summary>
    [Test]
    public async Task FileDecryptedToTheNameOfOneDecryptedBeforeIsOnlyOverwrittenWhenTheUserConfirms()
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/x.axx", "ENC:a.txt\nfirst");
        app.AddFile("docs/y.axx", "ENC:a.txt\nsecond");
        app.UserInterface.Answers.Enqueue(true);
        app.UserInterface.Answers.Enqueue(true);

        await PickAndRunAsync(app, docs, FileOperation.Decrypt, FolderScope.Folder);

        Assert.That(
            app.UserInterface.Confirmations[1],
            Is.EqualTo(string.Format(MobileTexts.DialogTextConfirmOverwrite, "a.txt")));
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["docs/a.txt"]));
        Assert.That(app.Access.Files["docs/a.txt"], Is.EqualTo("second"u8.ToArray()));
        Assert.That(app.Access.FolderListingCount, Is.EqualTo(1));
    }

    // The question, followed by the count and size of the files.
    private static string Confirmation(string question, int count, long size) =>
        $"{question} " +
        string.Format(MobileTexts.DialogTextFileCountAndSizeFormat, count.ToCountText(), size.ToSizeText());

    // The user picks the folder the picker starts in.
    // As the user does once the page for passwords is shown, which is left once the password has decrypted the file.
    private static async Task EnterPasswordWhenShownAsync(TestApp app, SemaphoreSlim shown, string password)
    {
        Assert.That(await shown.WaitAsync(TimeSpan.FromSeconds(5)), Is.True);
        EnterPasswordPageModel passwordPage = app.CreateEnterPasswordPage();
        passwordPage.Initialize(new DecryptionPasswordsPayload(DecryptionPasswordsIntent.Add, "x.axx"));
        passwordPage.Password = password;
        await passwordPage.SubmitCommand.ExecuteAsync(null);
        passwordPage.CancelWorkFolderRequest();
    }

    private static Task PickAndRunAsync(
        TestApp app,
        WorkFolder folder,
        FileOperation operation,
        FolderScope scope)
    {
        app.Access.PickedFolders.Enqueue(WorkFolderResult.Valid(folder));
        return app.FolderFiles.RunAsync(new FolderFilesRequest(folder, operation, scope));
    }
}
