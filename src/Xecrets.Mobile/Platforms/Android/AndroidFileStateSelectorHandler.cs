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

using Android.Content;

using Google.Android.Material.Button;

using Microsoft.Maui;
using Microsoft.Maui.Handlers;

using Xecrets.Mobile.Controls;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Utilities;

using AndroidView = Android.Views.View;

namespace Xecrets.Mobile.Platforms.Android;

public sealed class AndroidFileStateSelectorHandler()
    : ViewHandler<FileStateSelector, MaterialButtonToggleGroup>(Mapper)
{
    public static readonly IPropertyMapper<FileStateSelector, AndroidFileStateSelectorHandler> Mapper =
        new PropertyMapper<FileStateSelector, AndroidFileStateSelectorHandler>(ViewMapper)
        {
            [nameof(FileStateSelector.SelectedState)] = MapSelectedState,
        };

    private int _encryptedButtonId;
    private int _decryptedButtonId;
    private int _allButtonId;

    protected override MaterialButtonToggleGroup CreatePlatformView()
    {
        MaterialButtonToggleGroup group = new(Context)
        {
            SingleSelection = true,
            SelectionRequired = true,
        };

        MaterialButton encrypted = CreateButton(Context, MobileTexts.FileStateEncrypted);
        MaterialButton decrypted = CreateButton(Context, MobileTexts.FileStateDecrypted);
        MaterialButton all = CreateButton(Context, MobileTexts.FileStateAll);
        _encryptedButtonId = encrypted.Id;
        _decryptedButtonId = decrypted.Id;
        _allButtonId = all.Id;
        group.AddView(encrypted);
        group.AddView(decrypted);
        group.AddView(all);
        return group;
    }

    protected override void ConnectHandler(MaterialButtonToggleGroup platformView)
    {
        base.ConnectHandler(platformView);
        platformView.ButtonChecked += OnButtonChecked;
    }

    protected override void DisconnectHandler(MaterialButtonToggleGroup platformView)
    {
        platformView.ButtonChecked -= OnButtonChecked;
        base.DisconnectHandler(platformView);
    }

    private static MaterialButton CreateButton(Context context, string text) =>
        new(context, null, Resource.Attribute.materialButtonOutlinedStyle)
        {
            Id = AndroidView.GenerateViewId(),
            Text = text,
        };

    private static void MapSelectedState(AndroidFileStateSelectorHandler handler, FileStateSelector selector) =>
        handler.PlatformView.Check(selector.SelectedState switch
        {
            SelectedFileState.Encrypted => handler._encryptedButtonId,
            SelectedFileState.Decrypted => handler._decryptedButtonId,
            SelectedFileState.All => handler._allButtonId,
            _ => throw new InvalidOperationException($"Unknown file state {selector.SelectedState}."),
        });

    private void OnButtonChecked(object? sender, MaterialButtonToggleGroup.ButtonCheckedEventArgs e)
    {
        // The binding does not keep the Java parameter names: P1 is the checked button id and P2 whether it is checked.
        if (!e.P2)
        {
            return;
        }

        VirtualView.SelectedState = e.P1 == _encryptedButtonId
            ? SelectedFileState.Encrypted
            : e.P1 == _decryptedButtonId ? SelectedFileState.Decrypted : SelectedFileState.All;
    }
}
