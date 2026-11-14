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

using Xecrets.Core.Models;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Models.Services;

/// <summary>
/// The operation that a page runs, on files or on the profile, which the user may cancel by leaving the page while it
/// runs. One runs at a time, since a page does not start another while it is busy, and the user cannot leave it without
/// being asked. The operation is canceled where it can stop without leaving a file of the user changed in part: between
/// the files, and while a file is encrypted or decrypted to a temporary file, but not while a result is saved, or an
/// original wiped. An operation that cannot be canceled at all, such as one on the profile, is not left until it ends.
/// </summary>
public sealed class RunningOperation(IUserInterfaceService userInterfaceService)
{
    private Scope? _current;

    // The user's answer to whether to cancel, while the user is asked.
    private Task _confirmation = Task.CompletedTask;

    /// <summary>
    /// An operation begins or ends.
    /// </summary>
    public event EventHandler? IsRunningChanged;

    /// <summary>
    /// True if an operation runs, false otherwise.
    /// </summary>
    public bool IsRunning => _current is not null;

    /// <summary>
    /// True if an operation runs and can be canceled, false otherwise.
    /// </summary>
    public bool IsCancelable => _current is { IsCancelable: true };

    /// <summary>
    /// Begins the operation of the page shown.
    /// </summary>
    /// <param name="cancelable">True if the operation can be canceled, false otherwise.</param>
    /// <returns>A scope that ends the operation when it is disposed.</returns>
    public IDisposable Begin(bool cancelable = true)
    {
        _current = new Scope(this, userInterfaceService.CurrentLocation, cancelable);
        IsRunningChanged?.Invoke(this, EventArgs.Empty);
        return _current;
    }

    /// <summary>
    /// Tells if an operation runs on the page at a location.
    /// </summary>
    /// <param name="location">The location of the page, as <see cref="IUserInterfaceService.CurrentLocation"/>
    /// tells it.</param>
    /// <returns>True if the operation running began on the page, false otherwise.</returns>
    public bool IsRunningAt(string location) => _current?.Location == location;

    /// <summary>
    /// Asks the user whether to cancel the operation, and cancels it when the user confirms. The operation may have
    /// ended while the user was asked.
    /// </summary>
    public Task ConfirmCancelAsync() => _confirmation = AskToCancelAsync();

    private async Task AskToCancelAsync()
    {
        if (await userInterfaceService.DisplayConfirmationAsync(
                MobileTexts.DialogTextConfirmCancelOperation,
                MobileTexts.LabelYes,
                MobileTexts.LabelNo))
        {
            _current?.Cancel();
        }
    }

    /// <summary>
    /// Throws if the operation is canceled.
    /// </summary>
    /// <exception cref="OperationCanceledException">The operation is canceled.</exception>
    public void ThrowIfCanceled() => Token.ThrowIfCancellationRequested();

    /// <summary>
    /// Waits for the answer of the user while the user is asked whether to cancel, and then throws if the operation is
    /// canceled. The operation goes on while the user is asked, so it waits here before it asks the user anything
    /// itself, such as for a password, or starts another file.
    /// </summary>
    /// <exception cref="OperationCanceledException">The operation is canceled.</exception>
    public async Task ThrowIfCanceledAsync()
    {
        await _confirmation;
        ThrowIfCanceled();
    }

    /// <summary>
    /// Creates the progress for encrypting or decrypting a file, which cancels it when the operation is canceled.
    /// </summary>
    /// <returns>The progress, which never cancels if no operation runs.</returns>
    public IProgress<Progress> CreateProgress() => new CancelingProgress(Token);

    private CancellationToken Token => _current?.Token ?? CancellationToken.None;

    private void End()
    {
        _current = null;
        IsRunningChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class Scope(RunningOperation owner, string location, bool isCancelable) : IDisposable
    {
        private readonly CancellationTokenSource _cancellation = new();

        public string Location { get; } = location;

        public bool IsCancelable { get; } = isCancelable;

        public CancellationToken Token => _cancellation.Token;

        public void Cancel() => _cancellation.Cancel();

        public void Dispose()
        {
            owner.End();
            _cancellation.Dispose();
        }
    }
}
