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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Xecrets.Common.Models;

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Services;
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Models.PageModels;

public partial class PreviewPageModel(
    IPreviewService previewService,
    IDecryptedFileViewer decryptedFileViewer,
    IFileService fileService,
    FolderAccessWorkflow folderAccess,
    FileOperationWorkflow fileOperationWorkflow,
    IRecentFilesService recentFilesService,
    ICrashTestService crashTestService,
    IFlowContext flowContext,
    IUserInterfaceService userInterfaceService)
    : PageModelBase(userInterfaceService), IStatusTextPageModel, IBreadcrumbPageModel
{
    private const string _nonBreakingSpace = "\u00A0";

    public string Breadcrumb =>
        BreadcrumbFormatter.Format(flowContext.Origin, flowContext.Operation, MobileTexts.BreadcrumbPreview);

    [ObservableProperty]
    public partial string FileNameText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MessageText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MetadataText { get; set; } = string.Empty;

    public bool CanView => !IsBusy && ViewMode != FileViewMode.Unavailable;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ViewCommand))]
    private partial FileViewMode ViewMode { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ViewCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenInCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveAsCommand))]
    [NotifyCanExecuteChangedFor(nameof(SendToCommand))]
    [NotifyCanExecuteChangedFor(nameof(CloseCommand))]
    public partial bool IsBusy { get; set; }

    [RelayCommand]
    private async Task Initialize()
    {
        ViewMode = FileViewMode.Unavailable;

        IPreviewState state = previewService.Current;
        if (!state.IsReady)
        {
            await UserInterfaceService.GoBackAsync(null);
            return;
        }

        FileNameText = string.IsNullOrWhiteSpace(state.OriginalFileName)
            ? MobileTexts.DisplayNameProgram
            : state.OriginalFileName;
        MetadataText = CreateMetadataText(state);
        StatusText = string.Empty;
        ViewMode = await decryptedFileViewer.GetViewModeAsync(state.File!);
    }

    [RelayCommand(CanExecute = nameof(CanView))]
    private async Task View()
    {
        crashTestService.CrashIfArmed(CrashTestOperation.View);

        IPreviewState state = previewService.Current;
        if (!state.IsReady)
        {
            return;
        }

        DecryptedFileInfo file = state.File!;

        if (ViewMode == FileViewMode.Internal)
        {
            await recentFilesService.AddFlowSourceAsync(RecentFileOperation.View);
            await UserInterfaceService.NavigateToAsync(AppDestination.View);
            return;
        }

        try
        {
            IsBusy = true;
            StatusText = string.Empty;

            await decryptedFileViewer.ViewAsync(file);
            await recentFilesService.AddFlowSourceAsync(RecentFileOperation.View);
        }
        catch (OperationCanceledException)
        {
            StatusText = string.Empty;
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
    /// Edits the text. An edited file is saved over, so the file is first reached through one of My folders, which the
    /// user is asked to add when there is none. A file received from another app can only be saved as a copy.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task Edit()
    {
        IPreviewState state = previewService.Current;
        if (!state.IsReady)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusText = string.Empty;
            if (flowContext.Source is { } source)
            {
                FileReference? reachable = await folderAccess.EnsureFolderAccessAsync(source);
                if (reachable is null)
                {
                    return;
                }

                flowContext.UpdateSource(reachable);
                await recentFilesService.AddFlowSourceAsync(RecentFileOperation.Edit);
            }

            await UserInterfaceService.NavigateToAsync(AppDestination.Edit);
        }
        catch (OperationCanceledException)
        {
            StatusText = string.Empty;
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
    private async Task OpenIn()
    {
        IPreviewState state = previewService.Current;
        if (!state.IsReady)
        {
            return;
        }

        DecryptedFileInfo file = state.File!;

        try
        {
            IsBusy = true;
            StatusText = string.Empty;
            if (await fileService.OpenInAsync(file.FilePath, file.DisplayName))
            {
                await recentFilesService.AddFlowSourceAsync(RecentFileOperation.DecryptCopyOpenIn);
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = string.Empty;
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
    private async Task SaveAs()
    {
        IPreviewState state = previewService.Current;
        if (!state.IsReady)
        {
            return;
        }

        DecryptedFileInfo file = state.File!;

        try
        {
            IsBusy = true;
            StatusText = string.Empty;
            await using FileStream content = File.OpenRead(file.FilePath);
            if (await fileOperationWorkflow.SaveAsAsync(file.DisplayName, content) is not null)
            {
                await recentFilesService.AddFlowSourceAsync(RecentFileOperation.DecryptCopySaveAs);
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = string.Empty;
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
    private async Task SendTo()
    {
        IPreviewState state = previewService.Current;
        if (!state.IsReady)
        {
            return;
        }

        DecryptedFileInfo file = state.File!;

        try
        {
            IsBusy = true;
            StatusText = string.Empty;
            await fileService.SendToAsync(
                file.FilePath,
                file.DisplayName,
                string.Empty);
            await recentFilesService.AddFlowSourceAsync(RecentFileOperation.DecryptCopySendTo);
        }
        catch (OperationCanceledException)
        {
            StatusText = string.Empty;
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
    private async Task Close()
    {
        previewService.Current.Clear();
        await UserInterfaceService.GoBackAsync(null);
    }

    private bool CanUseCommand()
        => !IsBusy;

    private bool CanEdit()
        => !IsBusy && previewService.Current is { IsReady: true, Kind: PreviewKind.Text };

    private static string CreateMetadataText(IPreviewState state)
    {
        string sizeValue = $"{state.FileSize:N0}".Replace(" ", _nonBreakingSpace, StringComparison.Ordinal);

        return $"{state.ContentType} {sizeValue} bytes";
    }
}
