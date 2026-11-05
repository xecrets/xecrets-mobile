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

using System.Text;

using NUnit.Framework;

using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Services;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class PreviewServiceTests
{
    private string _directory = "";

    [SetUp]
    public void SetUp() => _directory = Directory.CreateTempSubdirectory().FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    /// <summary>
    /// The decrypted file keeps the times of the original, so that they can be restored when it is saved.
    /// The fake decryption tells the Unix epoch for all of them.
    /// </summary>
    [Test]
    public async Task DecryptedFileKeepsTheTimesOfTheOriginal()
    {
        string encryptedPath = Path.Combine(_directory, "x.axx");
        await File.WriteAllBytesAsync(encryptedPath, Encoding.UTF8.GetBytes("ENC:a.txt\ntext"));
        using FakeTransientFileService transient = new();
        PreviewState state = new();
        PreviewService preview = new(
            new FakeCoreServices(),
            new FakeProfileService(),
            transient,
            state,
            new DecryptionPasswordRequestState(),
            new FakeCrashLogService());

        bool prepared = await preview.PrepareAsync(new LocalReadableFile(encryptedPath, "x.axx"));

        Assert.That(prepared, Is.True);
        Assert.That(state.File!.OriginalTimes,
            Is.EqualTo(new FileTimes(DateTime.UnixEpoch, DateTime.UnixEpoch, DateTime.UnixEpoch)));
    }
}
