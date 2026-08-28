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

using Xecrets.Core.Abstractions;
using Xecrets.Core.Models;
using Xecrets.Common.Abstractions;
using Xecrets.Common.Models;
using Xecrets.Mobile.Models.Data;

using Xecrets.Mobile.Models.Abstractions;

namespace Xecrets.Mobile.Models.Services;

public sealed class ProfileService(
    ICoreServices coreServices,
    IXecretsDataStore dataStore,
    IProfileStore profileStore,
    ProfileSession session)
    : IProfileService
{
    public string CurrentEmail => session.Email;

    public bool IsAuthenticated => session.IsAuthenticated;

    public Task<bool> HasProfileAsync()
        => profileStore.HasProfileAsync();

    public Task<SignInKey?> LoadProfileAsync()
        => profileStore.LoadAsync();

    public async Task<ProfileActionResult> CreateProfileAsync(string email, string password)
    {
        if (!coreServices.TryParseEmail(email, out string? parsedEmail) || string.IsNullOrWhiteSpace(parsedEmail))
        {
            return new ProfileActionResult(ProfileActionStatus.InvalidEmail);
        }

        if (await profileStore.HasProfileAsync())
        {
            return new ProfileActionResult(ProfileActionStatus.AlreadyExists);
        }

        KeyPair keyPair = await coreServices.CreateKeyPairAsync(parsedEmail, password, DateTimeOffset.UtcNow);
        await profileStore.SaveAsync(new SignInKey(keyPair.Email, keyPair.CreatedUtc, keyPair.EncryptedBytes));

        return new ProfileActionResult(ProfileActionStatus.Success);
    }

    public async Task<ProfileActionResult> LoginAsync(string password)
    {
        SignInKey? profile = await profileStore.LoadAsync();
        if (profile is null)
        {
            return new ProfileActionResult(ProfileActionStatus.NotFound);
        }

        if (!coreServices.TryLoadKeyPair(profile.ProtectedBytes, [password], out LoadedKeyPair? loadedKeyPair))
        {
            return new ProfileActionResult(ProfileActionStatus.WrongPassword);
        }

        UserSummary user = (await dataStore.GetUsersAsync())[0];
        IUserDataStore userStore = await dataStore.OpenUserAsync(user.Id);
        Identity identity = new(password, [loadedKeyPair.KeyPair]);
        IXecretsProtection protection = new CoreXecretsProtection(coreServices, identity);
        IPersistentData<ExtraCredentials> extraCredentials = await userStore.LoadExtraCredentialsAsync(protection);
        session.SignIn(loadedKeyPair.KeyPair, password, extraCredentials, userStore);
        return new ProfileActionResult(ProfileActionStatus.Success);
    }

    public void SignOut()
        => session.SignOut();

    public Identity GetIdentity()
        => session.CreateIdentity();

    public IReadOnlyList<PasswordUsage> GetExtraPasswords()
        => session.ExtraCredentials!.Value.Passwords;

    public async Task RecordExtraPasswordUseAsync(string password)
    {
        await using IEditScope<ExtraCredentials> credentials = session.ExtraCredentials!.BeginEdit();
        PasswordUsage? entry = credentials.Value.Passwords
            .FirstOrDefault(extraPassword => string.Equals(extraPassword.Password, password, StringComparison.Ordinal));

        if (entry is null)
        {
            if (credentials.Value.Passwords.Count >= 25)
            {
                credentials.Value.Passwords.RemoveRange(24, credentials.Value.Passwords.Count - 24);
            }

            credentials.Value.Passwords.Add(new PasswordUsage { Password = password, UsageCount = 1, });
            return;
        }

        entry.UsageCount++;
        credentials.Value.Passwords =
        [
            .. credentials.Value.Passwords
                .OrderByDescending(extraPassword => extraPassword.UsageCount)
        ];
    }

    public PublicKey GetPublicKey() =>
        session.ProfileKeyPair is not null
            ? session.ProfileKeyPair.PublicKey
            : throw new InvalidOperationException("No authenticated profile is available.");
}
