namespace Claude.Agent;

public static partial class ClaudeAgentModelFactory
{
    /// <summary>Builds a <see cref="Claude.Agent.ArtifactPublishToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactPublishToolInput ArtifactPublishToolInput(
        string filePath = "",
        string? favicon = null,
        string? icon = null,
        string? title = null,
        string? description = null,
        string? label = null,
        string? url = null,
        bool? force = null,
        string? contract = null) =>
        new(filePath, favicon, icon, title, description, label, url, force, contract);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactListToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactListToolInput ArtifactListToolInput(long? limit = null, ArtifactScope? scope = null) =>
        new(limit, scope);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactReadToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactReadToolInput ArtifactReadToolInput(string url = "", string? prompt = null) =>
        new(url, prompt);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactListTypesToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactListTypesToolInput ArtifactListTypesToolInput() =>
        new();

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactWatchToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactWatchToolInput ArtifactWatchToolInput(string? url = null) =>
        new(url);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactUnwatchToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactUnwatchToolInput ArtifactUnwatchToolInput(string? url = null) =>
        new(url);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactStatusToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactStatusToolInput ArtifactStatusToolInput(string? url = null) =>
        new(url);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactUploadAssetToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactUploadAssetToolInput ArtifactUploadAssetToolInput(
        string url = "",
        string? filePath = null,
        IEnumerable<string>? filePaths = null) =>
        new(url, filePath, [.. filePaths ?? []]);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactListAssetsToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactListAssetsToolInput ArtifactListAssetsToolInput(string url = "", string? after = null) =>
        new(url, after);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactReadAssetToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactReadAssetToolInput ArtifactReadAssetToolInput(
        string url = "",
        string assetId = "",
        string? outputDirectory = null) =>
        new(url, assetId, outputDirectory);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactDeleteAssetToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactDeleteAssetToolInput ArtifactDeleteAssetToolInput(string url = "", string assetId = "") =>
        new(url, assetId);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactStoredPage"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactStoredPage ArtifactStoredPage(
        string contract = "",
        string? preferredContract = null,
        bool? carried = null,
        string? read = null) =>
        new(contract, preferredContract, carried, read);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactPublishedToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactPublishedToolOutput ArtifactPublishedToolOutput(
        string url = "",
        string path = "",
        string? artifactId = null,
        string? title = null,
        string? version = null,
        ArtifactStoredPage? stored = null,
        IEnumerable<string>? warnings = null,
        long? publishesRemaining = null,
        double? publishesResetAt = null,
        string? contract = null,
        bool? updated = null,
        string? icon = null,
        bool? faviconSent = null,
        bool? iconDropped = null,
        string? audience = null,
        long? sequence = null,
        bool? unchanged = null,
        string? liveSubscription = null) =>
        new(url, path, artifactId, title, version, stored, [.. warnings ?? []], publishesRemaining, publishesResetAt, contract, updated, icon, faviconSent, iconDropped, audience, sequence, unchanged, liveSubscription);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactListEntry"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactListEntry ArtifactListEntry(
        string title = "",
        string url = "",
        string? favicon = null,
        string? updatedAt = null,
        ArtifactRelation? relation = null,
        bool? external = null,
        ArtifactRole? role = null) =>
        new(title, url, favicon, updatedAt, relation, external, role);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactListToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactListToolOutput ArtifactListToolOutput(
        IEnumerable<ArtifactListEntry>? artifacts = null,
        bool? truncated = null,
        ArtifactListScope? scope = null,
        bool? externalListed = null) =>
        new([.. artifacts ?? []], truncated, scope, externalListed);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactReadResult"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactReadResult ArtifactReadResult(
        string url = "",
        long bytes = 0,
        long code = 0,
        string codeText = "",
        string result = "",
        TimeSpan duration = default) =>
        new(url, bytes, code, codeText, result, duration);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactVersionReference"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactVersionReference ArtifactVersionReference(
        string slug = "",
        string? version = null,
        bool? seeded = null) =>
        new(slug, version, seeded);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactReadToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactReadToolOutput ArtifactReadToolOutput(
        ArtifactReadResult? read = null,
        ArtifactVersionReference? artifactRead = null) =>
        new(read ?? ArtifactReadResult(), artifactRead);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactWatchState"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactWatchState ArtifactWatchState(
        string url = "",
        bool watching = false,
        string outcome = "",
        string? reason = null,
        string? durableSkipReason = null,
        string? taskId = null,
        double? since = null,
        double? tokenExpiresAt = null,
        string? rail = null,
        string? triggerId = null,
        string? durableSince = null,
        long? statusCode = null,
        string? detail = null,
        string? note = null,
        IEnumerable<string>? events = null) =>
        new(url, watching, outcome, reason, durableSkipReason, taskId, since, tokenExpiresAt, rail, triggerId, durableSince, statusCode, detail, note, [.. events ?? []]);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactWatchToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactWatchToolOutput ArtifactWatchToolOutput(ArtifactWatchState? watch = null) =>
        new(watch ?? ArtifactWatchState());

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactUnwatchState"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactUnwatchState ArtifactUnwatchState(string url = "", bool wasWatching = false) =>
        new(url, wasWatching);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactUnwatchToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactUnwatchToolOutput ArtifactUnwatchToolOutput(ArtifactUnwatchState? unwatch = null) =>
        new(unwatch ?? ArtifactUnwatchState());

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactLiveWatch"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactLiveWatch ArtifactLiveWatch(
        string url = "",
        string taskId = "",
        double since = 0,
        bool isExplicit = false,
        bool connected = false,
        double tokenExpiresAt = 0,
        bool? connecting = null,
        string? armedVia = null) =>
        new(url, taskId, since, isExplicit, connected, tokenExpiresAt, connecting, armedVia);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactDurableWakeWatch"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactDurableWakeWatch ArtifactDurableWakeWatch(
        string url = "",
        string triggerId = "",
        string since = "",
        IEnumerable<string>? events = null,
        bool? restored = null) =>
        new(url, triggerId, since, [.. events ?? []], restored);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactStoppedWatch"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactStoppedWatch ArtifactStoppedWatch(
        string url = "",
        string stopKind = "",
        double? since = null,
        bool? isExplicit = null,
        string? armedVia = null) =>
        new(url, stopKind, since, isExplicit, armedVia);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactWatchArm"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactWatchArm ArtifactWatchArm(
        string url = "",
        string state = "",
        string? rail = null,
        bool? reconnect = null,
        long? failureCount = null,
        long? maxFailureCount = null,
        TimeSpan? nextIn = null,
        string? lastFailure = null,
        string? reason = null,
        string? detail = null,
        string? serverMessage = null,
        double? at = null) =>
        new(url, state, rail, reconnect, failureCount, maxFailureCount, nextIn, lastFailure, reason, detail, serverMessage, at);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactStatusToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactStatusToolOutput ArtifactStatusToolOutput(
        IEnumerable<ArtifactWatch>? watches = null,
        string? filterUrl = null,
        IEnumerable<ArtifactWatchArm>? arms = null) =>
        new([.. watches ?? []], filterUrl, [.. arms ?? []]);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactWriteReference"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactWriteReference ArtifactWriteReference(string url = "") =>
        new(url);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactAssetUpload"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactAssetUpload ArtifactAssetUpload(
        string id = "",
        string url = "",
        long sizeInBytes = 0,
        string contentType = "",
        string fileName = "",
        string? sha256 = null) =>
        new(id, url, sizeInBytes, contentType, fileName, sha256);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactAssetUploadToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactAssetUploadToolOutput ArtifactAssetUploadToolOutput(
        ArtifactAssetUpload? assetUpload = null,
        ArtifactWriteReference? written = null) =>
        new(assetUpload ?? ArtifactAssetUpload(), written);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactAssetUploadedOutcome"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactAssetUploadedOutcome ArtifactAssetUploadedOutcome(
        string filePath = "",
        string id = "",
        string url = "",
        long sizeInBytes = 0,
        string contentType = "",
        string fileName = "",
        string? sha256 = null) =>
        new(filePath, id, url, sizeInBytes, contentType, fileName, sha256);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactAssetUploadFailedOutcome"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactAssetUploadFailedOutcome ArtifactAssetUploadFailedOutcome(
        string filePath = "",
        ArtifactAssetUploadStatus status = default,
        string reason = "",
        string message = "",
        bool? mayBeStored = null) =>
        new(filePath, status, reason, message, mayBeStored);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactAssetUploadBatch"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactAssetUploadBatch ArtifactAssetUploadBatch(
        string url = "",
        IEnumerable<ArtifactAssetUploadOutcome>? results = null) =>
        new(url, [.. results ?? []]);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactAssetUploadsToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactAssetUploadsToolOutput ArtifactAssetUploadsToolOutput(
        ArtifactAssetUploadBatch? assetUploads = null,
        ArtifactWriteReference? written = null) =>
        new(assetUploads ?? ArtifactAssetUploadBatch(), written);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactAsset"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactAsset ArtifactAsset(
        string id = "",
        string url = "",
        string contentType = "",
        long sizeInBytes = 0,
        string createdAt = "",
        string? sha256 = null) =>
        new(id, url, contentType, sizeInBytes, createdAt, sha256);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactAssetUsage"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactAssetUsage ArtifactAssetUsage(
        long fileCount = 0,
        long bytes = 0,
        long maxFileCount = 0,
        long maxBytes = 0) =>
        new(fileCount, bytes, maxFileCount, maxBytes);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactAssetListing"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactAssetListing ArtifactAssetListing(
        string url = "",
        IEnumerable<ArtifactAsset>? assets = null,
        ArtifactAssetUsage? usage = null,
        string? next = null,
        bool? cowritten = null,
        bool? outsideWriter = null) =>
        new(url, [.. assets ?? []], usage ?? ArtifactAssetUsage(), next, cowritten, outsideWriter);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactAssetListToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactAssetListToolOutput ArtifactAssetListToolOutput(ArtifactAssetListing? assetList = null) =>
        new(assetList ?? ArtifactAssetListing());

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactAssetRead"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactAssetRead ArtifactAssetRead(
        string id = "",
        string path = "",
        long sizeInBytes = 0,
        string contentType = "",
        string sha256 = "",
        bool? cowritten = null,
        bool? outsideWriter = null,
        bool? publicRead = null,
        bool? foreign = null) =>
        new(id, path, sizeInBytes, contentType, sha256, cowritten, outsideWriter, publicRead, foreign);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactAssetReadToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactAssetReadToolOutput ArtifactAssetReadToolOutput(ArtifactAssetRead? assetRead = null) =>
        new(assetRead ?? ArtifactAssetRead());

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactAssetDeletion"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactAssetDeletion ArtifactAssetDeletion(string id = "", bool deleted = false) =>
        new(id, deleted);

    /// <summary>Builds a <see cref="Claude.Agent.ArtifactAssetDeleteToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static ArtifactAssetDeleteToolOutput ArtifactAssetDeleteToolOutput(
        ArtifactAssetDeletion? assetDelete = null,
        ArtifactWriteReference? written = null) =>
        new(assetDelete ?? ArtifactAssetDeletion(), written);
}
