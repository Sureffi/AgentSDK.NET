using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;

namespace Claude.Agent.Recorder;

// The type the schema fixtures take their schema from, through SetOutputSchema<T>: a description on
// the type, on its members and on a nested type, a maximum length on a string and on an array, an
// enum written by name, required members, a nullable one that is not, and a nested object. The tests
// compile this file too, so the schema they rebuild is taken from the type the recorder asked with.
[Description("A report on one word.")]
internal sealed class WordReport
{
    [Description("The word, as it was given.")]
    [MaxLength(16)]
    public required string Word { get; init; }

    [Description("How many letters the word has.")]
    public required int Letters { get; init; }

    [Description("The word's letters in order, one letter a string.")]
    [Length(1, 16)]
    public required IReadOnlyList<string> Spelling { get; init; }

    [Description("The part of speech the word is most often.")]
    public required PartOfSpeech PartOfSpeech { get; init; }

    public required LetterCounts Counts { get; init; }

    [StringLength(80)]
    public string? Remark { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<PartOfSpeech>))]
internal enum PartOfSpeech
{
    Noun,
    Verb,
    Adjective,
    Interjection,
    Other,
}

[Description("The word's letters, counted by kind.")]
internal sealed class LetterCounts
{
    public required int Vowels { get; init; }

    public required int Consonants { get; init; }
}
