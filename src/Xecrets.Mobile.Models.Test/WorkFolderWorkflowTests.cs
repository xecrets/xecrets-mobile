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

using System.Diagnostics.CodeAnalysis;

using NUnit.Framework;

using Xecrets.Common.Models;
using Xecrets.Core.Abstractions;
using Xecrets.Core.Models;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.PageModels;
using Xecrets.Mobile.Models.Services;
using Xecrets.Mobile.Models.Utilities;

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
        WorkFolderWorkflow workflow = new(folders, null!, null!, null!, null!, null!, userInterface);

        WorkFolderFile? selected = await workflow.PickFileAsync(FilePickerKind.Any);

        Assert.That(selected, Is.SameAs(second));
        Assert.That(folders.PickedFolders, Is.EqualTo(new WorkFolder?[] { null, null }));
        Assert.That(folders.AddLocations, Is.EqualTo(["unknown"]));
        Assert.That(userInterface.ConfirmationCount, Is.EqualTo(1));
        Assert.That(userInterface.Destinations, Is.Empty);
    }

    [Test]
    public async Task DecliningUnknownFolderEndsSelectionWithoutAddingOrProcessing()
    {
        TestWorkFolderService folders = new();
        folders.Files.Enqueue(CreateFile("input.txt", "unknown", false));
        TestUserInterfaceService userInterface = new();
        WorkFolderWorkflow workflow = new(folders, null!, null!, null!, null!, null!, userInterface);

        Assert.That(await workflow.PickFileAsync(FilePickerKind.Any), Is.Null);
        Assert.That(folders.AddLocations, Is.Empty);
        Assert.That(folders.PickedFolders, Has.Count.EqualTo(1));
    }

    [TestCase("Downloads", "Downloads")]
    [TestCase("", "…")]
    public async Task AddFolderQuestionNamesTheFolderOfTheFile(string locationDisplayName, string expected)
    {
        TestWorkFolderService folders = new();
        folders.Files.Enqueue(CreateFile("input.txt", "unknown", false) with { LocationDisplayName = locationDisplayName });
        TestUserInterfaceService userInterface = new();
        WorkFolderWorkflow workflow = new(folders, null!, null!, null!, null!, null!, userInterface);

        await workflow.PickFileAsync(FilePickerKind.Any);

        Assert.That(
            userInterface.Confirmations,
            Is.EqualTo([string.Format(MobileTexts.DialogTextAddUnknownWorkFolderFormat, expected)]));
    }

    [TestCase("removed/file.txt", "removed", "removed")]
    [TestCase("file.txt", "…", null)]
    public async Task AddingTheFolderOfAnInaccessibleFileNamesItAndStartsThere(
        string fileId, string expectedName, string? expectedLocation)
    {
        TestWorkFolderService folders = new();
        TestUserInterfaceService userInterface = new() { Confirmation = true };
        WorkFolderWorkflow workflow = new(folders, null!, null!, null!, null!, null!, userInterface);

        bool added = await workflow.AddFolderForFileAsync(fileId);

        Assert.That(added, Is.True);
        Assert.That(
            userInterface.Confirmations,
            Is.EqualTo([string.Format(MobileTexts.DialogTextAddUnknownWorkFolderFormat, expectedName)]));
        Assert.That(folders.AddLocations, Is.EqualTo(new[] { expectedLocation }));
    }

    [Test]
    public async Task DecliningToAddTheFolderOfAnInaccessibleFileAddsNothing()
    {
        TestWorkFolderService folders = new();
        WorkFolderWorkflow workflow = new(folders, null!, null!, null!, null!, null!, new TestUserInterfaceService());

        Assert.That(await workflow.AddFolderForFileAsync("removed/file.txt"), Is.False);
        Assert.That(folders.AddLocations, Is.Empty);
    }

    [Test]
    public async Task FileUnderConfiguredFolderDoesNotAskForAnotherGrant()
    {
        TestWorkFolderService folders = new();
        WorkFolderFile file = CreateFile("input.txt", "known/child", true);
        folders.Files.Enqueue(file);
        TestUserInterfaceService userInterface = new();
        WorkFolderWorkflow workflow = new(folders, null!, null!, null!, null!, null!, userInterface);

        Assert.That(await workflow.PickFileAsync(FilePickerKind.Any), Is.SameAs(file));
        Assert.That(userInterface.ConfirmationCount, Is.Zero);
        Assert.That(folders.AddLocations, Is.Empty);
        Assert.That(folders.Folders[0].Id, Is.EqualTo("known/child"));
    }

    [TestCase("input.txt", false, WorkFolderOperation.Encrypt)]
    [TestCase("input.axx", false, WorkFolderOperation.Encrypt)]
    [TestCase("input.txt", true, WorkFolderOperation.Decrypt)]
    [TestCase("input.axx", true, WorkFolderOperation.Decrypt)]
    public async Task MyFoldersSelectsOperationFromFileContents(string name, bool isEncrypted, WorkFolderOperation expected)
    {
        TestWorkFolderService folders = new();
        WorkFolderFile file = CreateFile(name, "known", true);
        folders.Files.Enqueue(file);
        TestFileOperations fileOperations = new();
        if (isEncrypted)
        {
            fileOperations.Encrypted.Add(file.Id);
        }
        TestOperationService operations = new();
        TestUserInterfaceService userInterface = new();
        FlowContext flow = new();
        FileDetectionCoreServices coreServices = new();
        WorkFolderWorkflow workflow = new(folders, operations, fileOperations, null!, flow, coreServices, userInterface);
        WorkFoldersPageModel page = new(folders, workflow, userInterface);

        await page.OpenCommand.ExecuteAsync(folders.Folders[0]);

        Assert.That(operations.Operation, Is.EqualTo(expected));
        Assert.That(flow.Operation, Is.EqualTo(expected));
        Assert.That(folders.PickerKinds, Is.EqualTo([FilePickerKind.Any]));
        Assert.That(page.StatusText, Is.Empty);
    }

    [TestCase(WorkFolderOperation.Encrypt, false, FilePickerKind.Any, WorkFolderOperation.Encrypt)]
    [TestCase(WorkFolderOperation.Encrypt, true, FilePickerKind.Any, null)]
    [TestCase(WorkFolderOperation.Decrypt, true, FilePickerKind.Encrypted, WorkFolderOperation.Decrypt)]
    [TestCase(WorkFolderOperation.Decrypt, false, FilePickerKind.Encrypted, null)]
    public async Task ExplicitOperationOnlyAppliesToMatchingFiles(
        WorkFolderOperation operation, bool isEncrypted, FilePickerKind expectedKind, WorkFolderOperation? expected)
    {
        TestWorkFolderService folders = new();
        WorkFolderFile file = CreateFile("input.axx", "known", true);
        folders.Files.Enqueue(file);
        TestFileOperations fileOperations = new();
        if (isEncrypted)
        {
            fileOperations.Encrypted.Add(file.Id);
        }
        TestOperationService operations = new();
        WorkFolderWorkflow workflow = new(
            folders, operations, fileOperations, null!, new FlowContext(), new FileDetectionCoreServices(),
            new TestUserInterfaceService());

        await workflow.PickAndTransformAsync(operation);

        Assert.That(operations.Operation, Is.EqualTo(expected));
        Assert.That(folders.PickerKinds, Is.EqualTo([expectedKind]));
        Assert.That(folders.PickedFolders, Is.EqualTo(new WorkFolder?[] { null }));
    }

    [Test]
    public async Task AddingFolderOnlyGrantsAccess()
    {
        TestWorkFolderService folders = new();
        TestUserInterfaceService userInterface = new();
        WorkFolderWorkflow workflow = new(
            folders, new TestOperationService(), new TestFileOperations(), null!, new FlowContext(),
            new FileDetectionCoreServices(), userInterface);
        WorkFoldersPageModel page = new(folders, workflow, userInterface);

        await page.AddCommand.ExecuteAsync(null);

        Assert.That(folders.AddLocations, Has.Count.EqualTo(1));
        Assert.That(folders.PickerKinds, Is.Empty);
        Assert.That(page.StatusText, Is.Empty);
    }

    [Test]
    public async Task WrongPasswordNavigatesToExistingPasswordPage()
    {
        TestOperationService operations = new() { CanDecrypt = false };
        TestUserInterfaceService userInterface = new();
        WorkFolderWorkflow workflow = new(null!, operations, null!, null!, new FlowContext(), null!, userInterface);

        await workflow.TransformAsync(CreateFile("input.axx", "known", true), WorkFolderOperation.Decrypt);

        Assert.That(userInterface.Destinations, Is.EqualTo([AppDestination.EnterPassword]));
    }

    [Test]
    public async Task SaveStartsInTheKnownFolderOfTheSourceAndMovesItToTheTop()
    {
        TestWorkFolderService folders = new();
        folders.Folders.Add(new WorkFolder("other", "Other", "grant"));
        WorkFolderFile saved = CreateFile("copy.axx", "other", true);
        folders.Files.Enqueue(saved);
        TestUserInterfaceService userInterface = new();
        WorkFolderWorkflow workflow = new(folders, null!, null!, null!, null!, null!, userInterface);

        WorkFolderFile? result = await workflow.SaveFileAsync(
            "copy.axx",
            new MemoryStream("content"u8.ToArray()),
            CreateFile("plain.txt", "known", true));

        Assert.That(result, Is.SameAs(saved));
        Assert.That(folders.SavedFolders.Select(folder => folder?.Id), Is.EqualTo(["known"]));
        Assert.That(folders.SavedContents, Is.EqualTo(["content"]));
        Assert.That(folders.Folders[0].Id, Is.EqualTo("other"));
        Assert.That(userInterface.ConfirmationCount, Is.Zero);
    }

    [Test]
    public async Task SaveOfAReceivedFileStartsWithoutAFolder()
    {
        TestWorkFolderService folders = new();
        folders.Files.Enqueue(null);
        WorkFolderWorkflow workflow = new(folders, null!, null!, null!, null!, null!, new TestUserInterfaceService());

        Assert.That(await workflow.SaveFileAsync("copy.axx", new MemoryStream(), null), Is.Null);
        Assert.That(folders.SavedFolders, Is.EqualTo(new WorkFolder?[] { null }));
    }

    [Test]
    public async Task SaveInAFolderWithinAKnownFolderAddsIt()
    {
        TestWorkFolderService folders = new();
        WorkFolderFile saved = CreateFile("copy.axx", "known/child", true);
        folders.Files.Enqueue(saved);
        TestUserInterfaceService userInterface = new();
        WorkFolderWorkflow workflow = new(folders, null!, null!, null!, null!, null!, userInterface);

        Assert.That(await workflow.SaveFileAsync("copy.axx", new MemoryStream(), null), Is.SameAs(saved));
        Assert.That(folders.Folders[0].Id, Is.EqualTo("known/child"));
        Assert.That(userInterface.ConfirmationCount, Is.Zero);
    }

    [Test]
    public async Task UnknownFolderIsAddedAndThePickedFileUsedThroughItWithoutPickingAgain()
    {
        TestWorkFolderService folders = new();
        folders.Files.Enqueue(CreateFile("input.txt", "unknown", false));
        WorkFolderFile reopened = CreateFile("input.txt", "unknown", true);
        folders.Reopened["unknown/input.txt"] = reopened;
        TestUserInterfaceService userInterface = new() { Confirmation = true };
        WorkFolderWorkflow workflow = new(folders, null!, null!, null!, null!, null!, userInterface);

        Assert.That(await workflow.PickFileAsync(FilePickerKind.Any), Is.SameAs(reopened));
        Assert.That(folders.PickedFolders, Has.Count.EqualTo(1));
        Assert.That(folders.AddLocations, Is.EqualTo(["unknown"]));
        Assert.That(folders.Folders[0].Id, Is.EqualTo("unknown"));
    }

    [Test]
    public async Task PickReopenedThroughAParentFolderAddsTheFolderOfTheFile()
    {
        TestWorkFolderService folders = new() { ChosenFolderId = "parent" };
        folders.Files.Enqueue(CreateFile("input.txt", "parent/child", false));
        folders.Reopened["parent/child/input.txt"] = CreateFile("input.txt", "parent/child", true);
        WorkFolderWorkflow workflow = new(
            folders, null!, null!, null!, null!, null!, new TestUserInterfaceService { Confirmation = true });

        WorkFolderFile? result = await workflow.PickFileAsync(FilePickerKind.Any);

        Assert.That(result!.Id, Is.EqualTo("parent/child/input.txt"));
        Assert.That(folders.PickedFolders, Has.Count.EqualTo(1));
        Assert.That(folders.Folders.Select(folder => folder.Id), Is.EqualTo(["parent/child", "parent", "known"]));
    }

    [Test]
    public async Task SaveOutsideTheKnownFoldersReturnsTheFileWithoutAsking()
    {
        TestWorkFolderService folders = new();
        WorkFolderFile saved = CreateFile("copy.axx", "unknown", false);
        folders.Files.Enqueue(saved);
        TestUserInterfaceService userInterface = new() { Confirmation = true };
        WorkFolderWorkflow workflow = new(folders, null!, null!, null!, null!, null!, userInterface);

        WorkFolderFile? result = await workflow.SaveFileAsync("copy.axx", new MemoryStream(), null);

        Assert.That(result, Is.SameAs(saved));
        Assert.That(result!.IsInKnownWorkFolder, Is.False);
        Assert.That(userInterface.ConfirmationCount, Is.Zero);
        Assert.That(folders.AddLocations, Is.Empty);
    }

    [TestCase("unknown")]
    [TestCase("")]
    public async Task CopyPickOutsideTheKnownFoldersUsesTheFileWithoutAsking(string location)
    {
        TestWorkFolderService folders = new();
        WorkFolderFile picked = CreateFile("input.txt", location, false);
        folders.Files.Enqueue(picked);
        TestUserInterfaceService userInterface = new() { Confirmation = true };
        WorkFolderWorkflow workflow = new(folders, null!, null!, null!, null!, null!, userInterface);

        Assert.That(await workflow.PickFileForCopyAsync(FilePickerKind.Any), Is.SameAs(picked));
        Assert.That(userInterface.ConfirmationCount, Is.Zero);
        Assert.That(userInterface.Messages, Is.Empty);
        Assert.That(folders.AddLocations, Is.Empty);
        Assert.That(folders.Folders.Select(folder => folder.Id), Is.EqualTo(["known"]));
    }

    [Test]
    public async Task CopyPickWithinAKnownFolderAddsItsFolder()
    {
        TestWorkFolderService folders = new();
        WorkFolderFile picked = CreateFile("input.txt", "known/child", true);
        folders.Files.Enqueue(picked);
        WorkFolderWorkflow workflow = new(folders, null!, null!, null!, null!, null!, new TestUserInterfaceService());

        Assert.That(await workflow.PickFileForCopyAsync(FilePickerKind.Any), Is.SameAs(picked));
        Assert.That(folders.Folders[0].Id, Is.EqualTo("known/child"));
    }

    [Test]
    public async Task PickOfAFileWhoseFolderIsUnknownTellsTheUserItIsNotSupported()
    {
        TestWorkFolderService folders = new();
        folders.Files.Enqueue(CreateFile("input.txt", "", false));
        TestUserInterfaceService userInterface = new() { Confirmation = true };
        WorkFolderWorkflow workflow = new(folders, null!, null!, null!, null!, null!, userInterface);

        Assert.That(await workflow.PickFileAsync(FilePickerKind.Any), Is.Null);
        Assert.That(userInterface.Messages, Is.EqualTo([MobileTexts.DialogTextLocationNotSupported]));
        Assert.That(userInterface.ConfirmationCount, Is.Zero);
        Assert.That(folders.AddLocations, Is.Empty);
    }

    private static WorkFolderFile CreateFile(string name, string location, bool known) =>
        new($"{location}/{name}", name, location, location, "grant", known, null!);

    private sealed class TestWorkFolderService : IWorkFolderService
    {
        public List<WorkFolder> Folders { get; private set; } = [new("known", "Known", "grant")];
        public Queue<WorkFolderFile?> Files { get; } = new();
        public List<WorkFolder?> PickedFolders { get; } = [];
        public List<FilePickerKind> PickerKinds { get; } = [];
        public List<string?> AddLocations { get; } = [];
        public bool CancelAdd { get; init; }

        // The folder the user chooses instead of the initial location, such as one above it.
        public string? ChosenFolderId { get; init; }
        public Task<IReadOnlyList<WorkFolder>> GetFoldersAsync() => Task.FromResult<IReadOnlyList<WorkFolder>>(Folders);
        public Task<WorkFolderResult> AddFolderAsync(string? initialLocationId = null)
        {
            AddLocations.Add(initialLocationId);
            if (CancelAdd)
            {
                return Task.FromResult(WorkFolderResult.Canceled);
            }

            WorkFolder folder = new(ChosenFolderId ?? initialLocationId!, "Added", "grant");
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
        public List<WorkFolder?> SavedFolders { get; } = [];
        public List<string> SavedContents { get; } = [];
        public Task<WorkFolderFile?> SaveFileAsync(WorkFolder? folder, string fileName, Stream content)
        {
            SavedFolders.Add(folder);
            SavedContents.Add(new StreamReader(content).ReadToEnd());
            return Task.FromResult(Files.Dequeue());
        }
        public Dictionary<string, WorkFolderFile> Reopened { get; } = [];
        public Task<WorkFolderFileResult> OpenFileAsync(string fileId) => Task.FromResult(
            Reopened.TryGetValue(fileId, out WorkFolderFile? file)
                ? WorkFolderFileResult.Valid(file)
                : WorkFolderFileResult.NoAccess);
        public IReadOnlyList<string> GetFilePathSegments(string id, string? displayName = null) =>
            displayName is null ? id.Split('/') : [displayName];
        public string? GetFileLocationId(string fileId) =>
            fileId.Contains('/') ? fileId[..fileId.LastIndexOf('/')] : null;
    }

    private sealed class TestFileOperations : IWorkFolderFileOperations
    {
        public HashSet<string> Encrypted { get; } = [];
        public Task<Stream> OpenReadAsync(WorkFolderFile file) =>
            Task.FromResult<Stream>(new MemoryStream(Encrypted.Contains(file.Id) ? [0xe0] : [0x00]));
        public Task<bool> DestinationExistsAsync(WorkFolderFile file, string name) => throw new NotSupportedException();
        public Task<string> WriteDestinationAsync(WorkFolderFile file, string name, bool overwrite, Func<Stream, Task> writer) =>
            throw new NotSupportedException();
        public Task DeleteAsync(WorkFolderFile file) => throw new NotSupportedException();
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

    private sealed class FileDetectionCoreServices : ICoreServices
    {
        public async Task<bool> IsEncryptedAsync(Func<Task<Stream>> openReadAsync)
        {
            await using Stream stream = await openReadAsync();
            return stream.ReadByte() == 0xe0;
        }

        public Task EncryptAsync(Stream cleartext, Stream encrypted, EncryptRequest request) => throw new NotSupportedException();
        public Task<IDecryptionSession> OpenDecryptionAsync(Stream encrypted, DecryptRequest request) => throw new NotSupportedException();
        public Task<KeyPair> CreateKeyPairAsync(string email, string passphrase, DateTimeOffset createdUtc) => throw new NotSupportedException();
        public bool TryLoadKeyPair(ReadOnlyMemory<byte> encryptedKeyPair, IReadOnlyList<string> passphrases,
            [NotNullWhen(true)] out LoadedKeyPair? loadedKeyPair) => throw new NotSupportedException();
        public string ExportPublicKey(PublicKey publicKey) => throw new NotSupportedException();
        public PublicKey? ImportPublicKey(string serializedPublicKey) => throw new NotSupportedException();
        public PrivateKeyImportResult ImportPrivateKeys(string serializedAccounts, PrivateKeyImportRequest request) => throw new NotSupportedException();
        public bool TryParseEmail(string email, [NotNullWhen(true)] out string? address) => throw new NotSupportedException();
    }

    private sealed class TestUserInterfaceService : IUserInterfaceService
    {
        public bool Confirmation { get; init; }
        public int ConfirmationCount { get; private set; }
        public List<AppDestination> Destinations { get; } = [];
        public bool IsShellAvailable => true;
        public bool CanProcessIncomingFiles => true;
        public bool CanReceiveIncomingFiles => true;
        public IReadOnlyDictionary<string, string> IconMap => throw new NotSupportedException();
        public Task InvokeOnMainThreadAsync(Func<Task> action) => action();
        public List<string> Messages { get; } = [];
        public Task DisplayMessageAsync(string message)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
        public List<string> Confirmations { get; } = [];
        public Task<bool> DisplayConfirmationAsync(string message)
        {
            ConfirmationCount++;
            Confirmations.Add(message);
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
        public object? BackParameter { get; private set; }
        public Task GoBackAsync(object? parameter)
        {
            BackParameter = parameter;
            return Task.CompletedTask;
        }
        public Task OpenBrowserAsync(string url) => throw new NotSupportedException();

        public Task SetClipboardTextAsync(string text) => throw new NotSupportedException();
    }
}
