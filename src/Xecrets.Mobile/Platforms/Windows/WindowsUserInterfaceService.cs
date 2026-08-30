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
using System.Threading.Tasks;

using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Services;

namespace Xecrets.Mobile.Platforms.Windows;

public class WindowsUserInterfaceService(IBuildInformation buildInformation) : DefaultUserInterfaceService(buildInformation)
{
    // A Toast on Windows is an operating system notification rather than something shown in the app,
    // and it needs the app to be registered with the notification system, which an unpackaged app
    // isn't. Put the message on the page's message line instead.
    public override Task DisplayTransientMessageAsync(string message)
    {
        Page currentPage = Shell.Current.CurrentPage;
        if (currentPage.BindingContext is IStatusTextPageModel pageModel)
        {
            pageModel.MessageText = message;

            IDispatcherTimer timer = currentPage.Dispatcher.CreateTimer();
            timer.Interval = TimeSpan.FromSeconds(3);
            timer.IsRepeating = false;
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                pageModel.MessageText = string.Empty;
            };

            timer.Start();
        }

        return Task.CompletedTask;
    }
}
