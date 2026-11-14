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

using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

using Microsoft.Maui;
using Microsoft.Maui.Controls;

using Xecrets.Mobile.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.PageModels;
using Xecrets.Mobile.Utilities;

namespace Xecrets.Mobile.Pages;

/// <summary>
/// The page of the decryption passwords.
/// </summary>
internal partial class EnterPasswordPage : IQueryAttributable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EnterPasswordPage"/> class with the standard header.
    /// </summary>
    public EnterPasswordPage(
        EnterPasswordPageModel model,
        IPlatformServices platformServices,
        IPageHeaderService pageHeaderService)
        : base(model)
    {
        InitializeComponent();
        model.PropertyChanged += OnModelPropertyChanged;
        platformServices.ConfigurePasswordEntry(PasswordEntryControl.Entry, PasswordEntryPurpose.VerifyPassword);
        pageHeaderService.ApplyStandardHeader(this);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Requires a <see cref="DecryptionPasswordsPayload"/> payload.
    /// </remarks>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        Model.Initialize((DecryptionPasswordsPayload)query[nameof(NavigationParameter.Payload)]);
    }

    // Pressing Go keeps the keyboard open, also when the add form it was typed into is closed, covering the list.
    private async void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        bool isAddFormClosed = e.PropertyName == nameof(EnterPasswordPageModel.IsAdding) && !Model.IsAdding;
        Entry entry = PasswordEntryControl.Entry;
        if (isAddFormClosed && entry.IsSoftInputShowing())
        {
            await entry.HideSoftInputAsync(CancellationToken.None);
        }
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        UpdateCenteredContentLayout(width);
    }

    private void UpdateCenteredContentLayout(double pageWidth)
    {
        LayoutMetrics.UpdateCenteredFormLayout(pageWidth, ContentRoot, Card, ActionButtonStack);
    }
}
