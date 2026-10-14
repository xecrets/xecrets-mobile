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

using Microsoft.Extensions.DependencyInjection;

using NUnit.Framework;

using Xecrets.Common.Abstractions;
using Xecrets.Common.Implementation;
using Xecrets.Common.Models;
using Xecrets.Core.Abstractions;
using Xecrets.Core.Public;
using Xecrets.Mobile.Models.Data;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Services;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class ProfileServiceTests
{
    private static readonly ICoreServices CoreServices = CreateCoreServices();

    private string _directory = null!;
    private MobileDataStore _store = null!;
    private ProfileSession _session = null!;
    private ProfileService _profileService = null!;

    [SetUp]
    public async Task SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"xecrets-mobile-test-{Guid.NewGuid():N}");
        _store = new MobileDataStore(
            new MobileDataStoreTests.TestFileService(_directory),
            new MobileDataStoreTests.TestCrashLogService(),
            TimeProvider.System,
            new ProtectedPayload());
        _session = new ProfileSession();
        _profileService = new ProfileService(CoreServices, _store, new ProfileStore(_store), _session);

        await _profileService.CreateProfileAsync("old@example.com", "old password");
        await _profileService.LoginAsync("old password");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    [Test]
    public async Task ChangeProfileChangesEmailAndPassword()
    {
        string publicKeyTag = _profileService.GetPublicKey().Tag!;

        ProfileActionResult result = await _profileService.ChangeProfileAsync("new@example.com", "new password");

        Assert.That(result.Status, Is.EqualTo(ProfileActionStatus.Success));
        Assert.That(_profileService.CurrentEmail, Is.EqualTo("new@example.com"));
        Assert.That(_profileService.CurrentPassword, Is.EqualTo("new password"));
        Assert.That((await _store.GetUsersAsync()).Single().Email, Is.EqualTo("new@example.com"));

        _profileService.SignOut();
        Assert.That((await _profileService.LoginAsync("old password")).Status, Is.EqualTo(ProfileActionStatus.WrongPassword));
        Assert.That((await _profileService.LoginAsync("new password")).Status, Is.EqualTo(ProfileActionStatus.Success));
        Assert.That(_profileService.CurrentEmail, Is.EqualTo("new@example.com"));
        Assert.That(_profileService.GetPublicKey().Tag, Is.EqualTo(publicKeyTag));
    }

    [Test]
    public async Task ChangeProfileKeepsOldPasswordAsExtraPassword()
    {
        await _profileService.RecordExtraPasswordUseAsync("extra password");

        await _profileService.ChangeProfileAsync("old@example.com", "new password");
        _profileService.SignOut();
        await _profileService.LoginAsync("new password");

        Assert.That(
            _profileService.GetExtraPasswords().Select(extraPassword => extraPassword.Password),
            Is.EquivalentTo(new[] { "extra password", "old password", }));
    }

    [Test]
    public async Task ChangeProfileWithSamePasswordAddsNoExtraPassword()
    {
        await _profileService.ChangeProfileAsync("new@example.com", "old password");
        _profileService.SignOut();
        await _profileService.LoginAsync("old password");

        Assert.That(_profileService.CurrentEmail, Is.EqualTo("new@example.com"));
        Assert.That(_profileService.GetExtraPasswords(), Is.Empty);
    }

    [Test]
    public async Task ChangeProfileRejectsInvalidEmail()
    {
        ProfileActionResult result = await _profileService.ChangeProfileAsync("not an email", "new password");

        Assert.That(result.Status, Is.EqualTo(ProfileActionStatus.InvalidEmail));
        _profileService.SignOut();
        Assert.That((await _profileService.LoginAsync("old password")).Status, Is.EqualTo(ProfileActionStatus.Success));
        Assert.That(_profileService.CurrentEmail, Is.EqualTo("old@example.com"));
    }

    private static ICoreServices CreateCoreServices()
    {
        ServiceCollection services = new();
        services.AddXecretsCore();
        return services.BuildServiceProvider().GetRequiredService<ICoreServices>();
    }
}
