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

using System.Security.Cryptography;
using System.Text;

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
    : PageModelBase(userInterfaceService), IStatusTextPageModel, IBreadcrumbPageModel
{
    // A hash rather than a copy of the saved text, so that no more copies of the plaintext are kept than needed.
    private byte[] _savedTextHash = [];

    public string Breadcrumb => BreadcrumbFormatter.Format(
        flowContext.Origin,
        MobileTexts.BreadcrumbPreview,
        MobileTexts.BreadcrumbEdit);

    [ObservableProperty]
    public partial string FileNameText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MetadataText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MessageText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsBusy { get; set; }

    public bool HasUnsavedChanges => !HashText(Text).SequenceEqual(_savedTextHash);

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
        MetadataText = PreviewMetadataFormatter.Format(state);
        Text = state.Text;
        _savedTextHash = HashText(Text);
        StatusText = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private async Task Save()
    {
        if (!HasUnsavedChanges)
        {
            return;
        }

        await TrySaveAsync();
    }

    /// <summary>
    /// Asks whether to save unsaved changes, and returns whether to leave the editor.
    /// </summary>
    public async Task<bool> CanLeaveAsync()
    {
        if (!HasUnsavedChanges)
        {
            return true;
        }

        SaveChangesChoice choice = await UserInterfaceService.DisplaySaveChangesAsync(MobileTexts.DialogTextSaveChanges);
        return choice switch
        {
            SaveChangesChoice.Save => await TrySaveAsync(),
            SaveChangesChoice.Discard => true,
            SaveChangesChoice.Cancel => false,
            _ => throw new ArgumentOutOfRangeException(nameof(choice)),
        };
    }

    private bool CanUseCommand()
        => !IsBusy;

    /// <summary>
    /// Saves over the source, which is reached through one of My folders when there is one, and otherwise, for a file
    /// received from another app, saves a copy. Returns whether the text was saved.
    /// </summary>
    private async Task<bool> TrySaveAsync()
    {
        IPreviewState state = previewService.Current;
        if (!state.IsReady || state.Kind != PreviewKind.Text)
        {
            return false;
        }

        string text = Text;
        try
        {
            IsBusy = true;
            StatusText = string.Empty;

            if (flowContext.Source is null)
            {
                if (!await editSaveService.SaveCopyAsync(text))
                {
                    return false;
                }
            }
            else
            {
                await editSaveService.SaveAsync(text);
                await UserInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextFileEncrypted);
            }

            _savedTextHash = HashText(text);
            MetadataText = PreviewMetadataFormatter.Format(state);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            StatusText = ex.FormatException();
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static byte[] HashText(string text) => SHA256.HashData(Encoding.UTF8.GetBytes(text));
}
