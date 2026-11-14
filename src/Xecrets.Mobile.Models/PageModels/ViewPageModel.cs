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

/// <summary>
/// The page model of the page that shows the decrypted file being previewed in the app, as text or as an image.
/// </summary>
public partial class ViewPageModel(
    IPreviewService previewService,
    IFlowContext flowContext,
    IProfileSettingsService profileSettingsService,
    IUserInterfaceService userInterfaceService)
    : PageModelBase(userInterfaceService), IStatusTextPageModel, IBreadcrumbPageModel, IPageInformationPageModel
{
    /// <inheritdoc/>
    public string Heading => MobileTexts.ViewContentTitle;

    /// <inheritdoc/>
    public string Information => MobileTexts.ViewDescription;

    /// <summary>
    /// The font of the text.
    /// </summary>
    public TextEditorFontModel Font { get; } = new(profileSettingsService);

    // Once only, since the page appears again when returned to.
    private bool _initialized;

    /// <inheritdoc/>
    public string Breadcrumb => BreadcrumbFormatter.Format(
        flowContext.Origin,
        MobileTexts.BreadcrumbPreview,
        MobileTexts.BreadcrumbView);

    /// <summary>
    /// The name of the file, before it was encrypted.
    /// </summary>
    [ObservableProperty]
    public partial string FileNameText { get; set; } = string.Empty;

    /// <summary>
    /// The type and size of the file.
    /// </summary>
    [ObservableProperty]
    public partial string MetadataText { get; set; } = string.Empty;

    /// <inheritdoc/>
    [ObservableProperty]
    public partial string MessageText { get; set; } = string.Empty;

    /// <inheritdoc/>
    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    /// <summary>
    /// The text shown, or "" when an image is shown.
    /// </summary>
    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    /// <summary>
    /// The full path of the image shown, or "" when text is shown.
    /// </summary>
    [ObservableProperty]
    public partial string ImageFilePath { get; set; } = string.Empty;

    /// <summary>
    /// True if an image is shown, false otherwise.
    /// </summary>
    [ObservableProperty]
    public partial bool IsImageVisible { get; set; }

    /// <summary>
    /// True if text is shown, false otherwise.
    /// </summary>
    [ObservableProperty]
    public partial bool IsTextVisible { get; set; }

    [RelayCommand]
    private async Task Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        IPreviewState state = previewService.Current;
        if (state.File.Kind != PreviewKind.Image && state.File.Kind != PreviewKind.Text)
        {
            await UserInterfaceService.GoBackAsync(null);
            return;
        }

        FileNameText = state.File.OriginalFileName;
        MetadataText = PreviewMetadataFormatter.Format(state);
        ImageFilePath = state.File.Kind == PreviewKind.Image
            ? state.File.FilePath
            : string.Empty;
        Text = state.Text;
        IsImageVisible = state.File.Kind == PreviewKind.Image;
        IsTextVisible = state.File.Kind == PreviewKind.Text;
        await Font.LoadAsync();
    }
}
