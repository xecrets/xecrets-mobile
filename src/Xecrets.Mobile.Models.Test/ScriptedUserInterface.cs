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

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Models.Test;

/// <summary>
/// Answers the confirmations in turn, and no to any beyond those, and records what the user is shown.
/// </summary>
internal sealed class ScriptedUserInterface : IUserInterfaceService
{
    public Queue<bool> Answers { get; } = new();

    public List<string> Confirmations { get; } = [];

    public List<string> Messages { get; } = [];

    public List<string> TransientMessages { get; } = [];

    public List<AppDestination> Destinations { get; } = [];

    public bool IsShellAvailable => true;
    public bool CanProcessIncomingFiles => true;
    public bool CanReceiveIncomingFiles => true;
    public IReadOnlyDictionary<string, string> IconMap => throw new NotSupportedException();
    public Task InvokeOnMainThreadAsync(Func<Task> action) => action();

    public Task DisplayMessageAsync(string message)
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }

    public Task<bool> DisplayConfirmationAsync(string message)
    {
        Confirmations.Add(message);
        return Task.FromResult(Answers.Count > 0 && Answers.Dequeue());
    }

    public Task<string?> DisplayPromptAsync(string message, string initialValue) => throw new NotSupportedException();

    public Task DisplayTransientMessageAsync(string message)
    {
        TransientMessages.Add(message);
        return Task.CompletedTask;
    }

    public Task NavigateToAsync(AppDestination destination)
    {
        Destinations.Add(destination);
        return Task.CompletedTask;
    }

    public Task NavigateToAsync(AppDestination destination, object parameter) => NavigateToAsync(destination);
    public Task GoBackAsync(object? parameter) => throw new NotSupportedException();
    public Task OpenBrowserAsync(string url) => throw new NotSupportedException();
    public Task SetClipboardTextAsync(string text) => throw new NotSupportedException();
}
