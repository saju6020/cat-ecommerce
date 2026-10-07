using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Domain.Tests.Common;

/// <summary>HO-FIL-001 §8.6, backing I7.</summary>
public sealed class FileServingRulesTests
{
    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    [InlineData("video/mp4")]
    [InlineData("application/pdf")]
    public void Inline_eligible_types_honour_an_inline_request(string contentType)
    {
        Assert.Equal(FileServingRules.Inline, FileServingRules.ResolveDisposition(contentType, "inline"));
    }

    [Theory]
    [InlineData("application/dicom")]
    [InlineData("application/zip")]
    public void Dicom_and_zip_are_always_attachment_even_when_inline_is_requested(string contentType)
    {
        Assert.Equal(FileServingRules.Attachment, FileServingRules.ResolveDisposition(contentType, "inline"));
    }

    [Fact]
    public void An_unrecognised_requested_disposition_resolves_to_attachment()
    {
        Assert.Equal(FileServingRules.Attachment, FileServingRules.ResolveDisposition("image/png", "banana"));
    }

    [Fact]
    public void No_disposition_requested_resolves_to_attachment_even_for_an_inline_eligible_type()
    {
        Assert.Equal(FileServingRules.Attachment, FileServingRules.ResolveDisposition("image/png", "attachment"));
    }
}
