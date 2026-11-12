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

using Xecrets.Core.Models;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Services;

namespace Xecrets.Mobile.Models.Test;

/// <summary>
/// How a decrypted file is shown, from what its contents were classified as and whether it is kept in memory.
/// </summary>
[TestFixture]
public sealed class PreviewInfoFactoryTests
{
    private const string _handoffPath = "/cache/handoff/file";

    [Test]
    public void TextKeptInMemoryIsShownAsText()
    {
        DecryptedFileInfo file = PreviewInfoFactory.CreateInfo("", "small.txt", FileCategory.Text, 4, FileTimes.UtcNow);

        Assert.That(file.ContentType, Is.EqualTo("text/plain"));
        Assert.That(file.Kind, Is.EqualTo(PreviewKind.Text));
        Assert.That(file.FileSize, Is.EqualTo(4));
    }

    /// <summary>
    /// Text over the size cap is written to a file. It is still text, but is left to another app to show.
    /// </summary>
    [Test]
    public void TextWrittenToAFileIsShownExternally()
    {
        DecryptedFileInfo file = PreviewInfoFactory.CreateInfo(
            _handoffPath, "large.txt", FileCategory.Text, PreviewInfoFactory.MaxInlineTextSizeBytes + 1, FileTimes.UtcNow);

        Assert.That(file.ContentType, Is.EqualTo("text/plain"));
        Assert.That(file.Kind, Is.EqualTo(PreviewKind.External));
    }

    [TestCase("notes.xyz")]
    [TestCase("README")]
    public void TextWithoutARecognizedExtensionIsPlainText(string fileName)
    {
        DecryptedFileInfo file = PreviewInfoFactory.CreateInfo("", fileName, FileCategory.Text, 4, FileTimes.UtcNow);

        Assert.That(file.ContentType, Is.EqualTo("text/plain"));
        Assert.That(file.Kind, Is.EqualTo(PreviewKind.Text));
    }

    [Test]
    public void TextOfARecognizedTypeKeepsItsContentType()
    {
        DecryptedFileInfo file = PreviewInfoFactory.CreateInfo("", "data.json", FileCategory.Text, 4, FileTimes.UtcNow);

        Assert.That(file.ContentType, Is.EqualTo("application/json"));
        Assert.That(file.Kind, Is.EqualTo(PreviewKind.Text));
    }

    [Test]
    public void ImageIsShownAsAnImage()
    {
        DecryptedFileInfo file = PreviewInfoFactory.CreateInfo(_handoffPath, "photo.png", FileCategory.Image, 4, FileTimes.UtcNow);

        Assert.That(file.ContentType, Is.EqualTo("image/png"));
        Assert.That(file.Kind, Is.EqualTo(PreviewKind.Image));
    }

    [TestCase("document.pdf", FileCategory.Pdf, "application/pdf")]
    [TestCase("report.docx", FileCategory.Other, "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [TestCase("blob.xyz", FileCategory.Other, "application/octet-stream")]
    [TestCase("notes.txt", FileCategory.Other, "text/plain")]
    public void AnythingElseIsShownExternally(string fileName, FileCategory category, string contentType)
    {
        DecryptedFileInfo file = PreviewInfoFactory.CreateInfo(_handoffPath, fileName, category, 4, FileTimes.UtcNow);

        Assert.That(file.ContentType, Is.EqualTo(contentType));
        Assert.That(file.Kind, Is.EqualTo(PreviewKind.External));
    }
}
