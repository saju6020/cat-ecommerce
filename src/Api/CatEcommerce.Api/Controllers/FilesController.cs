using System.Security.Cryptography;
using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Filters;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Application.Commands.StoredFile;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Hosting;
using CatEcommerce.Read.Queries.StoredFile;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-FIL-001 §11.4/§12. The upload actions are actions, not command handlers -- they stream the
/// request body (<see cref="DisableFormValueModelBindingAttribute"/>, <see cref="MultipartReader"/>)
/// and a command handler must not take a <see cref="Stream"/> from the request
/// (.ai/command-handler-pattern.md §3). Each performs §8.2 steps 1-6 itself, then dispatches
/// <see cref="RegisterStoredFileCommand"/> (step 7), returning that command's own result directly
/// (gate-fix 2026-09-27, ARCHITECTURE MEDIUM: no second, follow-up query dispatch).
/// <see cref="GetFileContent"/> is <see cref="PublicEndpointAttribute"/> -- the signature is the
/// authorisation (§8.6).
/// </summary>
[ApiController]
public sealed class FilesController(
    IInMemoryDispatcher dispatcher,
    IUserContextProvider userContextProvider,
    IFileStore fileStore,
    IMalwareScanner malwareScanner,
    IImageVariantGenerator variantGenerator,
    IFileUrlSigner urlSigner,
    IFilePurposeAccessPolicy purposeAccessPolicy,
    IOptions<FileStorageOptions> fileStorageOptions,
    IClientIpAccessor clientIpAccessor,
    TimeProvider timeProvider,
    ILogger<FilesController> logger) : ControllerBase
{
    /// <summary>Large enough to cover every magic-byte check in <see cref="FileRules"/>, including
    /// DICOM's marker at offset 128-132 (§8.3) -- the tightest constraint of any allowed type.</summary>
    private const int MagicByteSampleSize = 256;

    [HttpPost("api/staff/files")]
    [DisableFormValueModelBinding]
    [RequestSizeLimit(60_000_000)]
    [EnableRateLimiting(FileRateLimitPolicies.FileUpload)]
    [RequireAction(Actions.Files.Upload)]
    public async Task<IActionResult> UploadStaffFile([FromQuery] string purpose, CancellationToken ct)
    {
        var caller = CurrentCaller();

        if (TryParseEnum<FilePurpose>(purpose, out var parsedPurpose) is { } parseError) return parseError;

        // Gate-fix 2026-09-27 (SECURITY MEDIUM): fails CLOSED. A purpose with no registered action
        // is refused for every staff role -- see FilePurposeAccess's own doc comment for why an
        // absent mapping used to mean "allowed" and no longer does.
        if (!purposeAccessPolicy.TryGetRequiredStaffAction(parsedPurpose!, out var requiredAction) ||
            !ActionRoles.TryGetRoles(requiredAction, out var allowedRoles) ||
            !allowedRoles.Intersect(caller.RoleIds, StringComparer.Ordinal).Any())
        {
            return StatusCode(403, Violation(
                BusinessViolationCodes.FilePurposeNotAllowedForCaller, "Purpose",
                $"You are not permitted to upload files for purpose '{parsedPurpose!.DisplayName}'."));
        }

        return await UploadAsync(parsedPurpose!, caller.AccountId, ct);
    }

    [HttpPost("api/customers/me/files")]
    [DisableFormValueModelBinding]
    [RequestSizeLimit(60_000_000)]
    [EnableRateLimiting(FileRateLimitPolicies.FileUpload)]
    [RequireCustomerSession]
    public async Task<IActionResult> UploadCustomerFile([FromQuery] string purpose, CancellationToken ct)
    {
        if (TryParseEnum<FilePurpose>(purpose, out var parsedPurpose) is { } parseError) return parseError;

        if (!purposeAccessPolicy.IsAllowedForCustomer(parsedPurpose!))
        {
            return StatusCode(403, Violation(
                BusinessViolationCodes.FilePurposeNotAllowedForCaller, "Purpose",
                $"Customers may not upload files for purpose '{parsedPurpose!.DisplayName}'."));
        }

        var caller = CurrentCaller();
        return await UploadAsync(parsedPurpose!, caller.AccountId, ct);
    }

    /// <summary>
    /// §8.6: the signature is the authorisation. Verified in constant time
    /// (<see cref="CryptographicOperations.FixedTimeEquals"/>, U8); a tampered or expired URL is
    /// refused with 403 and no detail. Headers per §8.6. Gate-fix 2026-09-27 (SECURITY MEDIUM):
    /// rate-limited per IP -- a signed URL is a bearer token replayable for its whole lifetime.
    /// </summary>
    [HttpGet("api/files/{fileId:guid}/content")]
    [EnableRateLimiting(FileRateLimitPolicies.FileServe)]
    [PublicEndpoint]
    public async Task<IActionResult> GetFileContent(Guid fileId, [FromQuery] long exp, [FromQuery] string? disp, [FromQuery] string? sig, CancellationToken ct)
    {
        // §8.6 verbatim: "private, no-store".
        Response.Headers.CacheControl = "private, no-store";

        // An empty ObjectResult.Value, rather than a bare StatusCodeResult, so [ApiController]'s
        // automatic client-error mapping does not substitute its own RFC 9110 ProblemDetails body
        // (which would still disclose nothing signature-specific, but is not the plain "403, no
        // detail" shape this host uses everywhere else -- RequireActionFilter/RequireCustomerSession
        // both return a plain object, never ProblemDetails).
        if (string.IsNullOrEmpty(disp) || string.IsNullOrEmpty(sig) || !urlSigner.TryVerify(fileId, exp, disp, sig))
            return StatusCode(403, new { });

        if (exp < timeProvider.GetUtcNow().ToUnixTimeSeconds())
            return StatusCode(403, new { });

        var info = await dispatcher.GetResultAsync(new GetStoredFileForServingQuery
        {
            FileId = fileId,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid()
        });

        if (info.Result is null) return NotFound();

        var streamResult = await fileStore.OpenReadAsync(info.Result.StorageKey, ct);
        if (!streamResult.IsSuccess)
        {
            // A row with no object is corruption, not a 404 to shrug at -- logged Error by the
            // adapter itself (§8.4); surfaced here as a server error, never a silent 404 (I9).
            return StatusCode(500);
        }

        var disposition = FileServingRules.ResolveDisposition(info.Result.ContentType, disp);

        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";
        Response.Headers["Referrer-Policy"] = "no-referrer";

        // Gate-fix 2026-09-27 (SECURITY MEDIUM): the served name's extension is always the
        // canonical one for the verified, stored content type -- never whatever extension the
        // originally-uploaded name happened to carry.
        var downloadFileName = FileNameSanitizer.ForDownload(info.Result.OriginalFileName, info.Result.ContentType);
        var contentDisposition = new ContentDispositionHeaderValue(disposition);
        contentDisposition.SetHttpFileName(downloadFileName);
        Response.Headers[HeaderNames.ContentDisposition] = contentDisposition.ToString();

        return File(streamResult.Value!, info.Result.ContentType);
    }

    /// <summary>§8.2 steps 1-7. Gate-fix 2026-09-27 (SECURITY LOW): malformed multipart input
    /// (too many part headers, a header over the length limit, no boundary, a body over Kestrel's
    /// own limit) now maps to 400/413 through this method's own try/catch, instead of an unhandled
    /// 500 from <see cref="MultipartReader.ReadNextSectionAsync"/> escaping the action entirely.</summary>
    private async Task<IActionResult> UploadAsync(FilePurpose purpose, Guid uploaderId, CancellationToken ct)
    {
        if (!MediaTypeHeaderValue.TryParse(Request.ContentType, out var mediaType) ||
            !mediaType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(Violation(BusinessViolationCodes.FileRejected, "Request", "A multipart/form-data request body is required."));
        }

        string boundary;
        try
        {
            boundary = GetBoundary(mediaType);
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(Violation(BusinessViolationCodes.FileRejected, "Request", ex.Message));
        }

        try
        {
            var reader = new MultipartReader(boundary, Request.Body);
            var section = await reader.ReadNextSectionAsync(ct);

            while (section is not null)
            {
                var contentDisposition = section.GetContentDispositionHeader();
                if (contentDisposition is { DispositionType.Value: "form-data" } && contentDisposition.FileName.HasValue)
                {
                    return await ProcessFileSectionAsync(section, contentDisposition, purpose, uploaderId, ct);
                }

                section = await reader.ReadNextSectionAsync(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Gate-fix 2026-09-27 (SECURITY LOW): the client aborted mid-upload -- HttpContext.
            // RequestAborted fired this token. Logged below Error (Information, not a fault) and
            // returned as a quiet, empty response, rather than an unhandled OperationCanceledException
            // reaching SafeExceptionHandlerMiddleware and being logged at `fail` with a stack trace
            // for what is ordinary client behaviour, not an application fault.
            logger.LogInformation("Client aborted the upload for purpose {Purpose}.", purpose.DisplayName);
            return new EmptyResult();
        }
        catch (InvalidDataException ex)
        {
            // MultipartReader's own parse failures -- no boundary found in the body, its own
            // HeadersCountLimit (16 parts) or HeadersLengthLimit (16 KB/header) exceeded. These are
            // MultipartReader's own checks (Microsoft.AspNetCore.WebUtilities), thrown as
            // InvalidDataException regardless of host -- TestServer and a real Kestrel listener
            // reach this identically (TC R16).
            return BadRequest(Violation(BusinessViolationCodes.FileRejected, "Request", ex.Message));
        }
        catch (BadHttpRequestException ex)
        {
            // Kestrel's own request-reading limits (e.g. MaxRequestBodySize -> 413) surface here as
            // BadHttpRequestException, distinct from MultipartReader's InvalidDataException above --
            // BadHttpRequestException.StatusCode carries the right code (400 or 413), so it is
            // forwarded rather than assumed. Not reproducible under TestServer (TC R16): TestServer
            // does not enforce Kestrel's body-size limit at all. Confirmed live by the SECURITY
            // gate's own Kestrel run: "a 61 MB non-file preamble -> 413 FileRejected".
            return StatusCode(ex.StatusCode, Violation(BusinessViolationCodes.FileRejected, "Request", "The request could not be read."));
        }
        catch (IOException ex)
        {
            // Gate-fix 2026-09-27 (SECURITY LOW, carried from first pass): a truncated body can also
            // surface as a plain IOException from THIS loop directly -- e.g. cut off while
            // MultipartReader is still reading section headers, or the final closing boundary,
            // rather than while a file section's own body is being read (that narrower case is
            // ProcessFileSectionAsync's own catch, below). Must stay AFTER the BadHttpRequestException
            // catch above -- BadHttpRequestException : IOException, so catching the base type first
            // would make that catch unreachable and fail to compile.
            logger.LogInformation(ex, "Malformed or truncated multipart body for purpose {Purpose}.", purpose.DisplayName);
            return BadRequest(Violation(BusinessViolationCodes.FileRejected, "Request", "The request could not be read."));
        }

        return BadRequest(Violation(BusinessViolationCodes.FileRejected, "Request", "No file part was found in the request."));
    }

    private async Task<IActionResult> ProcessFileSectionAsync(
        MultipartSection section,
        Microsoft.Net.Http.Headers.ContentDispositionHeaderValue contentDisposition,
        FilePurpose purpose,
        Guid uploaderId,
        CancellationToken ct)
    {
        var declaredContentType = string.IsNullOrWhiteSpace(section.ContentType) ? "application/octet-stream" : section.ContentType!;
        var originalFileName = FileNameSanitizer.Sanitize(contentDisposition.FileName.HasValue ? contentDisposition.FileName.Value : contentDisposition.FileNameStar.Value);

        var rule = FileRules.Find(purpose);
        if (rule is null || !rule.AllowedContentTypes.Contains(declaredContentType, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(Violation(BusinessViolationCodes.FileTypeNotAllowed, "ContentType",
                $"'{declaredContentType}' is not an allowed type for purpose '{purpose.DisplayName}'."));
        }

        var fileId = Guid.NewGuid();
        Directory.CreateDirectory(fileStorageOptions.Value.TempPath);
        var tempPath = Path.Combine(fileStorageOptions.Value.TempPath, $"upload-{fileId:N}.tmp");

        try
        {
            long sizeInBytes;
            byte[] headerBuffer;
            int headerBytesRead;
            string sha256Hex;

            // Gate-fix 2026-09-27 (CODE_REVIEW LOW): this try/catch(IOException) is now narrowed to
            // JUST the temp-file write window (the AV-quarantine case this exists for is entirely
            // local-disk) -- PutAsync (network/GCS) and the command dispatch (DB) below are outside
            // it, so a genuine infra failure there is never reported to the caller as "your file
            // was rejected".
            try
            {
                var headerBufferLocal = new byte[MagicByteSampleSize];
                var headerBytesReadLocal = 0;
                long sizeInBytesLocal = 0;
                using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

                await using (var tempFile = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true))
                {
                    var buffer = new byte[81920];
                    int read;
                    while ((read = await section.Body.ReadAsync(buffer, ct)) > 0)
                    {
                        sizeInBytesLocal += read;
                        if (sizeInBytesLocal > rule.MaxSizeInBytes)
                        {
                            return BadRequest(Violation(BusinessViolationCodes.FileTooLarge, "Content",
                                $"Exceeds the maximum size of {rule.MaxSizeInBytes} bytes for purpose '{purpose.DisplayName}'."));
                        }

                        if (headerBytesReadLocal < headerBufferLocal.Length)
                        {
                            var toCopy = Math.Min(read, headerBufferLocal.Length - headerBytesReadLocal);
                            Buffer.BlockCopy(buffer, 0, headerBufferLocal, headerBytesReadLocal, toCopy);
                            headerBytesReadLocal += toCopy;
                        }

                        hasher.AppendData(buffer, 0, read);
                        await tempFile.WriteAsync(buffer.AsMemory(0, read), ct);
                    }
                }

                sizeInBytes = sizeInBytesLocal;
                headerBuffer = headerBufferLocal;
                headerBytesRead = headerBytesReadLocal;
                sha256Hex = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
            }
            catch (BadHttpRequestException ex)
            {
                return StatusCode(ex.StatusCode, Violation(BusinessViolationCodes.FileTooLarge, "Content", "The request body is too large or malformed."));
            }
            catch (IOException ex)
            {
                // Most commonly a host-level antivirus product quarantining or deleting the temp
                // file out from under this process the instant a genuinely malicious byte sequence
                // (e.g. the EICAR test string) touches disk, before IMalwareScanner ever runs. Fail
                // closed exactly like ADR-319 requires for the scanner itself.
                //
                // Also the catch that a genuinely truncated multipart body reaches: MultipartReaderStream
                // throws plain IOException("Unexpected end of Stream") when a chunked-transfer
                // connection ends mid-section with no way to tell whether more bytes were meant to
                // follow (TC R16, regression-verified 2026-09-27: disabling this specific catch turns
                // FilesMalformedMultipartEndpointTests.Upload_with_a_truncated_body_returns_400_not_500
                // into an unhandled 500, with no effect on the other five cases in that suite).
                logger.LogWarning(ex, "Temp-file I/O failure for file {FileId}; refusing the upload.", fileId);
                await RecordRejectionAsync(fileId, BusinessViolationCodes.FileRejected, purpose, null, ct);
                return BadRequest(Violation(BusinessViolationCodes.FileRejected, "Content", "The uploaded file was rejected."));
            }

            if (sizeInBytes == 0)
                return BadRequest(Violation(BusinessViolationCodes.FileRejected, "Content", "The uploaded file is empty."));

            if (!FileRules.MagicBytesMatch(declaredContentType, headerBuffer.AsSpan(0, headerBytesRead)))
            {
                return BadRequest(Violation(BusinessViolationCodes.FileContentMismatch, "Content",
                    "The file's content does not match its declared type."));
            }

            // Gate-fix 2026-09-27 (SECURITY LOW, second pass): text/csv's deny-list is checked over
            // the WHOLE file, not just the 256-byte magic-byte sample -- see FileRules'
            // ContainsDeniedCsvSignatureAsync doc comment.
            if (declaredContentType == "text/csv")
            {
                bool containsDeniedSignature;
                await using (var csvScanStream = OpenTempForRead(tempPath))
                {
                    containsDeniedSignature = await FileRules.ContainsDeniedCsvSignatureAsync(csvScanStream, ct);
                }

                if (containsDeniedSignature)
                {
                    return BadRequest(Violation(BusinessViolationCodes.FileContentMismatch, "Content",
                        "The file's content does not match its declared type."));
                }
            }

            ScanResult scanResult;
            try
            {
                await using var scanStream = OpenTempForRead(tempPath);
                scanResult = await malwareScanner.ScanAsync(scanStream, ct);
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "Temp-file I/O failure scanning file {FileId}; refusing the upload.", fileId);
                await RecordRejectionAsync(fileId, BusinessViolationCodes.FileRejected, purpose, null, ct);
                return BadRequest(Violation(BusinessViolationCodes.FileRejected, "Content", "The uploaded file was rejected."));
            }

            if (scanResult.Outcome != ScanOutcome.Clean)
            {
                var violationCode = scanResult.Outcome == ScanOutcome.Infected
                    ? BusinessViolationCodes.FileRejected
                    : BusinessViolationCodes.FileScanUnavailable;

                await RecordRejectionAsync(fileId, violationCode, purpose, scanResult.ScannerName, ct);

                return BadRequest(Violation(violationCode, "Content",
                    scanResult.Outcome == ScanOutcome.Infected
                        ? "The uploaded file was rejected."
                        : "The malware scanner is currently unavailable. Try again shortly."));
            }

            // ADR-370: the resized copies are made BEFORE anything is stored, so a photo that cannot be processed is refused
            // like any other rejected file and a stored photo always has its copies (the direct URLs point at them).
            var variants = FileVariants.For(purpose);
            IReadOnlyList<GeneratedVariant> copies = [];
            if (variants.Count > 0)
            {
                await using var imageStream = OpenTempForRead(tempPath);
                var generated = variantGenerator.Generate(imageStream, variants);
                if (generated is null)
                {
                    await RecordRejectionAsync(fileId, BusinessViolationCodes.FileRejected, purpose, scanResult.ScannerName, ct);
                    return BadRequest(Violation(BusinessViolationCodes.FileRejected, "Content", "The image could not be processed."));
                }
                copies = generated;
            }

            // Everything from here on may fail against a genuinely external system (the object
            // store, the database) -- these are infra failures, not "your file was rejected", and
            // are deliberately left to propagate as unhandled exceptions (SafeExceptionHandlerMiddleware
            // logs them at Error and returns 500) rather than being folded into the IOException
            // handling above (gate-fix 2026-09-27, CODE_REVIEW LOW).
            FileStoreResult<string> putResult;
            await using (var putStream = OpenTempForRead(tempPath))
            {
                putResult = await fileStore.PutAsync(
                    new FilePutRequest(fileId, purpose, putStream, declaredContentType, sizeInBytes, sha256Hex), ct);
            }

            if (!putResult.IsSuccess)
            {
                return StatusCode(502, Violation(
                    putResult.ViolationCode ?? BusinessViolationCodes.FileRejected, "Content",
                    putResult.Message ?? "Failed to store the file."));
            }

            try
            {
                foreach (var copy in copies)
                {
                    using var copyStream = new MemoryStream(copy.Content, writable: false);
                    var copyResult = await fileStore.PutVariantAsync(putResult.Value!, copy.Variant, copyStream, ct);
                    if (!copyResult.IsSuccess)
                    {
                        // DeleteAsync removes the original and any copy already stored (ADR-370).
                        await fileStore.DeleteAsync(putResult.Value!, CancellationToken.None);
                        return StatusCode(502, Violation(
                            copyResult.ViolationCode ?? BusinessViolationCodes.FileRejected, "Content",
                            copyResult.Message ?? "Failed to store the file."));
                    }
                }
            }
            catch
            {
                // Same compensation as the registration below: no stored object may outlive a failed upload.
                await fileStore.DeleteAsync(putResult.Value!, CancellationToken.None);
                throw;
            }

            var command = new RegisterStoredFileCommand
            {
                FileId = fileId,
                Purpose = purpose,
                ContentType = declaredContentType,
                SizeInBytes = sizeInBytes,
                Sha256 = sha256Hex,
                OriginalFileName = originalFileName,
                StorageKey = putResult.Value!,
                UploadedBy = uploaderId,
                UploadedOn = timeProvider.GetUtcNow(),
                ScannerName = scanResult.ScannerName ?? "unknown",
                UserContext = userContextProvider.GetUserContext(),
                CorrelationId = Guid.NewGuid(),
                ValidateCommand = true
            };

            Platform.Infrastructure.Core.Commands.CommandResponse response;
            try
            {
                response = await dispatcher.SendAsync(command);
            }
            catch
            {
                // Gate-fix 2026-09-27 (SECURITY LOW): §8.2 step 7's compensation must also run when
                // the save THROWS (e.g. a DB outage/timeout), not only when it returns
                // IsSuccess=false -- otherwise the object is orphaned, invisible to retention and
                // erasure. Re-thrown so the caller still sees the real failure (500), not a false
                // "success".
                //
                // Gate-fix 2026-09-27, third pass (CODE_REVIEW LOW): CancellationToken.None, not
                // `ct` -- this same catch also fires when the CLIENT disconnects while SendAsync is
                // in flight, and by the time it runs `ct` is already cancelled. Passing a cancelled
                // token to GcsFileStore.DeleteAsync's underlying client.DeleteObjectAsync would skip
                // the delete entirely, silently orphaning the object -- exactly the outcome this fix
                // exists to close, just for a different trigger than the one named above.
                // DeleteStoredFileCommandHandler already sets this precedent (EraseAsync(id,
                // CancellationToken.None)) for the identical reason: a compensating delete must not
                // be skippable by the same cancellation that triggered it.
                await fileStore.DeleteAsync(putResult.Value!, CancellationToken.None);
                throw;
            }

            if (!response.IsSuccess)
            {
                // §8.2 step 7: the object is deleted again -- an object with no row is invisible
                // to the retention worker. CancellationToken.None for the same reason as above.
                await fileStore.DeleteAsync(putResult.Value!, CancellationToken.None);
                return BadRequest(response.ValidationResult);
            }

            return CreatedAtAction(nameof(GetFileContent), new { fileId }, response.Result);
        }
        finally
        {
            // Deleted in every path, including exceptions (§8.2 step 1).
            TryDeleteTempFile(tempPath);
        }
    }

    private async Task RecordRejectionAsync(Guid attemptedFileId, string violationCode, FilePurpose purpose, string? scannerName, CancellationToken ct)
    {
        try
        {
            await dispatcher.SendAsync(new RecordFileRejectionAuditCommand
            {
                AttemptedFileId = attemptedFileId,
                ViolationCode = violationCode,
                Purpose = purpose.DisplayName,
                ScannerName = scannerName,
                IpAddress = clientIpAccessor.GetClientIpAddress()?.ToString(),
                UserContext = userContextProvider.GetUserContext(),
                CorrelationId = Guid.NewGuid(),
                ValidateCommand = true
            });
        }
        catch (Exception ex)
        {
            // The rejection itself must still be returned even if the audit write fails.
            logger.LogError(ex, "Failed to record file rejection audit entry for {FileId}", attemptedFileId);
        }
    }

    private static FileStream OpenTempForRead(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);

    private static void TryDeleteTempFile(string path)
    {
        try
        {
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup -- never fails the response over a temp-file delete.
        }
    }

    private static string GetBoundary(MediaTypeHeaderValue contentType, int lengthLimit = 128)
    {
        var boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary).Value;
        if (string.IsNullOrWhiteSpace(boundary))
            throw new InvalidDataException("Missing multipart boundary.");
        if (boundary.Length > lengthLimit)
            throw new InvalidDataException("Multipart boundary is too long.");
        return boundary;
    }

    private ResolvedCaller CurrentCaller() =>
        HttpContext.Items[ResolvedCaller.HttpContextItemsKey] as ResolvedCaller ?? ResolvedCaller.Anonymous;

    /// <summary>Gate-fix 2026-09-27 (CODE_REVIEW LOW): the standard ADR-014 <see cref="ValidationResponse"/>
    /// shape, not a bespoke <c>{ error }</c> body -- <c>FilesInvalidPurpose</c> rather than the
    /// generic <c>InvalidEnumValue</c> name ho/CAT-001-gates coins for the same defect in
    /// <c>CatsController</c>, to avoid a duplicate-const-member conflict on merge (neither branch
    /// can see the other's exact declaration; recorded in HO-FIL-001.md §1 Amended).</summary>
    private IActionResult? TryParseEnum<T>(string value, out T? result) where T : Enumeration
    {
        var match = Enumeration.GetAll<T>()
            .FirstOrDefault(e => string.Equals(e.DisplayName, value, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            result = null;
            return BadRequest(Violation(BusinessViolationCodes.FilesInvalidPurpose, "Purpose", $"'{value}' is not a valid {typeof(T).Name}."));
        }

        result = match;
        return null;
    }

    private static ValidationResponse Violation(string code, string property, string message)
    {
        var response = new ValidationResponse();
        response.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: "StoredFile");
        return response;
    }
}
