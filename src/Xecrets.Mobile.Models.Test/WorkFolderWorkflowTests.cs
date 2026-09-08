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
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.PageModels;
using Xecrets.Mobile.Models.Services;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class WorkFolderWorkflowTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task UnknownFolderReopensPickerAfterAddDialogCloses(bool cancelAdd)
    {
        TestWorkFolderService folders = new() { CancelAdd = cancelAdd };
        WorkFolderFile first = CreateFile("first.txt", "unknown", false);
        WorkFolderFile second = CreateFile("second.txt", "known", true);
        folders.Files.Enqueue(first);
        folders.Files.Enqueue(second);
        TestUserInterfaceService userInterface = new() { Confirmation = true };
        WorkFolderWorkflow workflow = new(folders, null!, null!, userInterface);

        WorkFolderFile? selected = await workflow.PickFileAsync(FilePickerKind.Any);

        Assert.That(selected, Is.SameAs(second));
        Assert.That(folders.PickedFolders, Is.EqualTo(new WorkFolder?[] { null, null }));
        Assert.That(folders.AddLocations, Is.EqualTo(new[] { "unknown" }));
        Assert.That(userInterface.ConfirmationCount, Is.EqualTo(1));
        Assert.That(userInterface.Destinations, Is.Empty);
    }

    [Test]
    public async Task DecliningUnknownFolderEndsSelectionWithoutAddingOrProcessing()
    {
        TestWorkFolderService folders = new();
        folders.Files.Enqueue(CreateFile("input.txt", "unknown", false));
        TestUserInterfaceService userInterface = new();
        WorkFolderWorkflow workflow = new(folders, null!, null!, userInterface);

        Assert.That(await workflow.PickFileAsync(FilePickerKind.Any), Is.Null);
        Assert.That(folders.AddLocations, Is.Empty);
        Assert.That(folders.PickedFolders, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task FileUnderConfiguredFolderDoesNotAskForAnotherGrant()
    {
        TestWorkFolderService folders = new();
        WorkFolderFile file = CreateFile("input.txt", "known/child", true);
        folders.Files.Enqueue(file);
        TestUserInterfaceService userInterface = new();
        WorkFolderWorkflow workflow = new(folders, null!, null!, userInterface);

        Assert.That(await workflow.PickFileAsync(FilePickerKind.Any), Is.SameAs(file));
        Assert.That(userInterface.ConfirmationCount, Is.Zero);
        Assert.That(folders.AddLocations, Is.Empty);
        Assert.That(folders.Folders[0].Id, Is.EqualTo("known/child"));
    }

    [TestCase("input.txt", WorkFolderOperation.Encrypt)]
    [TestCase("input.axx", WorkFolderOperation.Decrypt)]
    public async Task MyFoldersSelectsOperationFromFileName(string name, WorkFolderOperation expected)
    {
        TestWorkFolderService folders = new();
        folders.Files.Enqueue(CreateFile(name, "known", true));
        TestOperationService operations = new();
        TestUserInterfaceService userInterface = new();
        FlowContext flow = new();
        WorkFolderWorkflow workflow = new(folders, operations, flow, userInterface);
        WorkFoldersPageModel page = new(folders, workflow, userInterface);

        await page.OpenCommand.ExecuteAsync(folders.Folders[0]);

        Assert.That(operations.Operation, Is.EqualTo(expected));
        Assert.That(flow.Operation, Is.EqualTo(expected));
        Assert.That(folders.PickerKinds, Is.EqualTo(new[] { FilePickerKind.Any }));
        Assert.That(page.StatusText, Is.Empty);
    }

    [Test]
    public async Task WrongPasswordNavigatesToExistingPasswordPage()
    {
        TestOperationService operations = new() { CanDecrypt = false };
        TestUserInterfaceService userInterface = new();
        WorkFolderWorkflow workflow = new(null!, operations, new FlowContext(), userInterface);

        await workflow.TransformAsync(CreateFile("input.axx", "known", true), WorkFolderOperation.Decrypt);

        Assert.That(userInterface.Destinations, Is.EqualTo(new[] { AppDestination.EnterPassword }));
    }

    private static WorkFolderFile CreateFile(string name, string location, bool known) =>
        new(name, location, location, "grant", known,
            () => throw new NotSupportedException(),
            _ => throw new NotSupportedException(),
            (_, _, _) => throw new NotSupportedException(),
            () => throw new NotSupportedException(),
            null!);

    private sealed class TestWorkFolderService : IWorkFolderService
    {
        public List<WorkFolder> Folders { get; private set; } = [new("known", "Known", "grant")];
        public Queue<WorkFolderFile?> Files { get; } = new();
        public List<WorkFolder?> PickedFolders { get; } = [];
        public List<FilePickerKind> PickerKinds { get; } = [];
        public List<string?> AddLocations { get; } = [];
        public bool CancelAdd { get; init; }
        public Task<IReadOnlyList<WorkFolder>> GetFoldersAsync() => Task.FromResult<IReadOnlyList<WorkFolder>>(Folders);
        public IReadOnlyList<string> GetPathSegments(WorkFolder folder) => [folder.DisplayName];
        public Task<WorkFolderResult> AddFolderAsync(string? initialLocationId = null)
        {
            AddLocations.Add(initialLocationId);
            if (CancelAdd)
            {
                return Task.FromResult(WorkFolderResult.Canceled);
            }

            WorkFolder folder = new(initialLocationId!, "Added", "grant");
            Folders.Add(folder);
            return Task.FromResult(WorkFolderResult.Valid(folder));
        }
        public Task<WorkFolder> AddDiscoveredFolderAsync(WorkFolderFile file)
        {
            WorkFolder folder = new(file.LocationId, file.LocationDisplayName, file.LocationGrantId);
            Folders.Add(folder);
            return Task.FromResult(folder);
        }
        public Task RemoveFolderAsync(WorkFolder folder) => throw new NotSupportedException();
        public Task RenameFolderAsync(WorkFolder folder, string displayName) => throw new NotSupportedException();
        public Task SaveFoldersAsync(IReadOnlyList<WorkFolder> folders)
        {
            Folders = [.. folders];
            return Task.CompletedTask;
        }
        public Task<WorkFolderFile?> PickFileAsync(WorkFolder? folder, FilePickerKind pickerKind)
        {
            PickedFolders.Add(folder);
            PickerKinds.Add(pickerKind);
            return Task.FromResult(Files.Dequeue());
        }
    }

    private sealed class TestOperationService : IWorkFolderOperationService
    {
        public WorkFolderOperation? Operation { get; private set; }
        public bool CanDecrypt { get; init; } = true;
        public bool HasPendingPasswordRequest => !CanDecrypt;
        public Task EncryptAsync(WorkFolderFile file)
        {
            Operation = WorkFolderOperation.Encrypt;
            return Task.CompletedTask;
        }
        public Task<bool> DecryptWithKnownPasswordsAsync(WorkFolderFile file)
        {
            Operation = WorkFolderOperation.Decrypt;
            return Task.FromResult(CanDecrypt);
        }
        public Task<bool> DecryptWithPasswordAsync(string password) => throw new NotSupportedException();
        public void CancelPasswordRequest() => throw new NotSupportedException();
    }

    private sealed class TestUserInterfaceService : IUserInterfaceService
    {
        public bool Confirmation { get; init; }
        public int ConfirmationCount { get; private set; }
        public List<AppDestination> Destinations { get; } = [];
        public bool IsShellAvailable => true;
        public bool CanProcessIncomingFiles => true;
        public bool CanReceiveIncomingFiles => true;
        public Task InvokeOnMainThreadAsync(Func<Task> action) => action();
        public Task DisplayMessageAsync(string message) => Task.CompletedTask;
        public Task<bool> DisplayConfirmationAsync(string message)
        {
            ConfirmationCount++;
            return Task.FromResult(Confirmation);
        }
        public Task<string?> DisplayPromptAsync(string message, string initialValue) => throw new NotSupportedException();
        public Task DisplayTransientMessageAsync(string message) => Task.CompletedTask;
        public Task NavigateToAsync(AppDestination destination)
        {
            Destinations.Add(destination);
            return Task.CompletedTask;
        }
        public Task NavigateToAsync(AppDestination destination, object parameter) => NavigateToAsync(destination);
        public Task GoBackAsync() => throw new NotSupportedException();
        public Task OpenBrowserAsync(string url) => throw new NotSupportedException();
    }
}
