namespace Claude.Agent.Tests.Wire;

// What one API request spent, read from the string. The recordings hold the twelve keys the binary
// writes today and one iteration kind; the other three kinds, the unknown of the set, and the
// fallback credit are declared and never recorded, so they are held over hand-written lines.
[TestClass]
public sealed class UsageDecodeTests
{
    [TestMethod]
    public void TheRecordedResultUsageCarriesEveryCountTheApiReported()
    {
        var usage = Recorded.Messages<ResultMessage>("payloads").Single().Usage;

        Assert.IsNotNull(usage);
        Assert.AreEqual(1263, usage.InputTokens);
        Assert.AreEqual(43, usage.OutputTokens);
        Assert.AreEqual(0, usage.CacheCreationInputTokens);
        Assert.AreEqual(0, usage.CacheReadInputTokens);
        Assert.AreEqual(0, usage.CacheCreation?.Ephemeral1HourInputTokens);
        Assert.AreEqual(0, usage.CacheCreation?.Ephemeral5MinuteInputTokens);
        Assert.AreEqual(36, usage.OutputTokensDetails?.ThinkingTokens);
        Assert.AreEqual(0, usage.ServerToolUse?.WebFetchRequests);
        Assert.AreEqual(0, usage.ServerToolUse?.WebSearchRequests);
        Assert.AreEqual(ServiceTier.Standard, usage.ServiceTier);
        Assert.AreEqual(InferenceSpeed.Standard, usage.Speed);
        Assert.AreEqual("not_available", usage.InferenceRegion);
        Assert.IsNull(usage.FallbackCredit);
    }

    [TestMethod]
    public void TheRecordedResultUsageCarriesOneMessageIterationWithItsOwnCounts()
    {
        var usage = Recorded.Messages<ResultMessage>("payloads").Single().Usage;

        var iteration = Is<MessageUsageIteration>(usage?.Iterations.Single());
        Assert.AreEqual(1263, iteration.InputTokens);
        Assert.AreEqual(43, iteration.OutputTokens);
        Assert.AreEqual(0, iteration.CacheCreationInputTokens);
        Assert.AreEqual(0, iteration.CacheReadInputTokens);
        Assert.AreEqual(0, iteration.CacheCreation?.Ephemeral1HourInputTokens);
        Assert.IsNull(iteration.Model);
    }

    [TestMethod]
    public void TheRecordedModelUsageCarriesItsThinkingTokensAndCostBasis()
    {
        var usage = Recorded.Messages<ResultMessage>("payloads").Single().ModelUsage.Values.Single();

        Assert.AreEqual(2162, usage.InputTokens);
        Assert.AreEqual(52, usage.OutputTokens);
        Assert.AreEqual(36, usage.ThinkingTokens);
        Assert.AreEqual(ModelUsageCostBasis.List, usage.CostBasis);
        Assert.AreEqual("claude-haiku-4-5", usage.CanonicalModel);
        Assert.AreEqual("firstParty", usage.Provider);
    }

    [TestMethod]
    public void EachIterationKindReadsAsItsOwnType()
    {
        var usage = Read(
            """
            {
              "iterations": [
                {"type": "message", "input_tokens": 1, "output_tokens": 2, "model": "claude-haiku-4-5"},
                {"type": "compaction", "input_tokens": 3, "output_tokens": 4},
                {"type": "advisor_message", "input_tokens": 5, "output_tokens": 6, "model": "claude-haiku-4-5"},
                {"type": "fallback_message", "input_tokens": 7, "output_tokens": 8, "model": "claude-opus-4-5"}
              ]
            }
            """);

        Assert.AreEqual(4, usage?.Iterations.Count);
        var message = Is<MessageUsageIteration>(usage!.Iterations[0]);
        Assert.AreEqual("claude-haiku-4-5", message.Model);
        Assert.AreEqual(1, message.InputTokens);

        var compaction = Is<CompactionUsageIteration>(usage.Iterations[1]);
        Assert.AreEqual(3, compaction.InputTokens);
        Assert.AreEqual(4, compaction.OutputTokens);

        var advisor = Is<AdvisorMessageUsageIteration>(usage.Iterations[2]);
        Assert.AreEqual("claude-haiku-4-5", advisor.Model);

        var fallback = Is<FallbackMessageUsageIteration>(usage.Iterations[3]);
        Assert.AreEqual("claude-opus-4-5", fallback.Model);
        Assert.AreEqual(8, fallback.OutputTokens);
    }

    [TestMethod]
    public void AnIterationOfAKindThisLibraryHasNoTypeForIsTheUnknownOfTheSet()
    {
        var usage = Read("""{"iterations": [{"type": "rehearsal", "input_tokens": 9}, 11]}""");

        var named = Is<UnknownUsageIteration>(usage?.Iterations[0]);
        Assert.AreEqual("rehearsal", named.Type);
        Assert.AreEqual(9, named.InputTokens);
        Assert.AreEqual("rehearsal", named.Raw.GetProperty("type").GetString());

        var bare = Is<UnknownUsageIteration>(usage!.Iterations[1]);
        Assert.AreEqual(string.Empty, bare.Type);
        Assert.IsNull(bare.InputTokens);
    }

    // The unknown of either set is a kind this library has no type for, or an entry that names no
    // kind; an entry of a kind the library types is that kind's type whatever its members hold, and a
    // member that does not fit is null.
    [TestMethod]
    public void AnEntryOfATypedKindThatDoesNotFitIsItsKindsTypeAndOneThatNamesNoKindIsTheUnknown()
    {
        var usage = Read(
            """
            {"iterations": [{"type": "message", "input_tokens": "many", "output_tokens": 2}, {"input_tokens": 3}],
             "fallback_credit": {"status": {"type": "not_applied", "reason": 5}}}
            """);

        var message = Is<MessageUsageIteration>(usage?.Iterations[0]);
        Assert.IsNull(message.InputTokens);
        Assert.AreEqual(2, message.OutputTokens);
        var unnamed = Is<UnknownUsageIteration>(usage!.Iterations[1]);
        Assert.AreEqual(string.Empty, unnamed.Type);
        Assert.AreEqual(3, unnamed.InputTokens);
        Assert.IsNull(Is<NotAppliedFallbackCreditStatus>(usage.FallbackCredit?.Status).Reason);

        var bare = Is<UnknownFallbackCreditStatus>(Read("""{"fallback_credit": {"status": {"until": "later"}}}""")?.FallbackCredit?.Status);
        Assert.AreEqual(string.Empty, bare.Type);
    }

    [TestMethod]
    public void ARedeemedFallbackCreditReadsAsRedeemed()
    {
        var usage = Read("""{"fallback_credit": {"status": {"type": "redeemed"}}}""");

        Assert.IsInstanceOfType<RedeemedFallbackCreditStatus>(usage?.FallbackCredit?.Status);
    }

    [TestMethod]
    public void AFallbackCreditThatWasNotAppliedCarriesItsReasonAndTheFieldsToDrop()
    {
        var usage = Read(
            """
            {"fallback_credit": {"status": {"type": "not_applied", "reason": "variant_fields_present", "remove_to_redeem": ["tools", "system"]}}}
            """);

        var status = Is<NotAppliedFallbackCreditStatus>(usage?.FallbackCredit?.Status);
        Assert.AreEqual(FallbackCreditNotAppliedReason.VariantFieldsPresent, status.Reason);
        CollectionAssert.AreEqual(new[] { "tools", "system" }, status.FieldsToRemove.ToArray());
    }

    [TestMethod]
    public void AFallbackCreditStatusOfAKindThisLibraryHasNoTypeForIsTheUnknownOfTheSet()
    {
        var usage = Read("""{"fallback_credit": {"status": {"type": "deferred", "until": "later"}}}""");

        var status = Is<UnknownFallbackCreditStatus>(usage?.FallbackCredit?.Status);
        Assert.AreEqual("deferred", status.Type);
        Assert.AreEqual("later", status.Raw.GetProperty("until").GetString());
    }

    [TestMethod]
    public void AFallbackCreditWithNoStatusIsACreditWithNoOutcome()
    {
        var usage = Read("""{"fallback_credit": {}}""");

        Assert.IsNotNull(usage?.FallbackCredit);
        Assert.IsNull(usage.FallbackCredit.Status);
    }

    [TestMethod]
    public void AUsageThatIsNotAnObjectIsNoUsageAtAll()
    {
        Assert.IsNull(UsageParser.ParseUsage(null));
        Assert.IsNull(Read("[]"));
        Assert.IsNull(Read("7"));
    }

    [TestMethod]
    public void AUsageWithNoKeysLeavesEveryCountNullAndItsIterationsEmpty()
    {
        var usage = Read("{}");

        Assert.IsNotNull(usage);
        Assert.IsNull(usage.InputTokens);
        Assert.IsNull(usage.OutputTokens);
        Assert.IsNull(usage.CacheCreation);
        Assert.IsNull(usage.OutputTokensDetails);
        Assert.IsNull(usage.ServerToolUse);
        Assert.IsNull(usage.ServiceTier);
        Assert.IsNull(usage.Speed);
        Assert.IsNull(usage.InferenceRegion);
        Assert.IsNull(usage.FallbackCredit);
        Assert.AreEqual(0, usage.Iterations.Count);
    }

    // A closed set is open to a value this library has not named: the value stands and reads as
    // undefined, rather than being dropped or refused.
    [TestMethod]
    public void ASpeedAndACostBasisTheLibraryDoesNotNameStillRead()
    {
        var usage = Read("""{"speed": "leisurely", "service_tier": "gold"}""");

        Assert.AreEqual("leisurely", usage?.Speed.ToString());
        Assert.IsFalse(usage!.Speed!.Value.IsDefined);
        Assert.AreEqual("gold", usage.ServiceTier.ToString());
        Assert.IsFalse(usage.ServiceTier!.Value.IsDefined);

        var models = UsageParser.ModelUsages(Recorded.Json("""{"claude-haiku-4-5": {"costBasis": "goodwill"}}"""));
        Assert.AreEqual("goodwill", models["claude-haiku-4-5"].CostBasis.ToString());
        Assert.IsFalse(models["claude-haiku-4-5"].CostBasis!.Value.IsDefined);
    }

    // MSTest's own assertion does not hand the value back, and every kind test needs it typed.
    private static TValue Is<TValue>(object? value)
    {
        Assert.IsInstanceOfType<TValue>(value);
        return (TValue)value!;
    }

    private static Usage? Read(string json) => UsageParser.ParseUsage(Recorded.Json(json));
}
