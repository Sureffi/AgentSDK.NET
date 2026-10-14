using System.Text.Json;

namespace Claude.Agent;

// The Artifact tool's input and output. The declaration writes the output as eleven object shapes with
// no key saying which is which, so each is told apart by a key only it carries, and a JSON carrying
// none of them reads as null beside the raw. Two shapes nested inside it are unions of their own: a
// watch, told apart by the arm it names and by that arm's absence, and one file of a batch upload,
// told apart by its status. No recording calls this tool: every shape here stands on the declaration,
// and which keys an input's action cannot do without stands on the binary's own checks.
internal static class ArtifactToolParser
{
    // The input is a union on `action`, one type a value. The declaration names "publish" as a value
    // and makes it the meaning of no action at all, so an absent action reads as a publish. An action
    // the declaration does not name, or one without the key the binary requires of it, reads as null.
    public static ArtifactToolInput? ReadInput(JsonElement raw)
    {
        var action = raw.TryProperty("action", out _) ? raw.TextOrNull("action") : "publish";
        return action switch
        {
            "publish" => raw.Text("file_path", out var filePath)
                ? new ArtifactPublishToolInput(
                    filePath,
                    raw.TextOrNull("favicon"),
                    raw.TextOrNull("icon"),
                    raw.TextOrNull("title"),
                    raw.TextOrNull("description"),
                    raw.TextOrNull("label"),
                    raw.TextOrNull("url"),
                    raw.FlagOrNull("force"),
                    raw.TextOrNull("contract"))
                : null,
            "list" => new ArtifactListToolInput(
                raw.CountOrNull("limit"),
                raw.SetOrNull("scope", value => new ArtifactScope(value))),
            "read" => raw.Text("url", out var url) ? new ArtifactReadToolInput(url, raw.TextOrNull("prompt")) : null,
            "list_types" => new ArtifactListTypesToolInput(),
            "watch" => new ArtifactWatchToolInput(raw.TextOrNull("url")),
            "unwatch" => new ArtifactUnwatchToolInput(raw.TextOrNull("url")),
            "status" => new ArtifactStatusToolInput(raw.TextOrNull("url")),
            "upload_asset" => raw.Text("url", out var url) && raw.TextsOrEmpty("file_paths", out var filePaths)
                    && (raw.Text("file_path", out _) || filePaths.Count > 0)
                ? new ArtifactUploadAssetToolInput(url, raw.TextOrNull("file_path"), filePaths)
                : null,
            "list_assets" => raw.Text("url", out var url) ? new ArtifactListAssetsToolInput(url, raw.TextOrNull("after")) : null,
            "read_asset" => raw.Text("url", out var url) && raw.Text("asset_id", out var assetId)
                ? new ArtifactReadAssetToolInput(url, assetId, raw.TextOrNull("out_dir"))
                : null,
            "delete_asset" => raw.Text("url", out var url) && raw.Text("asset_id", out var assetId)
                ? new ArtifactDeleteAssetToolInput(url, assetId)
                : null,
            _ => null,
        };
    }

    private static ArtifactStoredPage? AsArtifactStoredPage(JsonElement raw)
    {
        if (!raw.Text("contract", out var contract))
        {
            return null;
        }

        return new ArtifactStoredPage(
            contract,
            raw.TextOrNull("preferredContract"),
            raw.FlagOrNull("carried"),
            raw.TextOrNull("read"));
    }

    private static ArtifactPublishedToolOutput? AsArtifactPublishedToolOutput(JsonElement raw)
    {
        if (!raw.Text("url", out var url)
            || !raw.Text("path", out var path)
            || !raw.Nested("stored", AsArtifactStoredPage, out var stored)
            || !raw.TextsOrEmpty("warnings", out var warnings))
        {
            return null;
        }

        return new ArtifactPublishedToolOutput(
            url,
            path,
            raw.TextOrNull("artifact_id"),
            raw.TextOrNull("title"),
            raw.TextOrNull("version"),
            stored,
            warnings,
            raw.CountOrNull("publishesRemaining"),
            raw.NumberOrNull("publishesResetAt"),
            raw.TextOrNull("contract"),
            raw.FlagOrNull("updated"),
            raw.TextOrNull("icon"),
            raw.FlagOrNull("faviconSent"),
            raw.FlagOrNull("iconDropped"),
            raw.TextOrNull("audience"),
            raw.CountOrNull("seq"),
            raw.FlagOrNull("unchanged"),
            raw.TextOrNull("liveSubscription"));
    }

    private static ArtifactListEntry? AsArtifactListEntry(JsonElement raw)
    {
        if (!raw.Text("title", out var title)
            || !raw.Text("url", out var url))
        {
            return null;
        }

        return new ArtifactListEntry(
            title,
            url,
            raw.TextOrNull("favicon"),
            raw.TextOrNull("updatedAt"),
            raw.SetOrNull("rel", value => new ArtifactRelation(value)),
            raw.FlagOrNull("external"),
            raw.SetOrNull("role", value => new ArtifactRole(value)));
    }

    private static ArtifactListToolOutput? AsArtifactListToolOutput(JsonElement raw)
    {
        if (!raw.Items("artifacts", AsArtifactListEntry, out var artifacts))
        {
            return null;
        }

        return new ArtifactListToolOutput(
            artifacts,
            raw.FlagOrNull("truncated"),
            raw.SetOrNull("scope", value => new ArtifactListScope(value)),
            raw.FlagOrNull("external_listed"));
    }

    private static ArtifactReadResult? AsArtifactReadResult(JsonElement raw)
    {
        if (!raw.Text("url", out var url)
            || !raw.Count("bytes", out var bytes)
            || !raw.Count("code", out var code)
            || !raw.Text("codeText", out var codeText)
            || !raw.Text("result", out var result)
            || !raw.Milliseconds("durationMs", out var duration))
        {
            return null;
        }

        return new ArtifactReadResult(url, bytes, code, codeText, result, duration);
    }

    private static ArtifactVersionReference? AsArtifactVersionReference(JsonElement raw)
    {
        if (!raw.Text("slug", out var slug))
        {
            return null;
        }

        return new ArtifactVersionReference(slug, raw.TextOrNull("ver"), raw.FlagOrNull("seeded"));
    }

    private static ArtifactReadToolOutput? AsArtifactReadToolOutput(JsonElement raw)
    {
        if (!raw.Shape("read", AsArtifactReadResult, out var read)
            || !raw.Nested("artifactRead", AsArtifactVersionReference, out var artifactRead))
        {
            return null;
        }

        return new ArtifactReadToolOutput(read, artifactRead);
    }

    private static ArtifactWatchState? AsArtifactWatchState(JsonElement raw)
    {
        if (!raw.Text("url", out var url)
            || !raw.Flag("watching", out var watching)
            || !raw.Text("outcome", out var outcome)
            || !raw.TextsOrEmpty("events", out var events))
        {
            return null;
        }

        return new ArtifactWatchState(
            url,
            watching,
            outcome,
            raw.TextOrNull("reason"),
            raw.TextOrNull("durable_skip_reason"),
            raw.TextOrNull("task_id"),
            raw.NumberOrNull("since"),
            raw.NumberOrNull("token_expires_at"),
            raw.TextOrNull("rail"),
            raw.TextOrNull("trigger_id"),
            raw.TextOrNull("durable_since"),
            raw.CountOrNull("status"),
            raw.TextOrNull("detail"),
            raw.TextOrNull("note"),
            events);
    }

    private static ArtifactWatchToolOutput? AsArtifactWatchToolOutput(JsonElement raw) =>
        raw.Shape("watch", AsArtifactWatchState, out var watch) ? new ArtifactWatchToolOutput(watch) : null;

    private static ArtifactUnwatchState? AsArtifactUnwatchState(JsonElement raw)
    {
        if (!raw.Text("url", out var url)
            || !raw.Flag("was_watching", out var wasWatching))
        {
            return null;
        }

        return new ArtifactUnwatchState(url, wasWatching);
    }

    private static ArtifactUnwatchToolOutput? AsArtifactUnwatchToolOutput(JsonElement raw) =>
        raw.Shape("unwatch", AsArtifactUnwatchState, out var unwatch) ? new ArtifactUnwatchToolOutput(unwatch) : null;

    private static ArtifactLiveWatch? AsArtifactLiveWatch(JsonElement raw)
    {
        if (!raw.Text("url", out var url)
            || !raw.Text("task_id", out var taskId)
            || !raw.Number("since", out var since)
            || !raw.Flag("explicit", out var isExplicit)
            || !raw.Flag("connected", out var connected)
            || !raw.Number("token_expires_at", out var tokenExpiresAt))
        {
            return null;
        }

        return new ArtifactLiveWatch(
            url,
            taskId,
            since,
            isExplicit,
            connected,
            tokenExpiresAt,
            raw.FlagOrNull("connecting"),
            raw.TextOrNull("armed_via"));
    }

    private static ArtifactDurableWakeWatch? AsArtifactDurableWakeWatch(JsonElement raw)
    {
        if (!raw.Text("url", out var url)
            || !raw.Text("trigger_id", out var triggerId)
            || !raw.Text("since", out var since)
            || !raw.TextsOrEmpty("events", out var events))
        {
            return null;
        }

        return new ArtifactDurableWakeWatch(url, triggerId, since, events, raw.FlagOrNull("restored"));
    }

    private static ArtifactStoppedWatch? AsArtifactStoppedWatch(JsonElement raw)
    {
        if (!raw.Text("url", out var url)
            || !raw.Text("stop_kind", out var stopKind))
        {
            return null;
        }

        return new ArtifactStoppedWatch(
            url,
            stopKind,
            raw.NumberOrNull("since"),
            raw.FlagOrNull("explicit"),
            raw.TextOrNull("armed_via"));
    }

    private static ArtifactWatchArm? AsArtifactWatchArm(JsonElement raw)
    {
        if (!raw.Text("url", out var url)
            || !raw.Text("state", out var state))
        {
            return null;
        }

        return new ArtifactWatchArm(
            url,
            state,
            raw.TextOrNull("rail"),
            raw.FlagOrNull("reconnect"),
            raw.CountOrNull("failures"),
            raw.CountOrNull("max_failures"),
            raw.SecondsOrNull("next_in_s"),
            raw.TextOrNull("last_failure"),
            raw.TextOrNull("reason"),
            raw.TextOrNull("detail"),
            raw.TextOrNull("server_message"),
            raw.NumberOrNull("at"));
    }

    private static ArtifactStatusToolOutput? AsArtifactStatusToolOutput(JsonElement raw)
    {
        if (!raw.Items("watches", AsArtifactWatch, out var watches)
            || !raw.ItemsOrEmpty("arms", AsArtifactWatchArm, out var arms))
        {
            return null;
        }

        return new ArtifactStatusToolOutput(watches, raw.TextOrNull("filter_url"), arms);
    }

    private static ArtifactWriteReference? AsArtifactWriteReference(JsonElement raw) =>
        raw.Text("url", out var url) ? new ArtifactWriteReference(url) : null;

    private static ArtifactAssetUpload? AsArtifactAssetUpload(JsonElement raw)
    {
        if (!raw.Text("id", out var id)
            || !raw.Text("url", out var url)
            || !raw.Count("size_bytes", out var sizeInBytes)
            || !raw.Text("content_type", out var contentType)
            || !raw.Text("file_name", out var fileName))
        {
            return null;
        }

        return new ArtifactAssetUpload(id, url, sizeInBytes, contentType, fileName, raw.TextOrNull("sha256"));
    }

    private static ArtifactAssetUploadToolOutput? AsArtifactAssetUploadToolOutput(JsonElement raw)
    {
        if (!raw.Shape("asset_upload", AsArtifactAssetUpload, out var assetUpload)
            || !raw.Nested("written", AsArtifactWriteReference, out var written))
        {
            return null;
        }

        return new ArtifactAssetUploadToolOutput(assetUpload, written);
    }

    private static ArtifactAssetUploadedOutcome? AsArtifactAssetUploadedOutcome(JsonElement raw)
    {
        if (!raw.Text("file_path", out var filePath)
            || !raw.Text("id", out var id)
            || !raw.Text("url", out var url)
            || !raw.Count("size_bytes", out var sizeInBytes)
            || !raw.Text("content_type", out var contentType)
            || !raw.Text("file_name", out var fileName))
        {
            return null;
        }

        return new ArtifactAssetUploadedOutcome(filePath, id, url, sizeInBytes, contentType, fileName, raw.TextOrNull("sha256"));
    }

    private static ArtifactAssetUploadFailedOutcome? AsArtifactAssetUploadFailedOutcome(JsonElement raw)
    {
        if (!raw.Text("file_path", out var filePath)
            || !raw.Text("status", out var status)
            || !raw.Text("reason", out var reason)
            || !raw.Text("message", out var message))
        {
            return null;
        }

        return new ArtifactAssetUploadFailedOutcome(filePath, status, reason, message, raw.FlagOrNull("may_be_stored"));
    }

    private static ArtifactAssetUploadBatch? AsArtifactAssetUploadBatch(JsonElement raw)
    {
        if (!raw.Text("url", out var url)
            || !raw.Items("results", AsArtifactAssetUploadOutcome, out var results))
        {
            return null;
        }

        return new ArtifactAssetUploadBatch(url, results);
    }

    private static ArtifactAssetUploadsToolOutput? AsArtifactAssetUploadsToolOutput(JsonElement raw)
    {
        if (!raw.Shape("asset_uploads", AsArtifactAssetUploadBatch, out var assetUploads)
            || !raw.Nested("written", AsArtifactWriteReference, out var written))
        {
            return null;
        }

        return new ArtifactAssetUploadsToolOutput(assetUploads, written);
    }

    private static ArtifactAsset? AsArtifactAsset(JsonElement raw)
    {
        if (!raw.Text("id", out var id)
            || !raw.Text("url", out var url)
            || !raw.Text("content_type", out var contentType)
            || !raw.Count("size_bytes", out var sizeInBytes)
            || !raw.Text("created_at", out var createdAt))
        {
            return null;
        }

        return new ArtifactAsset(id, url, contentType, sizeInBytes, createdAt, raw.TextOrNull("sha256"));
    }

    private static ArtifactAssetUsage? AsArtifactAssetUsage(JsonElement raw)
    {
        if (!raw.Count("files", out var fileCount)
            || !raw.Count("bytes", out var bytes)
            || !raw.Count("max_files", out var maxFileCount)
            || !raw.Count("max_bytes", out var maxBytes))
        {
            return null;
        }

        return new ArtifactAssetUsage(fileCount, bytes, maxFileCount, maxBytes);
    }

    private static ArtifactAssetListing? AsArtifactAssetListing(JsonElement raw)
    {
        if (!raw.Text("url", out var url)
            || !raw.Items("assets", AsArtifactAsset, out var assets)
            || !raw.Shape("usage", AsArtifactAssetUsage, out var usage))
        {
            return null;
        }

        return new ArtifactAssetListing(
            url,
            assets,
            usage,
            raw.TextOrNull("next"),
            raw.FlagOrNull("cowritten"),
            raw.FlagOrNull("outside_writer"));
    }

    private static ArtifactAssetListToolOutput? AsArtifactAssetListToolOutput(JsonElement raw)
    {
        if (!raw.Shape("asset_list", AsArtifactAssetListing, out var assetList))
        {
            return null;
        }

        return new ArtifactAssetListToolOutput(assetList);
    }

    private static ArtifactAssetRead? AsArtifactAssetRead(JsonElement raw)
    {
        if (!raw.Text("id", out var id)
            || !raw.Text("path", out var path)
            || !raw.Count("size_bytes", out var sizeInBytes)
            || !raw.Text("content_type", out var contentType)
            || !raw.Text("sha256", out var sha256))
        {
            return null;
        }

        return new ArtifactAssetRead(
            id,
            path,
            sizeInBytes,
            contentType,
            sha256,
            raw.FlagOrNull("cowritten"),
            raw.FlagOrNull("outside_writer"),
            raw.FlagOrNull("public_read"),
            raw.FlagOrNull("foreign"));
    }

    private static ArtifactAssetReadToolOutput? AsArtifactAssetReadToolOutput(JsonElement raw)
    {
        if (!raw.Shape("asset_read", AsArtifactAssetRead, out var assetRead))
        {
            return null;
        }

        return new ArtifactAssetReadToolOutput(assetRead);
    }

    private static ArtifactAssetDeletion? AsArtifactAssetDeletion(JsonElement raw)
    {
        if (!raw.Text("id", out var id)
            || !raw.Flag("deleted", out var deleted))
        {
            return null;
        }

        return new ArtifactAssetDeletion(id, deleted);
    }

    private static ArtifactAssetDeleteToolOutput? AsArtifactAssetDeleteToolOutput(JsonElement raw)
    {
        if (!raw.Shape("asset_delete", AsArtifactAssetDeletion, out var assetDelete)
            || !raw.Nested("written", AsArtifactWriteReference, out var written))
        {
            return null;
        }

        return new ArtifactAssetDeleteToolOutput(assetDelete, written);
    }

    // A publish is written with its action named: the declaration's own value, which the binary reads
    // as it reads the action left out.
    public static void WriteInput(Utf8JsonWriter writer, ArtifactToolInput input)
    {
        writer.WriteStartObject();
        switch (input)
        {
            case ArtifactPublishToolInput publish:
                writer.WriteString("action", "publish");
                writer.WriteString("file_path", publish.FilePath);
                writer.Text("favicon", publish.Favicon);
                writer.Text("icon", publish.Icon);
                writer.Text("title", publish.Title);
                writer.Text("description", publish.Description);
                writer.Text("label", publish.Label);
                writer.Text("url", publish.Url);
                writer.Flag("force", publish.Force);
                writer.Text("contract", publish.Contract);
                break;
            case ArtifactListToolInput list:
                writer.WriteString("action", "list");
                writer.Count("limit", list.Limit);
                writer.Text("scope", list.Scope?.ToString());
                break;
            case ArtifactReadToolInput read:
                writer.WriteString("action", "read");
                writer.WriteString("url", read.Url);
                writer.Text("prompt", read.Prompt);
                break;
            case ArtifactListTypesToolInput:
                writer.WriteString("action", "list_types");
                break;
            case ArtifactWatchToolInput watch:
                writer.WriteString("action", "watch");
                writer.Text("url", watch.Url);
                break;
            case ArtifactUnwatchToolInput unwatch:
                writer.WriteString("action", "unwatch");
                writer.Text("url", unwatch.Url);
                break;
            case ArtifactStatusToolInput status:
                writer.WriteString("action", "status");
                writer.Text("url", status.Url);
                break;
            case ArtifactUploadAssetToolInput upload:
                writer.WriteString("action", "upload_asset");
                writer.WriteString("url", upload.Url);
                writer.Text("file_path", upload.FilePath);
                writer.Texts("file_paths", upload.FilePaths);
                break;
            case ArtifactListAssetsToolInput listAssets:
                writer.WriteString("action", "list_assets");
                writer.WriteString("url", listAssets.Url);
                writer.Text("after", listAssets.After);
                break;
            case ArtifactReadAssetToolInput readAsset:
                writer.WriteString("action", "read_asset");
                writer.WriteString("url", readAsset.Url);
                writer.WriteString("asset_id", readAsset.AssetId);
                writer.Text("out_dir", readAsset.OutputDirectory);
                break;
            case ArtifactDeleteAssetToolInput deleteAsset:
                writer.WriteString("action", "delete_asset");
                writer.WriteString("url", deleteAsset.Url);
                writer.WriteString("asset_id", deleteAsset.AssetId);
                break;
            default:
                throw new NotSupportedException($"No Artifact input of type {input.GetType().Name} can be written.");
        }

        writer.WriteEndObject();
    }

    private static void WriteArtifactStoredPage(Utf8JsonWriter writer, ArtifactStoredPage value)
    {
        writer.WriteString("contract", value.Contract);
        writer.Text("preferredContract", value.PreferredContract);
        writer.Flag("carried", value.Carried);
        writer.Text("read", value.Read);
    }

    private static void WriteArtifactPublishedToolOutput(Utf8JsonWriter writer, ArtifactPublishedToolOutput value)
    {
        writer.WriteString("url", value.Url);
        writer.WriteString("path", value.Path);
        writer.Text("artifact_id", value.ArtifactId);
        writer.Text("title", value.Title);
        writer.Text("version", value.Version);
        writer.Nested("stored", value.Stored, WriteArtifactStoredPage);
        writer.Texts("warnings", value.Warnings);
        writer.Count("publishesRemaining", value.PublishesRemaining);
        writer.Number("publishesResetAt", value.PublishesResetAt);
        writer.Text("contract", value.Contract);
        writer.Flag("updated", value.Updated);
        writer.Text("icon", value.Icon);
        writer.Flag("faviconSent", value.FaviconSent);
        writer.Flag("iconDropped", value.IconDropped);
        writer.Text("audience", value.Audience);
        writer.Count("seq", value.Sequence);
        writer.Flag("unchanged", value.Unchanged);
        writer.Text("liveSubscription", value.LiveSubscription);
    }

    private static void WriteArtifactListEntry(Utf8JsonWriter writer, ArtifactListEntry value)
    {
        writer.WriteString("title", value.Title);
        writer.WriteString("url", value.Url);
        writer.Text("favicon", value.Favicon);
        writer.Text("updatedAt", value.UpdatedAt);
        writer.Text("rel", value.Relation?.ToString());
        writer.Flag("external", value.External);
        writer.Text("role", value.Role?.ToString());
    }

    private static void WriteArtifactListToolOutput(Utf8JsonWriter writer, ArtifactListToolOutput value)
    {
        writer.ItemObjects("artifacts", value.Artifacts, WriteArtifactListEntry, whenEmpty: true);
        writer.Flag("truncated", value.Truncated);
        writer.Text("scope", value.Scope?.ToString());
        writer.Flag("external_listed", value.ExternalListed);
    }

    private static void WriteArtifactReadResult(Utf8JsonWriter writer, ArtifactReadResult value)
    {
        writer.WriteString("url", value.Url);
        writer.WriteNumber("bytes", value.Bytes);
        writer.WriteNumber("code", value.Code);
        writer.WriteString("codeText", value.CodeText);
        writer.WriteString("result", value.Result);
        writer.Milliseconds("durationMs", value.Duration);
    }

    private static void WriteArtifactVersionReference(Utf8JsonWriter writer, ArtifactVersionReference value)
    {
        writer.WriteString("slug", value.Slug);
        writer.Text("ver", value.Version);
        writer.Flag("seeded", value.Seeded);
    }

    private static void WriteArtifactReadToolOutput(Utf8JsonWriter writer, ArtifactReadToolOutput value)
    {
        writer.Nested("read", value.Read, WriteArtifactReadResult);
        writer.Nested("artifactRead", value.ArtifactRead, WriteArtifactVersionReference);
    }

    private static void WriteArtifactWatchState(Utf8JsonWriter writer, ArtifactWatchState value)
    {
        writer.WriteString("url", value.Url);
        writer.WriteBoolean("watching", value.Watching);
        writer.WriteString("outcome", value.Outcome);
        writer.Text("reason", value.Reason);
        writer.Text("durable_skip_reason", value.DurableSkipReason);
        writer.Text("task_id", value.TaskId);
        writer.Number("since", value.Since);
        writer.Number("token_expires_at", value.TokenExpiresAt);
        writer.Text("rail", value.Rail);
        writer.Text("trigger_id", value.TriggerId);
        writer.Text("durable_since", value.DurableSince);
        writer.Count("status", value.StatusCode);
        writer.Text("detail", value.Detail);
        writer.Text("note", value.Note);
        writer.Texts("events", value.Events);
    }

    private static void WriteArtifactWatchToolOutput(Utf8JsonWriter writer, ArtifactWatchToolOutput value)
    {
        writer.Nested("watch", value.Watch, WriteArtifactWatchState);
    }

    private static void WriteArtifactUnwatchState(Utf8JsonWriter writer, ArtifactUnwatchState value)
    {
        writer.WriteString("url", value.Url);
        writer.WriteBoolean("was_watching", value.WasWatching);
    }

    private static void WriteArtifactUnwatchToolOutput(Utf8JsonWriter writer, ArtifactUnwatchToolOutput value)
    {
        writer.Nested("unwatch", value.Unwatch, WriteArtifactUnwatchState);
    }

    private static void WriteArtifactLiveWatch(Utf8JsonWriter writer, ArtifactLiveWatch value)
    {
        writer.WriteString("url", value.Url);
        writer.WriteString("task_id", value.TaskId);
        writer.WriteNumber("since", value.Since);
        writer.WriteBoolean("explicit", value.IsExplicit);
        writer.WriteBoolean("connected", value.Connected);
        writer.WriteNumber("token_expires_at", value.TokenExpiresAt);
        writer.Flag("connecting", value.Connecting);
        writer.Text("armed_via", value.ArmedVia);
    }

    private static void WriteArtifactDurableWakeWatch(Utf8JsonWriter writer, ArtifactDurableWakeWatch value)
    {
        writer.WriteString("url", value.Url);
        writer.WriteString("trigger_id", value.TriggerId);
        writer.WriteString("since", value.Since);
        writer.Texts("events", value.Events);
        writer.Flag("restored", value.Restored);
    }

    private static void WriteArtifactStoppedWatch(Utf8JsonWriter writer, ArtifactStoppedWatch value)
    {
        writer.WriteString("url", value.Url);
        writer.WriteString("stop_kind", value.StopKind);
        writer.Number("since", value.Since);
        writer.Flag("explicit", value.IsExplicit);
        writer.Text("armed_via", value.ArmedVia);
    }

    private static void WriteArtifactWatchArm(Utf8JsonWriter writer, ArtifactWatchArm value)
    {
        writer.WriteString("url", value.Url);
        writer.WriteString("state", value.State);
        writer.Text("rail", value.Rail);
        writer.Flag("reconnect", value.Reconnect);
        writer.Count("failures", value.FailureCount);
        writer.Count("max_failures", value.MaxFailureCount);
        writer.Seconds("next_in_s", value.NextIn);
        writer.Text("last_failure", value.LastFailure);
        writer.Text("reason", value.Reason);
        writer.Text("detail", value.Detail);
        writer.Text("server_message", value.ServerMessage);
        writer.Number("at", value.At);
    }

    private static void WriteArtifactStatusToolOutput(Utf8JsonWriter writer, ArtifactStatusToolOutput value)
    {
        writer.ItemObjects("watches", value.Watches, WriteArtifactWatch, whenEmpty: true);
        writer.Text("filter_url", value.FilterUrl);
        writer.ItemObjects("arms", value.Arms, WriteArtifactWatchArm);
    }

    private static void WriteArtifactWriteReference(Utf8JsonWriter writer, ArtifactWriteReference value)
    {
        writer.WriteString("url", value.Url);
    }

    private static void WriteArtifactAssetUpload(Utf8JsonWriter writer, ArtifactAssetUpload value)
    {
        writer.WriteString("id", value.Id);
        writer.WriteString("url", value.Url);
        writer.WriteNumber("size_bytes", value.SizeInBytes);
        writer.WriteString("content_type", value.ContentType);
        writer.WriteString("file_name", value.FileName);
        writer.Text("sha256", value.Sha256);
    }

    private static void WriteArtifactAssetUploadToolOutput(Utf8JsonWriter writer, ArtifactAssetUploadToolOutput value)
    {
        writer.Nested("asset_upload", value.AssetUpload, WriteArtifactAssetUpload);
        writer.Nested("written", value.Written, WriteArtifactWriteReference);
    }

    private static void WriteArtifactAssetUploadedOutcome(Utf8JsonWriter writer, ArtifactAssetUploadedOutcome value)
    {
        writer.WriteString("file_path", value.FilePath);
        writer.WriteString("id", value.Id);
        writer.WriteString("url", value.Url);
        writer.WriteNumber("size_bytes", value.SizeInBytes);
        writer.WriteString("content_type", value.ContentType);
        writer.WriteString("file_name", value.FileName);
        writer.Text("sha256", value.Sha256);
    }

    private static void WriteArtifactAssetUploadFailedOutcome(Utf8JsonWriter writer, ArtifactAssetUploadFailedOutcome value)
    {
        writer.WriteString("file_path", value.FilePath);
        writer.WriteString("status", value.Status.ToString());
        writer.WriteString("reason", value.Reason);
        writer.WriteString("message", value.Message);
        writer.Flag("may_be_stored", value.MayBeStored);
    }

    private static void WriteArtifactAssetUploadBatch(Utf8JsonWriter writer, ArtifactAssetUploadBatch value)
    {
        writer.WriteString("url", value.Url);
        writer.ItemObjects("results", value.Results, WriteArtifactAssetUploadOutcome, whenEmpty: true);
    }

    private static void WriteArtifactAssetUploadsToolOutput(Utf8JsonWriter writer, ArtifactAssetUploadsToolOutput value)
    {
        writer.Nested("asset_uploads", value.AssetUploads, WriteArtifactAssetUploadBatch);
        writer.Nested("written", value.Written, WriteArtifactWriteReference);
    }

    private static void WriteArtifactAsset(Utf8JsonWriter writer, ArtifactAsset value)
    {
        writer.WriteString("id", value.Id);
        writer.WriteString("url", value.Url);
        writer.WriteString("content_type", value.ContentType);
        writer.WriteNumber("size_bytes", value.SizeInBytes);
        writer.WriteString("created_at", value.CreatedAt);
        writer.Text("sha256", value.Sha256);
    }

    private static void WriteArtifactAssetUsage(Utf8JsonWriter writer, ArtifactAssetUsage value)
    {
        writer.WriteNumber("files", value.FileCount);
        writer.WriteNumber("bytes", value.Bytes);
        writer.WriteNumber("max_files", value.MaxFileCount);
        writer.WriteNumber("max_bytes", value.MaxBytes);
    }

    private static void WriteArtifactAssetListing(Utf8JsonWriter writer, ArtifactAssetListing value)
    {
        writer.WriteString("url", value.Url);
        writer.ItemObjects("assets", value.Assets, WriteArtifactAsset, whenEmpty: true);
        writer.Nested("usage", value.Usage, WriteArtifactAssetUsage);
        writer.Text("next", value.Next);
        writer.Flag("cowritten", value.Cowritten);
        writer.Flag("outside_writer", value.OutsideWriter);
    }

    private static void WriteArtifactAssetListToolOutput(Utf8JsonWriter writer, ArtifactAssetListToolOutput value)
    {
        writer.Nested("asset_list", value.AssetList, WriteArtifactAssetListing);
    }

    private static void WriteArtifactAssetRead(Utf8JsonWriter writer, ArtifactAssetRead value)
    {
        writer.WriteString("id", value.Id);
        writer.WriteString("path", value.Path);
        writer.WriteNumber("size_bytes", value.SizeInBytes);
        writer.WriteString("content_type", value.ContentType);
        writer.WriteString("sha256", value.Sha256);
        writer.Flag("cowritten", value.Cowritten);
        writer.Flag("outside_writer", value.OutsideWriter);
        writer.Flag("public_read", value.PublicRead);
        writer.Flag("foreign", value.Foreign);
    }

    private static void WriteArtifactAssetReadToolOutput(Utf8JsonWriter writer, ArtifactAssetReadToolOutput value)
    {
        writer.Nested("asset_read", value.AssetRead, WriteArtifactAssetRead);
    }

    private static void WriteArtifactAssetDeletion(Utf8JsonWriter writer, ArtifactAssetDeletion value)
    {
        writer.WriteString("id", value.Id);
        writer.WriteBoolean("deleted", value.Deleted);
    }

    private static void WriteArtifactAssetDeleteToolOutput(Utf8JsonWriter writer, ArtifactAssetDeleteToolOutput value)
    {
        writer.Nested("asset_delete", value.AssetDelete, WriteArtifactAssetDeletion);
        writer.Nested("written", value.Written, WriteArtifactWriteReference);
    }

    public static ArtifactToolOutput? ReadOutput(JsonElement raw) =>
        raw.TryProperty("url", out _) && raw.TryProperty("path", out _) ? AsArtifactPublishedToolOutput(raw)
        : raw.TryProperty("artifacts", out _) ? AsArtifactListToolOutput(raw)
        : raw.TryProperty("read", out _) ? AsArtifactReadToolOutput(raw)
        : raw.TryProperty("watch", out _) ? AsArtifactWatchToolOutput(raw)
        : raw.TryProperty("unwatch", out _) ? AsArtifactUnwatchToolOutput(raw)
        : raw.TryProperty("watches", out _) ? AsArtifactStatusToolOutput(raw)
        : raw.TryProperty("asset_upload", out _) ? AsArtifactAssetUploadToolOutput(raw)
        : raw.TryProperty("asset_uploads", out _) ? AsArtifactAssetUploadsToolOutput(raw)
        : raw.TryProperty("asset_list", out _) ? AsArtifactAssetListToolOutput(raw)
        : raw.TryProperty("asset_read", out _) ? AsArtifactAssetReadToolOutput(raw)
        : raw.TryProperty("asset_delete", out _) ? AsArtifactAssetDeleteToolOutput(raw)
        : null;

    public static void WriteOutput(Utf8JsonWriter writer, ArtifactToolOutput output)
    {
        writer.WriteStartObject();
        switch (output)
        {
            case ArtifactPublishedToolOutput published:
                WriteArtifactPublishedToolOutput(writer, published);
                break;
            case ArtifactListToolOutput listed:
                WriteArtifactListToolOutput(writer, listed);
                break;
            case ArtifactReadToolOutput read:
                WriteArtifactReadToolOutput(writer, read);
                break;
            case ArtifactWatchToolOutput watch:
                WriteArtifactWatchToolOutput(writer, watch);
                break;
            case ArtifactUnwatchToolOutput unwatch:
                WriteArtifactUnwatchToolOutput(writer, unwatch);
                break;
            case ArtifactStatusToolOutput status:
                WriteArtifactStatusToolOutput(writer, status);
                break;
            case ArtifactAssetUploadToolOutput upload:
                WriteArtifactAssetUploadToolOutput(writer, upload);
                break;
            case ArtifactAssetUploadsToolOutput uploads:
                WriteArtifactAssetUploadsToolOutput(writer, uploads);
                break;
            case ArtifactAssetListToolOutput assets:
                WriteArtifactAssetListToolOutput(writer, assets);
                break;
            case ArtifactAssetReadToolOutput asset:
                WriteArtifactAssetReadToolOutput(writer, asset);
                break;
            case ArtifactAssetDeleteToolOutput deletion:
                WriteArtifactAssetDeleteToolOutput(writer, deletion);
                break;
            default:
                throw new NotSupportedException($"No Artifact output of type {output.GetType().Name} can be written.");
        }

        writer.WriteEndObject();
    }

    // A watch the binary names an arm for is that arm's shape; one it names none for is the connection
    // held open. A value under the arm that this library has no shape for takes the whole view with it.
    private static ArtifactWatch? AsArtifactWatch(JsonElement raw) =>
        raw.TextOrNull("rail") switch
        {
            null => AsArtifactLiveWatch(raw),
            "durable_wake" => AsArtifactDurableWakeWatch(raw),
            "live_stopped" => AsArtifactStoppedWatch(raw),
            _ => null,
        };

    private static void WriteArtifactWatch(Utf8JsonWriter writer, ArtifactWatch value)
    {
        switch (value)
        {
            case ArtifactLiveWatch live:
                WriteArtifactLiveWatch(writer, live);
                break;
            case ArtifactDurableWakeWatch durable:
                writer.WriteString("rail", "durable_wake");
                WriteArtifactDurableWakeWatch(writer, durable);
                break;
            case ArtifactStoppedWatch stopped:
                writer.WriteString("rail", "live_stopped");
                WriteArtifactStoppedWatch(writer, stopped);
                break;
            default:
                throw new NotSupportedException($"No watch of type {value.GetType().Name} can be written.");
        }
    }

    // One file of a batch upload: the status says whether it went up, and the shape follows from that.
    private static ArtifactAssetUploadOutcome? AsArtifactAssetUploadOutcome(JsonElement raw) =>
        raw.TextOrNull("status") switch
        {
            "uploaded" => AsArtifactAssetUploadedOutcome(raw),
            "failed" or "not_attempted" => AsArtifactAssetUploadFailedOutcome(raw),
            _ => null,
        };

    private static void WriteArtifactAssetUploadOutcome(Utf8JsonWriter writer, ArtifactAssetUploadOutcome value)
    {
        switch (value)
        {
            case ArtifactAssetUploadedOutcome uploaded:
                WriteArtifactAssetUploadedOutcome(writer, uploaded);
                writer.WriteString("status", "uploaded");
                break;
            case ArtifactAssetUploadFailedOutcome failed:
                WriteArtifactAssetUploadFailedOutcome(writer, failed);
                break;
            default:
                throw new NotSupportedException($"No upload result of type {value.GetType().Name} can be written.");
        }
    }
}
