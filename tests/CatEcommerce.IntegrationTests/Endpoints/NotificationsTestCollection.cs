using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-NTF-001 §16.1 -- the notification worker's due-work queues (<c>NotificationWorkQueue</c>)
/// are process-wide, not scoped to one test's own rows: <c>GetPendingRequestIdsAsync</c>/
/// <c>GetDueNotificationIdsAsync</c> batch up to 50 oldest-first items system-wide. xUnit runs
/// different test CLASSES in parallel by default, and two Notification test classes racing a
/// delivery pass at the same moment can otherwise "steal" or reorder each other's due items (an
/// observed, reproducible flake, not a hypothetical one). Every Notification endpoint test class
/// shares this one collection, disabling parallelism <b>between them</b> only -- they still run in
/// parallel with unrelated areas' test classes (Accounts, Administration, Cat).
/// </summary>
[CollectionDefinition("Notifications", DisableParallelization = true)]
public sealed class NotificationsTestCollection;
