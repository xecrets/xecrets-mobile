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

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Xecrets.Common.Models;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Services;
using Xecrets.Mobile.Models.Utilities;
using Xecrets.Texts;

namespace Xecrets.Mobile.Models.PageModels;

/// <summary>
/// The recent files, shown as listed without checking the files. A file is only checked when it is used: first that
/// there is access to it, and then that it can be reached. A file changed from the list is reached through one of My
/// folders, and the user is asked to add its folder when there is none. A file that cannot be reached is offered to be
/// removed, since it may be gone or only offline for now, which the user knows best.
/// </summary>
public partial class RecentFilesPageModel(
    IRecentFilesService recentFilesService,
    IFileAccess fileAccess,
    WorkFolderStorage workFolderStorage,
    FolderAccessWorkflow folderAccess,
    FileOperationWorkflow fileOperations,
    IWorkFolderFileLauncher fileLauncher,
    IUserInterfaceService userInterfaceService)
    : PageModelBase(userInterfaceService), IStatusTextPageModel, IBreadcrumbPageModel
{
    private List<RecentFileEntry> _available = [];

    public ObservableCollection<RecentFileEntry> Files { get; } = [];

    public string Breadcrumb =>
        string.Join(MobileTexts.BreadcrumbSeparator, MobileTexts.BreadcrumbHome, MobileTexts.BreadcrumbRecentFiles);

    [ObservableProperty] public partial string MessageText { get; set; } = string.Empty;

    [ObservableProperty] public partial string StatusText { get; set; } = string.Empty;

    public string Description => MobileTexts.RecentFilesDescription;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReverseAllVisible))]
    [NotifyPropertyChangedFor(nameof(ReverseAllEncrypts))]
    public partial SelectedFileState SelectedState { get; set; } = SelectedFileState.All;

    public bool IsReverseAllVisible => SelectedState is SelectedFileState.Encrypted or SelectedFileState.Decrypted;

    public bool ReverseAllEncrypts => SelectedState == SelectedFileState.Decrypted;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReverseCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReverseAllCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReloadCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    public partial bool IsBusy { get; set; }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            // A file listed by an operation this version does not know is not shown, since what to do with it is not
            // known either.
            List<RecentFile> recentFiles =
            [
                .. (await recentFilesService.GetFilesAsync())
                    .Where(file => file.Operation != RecentFileOperation.Unknown)
                    .DistinctBy(file => file.Id),
            ];

            _available = [.. recentFiles.Select(CreateEntry)];
            ShowRows();
        }
        catch (Exception ex)
        {
            StatusText = ex.FormatException();
        }
    }

    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private Task ReloadAsync() => LoadAsync();

    /// <summary>
    /// Lets the user pick a file in My folders, starting in the one first, and lists it as encrypted or decrypted where
    /// it is, showing it first among the files in its state.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private async Task AddAsync()
    {
        try
        {
            IsBusy = true;
            StatusText = string.Empty;
            WorkFolder? folder = (await workFolderStorage.LoadFoldersAsync()).FirstOrDefault();
            FileReference? file = await folderAccess.PickWritableFileAsync(folder, FilePickerKind.Any);
            if (file is null)
            {
                return;
            }

            await recentFilesService.AddAsync(file, RecentFileOperation.InPlace);
            SelectedState = file.Name.IsEncrypted() ? SelectedFileState.Encrypted : SelectedFileState.Decrypted;
            await LoadAsync();
        }
        catch (OperationCanceledException)
        {
            await UserInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextOperationNotCompleted);
        }
        catch (Exception ex)
        {
            StatusText = ex.FormatException();
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Shows a viewed or edited file again, like when decrypting a copy. Other files are opened in another app when
    /// decrypted, or shared when encrypted, where they are stored.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private async Task OpenAsync(RecentFileEntry entry)
    {
        try
        {
            IsBusy = true;
            StatusText = string.Empty;
            await RunOnFileAsync(entry, isChanged: false, file => OpenFileAsync(entry, file));
        }
        catch (Exception ex)
        {
            StatusText = ex.FormatException();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task OpenFileAsync(RecentFileEntry entry, FileReference file)
    {
        if (entry.IsViewed)
        {
            if (!await fileOperations.PreviewAsync(file))
            {
                StatusText = MobileTexts.DialogTextWrongPasswordOpen;
            }

            return;
        }

        if (entry.IsEncrypted)
        {
            await fileLauncher.ShareAsync(file);
            return;
        }

        if (!await fileLauncher.OpenAsync(file, allowWrite: entry.Operation.IsWriteClass()))
        {
            await UserInterfaceService.DisplayMessageAsync(MobileTexts.DialogTextNoAppToOpenFile);
        }
    }

    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private Task ReverseAsync(RecentFileEntry entry) => ReverseFilesAsync([entry]);

    /// <summary>
    /// Reverses all files shown, which in the encrypted or decrypted view all have the same state.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private Task ReverseAllAsync() => ReverseFilesAsync([.. Files]);

    /// <summary>
    /// Encrypts or decrypts the files in turn, stopping at the first one that cannot be reached or needs a password. A
    /// file that nothing is done with, such as one already encrypted, does not stop the others.
    /// </summary>
    private async Task ReverseFilesAsync(IReadOnlyList<RecentFileEntry> entries)
    {
        try
        {
            IsBusy = true;
            StatusText = string.Empty;
            foreach (RecentFileEntry entry in entries)
            {
                FileOperationOutcome? outcome = null;
                WorkFolderOperation operation =
                    entry.IsEncrypted ? WorkFolderOperation.Decrypt : WorkFolderOperation.Encrypt;
                await RunOnFileAsync(entry, isChanged: true,
                    async file => outcome = await fileOperations.TransformAsync(file, operation));
                await LoadAsync();
                if (outcome?.Status is not (FileOperationStatus.Completed or FileOperationStatus.NotDone))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            await UserInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextOperationNotCompleted);
        }
        catch (Exception ex)
        {
            StatusText = ex.FormatException();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private async Task RemoveAsync(RecentFileEntry entry)
    {
        try
        {
            IsBusy = true;
            StatusText = string.Empty;
            await RemoveEntryAsync(entry.Id);
        }
        catch (Exception ex)
        {
            StatusText = ex.FormatException();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanUseCommand() => !IsBusy;

    /// <summary>
    /// Runs the action on the file of a row, once there is access to it and it can be reached.
    /// </summary>
    private async Task RunOnFileAsync(RecentFileEntry entry, bool isChanged, Func<FileReference, Task> action)
    {
        FileReference? file = await AccessAsync(entry, isChanged);
        if (file is null)
        {
            return;
        }

        try
        {
            await action(file);
        }
        catch (FileNotAccessibleException)
        {
            await OfferRemoveAsync(ListedId(entry, file));
        }
    }

    /// <summary>
    /// Gets access to the file of a row, and checks that it can be reached. A file that is changed, or was listed by
    /// an operation that changes it, is reached through one of My folders, and the user is asked to add its folder
    /// when there is none. Other files are reached through the access to them alone. Returns null when there is no
    /// access, or the file cannot be reached, which the user is told.
    /// </summary>
    private async Task<FileReference?> AccessAsync(RecentFileEntry entry, bool isChanged)
    {
        FileReference file = new(entry.Id, entry.FileName);
        if (isChanged || entry.Operation.IsWriteClass())
        {
            FileReference? reachable = await folderAccess.EnsureFolderAccessAsync(file);
            if (reachable is null)
            {
                return null;
            }

            // A file listed as changed is listed as reached through the folder from now on.
            if (entry.Operation.IsWriteClass() && reachable.Id != entry.Id)
            {
                await recentFilesService.ReplaceIdAsync(entry.Id, reachable.Id);
                await LoadAsync();
            }

            file = reachable;
        }
        else if (!fileAccess.HasFileGrant(file.Id))
        {
            await OfferRemoveAsync(entry.Id);
            return null;
        }

        try
        {
            await using (await fileAccess.OpenReadAsync(file.Id))
            {
            }
        }
        catch (FileNotAccessibleException)
        {
            await OfferRemoveAsync(ListedId(entry, file));
            return null;
        }

        return file;
    }

    private static string ListedId(RecentFileEntry entry, FileReference file) =>
        entry.Operation.IsWriteClass() ? file.Id : entry.Id;

    /// <summary>
    /// Asks the user whether to remove a file that cannot be reached, since it may be gone, or only not available right
    /// now, such as when its storage is offline.
    /// </summary>
    private async Task OfferRemoveAsync(string fileId)
    {
        if (await UserInterfaceService.DisplayConfirmationAsync(MobileTexts.DialogTextRecentFileNotAccessible))
        {
            await RemoveEntryAsync(fileId);
        }
    }

    private async Task RemoveEntryAsync(string fileId)
    {
        await recentFilesService.RemoveAsync(fileId);
        _available.RemoveAll(available => available.Id == fileId);
        ShowRows();
    }

    // Changing the filter only regroups the files already loaded.
    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnSelectedStateChanged(SelectedFileState value) => ShowRows();

    private void ShowRows()
    {
        Files.Clear();
        foreach (RecentFileEntry entry in _available.Where(IsShown))
        {
            Files.Add(entry);
        }
    }

    private bool IsShown(RecentFileEntry entry) => SelectedState switch
    {
        SelectedFileState.Encrypted => entry.IsInPlace && entry.IsEncrypted,
        SelectedFileState.Decrypted => entry.IsInPlace && !entry.IsEncrypted,
        SelectedFileState.Viewed => entry.IsViewed,
        SelectedFileState.Other => entry.IsOther,
        SelectedFileState.All => true,
        _ => throw new InvalidOperationException($"Unknown file state {SelectedState}."),
    };

    // The name of a file is the one remembered when its id does not tell it. An id that cannot be made sense of, such
    // as from an earlier version, is shown by the name remembered, so that it can still be removed from the list.
    private RecentFileEntry CreateEntry(RecentFile recentFile)
    {
        IReadOnlyList<string> segments;
        try
        {
            segments = fileAccess.GetPathSegments(recentFile.Id, recentFile.Name);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            segments = [recentFile.Name ?? string.Empty];
        }

        string fileName = segments.Count > 0 ? segments[^1] : string.Empty;
        if (fileName.Length == 0)
        {
            segments = [.. segments.SkipLast(1), MobileTexts.RecentFileNameUnknown];
        }

        return new RecentFileEntry(
            recentFile.Id,
            string.Join(Path.DirectorySeparatorChar, segments),
            fileName,
            fileName.IsEncrypted(),
            recentFile.Operation);
    }
}
