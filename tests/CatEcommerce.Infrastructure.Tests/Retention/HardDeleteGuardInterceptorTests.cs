using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Retention;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.SharedDto.Cat;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Infrastructure.Tests.Retention;

/// <summary>
/// HO-XCT-001 U-A3..U-A6 -- ADR-299 layer 2. The guard runs on the change tracker, so these tests never open a connection:
/// they track real aggregates, mutate or remove them, and call the guard's check directly.
/// </summary>
public sealed class HardDeleteGuardInterceptorTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static UserContext Actor => new(Guid.NewGuid(), Guid.NewGuid());

    private static CatEcommerceDbContext NewDb() => new(new DbContextOptionsBuilder<CatEcommerceDbContext>()
        .UseNpgsql("Host=localhost;Database=model_only;Username=model_only").Options);

    private static CatAggregate CatWithAllergy()
    {
        var cat = new CatAggregate(Guid.NewGuid());
        cat.Register(new CatRegistrationDto(
            Guid.NewGuid(), "Mittens", "Cat", CatSex.Female, null, DateOfBirthPrecision.Estimated, null, null, Today, Actor, Guid.NewGuid()));
        cat.UpdateMedicalProfile(new CatMedicalProfileDto(
            [new AllergyInput("Chicken", "Itching", AllergySeverity.Mild, Today)], [], [], null, null, Actor, Guid.NewGuid()));
        return cat;
    }

    // U-A3
    [Fact]
    public void U_A3_RemovingAForbiddenRoot_IsRefusedBeforeAnySql()
    {
        using var db = NewDb();
        var cat = CatWithAllergy();
        db.Attach(cat);
        db.Remove(cat);

        var refused = Assert.Throws<ProtectedRecordDeletionException>(() => HardDeleteGuardInterceptor.EnsureNoProtectedDeletes(db));

        Assert.Equal("clinic", refused.Schema);
        Assert.Equal("cat", refused.Table);
        Assert.Equal(HardDeletePolicy.Forbidden, refused.Policy);
    }

    // U-A4
    [Fact]
    public void U_A4_AnAggregateReplacingItsOwnCurrentStateCollection_IsAllowed()
    {
        using var db = NewDb();
        var cat = CatWithAllergy();
        db.Attach(cat);

        // F32: UpdateMedicalProfile replaces the allergy collection wholesale, so EF marks the old owned item Deleted.
        cat.UpdateMedicalProfile(new CatMedicalProfileDto([], [], [], null, null, Actor, Guid.NewGuid()));

        Assert.Contains(db.ChangeTracker.Entries(), e => e.State == EntityState.Deleted && e.Metadata.GetTableName() == "cat_allergy");
        HardDeleteGuardInterceptor.EnsureNoProtectedDeletes(db);
    }

    // U-A5
    [Fact]
    public void U_A5_RemovingAnOwnedItemWhoseOwnerIsNotTracked_IsRefused()
    {
        using var db = NewDb();
        var addressBook = new CustomerAddressBookAggregate(Guid.NewGuid());
        addressBook.Open(new CustomerAddressBookOpenDto(Actor, Guid.NewGuid()));
        addressBook.AddAddress(new DeliveryAddressAddDto(Guid.NewGuid(), "Home", "1 Road", null, "Dhaka", "Dhaka", null, null, DateTimeOffset.UtcNow, Actor, Guid.NewGuid()));
        var address = addressBook.Addresses.Single();

        // Track the owner, then detach ONLY the owner so the owned item is deleted with no tracked owner.
        db.Attach(addressBook);
        db.Entry(addressBook).State = EntityState.Detached;
        db.Entry(address).State = EntityState.Deleted;

        var refused = Assert.Throws<ProtectedRecordDeletionException>(() => HardDeleteGuardInterceptor.EnsureNoProtectedDeletes(db));
        Assert.Equal("customer_delivery_address", refused.Table);
        Assert.Equal(HardDeletePolicy.AggregateOnly, refused.Policy);
    }

    // U-A6
    [Fact]
    public void U_A6_ATrackedDeleteOfAPurgeableRoot_IsRefused_ThePurgerIsTheOnlyPath()
    {
        using var db = NewDb();
        var session = new CustomerSessionAggregate(Guid.NewGuid());
        session.Issue(new SessionIssueDto(Guid.NewGuid(), "hash", Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), null, null, Actor, Guid.NewGuid(), "csrf"));
        db.Attach(session);
        db.Remove(session);

        var refused = Assert.Throws<ProtectedRecordDeletionException>(() => HardDeleteGuardInterceptor.EnsureNoProtectedDeletes(db));
        Assert.Equal("customer_session", refused.Table);
        Assert.Equal(HardDeletePolicy.Purgeable, refused.Policy);
    }

    [Fact]
    public void TheNamedChangeTrackedPurgeException_IsOnlyTheStoredFileRow_AndIsPurgeableInTheClassification()
    {
        var entry = Assert.Single(HardDeleteGuardInterceptor.ChangeTrackedPurgeAllowList);
        var classification = RecordClassification.Find(entry.Schema, entry.Table);

        Assert.NotNull(classification);
        Assert.Equal(HardDeletePolicy.Purgeable, classification!.HardDeletePolicy);
        Assert.False(string.IsNullOrWhiteSpace(entry.Reason));
    }
}
