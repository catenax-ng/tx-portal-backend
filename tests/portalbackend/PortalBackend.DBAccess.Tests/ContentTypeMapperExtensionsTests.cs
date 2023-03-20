using System.Reflection.Metadata;
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Extensions;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;

namespace Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Tests;

public class ContentTypeMapperExtensionsTests
{
    [Theory]
    [InlineData(DocumentMediaTypeId.GIF, "image/gif")]
    [InlineData(DocumentMediaTypeId.PDF, "application/pdf")]
    [InlineData(DocumentMediaTypeId.PNG, "image/png")]
    [InlineData(DocumentMediaTypeId.SVG, "image/svg+xml")]
    [InlineData(DocumentMediaTypeId.JSON, "application/json")]
    [InlineData(DocumentMediaTypeId.JPEG, "image/jpeg")]
    [InlineData(DocumentMediaTypeId.TIFF, "image/tiff")]
    public void MapToMediaType_WithValid_ReturnsExpected(DocumentMediaTypeId mediaTypeId, string result)
    {
        var mediaType = mediaTypeId.MapToMediaType();
        mediaType.Should().Be(result);
    }
    
    [Fact]
    public void MapToMediaType_WithInvalid_ThrowsConflictException()
    {
        void Act() => ((DocumentMediaTypeId)666).MapToMediaType();

        var ex = Assert.Throws<ConflictException>((Action) Act);
        ex.Message.Should().Be($"document mimetype 666 is not supported");
    }

    [Theory]
    [InlineData(DocumentMediaTypeId.GIF, "image/gif")]
    [InlineData(DocumentMediaTypeId.PDF, "application/pdf")]
    [InlineData(DocumentMediaTypeId.PNG, "image/png")]
    [InlineData(DocumentMediaTypeId.SVG, "image/svg+xml")]
    [InlineData(DocumentMediaTypeId.JSON, "application/json")]
    [InlineData(DocumentMediaTypeId.JPEG, "image/jpeg")]
    [InlineData(DocumentMediaTypeId.TIFF, "image/tiff")]
    public void MapToDocumentMediaType_WithValid_ReturnsExpected(DocumentMediaTypeId expectedResult, string mediaType)
    {
        var result = mediaType.MapToDocumentMediaType();
        result.Should().Be(expectedResult);
    }

    [Fact]
    public void MapToDocumentMediaType_WithInvalid_ThrowsUnsupportedMediaTypeException()
    {
        void Act() => "just a test".MapToDocumentMediaType();

        var ex = Assert.Throws<UnsupportedMediaTypeException>((Action) Act);
        ex.Message.Should().Be($"mimeType just a test is not supported");
    }
}