using System.ComponentModel.DataAnnotations;

namespace CatEcommerce.Infrastructure.Configuration;

/// <summary>HO-XCT-001 §12.2 -- section "Operations". Holds no secret (NFR-SEC-7).</summary>
public sealed class OperationsOptions
{
    public const string SectionName = "Operations";

    [Required]
    public RetentionWorkerOptions RetentionWorker { get; set; } = new();

    [Required]
    public DatabaseOptions Database { get; set; } = new();

    public sealed class RetentionWorkerOptions
    {
        /// <summary>False in Testing: tests call <c>RetentionWorkCycle</c> directly.</summary>
        public bool Enabled { get; set; } = true;

        [Range(1, 3600)]
        public int PollSeconds { get; set; } = 60;
    }

    public sealed class DatabaseOptions
    {
        /// <summary>ADR-300. May be false only in Development and Testing; <c>DatabaseRoleGuard</c> refuses to start the host otherwise.</summary>
        public bool EnforceNonOwnerRole { get; set; } = true;
    }
}
