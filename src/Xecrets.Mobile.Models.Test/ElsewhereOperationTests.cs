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

/// <summary>
/// A file in none of My folders, elsewhere, is replaced by a result saved where the user chooses, whether it was
/// picked starting in one of My folders or by browsing elsewhere.
/// </summary>
[TestFixture]
public sealed class ElsewhereOperationTests
{
    private static readonly string _encryptedName = "a.txt".ToEncryptedName(string.Empty);

    [Test]
    public async Task FileInMyFoldersPickedByBrowsingIsChangedWhereItIs()
    {
        TestApp app = new();
        app.AddFolder("docs");
        app.AddFile("docs/a.txt");
        app.Access.PickedFiles.Enqueue("docs/a.txt");

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Encrypt, null);

        Assert.That(app.Access.FilePickerStarts, Is.EqualTo([string.Empty]));
        Assert.That(app.Access.CreateFileStarts, Is.Empty);
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo([$"docs/{_encryptedName}"]));
        Assert.That(app.RecentIds(), Is.EqualTo([$"docs/{_encryptedName}"]));
    }

    [Test]
    public async Task FileElsewherePickedInAFolderIsEncryptedToTheFileSavedWithoutAskingForItsFolder()
    {
        TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("other/a.txt");
        app.Access.PickedFiles.Enqueue("other/a.txt");
        app.Access.PickedFiles.Enqueue($"saved/{_encryptedName}");

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Encrypt, docs);

        Assert.That(app.UserInterface.Confirmations, Is.Empty);
        Assert.That(app.Access.CreateFileStarts, Is.EqualTo(["other"]));
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo([$"saved/{_encryptedName}"]));
        Assert.That(app.Access.Files[$"saved/{_encryptedName}"], Is.EqualTo("ENC:a.txt\ntext"u8.ToArray()));
        Assert.That(app.Wiper.Wiped, Is.EqualTo(["other/a.txt"]));
        Assert.That(app.UserInterface.TransientMessages, Is.EqualTo([MobileTexts.DialogTextFileEncrypted]));
    }

    [Test]
    public async Task ResultElsewhereIsNotListedAndNoAccessIsKept()
    {
        TestApp app = new();
        app.AddFile("other/a.txt");
        app.AddRecent("other/a.txt", RecentFileOperation.View);
        app.Access.PickedFiles.Enqueue("other/a.txt");
        app.Access.PickedFiles.Enqueue($"other/{_encryptedName}");

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Auto, null);

        Assert.That(app.Access.Files.Keys, Is.EquivalentTo([$"other/{_encryptedName}"]));
        Assert.That(app.RecentIds(), Is.Empty);
        Assert.That(app.Access.FileGrants, Is.Empty);
    }

    [Test]
    public async Task FileElsewhereIsDecryptedWithAKnownPassword()
    {
        TestApp app = new();
        app.AddFile("other/x.axx", "ENC:a.txt\ntext");
        app.Access.PickedFiles.Enqueue("other/x.axx");
        app.Access.PickedFiles.Enqueue("other/a.txt");

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Decrypt, null);

        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["other/a.txt"]));
        Assert.That(app.Access.Files["other/a.txt"], Is.EqualTo("text"u8.ToArray()));
        Assert.That(app.UserInterface.TransientMessages, Is.EqualTo([MobileTexts.DialogTextFileDecrypted]));
    }

    [Test]
    public async Task FileElsewhereIsDecryptedWithThePasswordAskedFor()
    {
        TestApp app = new();
        app.AddFile("other/x.axx", "ENC:a.txt\ntext");
        app.Access.PickedFiles.Enqueue("other/x.axx");
        app.Core.Password = "other";

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Decrypt, null);

        Assert.That(app.UserInterface.Destinations, Is.EqualTo([AppDestination.EnterPassword]));
        Assert.That(app.Access.CreateFileStarts, Is.Empty);

        app.Access.PickedFiles.Enqueue("other/a.txt");
        FileOperationOutcome wrong = await app.OperationService.DecryptPendingWithPasswordAsync("wrong");
        FileOperationOutcome outcome = await app.OperationService.DecryptPendingWithPasswordAsync("other");

        Assert.That(wrong.Status, Is.EqualTo(FileOperationStatus.AwaitingPassword));
        Assert.That(outcome.Status, Is.EqualTo(FileOperationStatus.Completed));
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["other/a.txt"]));
        Assert.That(app.OperationService.HasPendingPasswordRequest, Is.False);
    }

    [TestCase(WorkFolderIntent.Encrypt, "other/a.txt", "other/b.txt")]
    [TestCase(WorkFolderIntent.Decrypt, "other/x.axx", "other/b.axx")]
    public async Task ResultSavedAsTheWrongFileTypeIsRemovedAndNothingIsDone(
        WorkFolderIntent intent,
        string source,
        string saved)
    {
        TestApp app = new();
        app.AddFile(source, intent == WorkFolderIntent.Decrypt ? "ENC:a.txt\ntext" : "text");
        app.Access.PickedFiles.Enqueue(source);
        app.Access.PickedFiles.Enqueue(saved);

        await app.Operations.PickAndRunAsync(intent, null);

        Assert.That(app.UserInterface.Messages, Is.EqualTo([MobileTexts.DialogTextFileTypeNotAllowed]));
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo([source]));
        Assert.That(app.Wiper.Wiped, Is.Empty);
    }

    [Test]
    public async Task CancelingTheSaveLeavesTheFileAsItIs()
    {
        TestApp app = new();
        app.AddFile("other/a.txt");
        app.Access.PickedFiles.Enqueue("other/a.txt");
        app.Access.PickedFiles.Enqueue(null);

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Encrypt, null);

        Assert.That(app.UserInterface.TransientMessages, Is.EqualTo([MobileTexts.DialogTextOperationNotCompleted]));
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["other/a.txt"]));
        Assert.That(app.Wiper.Wiped, Is.Empty);
    }

    /// <summary>
    /// Storage such as Dropbox gives the file another name when the one chosen is taken, which the user confirms.
    /// </summary>
    [Test]
    public async Task ResultSavedUnderAChangedNameIsSavedWhenConfirmed()
    {
        TestApp app = new();
        string changedName = $"{Path.GetFileNameWithoutExtension(_encryptedName)}_123{Path.GetExtension(_encryptedName)}";
        app.AddFile("other/a.txt");
        app.Access.PickedFiles.Enqueue("other/a.txt");
        app.Access.PickedFiles.Enqueue($"saved/{changedName}");
        app.UserInterface.Answers.Enqueue(true);

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Encrypt, null);

        Assert.That(
            app.UserInterface.Confirmations,
            Is.EqualTo([string.Format(MobileTexts.DialogTextConfirmSaveAs, changedName)]));
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo([$"saved/{changedName}"]));
        Assert.That(app.Wiper.Wiped, Is.EqualTo(["other/a.txt"]));
        Assert.That(app.UserInterface.TransientMessages, Is.EqualTo([MobileTexts.DialogTextFileEncrypted]));
    }

    [Test]
    public async Task ResultSavedUnderAChangedNameIsRemovedAndNothingIsDoneWhenNotConfirmed()
    {
        TestApp app = new();
        string changedName = $"{Path.GetFileNameWithoutExtension(_encryptedName)}_123{Path.GetExtension(_encryptedName)}";
        app.AddFile("other/a.txt");
        app.Access.PickedFiles.Enqueue("other/a.txt");
        app.Access.PickedFiles.Enqueue($"saved/{changedName}");

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Encrypt, null);

        Assert.That(
            app.UserInterface.Confirmations,
            Is.EqualTo([string.Format(MobileTexts.DialogTextConfirmSaveAs, changedName)]));
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["other/a.txt"]));
        Assert.That(app.Wiper.Wiped, Is.Empty);
        Assert.That(app.UserInterface.TransientMessages, Is.EqualTo([MobileTexts.DialogTextOperationNotCompleted]));
    }

    [Test]
    public async Task ResultNotConfirmedThatCannotBeDeletedIsToldAbout()
    {
        TestApp app = new();
        string changedName = $"{Path.GetFileNameWithoutExtension(_encryptedName)}_123{Path.GetExtension(_encryptedName)}";
        app.AddFile("other/a.txt");
        app.Access.PickedFiles.Enqueue("other/a.txt");
        app.Access.PickedFiles.Enqueue($"saved/{changedName}");
        app.Access.FailingDeletes.Add($"saved/{changedName}");

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Encrypt, null);

        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["other/a.txt", $"saved/{changedName}"]));
        Assert.That(
            app.UserInterface.Messages,
            Is.EqualTo([string.Format(MobileTexts.DialogTextCouldNotDeleteFile, changedName)]));
        Assert.That(app.UserInterface.TransientMessages, Is.EqualTo([MobileTexts.DialogTextOperationNotCompleted]));
        Assert.That(app.CrashLog.Swallowed, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ResultSavedUnderAnotherNameIsSavedWithoutAsking()
    {
        TestApp app = new();
        app.AddFile("other/a.txt");
        app.Access.PickedFiles.Enqueue("other/a.txt");
        app.Access.PickedFiles.Enqueue("saved/other.axx");

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Encrypt, null);

        Assert.That(app.UserInterface.Confirmations, Is.Empty);
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["saved/other.axx"]));
    }

    [Test]
    public async Task DecryptedResultSavedUnderAChangedNameIsSavedWhenConfirmed()
    {
        TestApp app = new();
        app.AddFile("other/x.axx", "ENC:a.txt\ntext");
        app.Access.PickedFiles.Enqueue("other/x.axx");
        app.Access.PickedFiles.Enqueue("other/a (1).txt");
        app.UserInterface.Answers.Enqueue(true);

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Decrypt, null);

        Assert.That(
            app.UserInterface.Confirmations,
            Is.EqualTo([string.Format(MobileTexts.DialogTextConfirmSaveAs, "a (1).txt")]));
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["other/a (1).txt"]));
        Assert.That(app.Access.Files["other/a (1).txt"], Is.EqualTo("text"u8.ToArray()));
    }

    [Test]
    public async Task FileElsewhereThatCannotBeWipedIsDeniedBeforeAskingWhereToSave()
    {
        TestApp app = new();
        app.AddFile("other/a.txt");
        app.Wiper.Denied.Add("other/a.txt");
        app.Access.PickedFiles.Enqueue("other/a.txt");

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Encrypt, null);

        Assert.That(app.UserInterface.Messages, Is.EqualTo([MobileTexts.DialogTextAccessDenied]));
        Assert.That(app.Access.CreateFileStarts, Is.Empty);
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["other/a.txt"]));
    }

    /// <summary>
    /// Storage such as Dropbox cannot yet tell the rights to a file it has just created, so they are not checked, and
    /// the result is written anyway.
    /// </summary>
    [Test]
    public async Task ResultIsWrittenAlsoWhenTheRightsToItAreNotTold()
    {
        TestApp app = new();
        app.AddFile("other/a.txt");
        app.Wiper.Denied.Add($"saved/{_encryptedName}");
        app.Access.PickedFiles.Enqueue("other/a.txt");
        app.Access.PickedFiles.Enqueue($"saved/{_encryptedName}");

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Encrypt, null);

        Assert.That(app.UserInterface.Messages, Is.Empty);
        Assert.That(app.UserInterface.TransientMessages, Is.EqualTo([MobileTexts.DialogTextFileEncrypted]));
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo([$"saved/{_encryptedName}"]));
    }

    [Test]
    public async Task FailureToWriteTheResultRemovesItAndLeavesTheFileAsItIs()
    {
        TestApp app = new();
        app.AddFile("other/a.txt");
        app.Access.FailingWrites.Add($"saved/{_encryptedName}");
        app.Access.PickedFiles.Enqueue("other/a.txt");
        app.Access.PickedFiles.Enqueue($"saved/{_encryptedName}");

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Encrypt, null);

        Assert.That(app.UserInterface.Messages, Is.EqualTo([MobileTexts.DialogTextAccessDenied]));
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["other/a.txt"]));
        Assert.That(app.Wiper.Wiped, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ExistingFileSavedAsTheWrongFileTypeIsKept(bool isLengthUnknown)
    {
        TestApp app = new();
        app.AddFile("other/a.txt");
        app.AddFile("saved/b.txt", "existing");
        if (isLengthUnknown)
        {
            app.Access.UnknownLengths.Add("saved/b.txt");
        }

        app.Access.PickedFiles.Enqueue("other/a.txt");
        app.Access.PickedFiles.Enqueue("saved/b.txt");

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Encrypt, null);

        Assert.That(app.UserInterface.Messages, Is.EqualTo([MobileTexts.DialogTextFileTypeNotAllowed]));
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["other/a.txt", "saved/b.txt"]));
        Assert.That(app.Access.Files["saved/b.txt"], Is.EqualTo("existing"u8.ToArray()));
    }

    [Test]
    public async Task ExistingFileSavedWhereItCannotBeWrittenIsKept()
    {
        TestApp app = new();
        app.AddFile("other/a.txt");
        app.AddFile($"saved/{_encryptedName}", "existing");
        app.Access.FailingOpens.Add($"saved/{_encryptedName}");
        app.Access.PickedFiles.Enqueue("other/a.txt");
        app.Access.PickedFiles.Enqueue($"saved/{_encryptedName}");

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Encrypt, null);

        Assert.That(app.UserInterface.Messages, Is.EqualTo([MobileTexts.DialogTextAccessDenied]));
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["other/a.txt", $"saved/{_encryptedName}"]));
    }

    [Test]
    public async Task ResultThatCannotBeOpenedIsKeptAndTheFileLeftAsItIs()
    {
        TestApp app = new();
        app.AddFile("other/a.txt");
        app.AddFile($"saved/{_encryptedName}", "existing");
        app.Access.FailingOpens.Add($"saved/{_encryptedName}");
        app.Access.PickedFiles.Enqueue("other/a.txt");
        app.Access.PickedFiles.Enqueue($"saved/{_encryptedName}");

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Encrypt, null);

        Assert.That(app.UserInterface.Messages, Is.EqualTo([MobileTexts.DialogTextAccessDenied]));
        Assert.That(app.Access.Files[$"saved/{_encryptedName}"], Is.EqualTo("existing"u8.ToArray()));
        Assert.That(app.Wiper.Wiped, Is.Empty);
    }

    [Test]
    public async Task ExistingLongerFileReplacedByTheResultHasOnlyTheResult()
    {
        TestApp app = new();
        app.AddFile("other/a.txt");
        app.AddFile($"saved/{_encryptedName}", new string('x', 100));
        app.Access.PickedFiles.Enqueue("other/a.txt");
        app.Access.PickedFiles.Enqueue($"saved/{_encryptedName}");

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Encrypt, null);

        Assert.That(app.Access.Files.Keys, Is.EquivalentTo([$"saved/{_encryptedName}"]));
        Assert.That(app.Access.Files[$"saved/{_encryptedName}"], Is.EqualTo("ENC:a.txt\ntext"u8.ToArray()));
        Assert.That(app.Wiper.Wiped, Is.EqualTo(["other/a.txt"]));
    }

    /// <summary>
    /// Storage may refuse to remove the file once the result is saved, such as Dropbox when offline, which is then told
    /// instead of the operation being done.
    /// </summary>
    [Test]
    public async Task FileThatCannotBeRemovedAfterSavingTheResultIsLeftAndTold()
    {
        TestApp app = new();
        app.AddFile("other/a.txt");
        app.Wiper.Fails = true;
        app.Access.PickedFiles.Enqueue("other/a.txt");
        app.Access.PickedFiles.Enqueue($"saved/{_encryptedName}");

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Encrypt, null);

        Assert.That(app.UserInterface.Messages, Is.Empty);
        Assert.That(app.UserInterface.TransientMessages, Is.EqualTo([MobileTexts.DialogTextOriginalNotRemoved]));
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["other/a.txt", $"saved/{_encryptedName}"]));
    }

    [Test]
    public async Task FileElsewhereIsWipedWithoutSavingAnything()
    {
        TestApp app = new();
        app.AddFile("other/a.txt");
        app.Access.PickedFiles.Enqueue("other/a.txt");
        app.UserInterface.Answers.Enqueue(true);

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Delete, null);

        Assert.That(app.UserInterface.Confirmations, Is.EqualTo([MobileTexts.MessageTextConfirmWipe]));
        Assert.That(app.Access.CreateFileStarts, Is.Empty);
        Assert.That(app.Access.Files, Is.Empty);
        Assert.That(app.UserInterface.TransientMessages, Is.EqualTo([MobileTexts.DialogTextFileOverwritten]));
    }

    [Test]
    public async Task FileElsewhereThatCannotBeWipedIsDeniedBeforeAskingToConfirm()
    {
        TestApp app = new();
        app.AddFile("other/a.txt");
        app.Wiper.Denied.Add("other/a.txt");
        app.Access.PickedFiles.Enqueue("other/a.txt");

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Delete, null);

        Assert.That(app.UserInterface.Confirmations, Is.Empty);
        Assert.That(app.UserInterface.Messages, Is.EqualTo([MobileTexts.DialogTextAccessDenied]));
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["other/a.txt"]));
    }

    [Test]
    public async Task DescriptionIsShownOnThePageOnlyWhileThereAreNoFolders()
    {
        TestApp app = new();
        WorkFoldersPageModel page = app.CreateWorkFoldersPage();

        await page.LoadCommand.ExecuteAsync(null);
        string withoutFolders = page.NoFoldersText;
        app.Access.PickedFolders.Enqueue(FakeFileAccess.Folder("docs"));
        await page.AddCommand.ExecuteAsync(null);

        Assert.That(withoutFolders, Is.EqualTo(MobileTexts.WorkFolderDescription));
        Assert.That(page.NoFoldersText, Is.Empty);
    }

    [Test]
    public async Task BrowsingFromThePageStartsWhereThePlatformDecides()
    {
        TestApp app = new();
        app.AddFolder("docs");
        app.Access.PickedFiles.Enqueue(null);
        WorkFoldersPageModel page = app.CreateWorkFoldersPage();
        page.Initialize(WorkFolderIntent.Encrypt);

        await page.BrowseCommand.ExecuteAsync(null);

        Assert.That(app.Access.FilePickerStarts, Is.EqualTo([string.Empty]));
    }
}
