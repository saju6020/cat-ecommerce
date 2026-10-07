using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using CatEcommerce.Shared.Common.Constants;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// TEST gate, R16: a persisted regression suite for the SECURITY gate's own ~250-request
/// malformed-multipart probe (no boundary, an over-length boundary, a truncated body, too many
/// part headers, an oversized part header, a non-multipart body) -- proving each maps to 400,
/// never an unhandled 500. Regression-verified 2026-09-27 by disabling each catch in turn and
/// confirming the specific test(s) it backs turn into an unhandled 500 with no effect on the
/// others, then restoring it:
/// <list type="bullet">
/// <item>No boundary / over-length boundary: <c>FilesController.GetBoundary</c>'s own
/// <c>InvalidDataException</c>, caught by <c>UploadAsync</c>'s first, narrow try/catch around
/// that call.</item>
/// <item>Too many part headers / an oversized part header: <see cref="Microsoft.AspNetCore.WebUtilities.MultipartReader"/>'s
/// own <c>HeadersCountLimit</c>/<c>HeadersLengthLimit</c> checks, thrown as
/// <c>InvalidDataException</c> (not <c>BadHttpRequestException</c> -- that type is Kestrel's own,
/// and <c>MultipartReader</c> has no dependency on Kestrel) and caught by <c>UploadAsync</c>'s
/// second try/catch, around the section-reading loop.</item>
/// <item>Truncated body: a genuinely ambiguous, mid-section end of a chunked-transfer connection
/// (forced via a non-seekable stream, below) makes <c>MultipartReaderStream</c> throw plain
/// <c>IOException("Unexpected end of Stream")</c> -- a different exception, and a different,
/// inner try/catch, from either case above: the one inside <c>ProcessFileSectionAsync</c> around
/// the temp-file write window (originally added for the antivirus-quarantine case).</item>
/// <item>A JSON body instead of multipart: no exception at all -- <c>UploadAsync</c>'s own
/// leading content-type check returns 400 directly.</item>
/// </list>
/// A body over Kestrel's own <c>MaxRequestBodySize</c> (-&gt; 413) is not re-probed here:
/// <c>TestServer</c> does not enforce that limit at all, so it cannot be constructed as an
/// integration test. It exercises the same, single line of mapping code
/// (<c>return StatusCode(ex.StatusCode, ...)</c> in <c>UploadAsync</c>'s own
/// <c>catch (BadHttpRequestException ex)</c>) that a real <see cref="BadHttpRequestException"/>
/// would reach for any Kestrel-level rejection; the SECURITY gate's own live Kestrel run already
/// confirmed the 413 case directly (its probe notes: "a 61 MB non-file preamble -&gt; 413
/// FileRejected (it was a 500)").
/// </summary>
public sealed class FilesMalformedMultipartEndpointTests(FilesApiFactory factory) : FilesIntegrationTestBase(factory)
{
    [Fact]
    public async Task Upload_with_no_multipart_boundary_returns_400_not_500()
    {
        using var client = await SignedInStaffClientAsync(StaffRoles.Administrator);

        using var content = new ByteArrayContent("garbage body, no boundary at all"u8.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("multipart/form-data"); // no boundary parameter

        var response = await client.PostAsync("/api/staff/files?purpose=ProductImage", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertUniformFileRejectedBodyAsync(response);
    }

    [Fact]
    public async Task Upload_with_a_boundary_over_128_characters_returns_400_not_500()
    {
        using var client = await SignedInStaffClientAsync(StaffRoles.Administrator);

        var boundary = new string('b', 200);
        using var content = new ByteArrayContent("irrelevant"u8.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("multipart/form-data") { Parameters = { new NameValueHeaderValue("boundary", $"\"{boundary}\"") } };

        var response = await client.PostAsync("/api/staff/files?purpose=ProductImage", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertUniformFileRejectedBodyAsync(response);
    }

    // A ByteArrayContent's own Content-Length always matches exactly what it sends, so
    // MultipartReaderStream's boundary lookahead always reaches a clean, unambiguous EOF against
    // it -- there is no way to reproduce a genuinely truncated connection (bytes promised by
    // Content-Length that never arrive) that way. This wraps the same, deliberately incomplete
    // multipart bytes (no closing boundary, ending mid-way through what could be one) in a
    // non-seekable stream, forcing chunked transfer encoding (no Content-Length at all) -- when
    // the chunked stream ends, MultipartReaderStream cannot tell whether more bytes were meant to
    // follow, which is exactly what makes it throw IOException("Unexpected end of Stream") rather
    // than report a clean, short end of body.
    [Fact]
    public async Task Upload_with_a_truncated_body_returns_400_not_500()
    {
        using var client = await SignedInStaffClientAsync(StaffRoles.Administrator);

        const string boundary = "test-boundary-truncated";
        var body = new StringBuilder()
            .Append($"--{boundary}\r\n")
            .Append("Content-Disposition: form-data; name=\"file\"; filename=\"cat.png\"\r\n")
            .Append("Content-Type: image/png\r\n")
            .Append("\r\n")
            .Append("some partial file bytes\r\n")
            .Append($"--{boundary.Substring(0, boundary.Length - 1)}") // one character short of a valid boundary line, then nothing
            .ToString();

        using var content = new NonSeekableStreamContent(Encoding.ASCII.GetBytes(body));
        content.Headers.ContentType = new MediaTypeHeaderValue("multipart/form-data") { Parameters = { new NameValueHeaderValue("boundary", boundary) } };

        var response = await client.PostAsync("/api/staff/files?purpose=ProductImage", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertUniformFileRejectedBodyAsync(response);
    }

    /// <summary>Forces chunked transfer encoding (no upfront Content-Length) by reporting
    /// <see cref="Stream.CanSeek"/> as <c>false</c> on the underlying stream -- <see cref="StreamContent"/>
    /// otherwise reads a seekable stream's own <see cref="Stream.Length"/> to set Content-Length,
    /// which would let the server always read exactly as many bytes as promised, unable to ever see
    /// a genuinely ambiguous, mid-lookahead end of stream.</summary>
    private sealed class NonSeekableStreamContent(byte[] bytes) : StreamContent(new NonSeekableMemoryStream(bytes))
    {
        private sealed class NonSeekableMemoryStream(byte[] bytes) : MemoryStream(bytes)
        {
            public override bool CanSeek => false;
        }
    }

    // Confirms the mapping FilesController.UploadAsync's `catch (BadHttpRequestException ex)`
    // performs (`StatusCode(ex.StatusCode, ...)`) against a REAL BadHttpRequestException that
    // MultipartReader itself throws (its own HeadersCountLimit, default 16) -- not a 500.
    [Fact]
    public async Task Upload_with_more_than_sixteen_part_headers_returns_400_not_500()
    {
        using var client = await SignedInStaffClientAsync(StaffRoles.Administrator);

        const string boundary = "test-boundary-too-many-headers";
        var partHeaders = new StringBuilder()
            .Append("Content-Disposition: form-data; name=\"file\"; filename=\"cat.png\"\r\n")
            .Append("Content-Type: image/png\r\n");
        for (var i = 0; i < 20; i++) partHeaders.Append($"X-Extra-Header-{i}: value\r\n");

        var body = new StringBuilder()
            .Append($"--{boundary}\r\n")
            .Append(partHeaders)
            .Append("\r\n")
            .Append("filedata")
            .Append($"\r\n--{boundary}--\r\n")
            .ToString();

        using var content = new ByteArrayContent(Encoding.ASCII.GetBytes(body));
        content.Headers.ContentType = new MediaTypeHeaderValue("multipart/form-data") { Parameters = { new NameValueHeaderValue("boundary", boundary) } };

        var response = await client.PostAsync("/api/staff/files?purpose=ProductImage", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertUniformFileRejectedBodyAsync(response);
    }

    // Same mapping as above, this time via MultipartReader's own HeadersLengthLimit (default
    // 16 KB) for a single oversized header.
    [Fact]
    public async Task Upload_with_an_oversized_part_header_returns_400_not_500()
    {
        using var client = await SignedInStaffClientAsync(StaffRoles.Administrator);

        const string boundary = "test-boundary-big-header";
        var bigHeaderValue = new string('a', 20_000); // > MultipartReader.HeadersLengthLimit (16 KB)
        var body = new StringBuilder()
            .Append($"--{boundary}\r\n")
            .Append("Content-Disposition: form-data; name=\"file\"; filename=\"cat.png\"\r\n")
            .Append("Content-Type: image/png\r\n")
            .Append($"X-Oversized-Header: {bigHeaderValue}\r\n")
            .Append("\r\n")
            .Append("filedata")
            .Append($"\r\n--{boundary}--\r\n")
            .ToString();

        using var content = new ByteArrayContent(Encoding.ASCII.GetBytes(body));
        content.Headers.ContentType = new MediaTypeHeaderValue("multipart/form-data") { Parameters = { new NameValueHeaderValue("boundary", boundary) } };

        var response = await client.PostAsync("/api/staff/files?purpose=ProductImage", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertUniformFileRejectedBodyAsync(response);
    }

    [Fact]
    public async Task Upload_with_a_json_body_instead_of_multipart_returns_400_not_500()
    {
        using var client = await SignedInStaffClientAsync(StaffRoles.Administrator);

        var response = await client.PostAsJsonAsync("/api/staff/files?purpose=ProductImage", new { notMultipart = true }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task AssertUniformFileRejectedBodyAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Json);
        var errorCode = body.GetProperty("errors")[0].GetProperty("errorCode").GetString();
        Assert.Equal(BusinessViolationCodes.FileRejected, errorCode);
    }
}
