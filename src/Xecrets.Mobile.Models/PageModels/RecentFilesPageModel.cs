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

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Services;
using Xecrets.Mobile.Models.Utilities;
using Xecrets.Texts;

namespace Xecrets.Mobile.Models.PageModels;

public partial class RecentFilesPageModel(
    IRecentFilesService recentFilesService,
    IWorkFolderService workFolderService,
    WorkFolderWorkflow workflow,
    IWorkFolderFileLauncher fileLauncher,
    IUserInterfaceService userInterfaceService,
    TimeProvider timeProvider)
    : PageModelBase(userInterfaceService), IStatusTextPageModel, IBreadcrumbPageModel
{
    // Source file id to result file id, for operations completed from the list in the current filter view.
    private readonly Dictionary<string, string> _completed = [];

    // The order of the rows in the current filter view, by source file id for completed rows.
    private List<string> _rowOrder = [];

    internal static readonly TimeSpan RefreshDelay = TimeSpan.FromSeconds(1);

    private List<RecentFileEntry> _available = [];

    private string? _pendingSourceId;

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

    public bool IsReverseAllVisible => SelectedState != SelectedFileState.All;

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
    private async Task Load()
    {
        try
        {
            IReadOnlyList<string> fileIds = await recentFilesService.GetFilesAsync();

            List<RecentFileEntry> available = [];
            foreach (string fileId in fileIds)
            {
                WorkFolderFileResult result = await workFolderService.OpenFileAsync(fileId);
                if (result.Status == WorkFolderFileResultStatus.NotFound)
                {
                    continue;
                }

                available.Add(CreateEntry(fileId, result.File));
            }

            // A successful operation deletes its source and puts the result at the top of the list. The source still
            // exists while a decryption waits for a password, or if the operation did not complete.
            if (_pendingSourceId is not null && available.All(entry => entry.Id != _pendingSourceId))
            {
                _completed[_pendingSourceId] = fileIds[0];
                _pendingSourceId = null;
            }

            _available = available;
            ShowRows();
        }
        catch (Exception ex)
        {
            StatusText = ex.FormatException();
        }
    }

    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private async Task Reload()
    {
        ResetView();
        await Load();
    }

    /// <summary>
    /// Lets the user pick a file in a known folder, which is then added and shown with <see cref="ShowAdded"/>.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private Task Add() =>
        UserInterfaceService.NavigateToAsync(AppDestination.WorkFolders, WorkFolderPickAction.AddToRecentFiles);

    // Reloads even if the filter is unchanged, since the rows otherwise keep their order and the added file would be
    // shown last rather than first.
    [RelayCommand]
    private Task ShowAdded(SelectedFileState state)
    {
        SelectedState = state;
        return Reload();
    }

    /// <summary>
    /// Opens a decrypted file in another app, or shares an encrypted one, where it is stored.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private async Task Open(RecentFileEntry entry)
    {
        if (entry.IsCompleted)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusText = string.Empty;
            WorkFolderFile? file = await OpenFileAsync(entry);
            if (file is null)
            {
                return;
            }

            if (entry.IsEncrypted)
            {
                await fileLauncher.ShareAsync(file);
                return;
            }

            if (!await fileLauncher.OpenAsync(file))
            {
                await UserInterfaceService.DisplayMessageAsync(MobileTexts.DialogTextNoAppToOpenFile);
            }
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
    private Task Reverse(RecentFileEntry entry) => ReverseFilesAsync([entry]);

    /// <summary>
    /// Reverses all files shown, which in the encrypted or decrypted view all have the same state.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private Task ReverseAll() => ReverseFilesAsync([.. Files.Where(entry => !entry.IsCompleted)]);

    /// <summary>
    /// Encrypts or decrypts the files in turn, stopping at the first one that does not complete, for example because
    /// it needs a password.
    /// </summary>
    private async Task ReverseFilesAsync(IReadOnlyList<RecentFileEntry> entries)
    {
        try
        {
            IsBusy = true;
            StatusText = string.Empty;
            foreach (RecentFileEntry entry in entries)
            {
                WorkFolderFile? file = await OpenFileAsync(entry);
                if (file is null)
                {
                    break;
                }

                _pendingSourceId = entry.Id;
                bool completed = await workflow.TransformAsync(
                    file,
                    entry.IsEncrypted ? WorkFolderOperation.Decrypt : WorkFolderOperation.Encrypt);
                await Load();
                if (!completed)
                {
                    break;
                }
            }

            // Executing a cancelable command cancels the token of a pending execution, so this restarts the wait.
            RefreshAfterDelayCommand.Execute(null);
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
    private async Task Remove(RecentFileEntry entry)
    {
        try
        {
            IsBusy = true;
            StatusText = string.Empty;
            await recentFilesService.RemoveAsync(entry.Id);
            _available.RemoveAll(available => available.Id == entry.Id);
            ShowRows();
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
    /// Opens the file of a row, telling the user and returning null when it no longer exists or cannot be accessed.
    /// </summary>
    private async Task<WorkFolderFile?> OpenFileAsync(RecentFileEntry entry)
    {
        WorkFolderFileResult result = await workFolderService.OpenFileAsync(entry.Id);
        if (result.Status == WorkFolderFileResultStatus.NotFound)
        {
            await UserInterfaceService.DisplayMessageAsync(MobileTexts.DialogTextRecentFileNotFound);
            await Load();
            return null;
        }

        if (result.Status == WorkFolderFileResultStatus.NoAccess)
        {
            await UserInterfaceService.DisplayMessageAsync(MobileTexts.DialogTextRecentFileNoAccess);
            return null;
        }

        return result.File!;
    }

    /// <summary>
    /// Stops a pending refresh, for example when the page is left.
    /// </summary>
    public void StopRefreshTimer() => RefreshAfterDelayCommand.Cancel();

    // Refreshes the list a while after the last operation, so the rows of completed operations are replaced by the
    // current list. Concurrent executions are allowed, since refresh calls may overlap.
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task RefreshAfterDelay(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(RefreshDelay, timeProvider, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!IsBusy)
        {
            await Reload();
        }
    }

    // Changing the filter only regroups the files already loaded.
    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnSelectedStateChanged(SelectedFileState value)
    {
        ResetView();
        ShowRows();
    }

    private void ResetView()
    {
        _completed.Clear();
        _rowOrder = [];
        _pendingSourceId = null;
    }

    /// <summary>
    /// Shows the available files of the current filter, keeping the order of the rows already shown so that the
    /// result of a completed operation takes the place of its source.
    /// </summary>
    private void ShowRows()
    {
        Dictionary<string, RecentFileEntry> entries = _available.ToDictionary(entry => entry.Id);
        List<(string Key, RecentFileEntry Entry)> rows = [];
        foreach (string key in _rowOrder)
        {
            if (_completed.TryGetValue(key, out string? resultId))
            {
                if (entries.TryGetValue(resultId, out RecentFileEntry? result))
                {
                    rows.Add((key, result with { IsCompleted = true }));
                }
            }
            else if (entries.TryGetValue(key, out RecentFileEntry? entry) && IsShown(entry))
            {
                rows.Add((key, entry));
            }
        }

        // Compared by entry rather than by key, since a completed result is also listed by itself when all files are
        // shown.
        rows.AddRange(_available
            .Where(entry => IsShown(entry) && rows.All(row => row.Entry.Id != entry.Id))
            .Select(entry => (entry.Id, entry)));

        _rowOrder = [.. rows.Select(row => row.Key)];
        Files.Clear();
        foreach ((string _, RecentFileEntry entry) in rows)
        {
            Files.Add(entry);
        }
    }

    private bool IsShown(RecentFileEntry entry) => SelectedState switch
    {
        SelectedFileState.Encrypted => entry.IsEncrypted,
        SelectedFileState.Decrypted => !entry.IsEncrypted,
        SelectedFileState.All => true,
        _ => throw new InvalidOperationException($"Unknown file state {SelectedState}."),
    };

    private RecentFileEntry CreateEntry(string fileId, WorkFolderFile? file)
    {
        IReadOnlyList<string> segments = workFolderService.GetFilePathSegments(fileId);
        string fileName = file?.FileName ?? segments[^1];
        return new RecentFileEntry(
            fileId,
            string.Join(Path.DirectorySeparatorChar, segments),
            fileName.IsEncrypted(),
            false);
    }
}
