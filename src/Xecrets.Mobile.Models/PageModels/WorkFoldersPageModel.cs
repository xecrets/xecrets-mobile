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
using System.Collections.Specialized;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Xecrets.Common.Models;
using Xecrets.Core.Abstractions;

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Services;
using Xecrets.Mobile.Models.Utilities;
using Xecrets.Texts;

namespace Xecrets.Mobile.Models.PageModels;

public partial class WorkFoldersPageModel : PageModelBase, IStatusTextPageModel
{
    private readonly IWorkFolderService _workFolderService;
    private readonly WorkFolderWorkflow _workflow;
    private readonly ICoreServices _coreServices;
    private bool _refreshingListDisplayNames;

    public WorkFoldersPageModel(
        IWorkFolderService workFolderService,
        WorkFolderWorkflow workflow,
        ICoreServices coreServices,
        IUserInterfaceService userInterfaceService)
        : base(userInterfaceService)
    {
        _workFolderService = workFolderService;
        _workflow = workflow;
        _coreServices = coreServices;
        Folders = new WorkFolderCollection(RefreshListDisplayNames);
    }

    public ObservableCollection<WorkFolderEntry> Folders { get; }

    public string Breadcrumb =>
        string.Join(MobileTexts.BreadcrumbSeparator, MobileTexts.BreadcrumbHome, MobileTexts.BreadcrumbMyFolders);

    [ObservableProperty] public partial string MessageText { get; set; } = string.Empty;

    [ObservableProperty] public partial string StatusText { get; set; } = string.Empty;

    public string Description => MobileTexts.WorkFolderDescription;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    [NotifyCanExecuteChangedFor(nameof(RenameCommand))]
    public partial bool IsBusy { get; set; }

    [RelayCommand]
    private async Task Load()
    {
        try
        {
            Folders.Clear();
            foreach (WorkFolder folder in await _workFolderService.GetFoldersAsync())
            {
                Folders.Add(CreateEntry(folder));
            }
        }
        catch (Exception ex)
        {
            StatusText = ex.FormatException();
        }
    }

    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private async Task Add()
    {
        try
        {
            IsBusy = true;
            StatusText = string.Empty;
            WorkFolder? folder = await _workflow.AddFolderAsync();
            if (folder is null)
            {
                return;
            }

            await Load();

            await PickAndTransformAsync(folder);
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
    private async Task Open(WorkFolder folder)
    {
        try
        {
            IsBusy = true;
            StatusText = string.Empty;
            await PickAndTransformAsync(folder);
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
    private async Task Remove(WorkFolder folder)
    {
        try
        {
            IsBusy = true;
            StatusText = string.Empty;
            await _workFolderService.RemoveFolderAsync(folder);
            Folders.Remove(Folders.Single(entry => entry.Folder == folder));
            await _workFolderService.SaveFoldersAsync([.. Folders.Select(entry => entry.Folder)]);
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
    private async Task Rename(WorkFolder folder)
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

            await _workFolderService.RenameFolderAsync(folder, displayName.Trim());

            // Reload rather than replace the one item, so that the names of any folders it used to share a
            // name with are disambiguated again from the new set of names.
            await Load();
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

    private async Task PickAndTransformAsync(WorkFolder initialFolder)
    {
        WorkFolderFile? file = await _workflow.PickFileAsync(FilePickerKind.Any, initialFolder);
        await Load();
        if (file is null)
        {
            return;
        }

        WorkFolderOperation operation = await _coreServices.IsEncryptedAsync(file.OpenReadAsync)
            ? WorkFolderOperation.Decrypt
            : WorkFolderOperation.Encrypt;
        await _workflow.TransformAsync(file, operation);
    }

    private bool CanUseCommand() => !IsBusy;

    private void RefreshListDisplayNames()
    {
        if (_refreshingListDisplayNames)
        {
            return;
        }

        _refreshingListDisplayNames = true;
        try
        {
            WorkFolder[] folders = [.. Folders.Select(entry => entry.Folder)];
            string[] duplicateDisplayNames =
            [
                .. folders
                    .GroupBy(folder => folder.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key),
            ];

            foreach (string duplicateDisplayName in duplicateDisplayNames)
            {
                int[] duplicateIndexes =
                [
                    .. folders
                        .Select((folder, index) => (Folder: folder, Index: index))
                        .Where(item => string.Equals(
                            item.Folder.DisplayName,
                            duplicateDisplayName,
                            StringComparison.OrdinalIgnoreCase))
                        .Select(item => item.Index),
                ];

                string[][] reversedPathSegments = new string[duplicateIndexes.Length][];
                for (int duplicateIndex = 0; duplicateIndex < duplicateIndexes.Length; duplicateIndex++)
                {
                    reversedPathSegments[duplicateIndex] =
                    [
                        .. _workFolderService
                            .GetPathSegments(folders[duplicateIndexes[duplicateIndex]])
                            .Reverse(),
                    ];
                }

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
                    Folders[folderIndex] = CreateEntry(folders[folderIndex] with
                    {
                        ListDisplayName = proposedDisplayNames[duplicateIndex],
                    });
                }
            }
        }
        finally
        {
            _refreshingListDisplayNames = false;
        }
    }

    private static string BuildListDisplayName(string[] reversedPathSegments, int pathDepth) =>
        string.Join(Path.DirectorySeparatorChar, reversedPathSegments.Take(pathDepth).Reverse());

    private WorkFolderEntry CreateEntry(WorkFolder folder) =>
        new(folder, RenameCommand, OpenCommand, RemoveCommand);

    private sealed class WorkFolderCollection(Action changed) : ObservableCollection<WorkFolderEntry>
    {
        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnCollectionChanged(e);
            changed();
        }
    }
}
