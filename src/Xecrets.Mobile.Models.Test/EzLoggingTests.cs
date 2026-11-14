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

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Services;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class EzLoggingTests
{
    private string _directory = "";

    [SetUp]
    public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), $"xecrets-mobile-test-{Guid.NewGuid():N}");

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private string LogPath => Path.Combine(_directory, "log.txt");

    [Test]
    public void EntryIsOneLineWithTimeAndAlignedLevel()
    {
        EzLogging log = new(_directory);

        log.Log(EzLogLevel.Information, "First");
        log.Log(EzLogLevel.Error, "Second");

        string[] lines = File.ReadAllLines(LogPath);
        Assert.That(lines, Has.Length.EqualTo(2));
        Assert.That(lines[0], Does.Match(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}Z Information First$"));
        Assert.That(lines[1], Does.Match(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}Z Error       Second$"));
    }

    [TestCase("a\r\nb")]
    [TestCase("a\nb")]
    [TestCase("a\rb")]
    public void LineBreaksAreReplaced(string text)
    {
        EzLogging log = new(_directory);

        log.Log(EzLogLevel.Information, text);

        Assert.That(File.ReadAllLines(LogPath).Single(), Does.EndWith(" a → b"));
    }

    [Test]
    public void InformationIsTheLevelUntilSet()
    {
        EzLogging log = new(_directory);

        log.Log(EzLogLevel.Debug, "debug");
        log.Log(EzLogLevel.Information, "information");

        Assert.That(File.ReadAllLines(LogPath), Has.Length.EqualTo(1));
        Assert.That(File.ReadAllLines(LogPath)[0], Does.EndWith(" information"));
    }

    [TestCase(EzLogLevel.Error, 1)]
    [TestCase(EzLogLevel.Warning, 2)]
    [TestCase(EzLogLevel.Information, 3)]
    [TestCase(EzLogLevel.Debug, 4)]
    public void OnlyTheLevelsIncludedInTheOneSetAreLogged(EzLogLevel level, int expectedCount)
    {
        EzLogging log = new(_directory);
        log.SetLevel(level);

        log.Log(EzLogLevel.Error, "error");
        log.Log(EzLogLevel.Warning, "warning");
        log.Log(EzLogLevel.Information, "information");
        log.Log(EzLogLevel.Debug, "debug");

        Assert.That(File.ReadAllLines(LogPath), Has.Length.EqualTo(expectedCount));
    }

    [Test]
    public void UnknownLevelThrows()
    {
        EzLogging log = new(_directory);

        Assert.That(() => log.SetLevel(EzLogLevel.Unknown), Throws.TypeOf<ArgumentOutOfRangeException>());
        Assert.That(() => log.Log(EzLogLevel.Unknown, "text"), Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void LogIsRotatedBeforeGrowingPastOneMegabyte()
    {
        EzLogging log = new(_directory);
        string text = new('x', 1000);

        // About 1040 bytes a line, so that the first file is full after somewhat more than 1000 lines.
        for (int i = 0; i < 1100; ++i)
        {
            log.Log(EzLogLevel.Information, text);
        }

        Assert.That(new FileInfo(Path.Combine(_directory, "log.1.txt")).Length, Is.LessThanOrEqualTo(1024 * 1024));
        Assert.That(new FileInfo(Path.Combine(_directory, "log.1.txt")).Length, Is.GreaterThan(1024 * 1024 - 1100));
        Assert.That(File.ReadAllLines(LogPath).Length + File.ReadAllLines(Path.Combine(_directory, "log.1.txt")).Length,
            Is.EqualTo(1100));
    }

    [Test]
    public void DeleteAllDeletesOnlyTheLogFiles()
    {
        EzLogging log = new(_directory);
        log.Log(EzLogLevel.Information, new string('x', 600 * 1024));
        log.Log(EzLogLevel.Information, new string('x', 600 * 1024));
        File.WriteAllText(Path.Combine(_directory, "crashlog.txt"), "crash");

        log.DeleteAll();

        Assert.That(Directory.GetFiles(_directory).Select(Path.GetFileName), Is.EqualTo(new[] { "crashlog.txt" }));
    }

    [Test]
    public void DeleteAllRemovesTheFolderWhenNothingElseIsInIt()
    {
        EzLogging log = new(_directory);
        log.Log(EzLogLevel.Information, "text");

        log.DeleteAll();

        Assert.That(Directory.Exists(_directory), Is.False);
    }

    [Test]
    public void DeleteAllWithoutAnyLogDoesNothing()
    {
        EzLogging log = new(_directory);

        log.DeleteAll();

        Assert.That(Directory.Exists(_directory), Is.False);
    }

    [Test]
    public void LoggingAfterDeleteAllStartsANewLog()
    {
        EzLogging log = new(_directory);
        log.Log(EzLogLevel.Information, "before");
        log.DeleteAll();

        log.Log(EzLogLevel.Information, "after");

        Assert.That(File.ReadAllLines(LogPath).Single(), Does.EndWith(" after"));
    }

    [Test]
    public void TheCurrentLogAndTheNineBeforeItAreKept()
    {
        EzLogging log = new(_directory);
        string text = new('x', 600 * 1024);

        // One entry fills more than half a file, so each one after the first rotates.
        for (int i = 0; i < 12; ++i)
        {
            log.Log(EzLogLevel.Information, $"{i} {text}");
        }

        Assert.That(Directory.GetFiles(_directory).Select(Path.GetFileName),
            Is.EquivalentTo((string[])["log.txt", .. Enumerable.Range(1, 9).Select(index => $"log.{index}.txt")]));
        Assert.That(File.ReadAllText(LogPath), Does.Contain(" 11 x"));
        Assert.That(File.ReadAllText(Path.Combine(_directory, "log.9.txt")), Does.Contain(" 2 x"));
    }
}
