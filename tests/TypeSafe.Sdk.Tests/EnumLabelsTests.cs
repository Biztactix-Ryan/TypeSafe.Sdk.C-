using System.ComponentModel;
using System.Text.Json.Serialization;
using TypeSafe.Internal;

namespace TypeSafe.Tests;

public class EnumLabelsTests
{
    public enum Tone
    {
        Calm,
        VeryUrgent,
        [JsonStringEnumMemberName("furious")]
        Angry,
    }

    public enum Priority
    {
        [Description("can wait a week")]
        Low,
        Medium,
        [Description("needs action today")]
        High,
    }

    public enum Duplicated
    {
        [JsonStringEnumMemberName("same")]
        First,
        [JsonStringEnumMemberName("same")]
        Second,
    }

    [Fact]
    public void AttributeLabelWinsOverSnakeCaseFallback()
    {
        Assert.Equal(new[] { "calm", "very_urgent", "furious" }, EnumLabels<Tone>.Entries.Select(e => e.Label));
        Assert.Equal(new[] { Tone.Calm, Tone.VeryUrgent, Tone.Angry }, EnumLabels<Tone>.Entries.Select(e => e.Value));
        Assert.All(EnumLabels<Tone>.Entries, entry => Assert.Null(entry.Description));
    }

    [Fact]
    public void DescriptionsAreReadInDeclarationOrderAndNullWhenAbsent()
    {
        Assert.Equal(
            new[] { "can wait a week", null, "needs action today" },
            EnumLabels<Priority>.Entries.Select(e => e.Description));
        Assert.Equal(new[] { "low", "medium", "high" }, EnumLabels<Priority>.Entries.Select(e => e.Label));
    }

    [Fact]
    public void EntriesAreComputedOnceAndCached()
    {
        Assert.Same(EnumLabels<Tone>.Entries, EnumLabels<Tone>.Entries);
    }

    [Fact]
    public void TryGetMemberResolvesLabelsOrdinally()
    {
        Assert.True(EnumLabels<Tone>.TryGetMember("very_urgent", out var urgent));
        Assert.Equal(Tone.VeryUrgent, urgent);

        Assert.True(EnumLabels<Tone>.TryGetMember("furious", out var angry));
        Assert.Equal(Tone.Angry, angry);
    }

    [Fact]
    public void TryGetMemberMissesUnknownLabels()
    {
        Assert.False(EnumLabels<Tone>.TryGetMember("nope", out var unknown));
        Assert.Equal(default, unknown);

        // The member name itself, a differently cased label, and null are all misses.
        Assert.False(EnumLabels<Tone>.TryGetMember("VeryUrgent", out _));
        Assert.False(EnumLabels<Tone>.TryGetMember("Very_Urgent", out _));
        Assert.False(EnumLabels<Tone>.TryGetMember("angry", out _));
        Assert.False(EnumLabels<Tone>.TryGetMember(null, out _));
    }

    [Fact]
    public void DuplicateLabelsThrowNamingTheEnumAndLabel()
    {
        var error = Assert.Throws<TypeSafeException>(() => EnumLabels<Duplicated>.Entries);
        Assert.Contains(nameof(Duplicated), error.Message);
        Assert.Contains("'same'", error.Message);

        // Every later use fails the same way rather than handing back a half-built table.
        Assert.Throws<TypeSafeException>(() => EnumLabels<Duplicated>.TryGetMember("same", out _));
    }
}
