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

using NUnit.Framework;

using Xecrets.Common.Implementation;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Data;
using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class MobileDeviceSettingsTests
{
    private string _directory = null!;
    private MobileDataStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"xecrets-mobile-test-{Guid.NewGuid():N}");
        _store = new MobileDataStore(new TestFileService(_directory), new TestCrashLogService(), TimeProvider.System, new ProtectedPayload());
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
    public void SetValueIsPersistedAndRestored()
    {
        MobileDeviceSettings deviceSettings = new(_store);

        deviceSettings["key"] = "value";

        Assert.That(new MobileDeviceSettings(_store)["key"], Is.EqualTo("value"));
    }

    [Test]
    public void MissingValueIsEmpty()
    {
        MobileDeviceSettings deviceSettings = new(_store);

        Assert.That(deviceSettings["missing"], Is.Empty);
    }

    [Test]
    public async Task ClearIsPersisted()
    {
        MobileDeviceSettings deviceSettings = new(_store);
        deviceSettings["key"] = "value";

        deviceSettings.Clear();

        Assert.That(deviceSettings["key"], Is.Empty);
        Assert.That((await _store.OpenApplicationSettingsAsync()).Value.DeviceSettings.Values, Is.Empty);
    }

    private sealed class TestCrashLogService : ICrashLogService
    {
        public bool HasPendingCrashLog => false;

        public void RegisterHandlers()
        {
        }

        public string ReadCurrent() => string.Empty;

        public void WriteCrashLog(string source, object? crash)
        {
        }
    }

    private sealed class TestFileService(string directory) : IFileService
    {
        public string PlatformId => "test";
        public string AppDataDirectory => directory;
        public string CacheDirectory => directory;
        public Task<bool> OpenInAsync(string filePath, string displayName) => throw new NotSupportedException();
        public Task SendToAsync(string filePath, string displayName, string contentType) => throw new NotSupportedException();
        public Task<bool> CanViewFileAsync(DecryptedFileInfo file) => throw new NotSupportedException();
        public Task ViewFileAsync(DecryptedFileInfo file) => throw new NotSupportedException();
        public bool IsSelfHandoffReference(string reference) => false;
    }
}
