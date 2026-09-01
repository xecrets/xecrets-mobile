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

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Utilities;
using Xecrets.Texts;

namespace Xecrets.Mobile.Models.PageModels;

public partial class WorkFoldersPageModel : PageModelBase, IStatusTextPageModel
{
    private readonly IWorkFolderService _workFolderService;
    private readonly IWorkFolderOperationService _operationService;
    private readonly IFlowContext _flowContext;
    private WorkFolderOperation _operation;
    private bool _refreshingListDisplayNames;

    public WorkFoldersPageModel(
        IWorkFolderService workFolderService,
        IWorkFolderOperationService operationService,
        IFlowContext flowContext,
        IUserInterfaceService userInterfaceService)
        : base(userInterfaceService)
    {
        _workFolderService = workFolderService;
        _operationService = operationService;
        _flowContext = flowContext;
        Folders = new WorkFolderCollection(RefreshListDisplayNames);
    }

    public ObservableCollection<WorkFolder> Folders { get; }

    public string Breadcrumb =>
        BreadcrumbFormatter.Format(_flowContext.Origin, _flowContext.Operation, MobileTexts.BreadcrumbMyFolders);

    [ObservableProperty] public partial string MessageText { get; set; } = string.Empty;

    [ObservableProperty] public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty] public partial string Description { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    [NotifyCanExecuteChangedFor(nameof(RenameCommand))]
    public partial bool IsBusy { get; set; }

    public void Initialize(WorkFolderOperation operation)
    {
        _operation = operation;
        Description = MobileTexts.WorkFolderDescription;
    }

    [RelayCommand]
    private async Task Load()
    {
        try
        {
            Folders.Clear();
            foreach (WorkFolder folder in await _workFolderService.GetFoldersAsync())
            {
                Folders.Add(folder);
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
            WorkFolder? folder = await _workFolderService.AddFolderAsync();
            if (folder is null)
            {
                return;
            }

            if (Folders.All(item => item.Id != folder.Id))
            {
                Folders.Insert(0, folder);
                await _workFolderService.SaveFoldersAsync(Folders);
            }

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
            Folders.Remove(folder);
            await _workFolderService.SaveFoldersAsync(Folders);
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
        WorkFolder folder = initialFolder;
        while (true)
        {
            FilePickerKind pickerKind = _operation == WorkFolderOperation.Encrypt
                ? FilePickerKind.Any
                : FilePickerKind.Encrypted;
            WorkFolderFile? file = await _workFolderService.PickFileAsync(folder, pickerKind);
            if (file is null)
            {
                return;
            }

            WorkFolder? knownLocation = Folders.FirstOrDefault(item => item.Id == file.LocationId);
            if (knownLocation is not null)
            {
                await TransformAsync(file);
                await MoveFolderToTopAsync(knownLocation);
                return;
            }

            if (file.IsInKnownWorkFolder)
            {
                WorkFolder discoveredFolder = await _workFolderService.AddDiscoveredFolderAsync(file);
                Folders.Insert(0, discoveredFolder);
                await _workFolderService.SaveFoldersAsync(Folders);
                await TransformAsync(file);
                return;
            }

            bool add = await UserInterfaceService.DisplayConfirmationAsync(MobileTexts.DialogTextAddUnknownWorkFolder);
            if (add)
            {
                WorkFolder? addedFolder = await _workFolderService.AddFolderAsync(file.LocationId);
                if (addedFolder is null)
                {
                    return;
                }

                if (Folders.All(item => item.Id != addedFolder.Id))
                {
                    Folders.Insert(0, addedFolder);
                    await _workFolderService.SaveFoldersAsync(Folders);
                }

                folder = addedFolder;
                continue;
            }

            return;
        }
    }

    private async Task TransformAsync(WorkFolderFile file)
    {
        if (_operation == WorkFolderOperation.Encrypt)
        {
            if (file.FileName.IsEncrypted())
            {
                await UserInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextAlreadyEncrypted);
                return;
            }

            await _operationService.EncryptAsync(file);
            await UserInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextResultSaved);
            return;
        }

        if (await _operationService.DecryptWithKnownPasswordsAsync(file))
        {
            await UserInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextResultSaved);
            return;
        }

        await UserInterfaceService.NavigateToAsync(AppDestination.EnterPassword);
    }

    private bool CanUseCommand() => !IsBusy;
    private async Task MoveFolderToTopAsync(WorkFolder folder)
    {
        int index = Folders.IndexOf(folder);
        if (index > 0)
        {
            Folders.Move(index, 0);
            await _workFolderService.SaveFoldersAsync(Folders);
        }
    }

    private void RefreshListDisplayNames()
    {
        if (_refreshingListDisplayNames)
        {
            return;
        }

        _refreshingListDisplayNames = true;
        try
        {
            WorkFolder[] folders = [.. Folders];
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
                    Folders[folderIndex] = folders[folderIndex] with
                    {
                        ListDisplayName = proposedDisplayNames[duplicateIndex],
                    };
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

    private sealed class WorkFolderCollection(Action changed) : ObservableCollection<WorkFolder>
    {
        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnCollectionChanged(e);
            changed();
        }
    }
}
