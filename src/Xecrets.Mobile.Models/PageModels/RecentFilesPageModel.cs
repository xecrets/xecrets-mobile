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

public partial class RecentFilesPageModel : PageModelBase, IStatusTextPageModel
{
    private const int _pathEllipsisLength = 40;

    private readonly IRecentFilesService _recentFilesService;
    private readonly IWorkFolderService _workFolderService;
    private readonly WorkFolderWorkflow _workflow;

    // Source file id to result file id, for operations completed from the list in the current filter view.
    private readonly Dictionary<string, string> _completed = [];

    // The order of the rows in the current filter view, by source file id for completed rows.
    private List<string> _rowOrder = [];

    private List<RecentFileEntry> _available = [];
    private bool _isFilterChosen;
    private string? _pendingSourceId;

    public RecentFilesPageModel(
        IRecentFilesService recentFilesService,
        IWorkFolderService workFolderService,
        WorkFolderWorkflow workflow,
        IUserInterfaceService userInterfaceService)
        : base(userInterfaceService)
    {
        _recentFilesService = recentFilesService;
        _workFolderService = workFolderService;
        _workflow = workflow;
    }

    public ObservableCollection<RecentFileEntry> Files { get; } = [];

    public string Breadcrumb =>
        string.Join(MobileTexts.BreadcrumbSeparator, MobileTexts.BreadcrumbHome, MobileTexts.BreadcrumbRecentFiles);

    [ObservableProperty] public partial string MessageText { get; set; } = string.Empty;

    [ObservableProperty] public partial string StatusText { get; set; } = string.Empty;

    public string Description => MobileTexts.RecentFilesDescription;

    [ObservableProperty] public partial SelectedFileState SelectedState { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReverseCommand))]
    public partial bool IsBusy { get; set; }

    [RelayCommand]
    private async Task Load()
    {
        try
        {
            IReadOnlyList<string> fileIds = await _recentFilesService.GetFilesAsync();

            // A successful operation replaces its source with the result at the top of the list. The source is
            // still listed while a decryption waits for a password, or if the operation did not complete.
            if (_pendingSourceId is not null && !fileIds.Contains(_pendingSourceId))
            {
                _completed[_pendingSourceId] = fileIds[0];
                _pendingSourceId = null;
            }

            List<RecentFileEntry> available = [];
            foreach (string fileId in fileIds)
            {
                WorkFolderFileResult result = await _workFolderService.OpenFileAsync(fileId);
                if (result.Status == WorkFolderFileResultStatus.NotFound)
                {
                    continue;
                }

                available.Add(CreateEntry(fileId, result.File));
            }

            _available = available;
            if (!_isFilterChosen)
            {
                SelectedState = available.All(entry => entry.IsEncrypted) ? SelectedFileState.Encrypted : SelectedFileState.Decrypted;
                _isFilterChosen = true;
            }

            ShowRows();
        }
        catch (Exception ex)
        {
            StatusText = ex.FormatException();
        }
    }

    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private async Task Reverse(RecentFileEntry entry)
    {
        try
        {
            IsBusy = true;
            StatusText = string.Empty;
            WorkFolderFileResult result = await _workFolderService.OpenFileAsync(entry.Id);
            if (result.Status == WorkFolderFileResultStatus.NotFound)
            {
                await UserInterfaceService.DisplayMessageAsync(MobileTexts.DialogTextRecentFileNotFound);
                await Load();
                return;
            }

            if (result.Status == WorkFolderFileResultStatus.NoAccess)
            {
                await UserInterfaceService.DisplayMessageAsync(MobileTexts.DialogTextRecentFileNoAccess);
                return;
            }

            _pendingSourceId = entry.Id;
            await _workflow.TransformAsync(
                result.File!,
                entry.IsEncrypted ? WorkFolderOperation.Decrypt : WorkFolderOperation.Encrypt);
            await Load();
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

    private bool CanUseCommand() => !IsBusy;

    // Changing the filter only regroups the files already loaded. The default chosen by the first load is not a
    // change of filter.
    partial void OnSelectedStateChanged(SelectedFileState value)
    {
        if (!_isFilterChosen)
        {
            return;
        }

        _completed.Clear();
        _rowOrder = [];
        _pendingSourceId = null;
        ShowRows();
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
        IReadOnlyList<string> segments = _workFolderService.GetFilePathSegments(fileId);
        string fileName = file?.FileName ?? segments[^1];
        return new RecentFileEntry(
            fileId,
            string.Join(Path.DirectorySeparatorChar, segments).PathEllipsis(_pathEllipsisLength),
            fileName.IsEncrypted(),
            false,
            ReverseCommand);
    }
}
