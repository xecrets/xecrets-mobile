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

using System.Runtime.ExceptionServices;

using Xecrets.Core.Abstractions;
using Xecrets.Core.Models;
using Xecrets.Common.Abstractions;
using Xecrets.Common.Implementation;
using Xecrets.Common.Models;
using Xecrets.Mobile.Models.Data;

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Models.Services;

internal sealed class ProfileService(
    ICoreServices coreServices,
    IXecretsDataStore dataStore,
    IProfileStore profileStore,
    ProfileSession session,
    IEzLogging log)
    : IProfileService
{
    public string CurrentEmail => session.Email;

    public string CurrentPassword => session.Password;

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

    // As when creating a profile, but the existing key pair is kept, so that everything encrypted with it can still be
    // opened. The old password is kept among the extra passwords, for files encrypted with it.
    public async Task<ProfileActionResult> ChangeProfileAsync(string email, string password)
    {
        if (!coreServices.TryParseEmail(email, out string? parsedEmail) || string.IsNullOrWhiteSpace(parsedEmail))
        {
            return new ProfileActionResult(ProfileActionStatus.InvalidEmail);
        }

        string oldEmail = session.Email;
        string oldPassword = session.Password;
        IUserDataStore userStore = session.UserStore!;

        KeyPair keyPair = await coreServices.ChangeKeyPairAsync(session.ProfileKeyPair!, parsedEmail, password);
        SignInKey oldKey = (await userStore.GetSignInKeysAsync())[0];
        await userStore.ReplaceSignInKeyAsync(
            oldKey,
            new SignInKey(keyPair.Email, oldKey.CreatedUtc, keyPair.EncryptedBytes),
            keyPair.Email,
            keyPair.Email);

        // With the old password too, the private keys encrypted with it are re-encrypted with the new one.
        ProfileActionResult result = await SignInAsync(password, [oldPassword]);
        if (!result.Succeeded)
        {
            return result;
        }

        bool isPasswordChanged = !string.Equals(oldPassword, password, StringComparison.Ordinal);
        string emails = string.Equals(oldEmail, keyPair.Email, StringComparison.Ordinal)
            ? $"for {keyPair.Email}"
            : $"from {oldEmail} to {keyPair.Email}";
        log.Information($"Profile changed {emails}, password {(isPasswordChanged ? "changed" : "unchanged")}");

        if (isPasswordChanged)
        {
            // Always a change, adding the password or counting its use, so the extra credentials are saved and thus
            // protected with the new password. The private keys are already updated with it when signing in.
            await CountExtraPasswordUseAsync(oldPassword, "from profile change");
        }

        return result;
    }

    public Task<ProfileActionResult> LoginAsync(string password)
        => SignInAsync(password, []);

    private async Task<ProfileActionResult> SignInAsync(string password, IReadOnlyList<string> previousPasswords)
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
        IPersistentData<ExtraCredentials> extraCredentials =
            await LoadExtraCredentialsAsync(userStore, password, loadedKeyPair.KeyPair);

        // Signed in only when the private keys are updated, so that it is not if that fails.
        IReadOnlyList<KeyPair> privateKeyPairs = await UpdatePrivateKeysAsync(
            userStore,
            loadedKeyPair.KeyPair,
            password,
            [.. previousPasswords, .. ExtraPasswords(extraCredentials)]);
        session.SignIn(loadedKeyPair.KeyPair, password, extraCredentials, userStore, privateKeyPairs);
        return new ProfileActionResult(ProfileActionStatus.Success);
    }

    // As on the desktop, the old key pair is kept, among the private keys since it was signed in with, but a new one is
    // used from now on. The extra passwords are encrypted with the forgotten password, and are cleared. It's signed out,
    // and the new password signs in.
    public async Task ResetProfileAsync(string password)
    {
        UserSummary user = (await dataStore.GetUsersAsync())[0];
        IUserDataStore userStore = await dataStore.OpenUserAsync(user.Id);
        SignInKey oldKey = (await userStore.GetSignInKeysAsync())[0];

        KeyPair keyPair = await coreServices.CreateKeyPairAsync(oldKey.Email, password, DateTimeOffset.UtcNow);
        await userStore.ReplaceSignInKeyAsync(
            oldKey,
            new SignInKey(keyPair.Email, keyPair.CreatedUtc, keyPair.EncryptedBytes),
            keyPair.Email,
            keyPair.Email);

        // Saved even though unchanged, as the old ones cannot be decrypted, so that they are replaced.
        await using IEditScope<ExtraCredentials> credentials =
            (await LoadExtraCredentialsAsync(userStore, password, keyPair)).BeginEdit();
        credentials.Value.Passwords = [];
        await credentials.SaveAsync(force: true);
        log.Information($"Password reset for {keyPair.Email}, new sign-in key, extra passwords cleared");
    }

    private Task<IPersistentData<ExtraCredentials>> LoadExtraCredentialsAsync(
        IUserDataStore userStore,
        string password,
        KeyPair keyPair) =>
        userStore.LoadExtraCredentialsAsync(
            new CoreXecretsProtection(coreServices, new Identity(password, [keyPair])));

    private static IEnumerable<string> ExtraPasswords(IPersistentData<ExtraCredentials> extraCredentials) =>
        extraCredentials.Value.Passwords.Select(extraPassword => extraPassword.Password);

    private async Task UpdateSessionPrivateKeysAsync() =>
        session.PrivateKeyPairs = await UpdatePrivateKeysAsync(
            session.UserStore!,
            session.ProfileKeyPair!,
            session.Password,
            [.. ExtraPasswords(session.ExtraCredentials!)]);

    // The profile key pair is kept among the private keys, and all of them that the profile password or the other
    // passwords decrypt are re-encrypted with the profile password, and returned to be used for decryption. A key pair
    // kept from before a password reset is thus decrypted once the old password is added, and from then on with the
    // profile password.
    private async Task<IReadOnlyList<KeyPair>> UpdatePrivateKeysAsync(
        IUserDataStore userStore,
        KeyPair profileKeyPair,
        string password,
        IReadOnlyList<string> otherPasswords)
    {
        IPersistentData<PrivateKeyData> privateKeys = await userStore.LoadPrivateKeysAsync();
        string json = JsonFile.Serialize(privateKeys.Value);
        PrivateKeyImportRequest request = new(
            [password, .. otherPasswords],
            password,
            profileKeyPair.Email,
            [profileKeyPair]);

        PrivateKeyImportResult result = await Task.Run(() => coreServices.ImportPrivateKeys(json, request));
        if (result.ReEncryptedAccountsJson != json)
        {
            int keysBefore = CountKeys(privateKeys.Value);
            int keysAfter;
            await using (IEditScope<PrivateKeyData> editablePrivateKeys = privateKeys.BeginEdit())
            {
                editablePrivateKeys.Value.Accounts =
                    JsonFile.Deserialize<PrivateKeyData>(result.ReEncryptedAccountsJson, ExceptionDispatchInfo.Throw)
                        .Accounts;
                keysAfter = CountKeys(editablePrivateKeys.Value);
            }

            string loaded = result.LoadedKeyPairs.Count == 0
                ? "none loaded"
                : $"loaded {string.Join(", ", result.LoadedKeyPairs.Select(keyPair => $"{keyPair.Email} created {keyPair.CreatedUtc:O}"))}";
            log.Information($"Stored private keys updated, {keysBefore} keys before, {keysAfter} after, {loaded}");
        }

        return result.LoadedKeyPairs;
    }

    private static int CountKeys(PrivateKeyData privateKeys) => privateKeys.Accounts.Sum(account => account.Keys.Count);

    public void SignOut()
        => session.SignOut();

    public Identity GetIdentity()
        => session.CreateIdentity();

    public IReadOnlyList<PasswordUsage> GetExtraPasswords()
        => session.ExtraCredentials!.Value.Passwords;

    // As when adding a password, a new one also decrypts any private keys encrypted with it.
    public async Task RecordExtraPasswordUseAsync(string password)
    {
        if (await CountExtraPasswordUseAsync(password, "on use"))
        {
            await UpdateSessionPrivateKeysAsync();
        }
    }

    // Returns true if the password is new among the extra passwords. The cause is logged if it is added.
    private async Task<bool> CountExtraPasswordUseAsync(string password, string cause)
    {
        await using IEditScope<ExtraCredentials> credentials = session.ExtraCredentials!.BeginEdit();

        PasswordUsage? entry = FindExtraPassword(credentials.Value, password);
        if (entry is null)
        {
            AddNewExtraPassword(credentials.Value, password, usageCount: 1, cause);
            return true;
        }

        entry.UsageCount++;
        credentials.Value.Passwords =
        [
            .. credentials.Value.Passwords
                .OrderByDescending(extraPassword => extraPassword.UsageCount)
        ];
        return false;
    }

    // A password added in advance is not counted as used until it decrypts a file. Added, it also decrypts any private
    // keys encrypted with it, such as one kept from before a password reset.
    public async Task AddExtraPasswordAsync(string password)
    {
        await using (IEditScope<ExtraCredentials> credentials = session.ExtraCredentials!.BeginEdit())
        {
            if (FindExtraPassword(credentials.Value, password) is not null)
            {
                return;
            }

            AddNewExtraPassword(credentials.Value, password, usageCount: 0, "in advance");
        }

        await UpdateSessionPrivateKeysAsync();
    }

    public async Task RemoveExtraPasswordAsync(string password)
    {
        await using IEditScope<ExtraCredentials> credentials = session.ExtraCredentials!.BeginEdit();

        credentials.Value.Passwords.RemoveAll(
            extraPassword => string.Equals(extraPassword.Password, password, StringComparison.Ordinal));
        log.Information($"Extra password removed, {credentials.Value.Passwords.Count} in list");
    }

    private static PasswordUsage? FindExtraPassword(ExtraCredentials credentials, string password) =>
        credentials.Passwords
            .FirstOrDefault(extraPassword => string.Equals(extraPassword.Password, password, StringComparison.Ordinal));

    // At most 25 passwords are kept, dropping the least used to make room.
    private void AddNewExtraPassword(ExtraCredentials credentials, string password, int usageCount, string cause)
    {
        if (credentials.Passwords.Count >= 25)
        {
            int droppedCount = credentials.Passwords.Count - 24;
            credentials.Passwords.RemoveRange(24, droppedCount);
            log.Information($"Least used extra passwords dropped: {droppedCount}");
        }

        credentials.Passwords.Add(new PasswordUsage { Password = password, UsageCount = usageCount, });
        log.Information($"Extra password added {cause}, {credentials.Passwords.Count} in list");
    }

    public PublicKey GetPublicKey() =>
        session.ProfileKeyPair is not null
            ? session.ProfileKeyPair.PublicKey
            : throw new InvalidOperationException("No authenticated profile is available.");

    public async Task<bool> ShouldShowAsync(DontShowAgain notice) =>
        (await session.UserStore!.LoadDeviceSettingsAsync()).Value.ShouldShow(notice);

    public async Task SetDontShowAgainAsync(DontShowAgain notice)
    {
        await using IEditScope<ProfileDeviceSettings> settings =
            (await session.UserStore!.LoadDeviceSettingsAsync()).BeginEdit();
        settings.Value.SetDontShowAgain(notice);
    }

    public async Task<TextEditor> GetTextEditorSettingsAsync() =>
        (await session.UserStore!.LoadSettingsAsync()).Value.TextEditor;

    public async Task SetTextEditorFontSizeAsync(double fontSize)
    {
        await using IEditScope<UserSettings> settings = (await session.UserStore!.LoadSettingsAsync()).BeginEdit();
        settings.Value.TextEditor.FontSize = fontSize;
    }

    public async Task SetTextEditorProportionalFontAsync(bool proportionalFont)
    {
        await using IEditScope<UserSettings> settings = (await session.UserStore!.LoadSettingsAsync()).BeginEdit();
        settings.Value.TextEditor.ProportionalFont = proportionalFont;
    }
}
