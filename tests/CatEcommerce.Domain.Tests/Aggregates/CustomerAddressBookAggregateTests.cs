using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;

namespace CatEcommerce.Domain.Tests.Aggregates;

/// <summary>HO-ACC-001 §15 -- Profile, addresses, preferences (E).</summary>
public sealed class CustomerAddressBookAggregateTests
{
    private static CustomerAddressBookAggregate Opened()
    {
        var book = new CustomerAddressBookAggregate(Guid.NewGuid());
        book.Open(new CustomerAddressBookOpenDto(TestUserContext.Create(), Guid.NewGuid()));
        return book;
    }

    private static DeliveryAddressAddDto AddDto(Guid? id = null) => new(
        id ?? Guid.NewGuid(), "Home", "123 Main St", null, "Dhaka", "Dhaka", null, null,
        DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid());

    // U-E4
    [Fact]
    public void AddAddress_First_BecomesDefault()
    {
        var book = Opened();
        var id = Guid.NewGuid();
        book.AddAddress(AddDto(id));
        Assert.Equal(id, book.DefaultAddressId);
    }

    [Fact]
    public void RemoveAddress_Default_WhileOthersExist_Throws()
    {
        var book = Opened();
        var first = Guid.NewGuid();
        book.AddAddress(AddDto(first));
        book.AddAddress(AddDto());

        var ex = Assert.Throws<DomainRuleViolationException>(() => book.RemoveAddress(new DeliveryAddressRemoveDto(first, TestUserContext.Create(), Guid.NewGuid())));
        Assert.Equal("DefaultAddressCannotBeRemoved", ex.ErrorCode);
    }

    [Fact]
    public void RemoveAddress_Last_ClearsDefault()
    {
        var book = Opened();
        var id = Guid.NewGuid();
        book.AddAddress(AddDto(id));

        book.RemoveAddress(new DeliveryAddressRemoveDto(id, TestUserContext.Create(), Guid.NewGuid()));

        Assert.Null(book.DefaultAddressId);
        Assert.Empty(book.Addresses);
    }

    [Fact]
    public void SetDefaultAddress_AlreadyDefault_NoEventNoVersionBump()
    {
        var book = Opened();
        var id = Guid.NewGuid();
        book.AddAddress(AddDto(id));
        book.ClearEvents();
        var versionBefore = book.Version;

        book.SetDefaultAddress(new DeliveryAddressSetDefaultDto(id, TestUserContext.Create(), Guid.NewGuid()));

        Assert.Empty(book.Events);
        Assert.Equal(versionBefore, book.Version);
    }

    // U-E6
    [Fact]
    public void AddAddress_Eleventh_Throws()
    {
        var book = Opened();
        for (var i = 0; i < 10; i++) book.AddAddress(AddDto());

        var ex = Assert.Throws<DomainRuleViolationException>(() => book.AddAddress(AddDto()));
        Assert.Equal("DeliveryAddressLimitExceeded", ex.ErrorCode);
    }

    // U-E5
    [Fact]
    public void RandomAddUpdateRemoveSetDefaultSequence_NeverLeavesInconsistentDefault()
    {
        var book = Opened();
        var random = new Random(42);
        var ids = new List<Guid>();

        for (var i = 0; i < 200; i++)
        {
            var operation = random.Next(4);
            if (operation == 0 && ids.Count < 10)
            {
                var id = Guid.NewGuid();
                book.AddAddress(AddDto(id));
                ids.Add(id);
            }
            else if (operation == 1 && ids.Count > 0)
            {
                var id = ids[random.Next(ids.Count)];
                if (book.DefaultAddressId == id && ids.Count > 1) continue; // would throw by design
                book.RemoveAddress(new DeliveryAddressRemoveDto(id, TestUserContext.Create(), Guid.NewGuid()));
                ids.Remove(id);
            }
            else if (operation == 2 && ids.Count > 0)
            {
                var id = ids[random.Next(ids.Count)];
                book.SetDefaultAddress(new DeliveryAddressSetDefaultDto(id, TestUserContext.Create(), Guid.NewGuid()));
            }
            else if (ids.Count > 0)
            {
                var id = ids[random.Next(ids.Count)];
                book.UpdateAddress(new DeliveryAddressUpdateDto(id, "Updated", "New line", null, "City", "District", null, null, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid()));
            }
        }

        Assert.Equal(book.Addresses.Count == 0, book.DefaultAddressId is null);
    }
}
