using CatEcommerce.Domain.Aggregates.StoredFile;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Aggregates.StoredFile;

public sealed class StoredFileAggregateTests
{
    private static StoredFileRegistrationDto ValidDto(Guid? fileId = null) => new(
        fileId ?? Guid.NewGuid(),
        FilePurpose.ProductImage,
        "image/png",
        1024,
        new string('a', 64),
        "cat.png",
        "ProductImage/2026/09/" + Guid.NewGuid().ToString("N"),
        Guid.NewGuid(),
        DateTimeOffset.UtcNow,
        "DevelopmentNoOp",
        null,
        TestUserContext.Create(),
        Guid.NewGuid());

    [Fact]
    public void Register_sets_every_field_and_starts_with_no_legal_hold()
    {
        var fileId = Guid.NewGuid();
        var dto = ValidDto(fileId);
        var file = new StoredFileAggregate(fileId);

        file.Register(dto);

        Assert.Equal(fileId, file.Id);
        Assert.Equal(dto.Purpose, file.Purpose);
        Assert.Equal(dto.ContentType, file.ContentType);
        Assert.Equal(dto.SizeInBytes, file.SizeInBytes);
        Assert.Equal(dto.Sha256, file.Sha256);
        Assert.Equal(dto.StorageKey, file.StorageKey);
        Assert.Equal(dto.UploadedBy, file.UploadedBy);
        Assert.False(file.LegalHold);
        Assert.Equal(1, file.Version);
    }

    [Fact]
    public void Register_raises_exactly_one_domain_event()
    {
        var file = new StoredFileAggregate(Guid.NewGuid());
        file.Register(ValidDto());

        Assert.Single(file.Events);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_rejects_a_blank_storage_key(string blank)
    {
        var file = new StoredFileAggregate(Guid.NewGuid());
        var dto = ValidDto() with { StorageKey = blank };

        Assert.Throws<DomainRuleViolationException>(() => file.Register(dto));
    }

    [Fact]
    public void Register_rejects_a_non_positive_size()
    {
        var file = new StoredFileAggregate(Guid.NewGuid());
        var dto = ValidDto() with { SizeInBytes = 0 };

        Assert.Throws<DomainRuleViolationException>(() => file.Register(dto));
    }

    [Fact]
    public void Register_rejects_a_blank_content_type()
    {
        var file = new StoredFileAggregate(Guid.NewGuid());
        var dto = ValidDto() with { ContentType = "" };

        Assert.Throws<DomainRuleViolationException>(() => file.Register(dto));
    }

    [Fact]
    public void Register_rejects_a_blank_sha256()
    {
        var file = new StoredFileAggregate(Guid.NewGuid());
        var dto = ValidDto() with { Sha256 = "" };

        Assert.Throws<DomainRuleViolationException>(() => file.Register(dto));
    }

    // Gate-fix 2026-09-27 (ARCHITECTURE MEDIUM): defence in depth against FileRules.
    [Fact]
    public void Register_rejects_a_content_type_not_allowed_for_the_purpose()
    {
        var file = new StoredFileAggregate(Guid.NewGuid());
        var dto = ValidDto() with { ContentType = "application/pdf" }; // ProductImage does not allow pdf

        Assert.Throws<DomainRuleViolationException>(() => file.Register(dto));
    }

    [Fact]
    public void Register_rejects_a_size_over_the_purposes_cap()
    {
        var file = new StoredFileAggregate(Guid.NewGuid());
        var rule = FileRules.Find(FilePurpose.ProductImage)!;
        var dto = ValidDto() with { SizeInBytes = rule.MaxSizeInBytes + 1 };

        Assert.Throws<DomainRuleViolationException>(() => file.Register(dto));
    }

    /// <summary>Gate-fix 2026-09-27, second pass (ARCHITECTURE HIGH): <c>IsEligibleForAttach</c> is
    /// a side-effect-free query, not a mutator -- <c>RedactUploaderPersonalData</c> is the only
    /// lifecycle mutator beyond <c>Register</c>, so this asserts the closed set of two.</summary>
    [Fact]
    public void The_aggregate_exposes_no_mutator_beyond_the_documented_two()
    {
        var publicMethods = typeof(StoredFileAggregate)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && m.Name != nameof(StoredFileAggregate.IsEligibleForAttach))
            .Select(m => m.Name)
            .ToList();

        Assert.Equal(["Register", "RedactUploaderPersonalData"], publicMethods);
    }

    // Gate-fix 2026-09-27, second pass (ARCHITECTURE HIGH): IsEligibleForAttach is side-effect-free
    // -- it never changes aggregate state and can be asked repeatedly with the same answer.
    [Fact]
    public void IsEligibleForAttach_is_true_for_the_uploader_within_the_window_and_never_mutates_state()
    {
        var file = new StoredFileAggregate(Guid.NewGuid());
        var dto = ValidDto();
        file.Register(dto);
        var versionBefore = file.Version;

        var eligible = file.IsEligibleForAttach(dto.Purpose, dto.UploadedBy, dto.UploadedOn.AddMinutes(1), TimeSpan.FromMinutes(30));

        Assert.True(eligible);
        Assert.Equal(versionBefore, file.Version);
    }

    [Fact]
    public void IsEligibleForAttach_gives_the_same_answer_when_asked_repeatedly()
    {
        var file = new StoredFileAggregate(Guid.NewGuid());
        var dto = ValidDto();
        file.Register(dto);

        var first = file.IsEligibleForAttach(dto.Purpose, dto.UploadedBy, dto.UploadedOn, TimeSpan.FromMinutes(30));
        var second = file.IsEligibleForAttach(dto.Purpose, dto.UploadedBy, dto.UploadedOn, TimeSpan.FromMinutes(30));

        Assert.True(first);
        Assert.True(second);
    }

    [Fact]
    public void IsEligibleForAttach_refuses_the_wrong_purpose()
    {
        var file = new StoredFileAggregate(Guid.NewGuid());
        var dto = ValidDto();
        file.Register(dto);

        var eligible = file.IsEligibleForAttach(FilePurpose.ClinicalPhoto, dto.UploadedBy, dto.UploadedOn, TimeSpan.FromMinutes(30));

        Assert.False(eligible);
    }

    [Fact]
    public void IsEligibleForAttach_refuses_a_caller_who_did_not_upload_the_file()
    {
        var file = new StoredFileAggregate(Guid.NewGuid());
        var dto = ValidDto();
        file.Register(dto);

        var eligible = file.IsEligibleForAttach(dto.Purpose, Guid.NewGuid(), dto.UploadedOn, TimeSpan.FromMinutes(30));

        Assert.False(eligible);
    }

    [Fact]
    public void IsEligibleForAttach_refuses_outside_the_attach_window()
    {
        var file = new StoredFileAggregate(Guid.NewGuid());
        var dto = ValidDto();
        file.Register(dto);

        var eligible = file.IsEligibleForAttach(dto.Purpose, dto.UploadedBy, dto.UploadedOn.AddMinutes(31), TimeSpan.FromMinutes(30));

        Assert.False(eligible);
    }

    // Gate-fix 2026-09-27 (ARCHITECTURE HIGH, C-ALL-2): RedactUploaderPersonalData.
    [Fact]
    public void RedactUploaderPersonalData_clears_the_uploader_and_original_file_name()
    {
        var file = new StoredFileAggregate(Guid.NewGuid());
        var dto = ValidDto();
        file.Register(dto);

        file.RedactUploaderPersonalData(TestUserContext.Create(), Guid.NewGuid());

        Assert.Equal(Guid.Empty, file.UploadedBy);
        Assert.Equal("[redacted]", file.OriginalFileName);
    }

    [Fact]
    public void RedactUploaderPersonalData_is_idempotent()
    {
        var file = new StoredFileAggregate(Guid.NewGuid());
        var dto = ValidDto();
        file.Register(dto);
        file.RedactUploaderPersonalData(TestUserContext.Create(), Guid.NewGuid());
        var versionAfterFirst = file.Version;

        file.RedactUploaderPersonalData(TestUserContext.Create(), Guid.NewGuid());

        Assert.Equal(versionAfterFirst, file.Version);
    }
}
