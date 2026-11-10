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

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class ModelExtensionsTests
{
    [TestCase("a-txt.axx (1)", "a-txt.axx")]
    [TestCase("photo.jpg (12)", "photo.jpg")]
    [TestCase("a.tar.gz (2)", "a.tar.gz")]
    [TestCase("a-txt.axx", "a-txt.axx")]
    [TestCase("a (1).txt", "a (1).txt")]
    [TestCase("report (1)", "report (1)")]
    [TestCase("a-txt.axx(1)", "a-txt.axx(1)")]
    [TestCase("a-txt.axx (x)", "a-txt.axx (x)")]
    public void NumberAfterTheExtensionIsNotPartOfTheName(string name, string expected)
    {
        Assert.That(name.WithoutCopyNumber(), Is.EqualTo(expected));
    }

    [TestCase("a-txt.axx", true)]
    [TestCase("a-txt.axx (1)", true)]
    [TestCase("A-TXT.AXX (3)", true)]
    [TestCase("a.txt (1)", false)]
    [TestCase("a.axx.txt", false)]
    [TestCase("axx (1)", false)]
    public void NameTellsIfTheFileIsEncrypted(string name, bool expected)
    {
        Assert.That(name.IsNameEncrypted(), Is.EqualTo(expected));
    }

    [Test]
    public void ContentTypeIsTheSameWithANumberAfterTheExtension()
    {
        Assert.That("photo.jpg (1)".DetectContentType(), Is.EqualTo("image/jpeg"));
    }
}
