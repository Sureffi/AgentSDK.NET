namespace Claude.Agent;

/// <summary>What the model asked the Artifact tool to do. The action is which type this is, one type a value, each with the keys that action takes; an absent action publishes.</summary>
public abstract class ArtifactToolInput : ToolInput
{
    private protected ArtifactToolInput()
    {
    }
}

/// <summary>An Artifact that publishes a page. The declaration names this action, and a call that names no action is this one too. The capabilities the page declares are arbitrary JSON, so they are read through the raw beside this view and are no member here.</summary>
/// <param name="filePath">The file to publish.</param>
/// <param name="favicon">The tab icon, under the name the binary now tells the model not to use; null when the call gave none.</param>
/// <param name="icon">One plain word for the tab icon; null when the call gave none.</param>
/// <param name="title">The name for the page, where the file itself carries none; null when the call gave none.</param>
/// <param name="description">One sentence under the name in the gallery; null when the call gave none.</param>
/// <param name="label">A few words naming this publish; null when the call gave none.</param>
/// <param name="url">The artifact to publish over in place; null publishes a new one, or over the one this session published.</param>
/// <param name="force">Whether to overwrite a newer version and lose it; null when the call said nothing.</param>
/// <param name="contract">The runtime version to publish against; null keeps the one the page has.</param>
public sealed class ArtifactPublishToolInput(
    string filePath,
    string? favicon = null,
    string? icon = null,
    string? title = null,
    string? description = null,
    string? label = null,
    string? url = null,
    bool? force = null,
    string? contract = null) : ArtifactToolInput
{
    /// <summary>The file to publish.</summary>
    public string FilePath { get; } = filePath;

    /// <summary>The tab icon, under the name the binary now tells the model not to use; null when the call gave none.</summary>
    public string? Favicon { get; } = favicon;

    /// <summary>One plain word for the tab icon; null when the call gave none.</summary>
    public string? Icon { get; } = icon;

    /// <summary>The name for the page, where the file itself carries none; null when the call gave none.</summary>
    public string? Title { get; } = title;

    /// <summary>One sentence under the name in the gallery; null when the call gave none.</summary>
    public string? Description { get; } = description;

    /// <summary>A few words naming this publish; null when the call gave none.</summary>
    public string? Label { get; } = label;

    /// <summary>The artifact to publish over in place; null publishes a new one, or over the one this session published.</summary>
    public string? Url { get; } = url;

    /// <summary>Whether to overwrite a newer version and lose it; null when the call said nothing.</summary>
    public bool? Force { get; } = force;

    /// <summary>The runtime version to publish against; null keeps the one the page has.</summary>
    public string? Contract { get; } = contract;
}

/// <summary>An Artifact that lists artifacts.</summary>
/// <param name="limit">How many artifacts the listing may name; null when the call left it to the binary.</param>
/// <param name="scope">Whose artifacts the listing asks for; null when the call said nothing.</param>
public sealed class ArtifactListToolInput(long? limit = null, ArtifactScope? scope = null) : ArtifactToolInput
{
    /// <summary>How many artifacts the listing may name; null when the call left it to the binary.</summary>
    public long? Limit { get; } = limit;

    /// <summary>Whose artifacts the listing asks for; null when the call said nothing.</summary>
    public ArtifactScope? Scope { get; } = scope;
}

/// <summary>An Artifact that reads a published page.</summary>
/// <param name="url">The artifact to read.</param>
/// <param name="prompt">What to pull out of an artifact someone else owns; null when the call asked for none.</param>
public sealed class ArtifactReadToolInput(string url, string? prompt = null) : ArtifactToolInput
{
    /// <summary>The artifact to read.</summary>
    public string Url { get; } = url;

    /// <summary>What to pull out of an artifact someone else owns; null when the call asked for none.</summary>
    public string? Prompt { get; } = prompt;
}

/// <summary>An Artifact that lists the types a new artifact can start from. The declaration gives it no other key.</summary>
public sealed class ArtifactListTypesToolInput : ArtifactToolInput
{
}

/// <summary>An Artifact that asks to follow a page's new versions.</summary>
/// <param name="url">The artifact to follow; null when the call named none.</param>
public sealed class ArtifactWatchToolInput(string? url = null) : ArtifactToolInput
{
    /// <summary>The artifact to follow; null when the call named none.</summary>
    public string? Url { get; } = url;
}

/// <summary>An Artifact that asks to stop following a page.</summary>
/// <param name="url">The artifact to stop following; null when the call named none.</param>
public sealed class ArtifactUnwatchToolInput(string? url = null) : ArtifactToolInput
{
    /// <summary>The artifact to stop following; null when the call named none.</summary>
    public string? Url { get; } = url;
}

/// <summary>An Artifact that asks which pages the session follows.</summary>
/// <param name="url">The one artifact to ask about; null asks about all of them.</param>
public sealed class ArtifactStatusToolInput(string? url = null) : ArtifactToolInput
{
    /// <summary>The one artifact to ask about; null asks about all of them.</summary>
    public string? Url { get; } = url;
}

/// <summary>An Artifact that adds files to an artifact's asset store.</summary>
/// <param name="url">The artifact the files go into.</param>
/// <param name="filePath">The one file to upload; null when the call sent several instead.</param>
/// <param name="filePaths">Several files to upload in one call, in place of the single one.</param>
public sealed class ArtifactUploadAssetToolInput(
    string url,
    string? filePath = null,
    IReadOnlyList<string>? filePaths = null) : ArtifactToolInput
{
    /// <summary>The artifact the files go into.</summary>
    public string Url { get; } = url;

    /// <summary>The one file to upload; null when the call sent several instead.</summary>
    public string? FilePath { get; } = filePath;

    /// <summary>Several files to upload in one call, in place of the single one.</summary>
    public IReadOnlyList<string> FilePaths { get; } = filePaths ?? [];
}

/// <summary>An Artifact that pages through the assets an artifact holds.</summary>
/// <param name="url">The artifact whose assets are listed.</param>
/// <param name="after">Where a previous listing left off; null when the call asked for the first page.</param>
public sealed class ArtifactListAssetsToolInput(string url, string? after = null) : ArtifactToolInput
{
    /// <summary>The artifact whose assets are listed.</summary>
    public string Url { get; } = url;

    /// <summary>Where a previous listing left off; null when the call asked for the first page.</summary>
    public string? After { get; } = after;
}

/// <summary>An Artifact that saves one asset to a local file.</summary>
/// <param name="url">The artifact the asset belongs to.</param>
/// <param name="assetId">The asset to save.</param>
/// <param name="outputDirectory">Where to save it; null when the call left it to the binary.</param>
public sealed class ArtifactReadAssetToolInput(string url, string assetId, string? outputDirectory = null) : ArtifactToolInput
{
    /// <summary>The artifact the asset belongs to.</summary>
    public string Url { get; } = url;

    /// <summary>The asset to save.</summary>
    public string AssetId { get; } = assetId;

    /// <summary>Where to save it; null when the call left it to the binary.</summary>
    public string? OutputDirectory { get; } = outputDirectory;
}

/// <summary>An Artifact that deletes one asset for good.</summary>
/// <param name="url">The artifact the asset belongs to.</param>
/// <param name="assetId">The asset to delete.</param>
public sealed class ArtifactDeleteAssetToolInput(string url, string assetId) : ArtifactToolInput
{
    /// <summary>The artifact the asset belongs to.</summary>
    public string Url { get; } = url;

    /// <summary>The asset to delete.</summary>
    public string AssetId { get; } = assetId;
}

/// <summary>What an Artifact handed back. The binary answers in one of eleven shapes, one type each, and each is told apart by a key only it carries rather than by a kind the wire names.</summary>
public abstract class ArtifactToolOutput : ToolOutput
{
    private protected ArtifactToolOutput()
    {
    }
}

/// <summary>What the artifact store kept for a published page. The capabilities it holds are arbitrary JSON, so they are read through the raw beside this view and are no member here.</summary>
/// <param name="contract">The runtime version the page is published against.</param>
/// <param name="preferredContract">The version the page asked for, where it differs; null when it did not ask.</param>
/// <param name="carried">Whether the stored declaration was carried over from the version before; null when the binary said nothing.</param>
/// <param name="read">What the store says about reading the page; null when it said nothing.</param>
public sealed class ArtifactStoredPage(
    string contract,
    string? preferredContract = null,
    bool? carried = null,
    string? read = null)
{
    /// <summary>The runtime version the page is published against.</summary>
    public string Contract { get; } = contract;

    /// <summary>The version the page asked for, where it differs; null when it did not ask.</summary>
    public string? PreferredContract { get; } = preferredContract;

    /// <summary>Whether the stored declaration was carried over from the version before; null when the binary said nothing.</summary>
    public bool? Carried { get; } = carried;

    /// <summary>What the store says about reading the page; null when it said nothing.</summary>
    public string? Read { get; } = read;
}

/// <summary>An Artifact that published a page. The capabilities the page declares are arbitrary JSON, so they are read through the raw beside this view and are no member here.</summary>
/// <param name="url">Where the page is now.</param>
/// <param name="path">The file it was published from.</param>
/// <param name="artifactId">The artifact itself, apart from its address; null when the binary named none.</param>
/// <param name="title">The name the page is shown under; null when the binary named none.</param>
/// <param name="version">The version just published; null when the binary named none.</param>
/// <param name="stored">What the store kept for the page; null when the binary sent none.</param>
/// <param name="warnings">What was worth saying about the publish and did not stop it.</param>
/// <param name="publishesRemaining">How many more publishes the account has in this window; null when the binary said nothing.</param>
/// <param name="publishesResetAt">When that window turns over, as the number the binary sent; the declaration gives it no unit. Null when the binary said nothing.</param>
/// <param name="contract">The runtime version the page is published against; null when the binary named none.</param>
/// <param name="updated">Whether an artifact already there was replaced rather than a new one made; null when the binary said nothing.</param>
/// <param name="icon">The tab icon the page now has; null when the binary named none.</param>
/// <param name="faviconSent">Whether the call gave the icon under the name the binary tells the model not to use; null when it did not.</param>
/// <param name="iconDropped">Whether the icon given was dropped; null when it was not.</param>
/// <param name="audience">Who may reach the page; null when the binary said nothing.</param>
/// <param name="sequence">Where this publish falls in the page's own count of them; null when the binary said nothing.</param>
/// <param name="unchanged">Whether the content was the same as the version before, so nothing new was stored; null when it was not.</param>
/// <param name="liveSubscription">The subscription through which the page is kept up to date; null when the binary named none.</param>
public sealed class ArtifactPublishedToolOutput(
    string url,
    string path,
    string? artifactId = null,
    string? title = null,
    string? version = null,
    ArtifactStoredPage? stored = null,
    IReadOnlyList<string>? warnings = null,
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
    string? liveSubscription = null) : ArtifactToolOutput
{
    /// <summary>Where the page is now.</summary>
    public string Url { get; } = url;

    /// <summary>The file it was published from.</summary>
    public string Path { get; } = path;

    /// <summary>The artifact itself, apart from its address; null when the binary named none.</summary>
    public string? ArtifactId { get; } = artifactId;

    /// <summary>The name the page is shown under; null when the binary named none.</summary>
    public string? Title { get; } = title;

    /// <summary>The version just published; null when the binary named none.</summary>
    public string? Version { get; } = version;

    /// <summary>What the store kept for the page; null when the binary sent none.</summary>
    public ArtifactStoredPage? Stored { get; } = stored;

    /// <summary>What was worth saying about the publish and did not stop it.</summary>
    public IReadOnlyList<string> Warnings { get; } = warnings ?? [];

    /// <summary>How many more publishes the account has in this window; null when the binary said nothing.</summary>
    public long? PublishesRemaining { get; } = publishesRemaining;

    /// <summary>When that window turns over, as the number the binary sent; the declaration gives it no unit. Null when the binary said nothing.</summary>
    public double? PublishesResetAt { get; } = publishesResetAt;

    /// <summary>The runtime version the page is published against; null when the binary named none.</summary>
    public string? Contract { get; } = contract;

    /// <summary>Whether an artifact already there was replaced rather than a new one made; null when the binary said nothing.</summary>
    public bool? Updated { get; } = updated;

    /// <summary>The tab icon the page now has; null when the binary named none.</summary>
    public string? Icon { get; } = icon;

    /// <summary>Whether the call gave the icon under the name the binary tells the model not to use; null when it did not.</summary>
    public bool? FaviconSent { get; } = faviconSent;

    /// <summary>Whether the icon given was dropped; null when it was not.</summary>
    public bool? IconDropped { get; } = iconDropped;

    /// <summary>Who may reach the page; null when the binary said nothing.</summary>
    public string? Audience { get; } = audience;

    /// <summary>Where this publish falls in the page's own count of them; null when the binary said nothing.</summary>
    public long? Sequence { get; } = sequence;

    /// <summary>Whether the content was the same as the version before, so nothing new was stored; null when it was not.</summary>
    public bool? Unchanged { get; } = unchanged;

    /// <summary>The subscription through which the page is kept up to date; null when the binary named none.</summary>
    public string? LiveSubscription { get; } = liveSubscription;
}

/// <summary>One artifact of a listing.</summary>
/// <param name="title">The name the page is shown under.</param>
/// <param name="url">Where the page is.</param>
/// <param name="favicon">The page's tab icon; null when the binary named none.</param>
/// <param name="updatedAt">When it last changed, as the timestamp the binary wrote; null when the binary gave none.</param>
/// <param name="relation">How the account stands to it; null when the binary said nothing.</param>
/// <param name="external">Whether it sits outside the account's own gallery; null when it does not.</param>
/// <param name="role">What the account may do with it; null when the binary said nothing.</param>
public sealed class ArtifactListEntry(
    string title,
    string url,
    string? favicon = null,
    string? updatedAt = null,
    ArtifactRelation? relation = null,
    bool? external = null,
    ArtifactRole? role = null)
{
    /// <summary>The name the page is shown under.</summary>
    public string Title { get; } = title;

    /// <summary>Where the page is.</summary>
    public string Url { get; } = url;

    /// <summary>The page's tab icon; null when the binary named none.</summary>
    public string? Favicon { get; } = favicon;

    /// <summary>When it last changed, as the timestamp the binary wrote; null when the binary gave none.</summary>
    public string? UpdatedAt { get; } = updatedAt;

    /// <summary>How the account stands to it; null when the binary said nothing.</summary>
    public ArtifactRelation? Relation { get; } = relation;

    /// <summary>Whether it sits outside the account's own gallery; null when it does not.</summary>
    public bool? External { get; } = external;

    /// <summary>What the account may do with it; null when the binary said nothing.</summary>
    public ArtifactRole? Role { get; } = role;
}

/// <summary>An Artifact that listed what is there.</summary>
/// <param name="artifacts">The artifacts named.</param>
/// <param name="truncated">Whether more exist than are named here; null when the binary said nothing.</param>
/// <param name="scope">Whose artifacts these are, where the listing went beyond the account's own; null when it did not.</param>
/// <param name="externalListed">Whether artifacts outside the account's own gallery were named; null when none were.</param>
public sealed class ArtifactListToolOutput(
    IReadOnlyList<ArtifactListEntry> artifacts,
    bool? truncated = null,
    ArtifactListScope? scope = null,
    bool? externalListed = null) : ArtifactToolOutput
{
    /// <summary>The artifacts named.</summary>
    public IReadOnlyList<ArtifactListEntry> Artifacts { get; } = artifacts;

    /// <summary>Whether more exist than are named here; null when the binary said nothing.</summary>
    public bool? Truncated { get; } = truncated;

    /// <summary>Whose artifacts these are, where the listing went beyond the account's own; null when it did not.</summary>
    public ArtifactListScope? Scope { get; } = scope;

    /// <summary>Whether artifacts outside the account's own gallery were named; null when none were.</summary>
    public bool? ExternalListed { get; } = externalListed;
}

/// <summary>The fetch behind an Artifact read.</summary>
/// <param name="url">The page that was fetched.</param>
/// <param name="bytes">How many bytes came back.</param>
/// <param name="code">The status the fetch answered with.</param>
/// <param name="codeText">That status in words.</param>
/// <param name="result">What the fetch is worth to the model: the page, or a summary of one someone else owns.</param>
/// <param name="duration">How long the fetch took; the wire carries it in milliseconds.</param>
public sealed class ArtifactReadResult(
    string url,
    long bytes,
    long code,
    string codeText,
    string result,
    TimeSpan duration)
{
    /// <summary>The page that was fetched.</summary>
    public string Url { get; } = url;

    /// <summary>How many bytes came back.</summary>
    public long Bytes { get; } = bytes;

    /// <summary>The status the fetch answered with.</summary>
    public long Code { get; } = code;

    /// <summary>That status in words.</summary>
    public string CodeText { get; } = codeText;

    /// <summary>What the fetch is worth to the model: the page, or a summary of one someone else owns.</summary>
    public string Result { get; } = result;

    /// <summary>How long the fetch took; the wire carries it in milliseconds.</summary>
    public TimeSpan Duration { get; } = duration;
}

/// <summary>An artifact version an Artifact read marked as seen.</summary>
/// <param name="slug">The artifact's name.</param>
/// <param name="version">The version that now counts as read; null when the binary named none.</param>
/// <param name="seeded">Whether the content was already at hand rather than fetched; null when the binary said nothing.</param>
public sealed class ArtifactVersionReference(string slug, string? version = null, bool? seeded = null)
{
    /// <summary>The artifact's name.</summary>
    public string Slug { get; } = slug;

    /// <summary>The version that now counts as read; null when the binary named none.</summary>
    public string? Version { get; } = version;

    /// <summary>Whether the content was already at hand rather than fetched; null when the binary said nothing.</summary>
    public bool? Seeded { get; } = seeded;
}

/// <summary>An Artifact that read a page.</summary>
/// <param name="read">The fetch and what it is worth.</param>
/// <param name="artifactRead">The version this read marked as seen; null when it marked none.</param>
public sealed class ArtifactReadToolOutput(
    ArtifactReadResult read,
    ArtifactVersionReference? artifactRead = null) : ArtifactToolOutput
{
    /// <summary>The fetch and what it is worth.</summary>
    public ArtifactReadResult Read { get; } = read;

    /// <summary>The version this read marked as seen; null when it marked none.</summary>
    public ArtifactVersionReference? ArtifactRead { get; } = artifactRead;
}

/// <summary>What became of a request to follow a page.</summary>
/// <param name="url">The page in question.</param>
/// <param name="watching">Whether the session follows it now.</param>
/// <param name="outcome">What the request came to, in the binary's own word.</param>
/// <param name="reason">Why it came to that; null when the binary gave none.</param>
/// <param name="durableSkipReason">Why the lasting arm of the watch was not set up; null when it was.</param>
/// <param name="taskId">The background task the watch runs as; null when there is none.</param>
/// <param name="since">Where the watch starts from, as the number the binary sent; the declaration gives it no unit. Null when the binary said nothing.</param>
/// <param name="tokenExpiresAt">When the watch's token runs out, as the number the binary sent; the declaration gives it no unit. Null when the binary said nothing.</param>
/// <param name="rail">Which arm of the watch this is about; null when the binary named none.</param>
/// <param name="triggerId">The trigger behind the lasting arm; null when there is none.</param>
/// <param name="durableSince">Where the lasting arm starts from, as the timestamp the binary wrote; null when the binary gave none.</param>
/// <param name="statusCode">The status a request behind the watch answered with; null when the binary said nothing.</param>
/// <param name="detail">What the binary adds to the outcome; null when it added nothing.</param>
/// <param name="note">What the binary says about the watch besides; null when it said nothing.</param>
/// <param name="events">The kinds of change the watch reports.</param>
public sealed class ArtifactWatchState(
    string url,
    bool watching,
    string outcome,
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
    IReadOnlyList<string>? events = null)
{
    /// <summary>The page in question.</summary>
    public string Url { get; } = url;

    /// <summary>Whether the session follows it now.</summary>
    public bool Watching { get; } = watching;

    /// <summary>What the request came to, in the binary's own word.</summary>
    public string Outcome { get; } = outcome;

    /// <summary>Why it came to that; null when the binary gave none.</summary>
    public string? Reason { get; } = reason;

    /// <summary>Why the lasting arm of the watch was not set up; null when it was.</summary>
    public string? DurableSkipReason { get; } = durableSkipReason;

    /// <summary>The background task the watch runs as; null when there is none.</summary>
    public string? TaskId { get; } = taskId;

    /// <summary>Where the watch starts from, as the number the binary sent; the declaration gives it no unit. Null when the binary said nothing.</summary>
    public double? Since { get; } = since;

    /// <summary>When the watch's token runs out, as the number the binary sent; the declaration gives it no unit. Null when the binary said nothing.</summary>
    public double? TokenExpiresAt { get; } = tokenExpiresAt;

    /// <summary>Which arm of the watch this is about; null when the binary named none.</summary>
    public string? Rail { get; } = rail;

    /// <summary>The trigger behind the lasting arm; null when there is none.</summary>
    public string? TriggerId { get; } = triggerId;

    /// <summary>Where the lasting arm starts from, as the timestamp the binary wrote; null when the binary gave none.</summary>
    public string? DurableSince { get; } = durableSince;

    /// <summary>The status a request behind the watch answered with; null when the binary said nothing.</summary>
    public long? StatusCode { get; } = statusCode;

    /// <summary>What the binary adds to the outcome; null when it added nothing.</summary>
    public string? Detail { get; } = detail;

    /// <summary>What the binary says about the watch besides; null when it said nothing.</summary>
    public string? Note { get; } = note;

    /// <summary>The kinds of change the watch reports.</summary>
    public IReadOnlyList<string> Events { get; } = events ?? [];
}

/// <summary>An Artifact that was asked to follow a page.</summary>
/// <param name="watch">What became of the request.</param>
public sealed class ArtifactWatchToolOutput(ArtifactWatchState watch) : ArtifactToolOutput
{
    /// <summary>What became of the request.</summary>
    public ArtifactWatchState Watch { get; } = watch;
}

/// <summary>What became of a request to stop following a page.</summary>
/// <param name="url">The page in question.</param>
/// <param name="wasWatching">Whether the session was following it at all.</param>
public sealed class ArtifactUnwatchState(string url, bool wasWatching)
{
    /// <summary>The page in question.</summary>
    public string Url { get; } = url;

    /// <summary>Whether the session was following it at all.</summary>
    public bool WasWatching { get; } = wasWatching;
}

/// <summary>An Artifact that was asked to stop following a page.</summary>
/// <param name="unwatch">What became of the request.</param>
public sealed class ArtifactUnwatchToolOutput(ArtifactUnwatchState unwatch) : ArtifactToolOutput
{
    /// <summary>What became of the request.</summary>
    public ArtifactUnwatchState Unwatch { get; } = unwatch;
}

/// <summary>One page the session follows. The binary writes it in one of three shapes, one type each, told apart by the arm it names and by that arm's absence.</summary>
public abstract class ArtifactWatch
{
    private protected ArtifactWatch()
    {
    }
}

/// <summary>A page followed over a connection held open. The binary names no arm for it.</summary>
/// <param name="url">The page being followed.</param>
/// <param name="taskId">The background task the watch runs as.</param>
/// <param name="since">Where the watch starts from, as the number the binary sent; the declaration gives it no unit.</param>
/// <param name="isExplicit">Whether the session was asked for this watch rather than taking it up itself.</param>
/// <param name="connected">Whether the connection stands.</param>
/// <param name="tokenExpiresAt">When the watch's token runs out, as the number the binary sent; the declaration gives it no unit.</param>
/// <param name="connecting">Whether the connection is being made; null when the binary said nothing.</param>
/// <param name="armedVia">What set the watch up; null when the binary said nothing.</param>
public sealed class ArtifactLiveWatch(
    string url,
    string taskId,
    double since,
    bool isExplicit,
    bool connected,
    double tokenExpiresAt,
    bool? connecting = null,
    string? armedVia = null) : ArtifactWatch
{
    /// <summary>The page being followed.</summary>
    public string Url { get; } = url;

    /// <summary>The background task the watch runs as.</summary>
    public string TaskId { get; } = taskId;

    /// <summary>Where the watch starts from, as the number the binary sent; the declaration gives it no unit.</summary>
    public double Since { get; } = since;

    /// <summary>Whether the session was asked for this watch rather than taking it up itself.</summary>
    public bool IsExplicit { get; } = isExplicit;

    /// <summary>Whether the connection stands.</summary>
    public bool Connected { get; } = connected;

    /// <summary>When the watch's token runs out, as the number the binary sent; the declaration gives it no unit.</summary>
    public double TokenExpiresAt { get; } = tokenExpiresAt;

    /// <summary>Whether the connection is being made; null when the binary said nothing.</summary>
    public bool? Connecting { get; } = connecting;

    /// <summary>What set the watch up; null when the binary said nothing.</summary>
    public string? ArmedVia { get; } = armedVia;
}

/// <summary>A page followed by a trigger that wakes the session, which outlives any held connection.</summary>
/// <param name="url">The page being followed.</param>
/// <param name="triggerId">The trigger that wakes the session.</param>
/// <param name="since">Where the watch starts from, as the timestamp the binary wrote; this arm writes it as text where the held connection writes a number.</param>
/// <param name="events">The kinds of change the trigger fires on.</param>
/// <param name="restored">Whether the watch was brought back with the session rather than set up in it; null when the binary said nothing.</param>
public sealed class ArtifactDurableWakeWatch(
    string url,
    string triggerId,
    string since,
    IReadOnlyList<string>? events = null,
    bool? restored = null) : ArtifactWatch
{
    /// <summary>The page being followed.</summary>
    public string Url { get; } = url;

    /// <summary>The trigger that wakes the session.</summary>
    public string TriggerId { get; } = triggerId;

    /// <summary>Where the watch starts from, as the timestamp the binary wrote; this arm writes it as text where the held connection writes a number.</summary>
    public string Since { get; } = since;

    /// <summary>The kinds of change the trigger fires on.</summary>
    public IReadOnlyList<string> Events { get; } = events ?? [];

    /// <summary>Whether the watch was brought back with the session rather than set up in it; null when the binary said nothing.</summary>
    public bool? Restored { get; } = restored;
}

/// <summary>A page whose held connection has stopped.</summary>
/// <param name="url">The page that was being followed.</param>
/// <param name="stopKind">What stopped it, in the binary's own word.</param>
/// <param name="since">Where the watch started from, as the number the binary sent; the declaration gives it no unit. Null when the binary said nothing.</param>
/// <param name="isExplicit">Whether the session was asked for the watch rather than taking it up itself; null when the binary said nothing.</param>
/// <param name="armedVia">What set the watch up; null when the binary said nothing.</param>
public sealed class ArtifactStoppedWatch(
    string url,
    string stopKind,
    double? since = null,
    bool? isExplicit = null,
    string? armedVia = null) : ArtifactWatch
{
    /// <summary>The page that was being followed.</summary>
    public string Url { get; } = url;

    /// <summary>What stopped it, in the binary's own word.</summary>
    public string StopKind { get; } = stopKind;

    /// <summary>Where the watch started from, as the number the binary sent; the declaration gives it no unit. Null when the binary said nothing.</summary>
    public double? Since { get; } = since;

    /// <summary>Whether the session was asked for the watch rather than taking it up itself; null when the binary said nothing.</summary>
    public bool? IsExplicit { get; } = isExplicit;

    /// <summary>What set the watch up; null when the binary said nothing.</summary>
    public string? ArmedVia { get; } = armedVia;
}

/// <summary>One connection behind the watches, and how it is faring.</summary>
/// <param name="url">The page the connection is for.</param>
/// <param name="state">Where the connection stands, in the binary's own word.</param>
/// <param name="rail">Which arm of the watch this is; null when the binary named none.</param>
/// <param name="reconnect">Whether it will be made again; null when the binary said nothing.</param>
/// <param name="failureCount">How many times it has failed; null when the binary said nothing.</param>
/// <param name="maxFailureCount">How many failures it is given before it is dropped; null when the binary said nothing.</param>
/// <param name="nextIn">How long until it is tried again; the wire carries it in seconds. Null when the binary said nothing.</param>
/// <param name="lastFailure">When it last failed, as the timestamp the binary wrote; null when the binary gave none.</param>
/// <param name="reason">Why it stands where it does; null when the binary gave none.</param>
/// <param name="detail">What the binary adds to that; null when it added nothing.</param>
/// <param name="serverMessage">What the other end said; null when it said nothing.</param>
/// <param name="at">When the state was taken, as the number the binary sent; the declaration gives it no unit. Null when the binary said nothing.</param>
public sealed class ArtifactWatchArm(
    string url,
    string state,
    string? rail = null,
    bool? reconnect = null,
    long? failureCount = null,
    long? maxFailureCount = null,
    TimeSpan? nextIn = null,
    string? lastFailure = null,
    string? reason = null,
    string? detail = null,
    string? serverMessage = null,
    double? at = null)
{
    /// <summary>The page the connection is for.</summary>
    public string Url { get; } = url;

    /// <summary>Where the connection stands, in the binary's own word.</summary>
    public string State { get; } = state;

    /// <summary>Which arm of the watch this is; null when the binary named none.</summary>
    public string? Rail { get; } = rail;

    /// <summary>Whether it will be made again; null when the binary said nothing.</summary>
    public bool? Reconnect { get; } = reconnect;

    /// <summary>How many times it has failed; null when the binary said nothing.</summary>
    public long? FailureCount { get; } = failureCount;

    /// <summary>How many failures it is given before it is dropped; null when the binary said nothing.</summary>
    public long? MaxFailureCount { get; } = maxFailureCount;

    /// <summary>How long until it is tried again; the wire carries it in seconds. Null when the binary said nothing.</summary>
    public TimeSpan? NextIn { get; } = nextIn;

    /// <summary>When it last failed, as the timestamp the binary wrote; null when the binary gave none.</summary>
    public string? LastFailure { get; } = lastFailure;

    /// <summary>Why it stands where it does; null when the binary gave none.</summary>
    public string? Reason { get; } = reason;

    /// <summary>What the binary adds to that; null when it added nothing.</summary>
    public string? Detail { get; } = detail;

    /// <summary>What the other end said; null when it said nothing.</summary>
    public string? ServerMessage { get; } = serverMessage;

    /// <summary>When the state was taken, as the number the binary sent; the declaration gives it no unit. Null when the binary said nothing.</summary>
    public double? At { get; } = at;
}

/// <summary>An Artifact that said which pages the session follows.</summary>
/// <param name="watches">The pages being followed.</param>
/// <param name="filterUrl">The one page the answer was held to; null when it names them all.</param>
/// <param name="arms">The connections behind them, and how each is faring.</param>
public sealed class ArtifactStatusToolOutput(
    IReadOnlyList<ArtifactWatch> watches,
    string? filterUrl = null,
    IReadOnlyList<ArtifactWatchArm>? arms = null) : ArtifactToolOutput
{
    /// <summary>The pages being followed.</summary>
    public IReadOnlyList<ArtifactWatch> Watches { get; } = watches;

    /// <summary>The one page the answer was held to; null when it names them all.</summary>
    public string? FilterUrl { get; } = filterUrl;

    /// <summary>The connections behind them, and how each is faring.</summary>
    public IReadOnlyList<ArtifactWatchArm> Arms { get; } = arms ?? [];
}

/// <summary>The artifact a call wrote to.</summary>
/// <param name="url">Where that artifact is.</param>
public sealed class ArtifactWriteReference(string url)
{
    /// <summary>Where that artifact is.</summary>
    public string Url { get; } = url;
}

/// <summary>One file put into an artifact's store.</summary>
/// <param name="id">The asset, as a read or a delete takes it.</param>
/// <param name="url">Where the asset is served from.</param>
/// <param name="sizeInBytes">How large the file is.</param>
/// <param name="contentType">What kind of file it is, as a media type.</param>
/// <param name="fileName">The name it went up under.</param>
/// <param name="sha256">The digest of its bytes; null when the binary gave none.</param>
public sealed class ArtifactAssetUpload(
    string id,
    string url,
    long sizeInBytes,
    string contentType,
    string fileName,
    string? sha256 = null)
{
    /// <summary>The asset, as a read or a delete takes it.</summary>
    public string Id { get; } = id;

    /// <summary>Where the asset is served from.</summary>
    public string Url { get; } = url;

    /// <summary>How large the file is.</summary>
    public long SizeInBytes { get; } = sizeInBytes;

    /// <summary>What kind of file it is, as a media type.</summary>
    public string ContentType { get; } = contentType;

    /// <summary>The name it went up under.</summary>
    public string FileName { get; } = fileName;

    /// <summary>The digest of its bytes; null when the binary gave none.</summary>
    public string? Sha256 { get; } = sha256;
}

/// <summary>An Artifact that put one file into a store.</summary>
/// <param name="assetUpload">The file that went up.</param>
/// <param name="written">The artifact it went into; null when the binary named none.</param>
public sealed class ArtifactAssetUploadToolOutput(
    ArtifactAssetUpload assetUpload,
    ArtifactWriteReference? written = null) : ArtifactToolOutput
{
    /// <summary>The file that went up.</summary>
    public ArtifactAssetUpload AssetUpload { get; } = assetUpload;

    /// <summary>The artifact it went into; null when the binary named none.</summary>
    public ArtifactWriteReference? Written { get; } = written;
}

/// <summary>How one file of a batch upload ended. The binary writes it in one of two shapes, told apart by the status it carries.</summary>
public abstract class ArtifactAssetUploadOutcome
{
    private protected ArtifactAssetUploadOutcome()
    {
    }
}

/// <summary>A file of a batch upload that went up.</summary>
/// <param name="filePath">The local file that was uploaded.</param>
/// <param name="id">The asset, as a read or a delete takes it.</param>
/// <param name="url">Where the asset is served from.</param>
/// <param name="sizeInBytes">How large the file is.</param>
/// <param name="contentType">What kind of file it is, as a media type.</param>
/// <param name="fileName">The name it went up under.</param>
/// <param name="sha256">The digest of its bytes; null when the binary gave none.</param>
public sealed class ArtifactAssetUploadedOutcome(
    string filePath,
    string id,
    string url,
    long sizeInBytes,
    string contentType,
    string fileName,
    string? sha256 = null) : ArtifactAssetUploadOutcome
{
    /// <summary>The local file that was uploaded.</summary>
    public string FilePath { get; } = filePath;

    /// <summary>The asset, as a read or a delete takes it.</summary>
    public string Id { get; } = id;

    /// <summary>Where the asset is served from.</summary>
    public string Url { get; } = url;

    /// <summary>How large the file is.</summary>
    public long SizeInBytes { get; } = sizeInBytes;

    /// <summary>What kind of file it is, as a media type.</summary>
    public string ContentType { get; } = contentType;

    /// <summary>The name it went up under.</summary>
    public string FileName { get; } = fileName;

    /// <summary>The digest of its bytes; null when the binary gave none.</summary>
    public string? Sha256 { get; } = sha256;
}

/// <summary>A file of a batch upload that did not go up, whether it was tried or not.</summary>
/// <param name="filePath">The local file that was to be uploaded.</param>
/// <param name="status">Whether it was tried at all.</param>
/// <param name="reason">Why it did not go up, as a short word.</param>
/// <param name="message">The same said to the model.</param>
/// <param name="mayBeStored">Whether it may have gone up despite this; null when the binary said nothing.</param>
public sealed class ArtifactAssetUploadFailedOutcome(
    string filePath,
    ArtifactAssetUploadStatus status,
    string reason,
    string message,
    bool? mayBeStored = null) : ArtifactAssetUploadOutcome
{
    /// <summary>The local file that was to be uploaded.</summary>
    public string FilePath { get; } = filePath;

    /// <summary>Whether it was tried at all.</summary>
    public ArtifactAssetUploadStatus Status { get; } = status;

    /// <summary>Why it did not go up, as a short word.</summary>
    public string Reason { get; } = reason;

    /// <summary>The same said to the model.</summary>
    public string Message { get; } = message;

    /// <summary>Whether it may have gone up despite this; null when the binary said nothing.</summary>
    public bool? MayBeStored { get; } = mayBeStored;
}

/// <summary>One call's worth of files put into an artifact's store.</summary>
/// <param name="url">The artifact they went into.</param>
/// <param name="results">How each file ended.</param>
public sealed class ArtifactAssetUploadBatch(string url, IReadOnlyList<ArtifactAssetUploadOutcome> results)
{
    /// <summary>The artifact they went into.</summary>
    public string Url { get; } = url;

    /// <summary>How each file ended.</summary>
    public IReadOnlyList<ArtifactAssetUploadOutcome> Results { get; } = results;
}

/// <summary>An Artifact that put several files into a store in one call.</summary>
/// <param name="assetUploads">The batch and how each file of it ended.</param>
/// <param name="written">The artifact they went into; null when the binary named none.</param>
public sealed class ArtifactAssetUploadsToolOutput(
    ArtifactAssetUploadBatch assetUploads,
    ArtifactWriteReference? written = null) : ArtifactToolOutput
{
    /// <summary>The batch and how each file of it ended.</summary>
    public ArtifactAssetUploadBatch AssetUploads { get; } = assetUploads;

    /// <summary>The artifact they went into; null when the binary named none.</summary>
    public ArtifactWriteReference? Written { get; } = written;
}

/// <summary>One file in an artifact's store.</summary>
/// <param name="id">The asset, as a read or a delete takes it.</param>
/// <param name="url">Where the asset is served from.</param>
/// <param name="contentType">What kind of file it is, as a media type.</param>
/// <param name="sizeInBytes">How large the file is.</param>
/// <param name="createdAt">When it went up, as the timestamp the binary wrote.</param>
/// <param name="sha256">The digest of its bytes; null when the binary gave none.</param>
public sealed class ArtifactAsset(
    string id,
    string url,
    string contentType,
    long sizeInBytes,
    string createdAt,
    string? sha256 = null)
{
    /// <summary>The asset, as a read or a delete takes it.</summary>
    public string Id { get; } = id;

    /// <summary>Where the asset is served from.</summary>
    public string Url { get; } = url;

    /// <summary>What kind of file it is, as a media type.</summary>
    public string ContentType { get; } = contentType;

    /// <summary>How large the file is.</summary>
    public long SizeInBytes { get; } = sizeInBytes;

    /// <summary>When it went up, as the timestamp the binary wrote.</summary>
    public string CreatedAt { get; } = createdAt;

    /// <summary>The digest of its bytes; null when the binary gave none.</summary>
    public string? Sha256 { get; } = sha256;
}

/// <summary>How much of an artifact's store is taken, and how much it holds.</summary>
/// <param name="fileCount">How many files are in it.</param>
/// <param name="bytes">How many bytes they come to.</param>
/// <param name="maxFileCount">How many files it holds at most.</param>
/// <param name="maxBytes">How many bytes it holds at most.</param>
public sealed class ArtifactAssetUsage(long fileCount, long bytes, long maxFileCount, long maxBytes)
{
    /// <summary>How many files are in it.</summary>
    public long FileCount { get; } = fileCount;

    /// <summary>How many bytes they come to.</summary>
    public long Bytes { get; } = bytes;

    /// <summary>How many files it holds at most.</summary>
    public long MaxFileCount { get; } = maxFileCount;

    /// <summary>How many bytes it holds at most.</summary>
    public long MaxBytes { get; } = maxBytes;
}

/// <summary>An artifact's store, as a listing reads it.</summary>
/// <param name="url">The artifact whose store this is.</param>
/// <param name="assets">The files in it.</param>
/// <param name="usage">How much of it is taken.</param>
/// <param name="next">What a later call passes to go on from here; null when the listing is whole.</param>
/// <param name="cowritten">Whether someone else has written to the store too; null when nobody has.</param>
/// <param name="outsideWriter">Whether the last writer was not this session; null when it was.</param>
public sealed class ArtifactAssetListing(
    string url,
    IReadOnlyList<ArtifactAsset> assets,
    ArtifactAssetUsage usage,
    string? next = null,
    bool? cowritten = null,
    bool? outsideWriter = null)
{
    /// <summary>The artifact whose store this is.</summary>
    public string Url { get; } = url;

    /// <summary>The files in it.</summary>
    public IReadOnlyList<ArtifactAsset> Assets { get; } = assets;

    /// <summary>How much of it is taken.</summary>
    public ArtifactAssetUsage Usage { get; } = usage;

    /// <summary>What a later call passes to go on from here; null when the listing is whole.</summary>
    public string? Next { get; } = next;

    /// <summary>Whether someone else has written to the store too; null when nobody has.</summary>
    public bool? Cowritten { get; } = cowritten;

    /// <summary>Whether the last writer was not this session; null when it was.</summary>
    public bool? OutsideWriter { get; } = outsideWriter;
}

/// <summary>An Artifact that listed a store.</summary>
/// <param name="assetList">The store and what is in it.</param>
public sealed class ArtifactAssetListToolOutput(ArtifactAssetListing assetList) : ArtifactToolOutput
{
    /// <summary>The store and what is in it.</summary>
    public ArtifactAssetListing AssetList { get; } = assetList;
}

/// <summary>One file of a store, saved to disk.</summary>
/// <param name="id">The asset that was read.</param>
/// <param name="path">Where it was saved.</param>
/// <param name="sizeInBytes">How large it is.</param>
/// <param name="contentType">What kind of file it is, as a media type.</param>
/// <param name="sha256">The digest of its bytes.</param>
/// <param name="cowritten">Whether someone else has written to the store too; null when nobody has.</param>
/// <param name="outsideWriter">Whether the last writer was not this session; null when it was.</param>
/// <param name="publicRead">Whether anyone may fetch it; null when only the account may.</param>
/// <param name="foreign">Whether it belongs to an artifact the account does not own; null when it does not.</param>
public sealed class ArtifactAssetRead(
    string id,
    string path,
    long sizeInBytes,
    string contentType,
    string sha256,
    bool? cowritten = null,
    bool? outsideWriter = null,
    bool? publicRead = null,
    bool? foreign = null)
{
    /// <summary>The asset that was read.</summary>
    public string Id { get; } = id;

    /// <summary>Where it was saved.</summary>
    public string Path { get; } = path;

    /// <summary>How large it is.</summary>
    public long SizeInBytes { get; } = sizeInBytes;

    /// <summary>What kind of file it is, as a media type.</summary>
    public string ContentType { get; } = contentType;

    /// <summary>The digest of its bytes.</summary>
    public string Sha256 { get; } = sha256;

    /// <summary>Whether someone else has written to the store too; null when nobody has.</summary>
    public bool? Cowritten { get; } = cowritten;

    /// <summary>Whether the last writer was not this session; null when it was.</summary>
    public bool? OutsideWriter { get; } = outsideWriter;

    /// <summary>Whether anyone may fetch it; null when only the account may.</summary>
    public bool? PublicRead { get; } = publicRead;

    /// <summary>Whether it belongs to an artifact the account does not own; null when it does not.</summary>
    public bool? Foreign { get; } = foreign;
}

/// <summary>An Artifact that saved one file of a store to disk.</summary>
/// <param name="assetRead">The file that was saved.</param>
public sealed class ArtifactAssetReadToolOutput(ArtifactAssetRead assetRead) : ArtifactToolOutput
{
    /// <summary>The file that was saved.</summary>
    public ArtifactAssetRead AssetRead { get; } = assetRead;
}

/// <summary>One file taken out of an artifact's store.</summary>
/// <param name="id">The asset that was removed.</param>
/// <param name="deleted">Whether it is gone.</param>
public sealed class ArtifactAssetDeletion(string id, bool deleted)
{
    /// <summary>The asset that was removed.</summary>
    public string Id { get; } = id;

    /// <summary>Whether it is gone.</summary>
    public bool Deleted { get; } = deleted;
}

/// <summary>An Artifact that took one file out of a store.</summary>
/// <param name="assetDelete">The file that was removed.</param>
/// <param name="written">The artifact it came out of; null when the binary named none.</param>
public sealed class ArtifactAssetDeleteToolOutput(
    ArtifactAssetDeletion assetDelete,
    ArtifactWriteReference? written = null) : ArtifactToolOutput
{
    /// <summary>The file that was removed.</summary>
    public ArtifactAssetDeletion AssetDelete { get; } = assetDelete;

    /// <summary>The artifact it came out of; null when the binary named none.</summary>
    public ArtifactWriteReference? Written { get; } = written;
}
