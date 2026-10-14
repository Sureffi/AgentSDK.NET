using System.Text.Json;

namespace Claude.Agent;

// The ReportFindings tool's input and output, which carry the same finding shape both ways. No
// recording calls this tool: the shape stands on the declaration alone.
internal static class ReportFindingsToolParser
{
    private static ReportFindingsToolFinding? AsReportFindingsToolFinding(JsonElement raw)
    {
        if (!raw.Text("file", out var file)
            || !raw.Text("summary", out var summary)
            || !raw.Text("failure_scenario", out var failureScenario))
        {
            return null;
        }

        return new ReportFindingsToolFinding(
            file,
            summary,
            failureScenario,
            raw.CountOrNull("line"),
            raw.TextOrNull("short_summary"),
            raw.TextOrNull("category"),
            raw.SetOrNull("verdict", value => new ReportFindingsVerdict(value)),
            raw.SetOrNull("outcome", value => new ReportFindingsOutcome(value)));
    }

    public static ReportFindingsToolInput? ReadInput(JsonElement raw)
    {
        if (!raw.Items("findings", AsReportFindingsToolFinding, out var findings))
        {
            return null;
        }

        return new ReportFindingsToolInput(findings, raw.SetOrNull("level", value => new Effort(value)));
    }

    public static ReportFindingsToolOutput? ReadOutput(JsonElement raw)
    {
        if (!raw.Count("count", out var count)
            || !raw.Items("findings", AsReportFindingsToolFinding, out var findings))
        {
            return null;
        }

        return new ReportFindingsToolOutput(count, findings, raw.SetOrNull("level", value => new Effort(value)));
    }

    private static void WriteReportFindingsToolFinding(Utf8JsonWriter writer, ReportFindingsToolFinding value)
    {
        writer.WriteString("file", value.File);
        writer.WriteString("summary", value.Summary);
        writer.WriteString("failure_scenario", value.FailureScenario);
        writer.Count("line", value.Line);
        writer.Text("short_summary", value.ShortSummary);
        writer.Text("category", value.Category);
        writer.Text("verdict", value.Verdict?.ToString());
        writer.Text("outcome", value.Outcome?.ToString());
    }

    public static void WriteInput(Utf8JsonWriter writer, ReportFindingsToolInput input)
    {
        writer.WriteStartObject();
        WriteReportFindingsToolInput(writer, input);
        writer.WriteEndObject();
    }

    private static void WriteReportFindingsToolInput(Utf8JsonWriter writer, ReportFindingsToolInput value)
    {
        writer.ItemObjects("findings", value.Findings, WriteReportFindingsToolFinding, whenEmpty: true);
        writer.Text("level", value.Level?.ToString());
    }

    public static void WriteOutput(Utf8JsonWriter writer, ReportFindingsToolOutput output)
    {
        writer.WriteStartObject();
        WriteReportFindingsToolOutput(writer, output);
        writer.WriteEndObject();
    }

    private static void WriteReportFindingsToolOutput(Utf8JsonWriter writer, ReportFindingsToolOutput value)
    {
        writer.WriteNumber("count", value.Count);
        writer.ItemObjects("findings", value.Findings, WriteReportFindingsToolFinding, whenEmpty: true);
        writer.Text("level", value.Level?.ToString());
    }
}
