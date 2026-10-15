using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Common;

/// <summary>
/// Gate-fix 2026-09-27 (SECURITY MEDIUM, HO-FIL-001-SECURITY.md first pass): the per-purpose gate
/// must fail CLOSED -- a purpose with no registered staff action is refused for every role, rather
/// than falling through to the baseline <c>Actions.Files.Upload</c> check (which every staff role
/// holds). No automated test proved this at the policy level before this TEST gate: every
/// <c>FilesApiFactory</c>-based integration test runs behind a test-only decorator
/// (<c>UnownedPurposeAllowingPolicy</c>) that deliberately widens unmapped purposes back open, by
/// design, so the real, fail-closed <see cref="FilePurposeAccess"/> was exercised only by the
/// SECURITY gate's own throwaway probe (never persisted).
/// </summary>
public sealed class FilePurposeAccessTests
{
    public static IEnumerable<object[]> EveryCanonicalPurposeExceptCatProfilePhoto()
    {
        yield return [FilePurpose.LabResult];
        yield return [FilePurpose.ClinicalPhoto];
        yield return [FilePurpose.ConsentForm];
        yield return [FilePurpose.BoardingMedia];
        yield return [FilePurpose.BoardingBelongings];
        yield return [FilePurpose.ReportExport];
    }

    public static IEnumerable<object[]> PurposesWithNoRegisteredOwner()
    {
        yield return [FilePurpose.ReportExport];
    }

    [Fact]
    public void TryGetRequiredStaffAction_maps_the_clinical_purposes_to_their_EMR_actions()
    {
        Assert.True(FilePurposeAccess.TryGetRequiredStaffAction(FilePurpose.LabResult, out var lab));
        Assert.Equal(Actions.Visits.WriteNote, lab);
        Assert.True(FilePurposeAccess.TryGetRequiredStaffAction(FilePurpose.ClinicalPhoto, out var photo));
        Assert.Equal(Actions.Visits.WriteNote, photo);
        Assert.True(FilePurposeAccess.TryGetRequiredStaffAction(FilePurpose.ConsentForm, out var consent));
        Assert.Equal(Actions.Consents.Record, consent);
    }

    [Theory]
    [MemberData(nameof(PurposesWithNoRegisteredOwner))]
    public void TryGetRequiredStaffAction_refuses_every_purpose_with_no_registered_owner(FilePurpose purpose)
    {
        var found = FilePurposeAccess.TryGetRequiredStaffAction(purpose, out var action);

        Assert.False(found);
        Assert.Equal("", action);
    }

    [Fact]
    public void TryGetRequiredStaffAction_maps_the_boarding_purposes_to_their_Boarding_actions()
    {
        Assert.True(FilePurposeAccess.TryGetRequiredStaffAction(FilePurpose.BoardingMedia, out var media));
        Assert.Equal(Actions.Boarding.RecordCare, media);
        Assert.True(FilePurposeAccess.TryGetRequiredStaffAction(FilePurpose.BoardingBelongings, out var belongings));
        Assert.Equal(Actions.Boarding.CheckInOut, belongings);
    }

    [Fact]
    public void TryGetRequiredStaffAction_maps_CatProfilePhoto_to_CatsEditProfile()
    {
        var found = FilePurposeAccess.TryGetRequiredStaffAction(FilePurpose.CatProfilePhoto, out var action);

        Assert.True(found);
        Assert.Equal(Actions.Cats.EditProfile, action);
    }

    [Fact]
    public void TryGetRequiredStaffAction_maps_ProductImage_to_ShopProductsManage()
    {
        var found = FilePurposeAccess.TryGetRequiredStaffAction(FilePurpose.ProductImage, out var action);

        Assert.True(found);
        Assert.Equal(Actions.Shop.ProductsManage, action);
    }

    [Fact]
    public void IsAllowedForCustomer_refuses_ProductImage()
    {
        Assert.False(FilePurposeAccess.IsAllowedForCustomer(FilePurpose.ProductImage));
    }

    [Theory]
    [MemberData(nameof(EveryCanonicalPurposeExceptCatProfilePhoto))]
    public void IsAllowedForCustomer_refuses_every_purpose_except_CatProfilePhoto(FilePurpose purpose)
    {
        Assert.False(FilePurposeAccess.IsAllowedForCustomer(purpose));
    }

    [Fact]
    public void IsAllowedForCustomer_allows_CatProfilePhoto()
    {
        Assert.True(FilePurposeAccess.IsAllowedForCustomer(FilePurpose.CatProfilePhoto));
    }
}
