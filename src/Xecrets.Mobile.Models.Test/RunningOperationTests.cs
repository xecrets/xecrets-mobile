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
using Xecrets.Texts;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class RunningOperationTests
{
    [Test]
    public async Task FolderRunCanceledWhileAFileIsEncryptedLeavesItAndTellsTheFilesDone()
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/a.txt");
        app.AddFile("docs/b.txt");
        app.UserInterface.Answers.Enqueue(true);
        app.Core.Writing = CancelOnCall(app, 2);

        await RunFolderFilesAsync(app, docs, FileOperation.Encrypt);

        Assert.That(app.Access.Files.Keys, Has.Count.EqualTo(2));
        Assert.That(app.Access.Files.Keys.Count(name => name.EndsWith(".axx", StringComparison.Ordinal)), Is.EqualTo(1));
        Assert.That(
            app.UserInterface.TransientMessages,
            Is.EqualTo([$"{MobileTexts.DialogTextCanceled} {string.Format(MobileTexts.DialogTextFilesEncryptedFormat, 1.ToCountText())}"]));
        Assert.That(app.Transient.RemainingFiles(), Is.Empty);
        Assert.That(app.SavesInProgress.Current, Is.Null);
    }

    [Test]
    public async Task FolderRunCanceledWhileAResultIsSavedFinishesTheFileAndStopsBeforeTheNext()
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/a.txt");
        app.AddFile("docs/b.txt");
        app.UserInterface.Answers.Enqueue(true);
        app.SavesInProgress.Saving = CancelOnCall(app, 1);

        await RunFolderFilesAsync(app, docs, FileOperation.Encrypt);

        SaveInProgress save = app.SavesInProgress.Written.Single();
        string left = save.SourceId == "docs/a.txt" ? "docs/b.txt" : "docs/a.txt";
        string encryptedName = save.SourceId["docs/".Length..].ToEncryptedName("");
        Assert.That(app.Access.Files.Keys, Is.EquivalentTo([$"docs/{encryptedName}", left]));
        Assert.That(
            save,
            Is.EqualTo(new SaveInProgress
            {
                UserId = "me",
                FolderId = "docs",
                Name = encryptedName,
                SourceId = save.SourceId,
            }));
        Assert.That(
            app.UserInterface.TransientMessages,
            Is.EqualTo([$"{MobileTexts.DialogTextCanceled} {string.Format(MobileTexts.DialogTextFilesEncryptedFormat, 1.ToCountText())}"]));
        Assert.That(app.SavesInProgress.Current, Is.Null);
    }

    [Test]
    public async Task FolderRunGoesOnWhenTheUserDoesNotCancel()
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/a.txt");
        app.AddFile("docs/b.txt");
        app.UserInterface.Answers.Enqueue(true);
        app.Core.Writing = () => _ = app.Running.ConfirmCancelAsync();

        await RunFolderFilesAsync(app, docs, FileOperation.Encrypt);

        Assert.That(
            app.Access.Files.Keys,
            Is.EquivalentTo([$"docs/{"a.txt".ToEncryptedName("")}", $"docs/{"b.txt".ToEncryptedName("")}"]));
        Assert.That(
            app.UserInterface.Confirmations.Skip(1),
            Is.EqualTo([MobileTexts.DialogTextConfirmCancelOperation, MobileTexts.DialogTextConfirmCancelOperation]));
    }

    [Test]
    public async Task FileCanceledWhileItIsDecryptedIsLeftAsItWas()
    {
        using TestApp app = new();
        app.AddFolder("docs");
        app.AddFile("docs/x.axx", "ENC:x.txt\ntext");
        app.Core.Writing = CancelOnCall(app, 1);
        FileOperationOutcome outcome;

        using (app.Running.Begin())
        {
            outcome = await app.Operations.TransformAsync(
                new FileReference("docs/x.axx", "x.axx"),
                WorkFolderOperation.Decrypt,
                FlowOrigin.MyFolders);
        }

        Assert.That(outcome.Status, Is.EqualTo(FileOperationStatus.Canceled));
        Assert.That(app.Access.Files.Keys, Is.EqualTo(["docs/x.axx"]));
        Assert.That(app.UserInterface.TransientMessages, Is.EqualTo([MobileTexts.DialogTextOperationNotCompleted]));
        Assert.That(app.Transient.RemainingFiles(), Is.Empty);
    }

    [Test]
    public async Task FolderRunCanceledWhileAFileIsFoundToNeedAPasswordAsksForNone()
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/x.axx", "ENC:x.txt\ntext");
        app.Core.Password = "other";
        app.UserInterface.Answers.Enqueue(true);
        TaskCompletionSource<bool> answer = AskToCancelWhenOpened(app);

        Task run = RunFolderFilesAsync(app, docs, FileOperation.Decrypt);
        answer.SetResult(true);
        // Bounded, since a run that asks for a password waits for one, which no one enters here.
        await run.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.That(app.UserInterface.Destinations, Is.Empty);
        Assert.That(app.OperationService.HasPendingPasswordRequest, Is.False);
        Assert.That(app.Access.Files.Keys, Is.EqualTo(["docs/x.axx"]));
        Assert.That(
            app.UserInterface.TransientMessages,
            Is.EqualTo([$"{MobileTexts.DialogTextCanceled} {string.Format(MobileTexts.DialogTextFilesDecryptedFormat, 0.ToCountText())}"]));
    }

    [Test]
    public async Task FolderRunNotCanceledWhileAFileIsFoundToNeedAPasswordAsksForIt()
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/x.axx", "ENC:x.txt\ntext");
        app.Core.Password = "other";
        app.UserInterface.Answers.Enqueue(true);
        TaskCompletionSource<bool> answer = AskToCancelWhenOpened(app);

        Task run = RunFolderFilesAsync(app, docs, FileOperation.Decrypt);
        answer.SetResult(false);
        EnterPasswordPageModel passwordPage = app.CreateEnterPasswordPage();
        passwordPage.Initialize(DecryptionPasswordsIntent.Add);
        passwordPage.Password = "other";
        await passwordPage.SubmitCommand.ExecuteAsync(null);
        passwordPage.CancelWorkFolderRequest();
        await run;

        Assert.That(app.UserInterface.Destinations, Is.EqualTo([AppDestination.EnterPassword]));
        Assert.That(app.Access.Files.Keys, Is.EqualTo(["docs/x.txt"]));
        Assert.That(
            app.UserInterface.TransientMessages,
            Is.EqualTo([$"{MobileTexts.DialogTextDone} {string.Format(MobileTexts.DialogTextFilesDecryptedFormat, 1.ToCountText())}"]));
    }

    [Test]
    public async Task PreviewCanceledBeforeThePasswordIsAskedForLeavesNoRequest()
    {
        using TestApp app = new();
        app.AddFolder("docs");
        app.AddFile("docs/x.axx", "ENC:x.txt\ntext");
        app.Preview.NeedsPassword = true;

        using (app.Running.Begin())
        {
            app.UserInterface.Answers.Enqueue(true);
            await app.Running.ConfirmCancelAsync();

            Assert.That(
                async () => await app.Operations.PreviewAsync(new FileReference("docs/x.axx", "x.axx"), FlowOrigin.RecentFiles),
                Throws.InstanceOf<OperationCanceledException>());
        }

        Assert.That(app.UserInterface.Destinations, Is.Empty);
        Assert.That(app.Preview.HasPendingPasswordRequest, Is.False);
    }

    [Test]
    public void OperationRunsWhereItBeganUntilItEnds()
    {
        ScriptedUserInterface userInterface = new() { CurrentLocation = "//home/work-folders" };
        RunningOperation running = new(userInterface);
        List<bool> changes = [];
        running.IsRunningChanged += (_, _) => changes.Add(running.IsRunning);

        IDisposable scope = running.Begin();
        bool isRunningWhereBegun = running.IsRunningAt("//home/work-folders");
        bool isRunningElsewhere = running.IsRunningAt("//home/work-folders/enter-password");
        scope.Dispose();

        Assert.That(isRunningWhereBegun, Is.True);
        Assert.That(isRunningElsewhere, Is.False);
        Assert.That(running.IsRunningAt("//home/work-folders"), Is.False);
        Assert.That(changes, Is.EqualTo([true, false]));
    }

    [Test]
    public void OperationIsCancelableUnlessBegunAsNot()
    {
        RunningOperation running = new(new ScriptedUserInterface());

        bool isCancelable;
        using (running.Begin())
        {
            isCancelable = running.IsCancelable;
        }

        bool isNotCancelable;
        bool isRunning;
        using (running.Begin(cancelable: false))
        {
            isNotCancelable = !running.IsCancelable;
            isRunning = running.IsRunning;
        }

        Assert.That(isCancelable, Is.True);
        Assert.That(isNotCancelable, Is.True);
        Assert.That(isRunning, Is.True);
    }

    // When done for the given time, the user is asked to cancel, and confirms it.
    private static Action CancelOnCall(TestApp app, int call)
    {
        int calls = 0;
        return () =>
        {
            if (++calls == call)
            {
                app.UserInterface.Answers.Enqueue(true);
                _ = app.Running.ConfirmCancelAsync();
            }
        };
    }

    // When the first file is opened, the user is asked to cancel, and answers once the test sets the answer.
    private static TaskCompletionSource<bool> AskToCancelWhenOpened(TestApp app)
    {
        TaskCompletionSource<bool> answer = new();
        app.Core.Opening = () =>
        {
            app.Core.Opening = null;
            app.UserInterface.HeldAnswer = answer;
            _ = app.Running.ConfirmCancelAsync();
        };
        return answer;
    }

    // As the menu of a folder runs it, the user picking the folder the picker starts in.
    private static Task RunFolderFilesAsync(TestApp app, WorkFolder folder, FileOperation operation)
    {
        app.Access.PickedFolders.Enqueue(WorkFolderResult.Valid(folder));
        return app.CreateWorkFoldersPage().RunFolderFilesCommand.ExecuteAsync(
            new FolderFilesRequest(folder, operation, FolderScope.Folder));
    }
}
