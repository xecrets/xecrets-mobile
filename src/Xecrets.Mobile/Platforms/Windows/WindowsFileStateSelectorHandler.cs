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

using System.Runtime.Versioning;

using Microsoft.Maui;
using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

using Xecrets.Mobile.Controls;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Platforms.Windows;

// WinUI has no built-in segmented control, and Windows is only a development target, so a row of toggle buttons
// stands in for one.
[SupportedOSPlatform("windows10.0.19041")]
public sealed class WindowsFileStateSelectorHandler() : ViewHandler<FileStateSelector, StackPanel>(Mapper)
{
    public static readonly IPropertyMapper<FileStateSelector, WindowsFileStateSelectorHandler> Mapper =
        new PropertyMapper<FileStateSelector, WindowsFileStateSelectorHandler>(ViewMapper)
        {
            [nameof(FileStateSelector.SelectedState)] = MapSelectedState,
        };

    private readonly ToggleButton _encrypted = new() { Content = MobileTexts.FileStateEncrypted };
    private readonly ToggleButton _decrypted = new() { Content = MobileTexts.FileStateDecrypted };
    private readonly ToggleButton _all = new() { Content = MobileTexts.FileStateAll };

    protected override StackPanel CreatePlatformView()
    {
        StackPanel panel = new() { Orientation = Orientation.Horizontal };
        panel.Children.Add(_encrypted);
        panel.Children.Add(_decrypted);
        panel.Children.Add(_all);
        return panel;
    }

    protected override void ConnectHandler(StackPanel platformView)
    {
        base.ConnectHandler(platformView);
        _encrypted.Click += OnEncryptedClick;
        _decrypted.Click += OnDecryptedClick;
        _all.Click += OnAllClick;
    }

    protected override void DisconnectHandler(StackPanel platformView)
    {
        _encrypted.Click -= OnEncryptedClick;
        _decrypted.Click -= OnDecryptedClick;
        _all.Click -= OnAllClick;
        base.DisconnectHandler(platformView);
    }

    private static void MapSelectedState(WindowsFileStateSelectorHandler handler, FileStateSelector selector) =>
        handler.UpdateButtons();

    private void OnEncryptedClick(object sender, RoutedEventArgs e) => Select(SelectedFileState.Encrypted);

    private void OnDecryptedClick(object sender, RoutedEventArgs e) => Select(SelectedFileState.Decrypted);

    private void OnAllClick(object sender, RoutedEventArgs e) => Select(SelectedFileState.All);

    // Clicking the button that is already checked unchecks it without changing the state, so the buttons are
    // always updated from the state after a click.
    private void Select(SelectedFileState state)
    {
        VirtualView.SelectedState = state;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        _encrypted.IsChecked = VirtualView.SelectedState == SelectedFileState.Encrypted;
        _decrypted.IsChecked = VirtualView.SelectedState == SelectedFileState.Decrypted;
        _all.IsChecked = VirtualView.SelectedState == SelectedFileState.All;
    }
}
