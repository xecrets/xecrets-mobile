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

namespace Xecrets.Mobile.Models.PageModels;

/// <summary>
/// My folders, the folders the user has given the app access to. Files are changed where they are by picking them in
/// one of these, for the operation the page was opened for, or by default to encrypt or decrypt them as they are.
/// </summary>
public partial class WorkFoldersPageModel : PageModelBase, IStatusTextPageModel, IBreadcrumbPageModel
{
    private readonly WorkFolderStorage _storage;
    private readonly IFileAccess _fileAccess;
    private readonly FolderAccessWorkflow _folderAccess;
    private readonly FileOperationWorkflow _fileOperations;

    // The hint for the operation the page was opened for, shown once when the page has been shown.
    private string _pendingIntentHint = string.Empty;

    public WorkFoldersPageModel(
        WorkFolderStorage storage,
        IFileAccess fileAccess,
        FolderAccessWorkflow folderAccess,
        FileOperationWorkflow fileOperations,
        IUserInterfaceService userInterfaceService)
        : base(userInterfaceService)
    {
        _storage = storage;
        _fileAccess = fileAccess;
        _folderAccess = folderAccess;
        _fileOperations = fileOperations;
    }

    public ObservableCollection<WorkFolderEntry> Folders { get; } = [];

    public string Breadcrumb =>
        string.Join(MobileTexts.BreadcrumbSeparator, MobileTexts.BreadcrumbHome, MobileTexts.BreadcrumbMyFolders);

    [ObservableProperty] public partial string MessageText { get; set; } = string.Empty;

    [ObservableProperty] public partial string StatusText { get; set; } = string.Empty;

    public string Description => MobileTexts.WorkFolderDescription;

    /// <summary>
    /// What is done with a file picked in a folder.
    /// </summary>
    public WorkFolderIntent Intent { get; private set; } = WorkFolderIntent.Auto;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    [NotifyCanExecuteChangedFor(nameof(RenameCommand))]
    public partial bool IsBusy { get; set; }

    public void Initialize(WorkFolderIntent intent)
    {
        Intent = intent;
        _pendingIntentHint = intent switch
        {
            WorkFolderIntent.Encrypt => MobileTexts.WorkFolderIntentEncrypt,
            WorkFolderIntent.Decrypt => MobileTexts.WorkFolderIntentDecrypt,
            WorkFolderIntent.Delete => MobileTexts.WorkFolderIntentDelete,
            _ => string.Empty,
        };
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            WorkFolder[] folders = WithListDisplayNames(
                [.. await _storage.LoadFoldersAsync()],
                folder => _fileAccess.GetPathSegments(folder.Id, folder.DisplayName));

            Folders.Clear();
            foreach (WorkFolder folder in folders)
            {
                Folders.Add(CreateEntry(folder));
            }

            if (_pendingIntentHint.Length > 0)
            {
                string hint = _pendingIntentHint;
                _pendingIntentHint = string.Empty;
                await UserInterfaceService.DisplayTransientMessageAsync(hint);
            }
        }
        catch (Exception ex)
        {
            StatusText = ex.FormatException();
        }
    }

    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private async Task AddAsync()
    {
        try
        {
            IsBusy = true;
            StatusText = string.Empty;
            WorkFolder? folder = await _folderAccess.AddFolderAsync();
            if (folder is null)
            {
                return;
            }

            await LoadAsync();

            // Opened for an operation, the folder is added to pick the file in, so the file is picked right away.
            if (Intent != WorkFolderIntent.Auto)
            {
                await PickAndRunAsync(folder);
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
    private async Task OpenAsync(WorkFolder folder)
    {
        try
        {
            IsBusy = true;
            StatusText = string.Empty;
            await PickAndRunAsync(folder);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PickAndRunAsync(WorkFolder folder)
    {
        try
        {
            await _fileOperations.PickAndRunAsync(Intent, folder);
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
            // Picking may have added a folder or moved one to the top, even if the operation did not complete.
            await LoadAsync();
        }
    }

    /// <summary>
    /// Removes the folder from My folders. Its grant is released when the app starts or the user signs out next time,
    /// unless it has been added again by then.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private async Task RemoveAsync(WorkFolder folder)
    {
        try
        {
            IsBusy = true;
            StatusText = string.Empty;
            await _storage.RemoveFolderAsync(folder);
            Folders.Remove(Folders.Single(entry => entry.Folder == folder));
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
    private async Task RenameAsync(WorkFolder folder)
    {
        try
        {
            IsBusy = true;
            StatusText = string.Empty;
            string? displayName = await UserInterfaceService.DisplayPromptAsync(
                MobileTexts.DialogTextFolderName,
                folder.DisplayName);
            if (displayName is null || displayName.Trim().Length == 0)
            {
                return;
            }

            await _storage.RenameFolderAsync(folder, displayName.Trim());

            // Reload rather than replace the one item, so that the names of any folders it used to share a
            // name with are disambiguated again from the new set of names.
            await LoadAsync();
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
    /// Gives folders that share a display name as much of their path as it takes to tell them apart in the list. The
    /// other folders are returned as they are, all in the same order.
    /// </summary>
    private static WorkFolder[] WithListDisplayNames(
        IReadOnlyList<WorkFolder> folders,
        Func<WorkFolder, IReadOnlyList<string>> getPathSegments)
    {
        WorkFolder[] result = [.. folders];
        IEnumerable<int[]> duplicateGroups = Enumerable.Range(0, result.Length)
            .GroupBy(index => result[index].DisplayName, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.ToArray());

        foreach (int[] duplicateIndexes in duplicateGroups)
        {
            string[][] reversedPathSegments =
            [
                .. duplicateIndexes.Select(index => getPathSegments(result[index]).Reverse().ToArray()),
            ];

            int pathDepth = 1;
            string[] proposedDisplayNames;
            while (true)
            {
                int depth = pathDepth;
                proposedDisplayNames =
                [
                    .. reversedPathSegments.Select(segments => BuildListDisplayName(segments, depth)),
                ];

                bool displayNamesAreUnique = proposedDisplayNames
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count() == proposedDisplayNames.Length;

                if (displayNamesAreUnique ||
                    reversedPathSegments.All(segments => pathDepth >= segments.Length))
                {
                    break;
                }

                pathDepth++;
            }

            for (int duplicateIndex = 0; duplicateIndex < duplicateIndexes.Length; duplicateIndex++)
            {
                int folderIndex = duplicateIndexes[duplicateIndex];
                result[folderIndex] = result[folderIndex] with
                {
                    ListDisplayName = proposedDisplayNames[duplicateIndex],
                };
            }
        }

        return result;
    }

    private static string BuildListDisplayName(string[] reversedPathSegments, int pathDepth) =>
        string.Join(Path.DirectorySeparatorChar, reversedPathSegments.Take(pathDepth).Reverse());

    private WorkFolderEntry CreateEntry(WorkFolder folder) =>
        new(folder, RenameCommand, OpenCommand, RemoveCommand);
}
