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

using System;

using Microsoft.Maui;
using Microsoft.Maui.Handlers;

using UIKit;

using Xecrets.Mobile.Controls;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Platforms.Apple;

public sealed class AppleFileStateSelectorHandler() : ViewHandler<FileStateSelector, UISegmentedControl>(Mapper)
{
    public static readonly IPropertyMapper<FileStateSelector, AppleFileStateSelectorHandler> Mapper =
        new PropertyMapper<FileStateSelector, AppleFileStateSelectorHandler>(ViewMapper)
        {
            [nameof(FileStateSelector.SelectedState)] = MapSelectedState,
        };

    protected override UISegmentedControl CreatePlatformView() =>
        new([MobileTexts.FileStateEncrypted, MobileTexts.FileStateDecrypted, MobileTexts.FileStateAll]);

    protected override void ConnectHandler(UISegmentedControl platformView)
    {
        base.ConnectHandler(platformView);
        platformView.ValueChanged += OnValueChanged;
    }

    protected override void DisconnectHandler(UISegmentedControl platformView)
    {
        platformView.ValueChanged -= OnValueChanged;
        base.DisconnectHandler(platformView);
    }

    private static void MapSelectedState(AppleFileStateSelectorHandler handler, FileStateSelector selector) =>
        handler.PlatformView.SelectedSegment = selector.SelectedState switch
        {
            FileState.Encrypted => 0,
            FileState.Decrypted => 1,
            FileState.All => 2,
            _ => throw new InvalidOperationException($"Unknown file state {selector.SelectedState}."),
        };

    private void OnValueChanged(object? sender, EventArgs e) =>
        VirtualView.SelectedState = PlatformView.SelectedSegment switch
        {
            0 => FileState.Encrypted,
            1 => FileState.Decrypted,
            _ => FileState.All,
        };
}
