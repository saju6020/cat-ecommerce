using CatEcommerce.Application.CommandHandlers.Cat;
using CatEcommerce.Application.Commands.Cat;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.StoredFile;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Application.Tests.CommandHandlers.Cat;

/// <summary>
/// HO-FIL-001 §1 Amended -- closes the HO-CAT-002-SECURITY carried item. Every case here is about
/// the server-side eligibility check that replaced the old client-trusted StorageKey/ContentType/
/// SizeInBytes/FileName fields.
/// </summary>
public sealed class AddCatPhotoCommandHandlerTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly Guid Uploader = Guid.NewGuid();

    private static CatAggregate SeededCat(FakeAggregateRootRepository<CatAggregate> repository)
    {
        var cat = new CatAggregate(Guid.NewGuid());
        cat.Register(new CatRegistrationDto(
            Guid.NewGuid(), "Mittens", "Cat", CatSex.Female, Today.AddYears(-2),
            DateOfBirthPrecision.Exact, null, null, Today, TestUserContext.Create(), Guid.NewGuid()));
        repository.Seed(cat);
        return cat;
    }

    private static StoredFileAggregate SeededFile(
        FakeAggregateRootRepository<StoredFileAggregate> repository,
        DateTimeOffset uploadedOn,
        FilePurpose? purpose = null,
        Guid? uploadedBy = null)
    {
        var file = new StoredFileAggregate(Guid.NewGuid());
        file.Register(new StoredFileRegistrationDto(
            file.Id, purpose ?? FilePurpose.CatProfilePhoto, "image/jpeg", 1024, new string('a', 64),
            "cat.jpg", "CatProfilePhoto/2026/09/" + file.Id.ToString("N"), uploadedBy ?? Uploader,
            uploadedOn, "DevelopmentNoOp", null, TestUserContext.Create(), Guid.NewGuid()));
        repository.Seed(file);
        return file;
    }

    private static UserContext UploaderContext() => new(Uploader, Guid.NewGuid());

    private static AddCatPhotoCommandHandler CreateHandler(
        FakeAggregateRootRepository<CatAggregate> catRepository,
        FakeAggregateRootRepository<StoredFileAggregate> fileRepository,
        DateTimeOffset now) =>
        new(NullLogger<AddCatPhotoCommandHandler>.Instance, catRepository, fileRepository, new FakeTimeProvider(now));

    [Fact]
    public async Task HandleAsync_CatNotFound_ReturnsCatNotFound()
    {
        var catRepository = new FakeAggregateRootRepository<CatAggregate>();
        var fileRepository = new FakeAggregateRootRepository<StoredFileAggregate>();
        var handler = CreateHandler(catRepository, fileRepository, DateTimeOffset.UtcNow);

        var response = await handler.HandleAsync(new AddCatPhotoCommand
        {
            CatId = Guid.NewGuid(), PhotoId = Guid.NewGuid(), FileId = Guid.NewGuid(), IsPrimary = true,
            UserContext = UploaderContext(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Equal(BusinessViolationCodes.CatNotFound, response.ValidationResult!.Errors[0].ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_UnknownFileId_ReturnsPhotoFileNotEligible()
    {
        var catRepository = new FakeAggregateRootRepository<CatAggregate>();
        var cat = SeededCat(catRepository);
        var fileRepository = new FakeAggregateRootRepository<StoredFileAggregate>();
        var handler = CreateHandler(catRepository, fileRepository, DateTimeOffset.UtcNow);

        var response = await handler.HandleAsync(new AddCatPhotoCommand
        {
            CatId = cat.Id, PhotoId = Guid.NewGuid(), FileId = Guid.NewGuid(), IsPrimary = true,
            UserContext = UploaderContext(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Equal(BusinessViolationCodes.PhotoFileNotEligible, response.ValidationResult!.Errors[0].ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_FileWithWrongPurpose_ReturnsPhotoFileNotEligible()
    {
        var catRepository = new FakeAggregateRootRepository<CatAggregate>();
        var cat = SeededCat(catRepository);
        var fileRepository = new FakeAggregateRootRepository<StoredFileAggregate>();
        var now = DateTimeOffset.UtcNow;
        var file = SeededFile(fileRepository, now, purpose: FilePurpose.ProductImage);
        var handler = CreateHandler(catRepository, fileRepository, now);

        var response = await handler.HandleAsync(new AddCatPhotoCommand
        {
            CatId = cat.Id, PhotoId = Guid.NewGuid(), FileId = file.Id, IsPrimary = true,
            UserContext = UploaderContext(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Equal(BusinessViolationCodes.PhotoFileNotEligible, response.ValidationResult!.Errors[0].ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_FileUploadedByAnotherCaller_ReturnsPhotoFileNotEligible()
    {
        var catRepository = new FakeAggregateRootRepository<CatAggregate>();
        var cat = SeededCat(catRepository);
        var fileRepository = new FakeAggregateRootRepository<StoredFileAggregate>();
        var now = DateTimeOffset.UtcNow;
        var file = SeededFile(fileRepository, now, uploadedBy: Guid.NewGuid());
        var handler = CreateHandler(catRepository, fileRepository, now);

        var response = await handler.HandleAsync(new AddCatPhotoCommand
        {
            CatId = cat.Id, PhotoId = Guid.NewGuid(), FileId = file.Id, IsPrimary = true,
            UserContext = UploaderContext(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Equal(BusinessViolationCodes.PhotoFileNotEligible, response.ValidationResult!.Errors[0].ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_FileUploadedOutsideTheAttachWindow_ReturnsPhotoFileNotEligible()
    {
        var catRepository = new FakeAggregateRootRepository<CatAggregate>();
        var cat = SeededCat(catRepository);
        var fileRepository = new FakeAggregateRootRepository<StoredFileAggregate>();
        var uploadedOn = DateTimeOffset.UtcNow.Add(-FileRules.AttachWindow).AddMinutes(-1);
        var file = SeededFile(fileRepository, uploadedOn);
        var handler = CreateHandler(catRepository, fileRepository, DateTimeOffset.UtcNow);

        var response = await handler.HandleAsync(new AddCatPhotoCommand
        {
            CatId = cat.Id, PhotoId = Guid.NewGuid(), FileId = file.Id, IsPrimary = true,
            UserContext = UploaderContext(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Equal(BusinessViolationCodes.PhotoFileNotEligible, response.ValidationResult!.Errors[0].ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_EligibleFile_AddsThePhotoUsingTheServerVerifiedMetadata()
    {
        var catRepository = new FakeAggregateRootRepository<CatAggregate>();
        var cat = SeededCat(catRepository);
        var fileRepository = new FakeAggregateRootRepository<StoredFileAggregate>();
        var now = DateTimeOffset.UtcNow;
        var file = SeededFile(fileRepository, now);
        var handler = CreateHandler(catRepository, fileRepository, now);
        var photoId = Guid.NewGuid();

        var response = await handler.HandleAsync(new AddCatPhotoCommand
        {
            CatId = cat.Id, PhotoId = photoId, FileId = file.Id, IsPrimary = true,
            UserContext = UploaderContext(), CorrelationId = Guid.NewGuid()
        });

        Assert.True(response.IsSuccess);
        Assert.Equal(photoId, response.Result);

        var savedCat = Assert.Single(catRepository.Updated);
        var photo = Assert.Single(savedCat.Photos);
        Assert.Equal(file.Id, photo.FileId);

        // Gate-fix 2026-09-27 (ARCHITECTURE HIGH, second pass): exactly one aggregate is mutated --
        // StoredFileAggregate.IsEligibleForAttach is a side-effect-free query, so the file is never
        // saved at all. "One file, one cat" is enforced by the unique database index on
        // clinic.cat_photo.file_id (see UniqueConstraintExceptionFilter), not by state on the file.
        Assert.Empty(fileRepository.Updated);
        Assert.Empty(fileRepository.Saved);
    }

    // Gate-fix 2026-09-27, second pass (ARCHITECTURE HIGH): a refusal on the CAT side (photo limit,
    // deceased cat) must leave the file completely untouched -- eligible for another cat to attach
    // -- since the file is never mutated or saved by this handler at all.
    [Fact]
    public async Task HandleAsync_PhotoLimitExceeded_LeavesTheFileUntouchedAndAttachableElsewhere()
    {
        var catRepository = new FakeAggregateRootRepository<CatAggregate>();
        var cat = SeededCat(catRepository);
        for (var i = 0; i < CatPhotoRules.MaxPhotosPerCat; i++)
        {
            cat.AddPhoto(new CatPhotoDto(Guid.NewGuid(), Guid.NewGuid(), false, DateTimeOffset.UtcNow, UploaderContext(), Guid.NewGuid()));
        }

        var fileRepository = new FakeAggregateRootRepository<StoredFileAggregate>();
        var now = DateTimeOffset.UtcNow;
        var file = SeededFile(fileRepository, now);
        var handler = CreateHandler(catRepository, fileRepository, now);

        var response = await handler.HandleAsync(new AddCatPhotoCommand
        {
            CatId = cat.Id, PhotoId = Guid.NewGuid(), FileId = file.Id, IsPrimary = true,
            UserContext = UploaderContext(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Equal(BusinessViolationCodes.PhotoLimitExceeded, response.ValidationResult!.Errors[0].ErrorCode);
        Assert.Empty(fileRepository.Updated);
        Assert.Empty(fileRepository.Saved);
        Assert.Empty(catRepository.Updated); // the cat's own invariant failure also never reaches UpdateAsync
    }

    [Fact]
    public async Task HandleAsync_DeceasedCat_LeavesTheFileUntouchedAndAttachableElsewhere()
    {
        var catRepository = new FakeAggregateRootRepository<CatAggregate>();
        var cat = SeededCat(catRepository);
        cat.MarkDeceased(new CatDeceasedDto(Today, null, Today, UploaderContext(), Guid.NewGuid()));

        var fileRepository = new FakeAggregateRootRepository<StoredFileAggregate>();
        var now = DateTimeOffset.UtcNow;
        var file = SeededFile(fileRepository, now);
        var handler = CreateHandler(catRepository, fileRepository, now);

        var response = await handler.HandleAsync(new AddCatPhotoCommand
        {
            CatId = cat.Id, PhotoId = Guid.NewGuid(), FileId = file.Id, IsPrimary = true,
            UserContext = UploaderContext(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Equal(BusinessViolationCodes.CatIsDeceased, response.ValidationResult!.Errors[0].ErrorCode);
        Assert.Empty(fileRepository.Updated);
        Assert.Empty(fileRepository.Saved);
        Assert.Empty(catRepository.Updated);
    }

    /// <summary>A malicious client can no longer choose the storage key at all -- the command has
    /// no such field any more (compile-time closure of the original finding).</summary>
    [Fact]
    public void AddCatPhotoCommand_has_no_client_supplied_storage_metadata()
    {
        var properties = typeof(AddCatPhotoCommand).GetProperties().Select(p => p.Name).ToArray();

        Assert.DoesNotContain(nameof(StoredFileAggregate.StorageKey), properties);
        Assert.DoesNotContain("ContentType", properties);
        Assert.DoesNotContain("SizeInBytes", properties);
        Assert.DoesNotContain("FileName", properties);
        Assert.Contains("FileId", properties);
    }
}
