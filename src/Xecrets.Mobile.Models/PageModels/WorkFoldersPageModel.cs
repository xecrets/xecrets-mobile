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
/// Opened to make a copy, the folders are only where to start looking for the file, which is not changed.
/// </summary>
public partial class WorkFoldersPageModel(
    WorkFolderStorage storage,
    IFileAccess fileAccess,
    FolderAccessWorkflow folderAccess,
    FileOperationWorkflow fileOperations,
    ICrashLogService crashLogService,
    IUserInterfaceService userInterfaceService) : PageModelBase(userInterfaceService), IStatusTextPageModel, IBreadcrumbPageModel
{

    // The hint for the operation the page was opened for, shown once when the page has been shown.
    private string _pendingIntentHint = string.Empty;

    public ObservableCollection<WorkFolderEntry> Folders { get; } = [];

    // Opened for an operation, the page is named for it, since that is what the user is doing here.
    public string Breadcrumb => string.Join(
        MobileTexts.BreadcrumbSeparator,
        MobileTexts.BreadcrumbHome,
        Intent switch
        {
            WorkFolderIntent.Auto => MobileTexts.BreadcrumbMyFolders,
            WorkFolderIntent.Delete => MobileTexts.BreadcrumbWipe,
            WorkFolderIntent.Copy => MobileTexts.BreadcrumbCopy,
            WorkFolderIntent.Share => MobileTexts.BreadcrumbEncryptToShare,
            _ => throw new ArgumentOutOfRangeException(nameof(Intent), Intent, @"Unknown work folder intent."),
        });

    [ObservableProperty] public partial string MessageText { get; set; } = string.Empty;

    [ObservableProperty] public partial string StatusText { get; set; } = string.Empty;

    // Making a copy, the folders are only where to start looking for the file.
    public string Description => Intent switch
    {
        WorkFolderIntent.Auto => MobileTexts.WorkFolderDescription,
        WorkFolderIntent.Delete => MobileTexts.WorkFolderDescription,
        WorkFolderIntent.Copy => MobileTexts.WorkFolderCopyDescription,
        WorkFolderIntent.Share => MobileTexts.WorkFolderCopyDescription,
        _ => throw new ArgumentOutOfRangeException(nameof(Intent), Intent, @"Unknown work folder intent."),
    };

    /// <summary>
    /// The description, shown on the page itself while there are no folders, to make it easier to find.
    /// </summary>
    [ObservableProperty] public partial string NoFoldersText { get; set; } = string.Empty;

    /// <summary>
    /// What is done with a file picked in a folder.
    /// </summary>
    public WorkFolderIntent Intent { get; private set; } = WorkFolderIntent.Auto;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    [NotifyCanExecuteChangedFor(nameof(RenameCommand))]
    public partial bool IsBusy { get; set; }

    public void Initialize(WorkFolderIntent intent)
    {
        Intent = intent;
        OnPropertyChanged(nameof(Breadcrumb));
        OnPropertyChanged(nameof(Description));
        _pendingIntentHint = intent switch
        {
            WorkFolderIntent.Auto => string.Empty,
            WorkFolderIntent.Delete => MobileTexts.WorkFolderIntentDelete,
            WorkFolderIntent.Copy => MobileTexts.WorkFolderIntentCopy,
            WorkFolderIntent.Share => MobileTexts.WorkFolderIntentShare,
            _ => throw new ArgumentOutOfRangeException(nameof(intent), intent, @"Unknown work folder intent."),
        };
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            // A folder added by an earlier version does not tell where it is stored, and only its id tells its path.
            WorkFolder[] folders = WithListDisplayNames(
                [.. await storage.LoadFoldersAsync()],
                folder => folder.Root == RootProvider.Unknown
                    ? fileAccess.GetPathSegments(folder.Id, folder.DisplayName)
                    : [.. folder.Root.ToPathSegments(), .. folder.FolderPath, folder.DisplayName]);

            Folders.Clear();
            foreach (WorkFolder folder in folders)
            {
                Folders.Add(CreateEntry(folder));
            }

            UpdateNoFoldersText();

            if (_pendingIntentHint.Length > 0)
            {
                string hint = _pendingIntentHint;
                _pendingIntentHint = string.Empty;
                await UserInterfaceService.DisplayTransientMessageAsync(hint);
            }
        }
        catch (Exception ex)
        {
            crashLogService.WriteSwallowedException(ex);
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
            WorkFolder? folder = await folderAccess.AddFolderAsync();
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
            crashLogService.WriteSwallowedException(ex);
            StatusText = ex.FormatException();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private Task OpenAsync(WorkFolder folder) => RunBusyAsync(folder);

    /// <summary>
    /// Lets the user pick a file anywhere, which is changed through one of My folders if it is in one, and otherwise
    /// replaced by a result saved where the user chooses. A copy is made of the file wherever it is.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private Task BrowseAsync() => RunBusyAsync(null);

    private async Task RunBusyAsync(WorkFolder? folder)
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

    private void UpdateNoFoldersText() => NoFoldersText = Folders.Count == 0 ? Description : string.Empty;

    private async Task PickAndRunAsync(WorkFolder? folder)
    {
        try
        {
            await fileOperations.PickAndRunAsync(Intent, folder);
        }
        catch (OperationCanceledException)
        {
            await UserInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextOperationNotCompleted);
        }
        catch (Exception ex)
        {
            crashLogService.WriteSwallowedException(ex);
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
            await storage.RemoveFolderAsync(folder);
            Folders.Remove(Folders.Single(entry => entry.Folder == folder));
            UpdateNoFoldersText();
        }
        catch (Exception ex)
        {
            crashLogService.WriteSwallowedException(ex);
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

            await storage.RenameFolderAsync(folder, displayName.Trim());

            // Reload rather than replace the one item, so that the names of any folders it used to share a
            // name with are disambiguated again from the new set of names.
            await LoadAsync();
        }
        catch (Exception ex)
        {
            crashLogService.WriteSwallowedException(ex);
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
