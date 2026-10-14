using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;

namespace Claude.Agent.Recorder;

// The evidence the ask fixture is asked over, through AskAsync, answered as a WordReport: a
// description on the type and on its members, a list, and a word outside ASCII, so the prompt the
// model reads carries the descriptions and the text unescaped. The tests compile this file too, so
// the prompt they rebuild is written off the type the recorder asked with.
[Description("Where the word comes from.")]
internal sealed class WordEvidence
{
    [Description("The word to report on.")]
    public required string Word { get; init; }

    [Description("The language the word is in.")]
    public required string Language { get; init; }

    [Description("Sentences the word was read in, one a string.")]
    public required IReadOnlyList<string> SeenIn { get; init; }

    // The task the recorder asked with, and the evidence it asked over; the replay asks the same.
    public const string AskTask = "Report on the word the evidence gives.";

    public static WordEvidence Recorded { get; } = new()
    {
        Word = "pöllö",
        Language = "Finnish",
        SeenIn = ["Pöllö huhuilee yöllä metsässä."],
    };
}
