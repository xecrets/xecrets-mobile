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

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Models.PageModels;

public partial class EditPageModel(
    IPreviewService previewService,
    IEditSaveService editSaveService,
    IFlowContext flowContext,
    IUserInterfaceService userInterfaceService)
    : PageModelBase(userInterfaceService), IStatusTextPageModel
{
    [ObservableProperty]
    public partial string FileNameText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MessageText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSaveVisible { get; set; }

    [ObservableProperty]
    public partial bool IsSaveToLocationVisible { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveToLocationCommand))]
    [NotifyCanExecuteChangedFor(nameof(CloseCommand))]
    public partial bool IsBusy { get; set; }

    [RelayCommand]
    private async Task Initialize()
    {
        IPreviewState state = previewService.Current;
        if (!state.IsReady || state.Kind != PreviewKind.Text)
        {
            await UserInterfaceService.GoBackAsync(null);
            return;
        }

        FileNameText = string.IsNullOrWhiteSpace(state.OriginalFileName)
            ? MobileTexts.DisplayNameProgram
            : state.OriginalFileName;
        Text = state.Text;
        ShowSaveCommands();
        StatusText = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private async Task Save()
    {
        IPreviewState state = previewService.Current;
        if (!state.IsReady || state.Kind != PreviewKind.Text || !IsSaveVisible)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusText = string.Empty;

            await editSaveService.SaveAsync(Text);
            await UserInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextFileEncrypted);
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
    private async Task SaveToLocation()
    {
        IPreviewState state = previewService.Current;
        if (!state.IsReady || state.Kind != PreviewKind.Text || !IsSaveToLocationVisible)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusText = string.Empty;

            await editSaveService.SaveCopyAsync(Text);
            ShowSaveCommands();
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
        await UserInterfaceService.GoBackAsync(null);
    }

    private bool CanUseCommand()
        => !IsBusy;

    /// <summary>
    /// Offers to save over the source, which is reached through one of My folders when there is one, and otherwise, for
    /// a file received from another app, only to save a copy.
    /// </summary>
    private void ShowSaveCommands()
    {
        IsSaveVisible = flowContext.Source is not null;
        IsSaveToLocationVisible = !IsSaveVisible;
    }
}
