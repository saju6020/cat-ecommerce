namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-XCT-001 §11.6 -- the C4 (reapply after restore) call stamps this prefix on the correlation of every account
/// it anonymises, so the ledger-opening handler can tell a restore reconciliation from a customer or staff request.
/// A correlation is a Guid, so the marker is the first 4 bytes of the id.</summary>
public static class RetentionCorrelation
{
    private const uint ReapplyMarker = 0x52455041; // "REPA"

    public static Guid NewReapply()
    {
        var bytes = Guid.NewGuid().ToByteArray();
        BitConverter.GetBytes(ReapplyMarker).CopyTo(bytes, 0);
        return new Guid(bytes);
    }

    public static bool IsReapply(Guid correlationId) =>
        BitConverter.ToUInt32(correlationId.ToByteArray(), 0) == ReapplyMarker;
}
