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

namespace Xecrets.Mobile.Models.PageModels;

/// <summary>
/// The font of the text in the view and edit pages, which is the user's text editor preference.
/// </summary>
public partial class TextEditorFontModel(IProfileService profileService) : ObservableObject
{
    private const double DefaultFontSize = 16;

    private const double FontSizeStep = 2;

    private const double MinFontSize = 10;

    private const double MaxFontSize = 32;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DecreaseFontSizeCommand), nameof(IncreaseFontSizeCommand))]
    public partial double FontSize { get; set; } = DefaultFontSize;

    [ObservableProperty]
    public partial bool IsProportionalFont { get; set; }

    public async Task LoadAsync()
    {
        TextEditor settings = await profileService.GetTextEditorSettingsAsync();
        FontSize = Math.Clamp(settings.FontSize ?? DefaultFontSize, MinFontSize, MaxFontSize);
        IsProportionalFont = settings.ProportionalFont == true;
    }

    [RelayCommand(CanExecute = nameof(CanDecreaseFontSize))]
    private Task DecreaseFontSize() => SetFontSizeAsync(FontSize - FontSizeStep);

    private bool CanDecreaseFontSize() => FontSize - FontSizeStep >= MinFontSize;

    [RelayCommand(CanExecute = nameof(CanIncreaseFontSize))]
    private Task IncreaseFontSize() => SetFontSizeAsync(FontSize + FontSizeStep);

    private bool CanIncreaseFontSize() => FontSize + FontSizeStep <= MaxFontSize;

    [RelayCommand]
    private async Task ToggleProportionalFont()
    {
        IsProportionalFont = !IsProportionalFont;
        await profileService.SetTextEditorProportionalFontAsync(IsProportionalFont);
    }

    private async Task SetFontSizeAsync(double fontSize)
    {
        FontSize = fontSize;
        await profileService.SetTextEditorFontSizeAsync(fontSize);
    }
}
