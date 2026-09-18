using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TypeSafe.Internal;

/// <summary>One enum member with the wire label and optional description the SDK sends for it.</summary>
/// <typeparam name="TEnum">The enum the member belongs to.</typeparam>
/// <param name="Value">The enum member.</param>
/// <param name="Label">The wire label: <c>[JsonStringEnumMemberName]</c> when present, otherwise the snake_case member name.</param>
/// <param name="Description">The member's <c>[Description]</c>, or <c>null</c> when it carries none.</param>
internal readonly record struct EnumLabel<TEnum>(TEnum Value, string Label, string? Description)
    where TEnum : struct, Enum;

/// <summary>
/// Wire labels and descriptions for <typeparamref name="TEnum"/>, computed once per enum and cached.
/// </summary>
/// <remarks>
/// <para>
/// A label comes from <c>[JsonStringEnumMemberName("...")]</c> when the member carries one, otherwise from
/// <see cref="JsonNamingPolicy.SnakeCaseLower"/> applied to the member name (<c>VeryUrgent</c> becomes
/// <c>very_urgent</c>), which matches how the source-generated context writes enums on the wire. A
/// description comes from <c>[System.ComponentModel.Description]</c>, or is <c>null</c>.
/// </para>
/// <para>
/// The generic parameter is annotated <see cref="DynamicallyAccessedMemberTypes.PublicFields"/> so the
/// per-member <see cref="Type.GetField(string, BindingFlags)"/> lookup is trim- and AOT-safe: callers must
/// pass a statically known enum, which every SDK entry point does.
/// </para>
/// </remarks>
/// <typeparam name="TEnum">The enum to describe.</typeparam>
internal static class EnumLabels<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>
    where TEnum : struct, Enum
{
    // Lazy rather than a static field initializer so a duplicate label surfaces as TypeSafeException at
    // first use instead of a TypeInitializationException wrapping it.
    private static readonly Lazy<Table> Cached = new(Build);

    /// <summary>The enum's members in declaration order, with their labels and descriptions.</summary>
    /// <exception cref="TypeSafeException">Two members map to the same label.</exception>
    public static IReadOnlyList<EnumLabel<TEnum>> Entries => Cached.Value.Entries;

    /// <summary>Look a label up, matching ordinally, and return the member it names.</summary>
    /// <param name="label">The wire label to resolve.</param>
    /// <param name="value">The member the label names, or <c>default</c> when it names none.</param>
    /// <returns><c>true</c> when the label is declared on the enum.</returns>
    /// <exception cref="TypeSafeException">Two members map to the same label.</exception>
    public static bool TryGetMember(string? label, out TEnum value)
    {
        if (label is not null && Cached.Value.ByLabel.TryGetValue(label, out value)) return true;
        value = default;
        return false;
    }

    private static Table Build()
    {
        var names = Enum.GetNames<TEnum>();
        var values = Enum.GetValues<TEnum>();
        var entries = new EnumLabel<TEnum>[names.Length];
        var byLabel = new Dictionary<string, TEnum>(names.Length, StringComparer.Ordinal);

        for (var i = 0; i < names.Length; i++)
        {
            var member = typeof(TEnum).GetField(names[i], BindingFlags.Public | BindingFlags.Static);
            var label = member?.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name
                ?? JsonNamingPolicy.SnakeCaseLower.ConvertName(names[i]);
            var description = member?.GetCustomAttribute<DescriptionAttribute>()?.Description;

            entries[i] = new EnumLabel<TEnum>(values[i], label, description);
            if (!byLabel.TryAdd(label, values[i]))
            {
                throw new TypeSafeException(
                    $"Enum {typeof(TEnum).FullName ?? typeof(TEnum).Name} maps more than one member to the label '{label}'; "
                    + "give one of them a different [JsonStringEnumMemberName].");
            }
        }

        return new Table(entries, byLabel);
    }

    private sealed record Table(EnumLabel<TEnum>[] Entries, Dictionary<string, TEnum> ByLabel);
}

/// <summary>
/// The rubric rules an enum must satisfy to stand in for the ordered levels of a score question, and the
/// score-to-member mapping that follows from them.
/// </summary>
/// <remarks>
/// A score rubric is a list indexed by score from zero, so an enum used as one must number its members
/// exactly <c>0</c> to <c>N-1</c> in declaration order. <see cref="Validate{TEnum}"/> is what every entry
/// point checks first; once it has passed, the member for score <c>k</c> is simply the <c>k</c>-th entry
/// of <see cref="EnumLabels{TEnum}.Entries"/>, so nothing here needs reflection of its own.
/// </remarks>
internal static class Rubric
{
    /// <summary>
    /// Check that <paramref name="entries"/> number their members <c>0</c> to <c>N-1</c> in declaration
    /// order, which is what lets a score index them directly.
    /// </summary>
    /// <typeparam name="TEnum">The enum offered as a rubric.</typeparam>
    /// <param name="entries">The enum's members in declaration order, from <see cref="EnumLabels{TEnum}.Entries"/>.</param>
    /// <exception cref="TypeSafeException">The enum declares no members, or values that are not contiguous from zero.</exception>
    public static void Validate<TEnum>(IReadOnlyList<EnumLabel<TEnum>> entries)
        where TEnum : struct, Enum
    {
        if (entries.Count == 0) throw NotARubric<TEnum>();
        for (var i = 0; i < entries.Count; i++)
        {
            long value;
            try
            {
                // Works for every underlying integral type; a ulong member above long.MaxValue overflows,
                // which is simply another way of not being the value i.
                value = Convert.ToInt64(entries[i].Value, CultureInfo.InvariantCulture);
            }
            catch (OverflowException)
            {
                throw NotARubric<TEnum>();
            }
            if (value != i) throw NotARubric<TEnum>();
        }
    }

    /// <summary>
    /// The member a score names, for an enum <see cref="Validate{TEnum}"/> has accepted.
    /// </summary>
    /// <typeparam name="TEnum">The enum used as the rubric.</typeparam>
    /// <param name="score">The integer score the server sent.</param>
    /// <param name="level">The member with that value, or <c>default</c> when the score is outside the rubric.</param>
    /// <returns><c>true</c> when the score names a member of <typeparamref name="TEnum"/>.</returns>
    public static bool TryGetLevel<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>(
        int score, out TEnum level)
        where TEnum : struct, Enum
    {
        var entries = EnumLabels<TEnum>.Entries;
        if (score >= 0 && score < entries.Count)
        {
            level = entries[score].Value;
            return true;
        }
        level = default;
        return false;
    }

    /// <summary>
    /// The member an expected score is nearest to: the score rounded half away from zero and clamped to
    /// the rubric, so a score between two levels picks the closer one and one outside picks an end.
    /// </summary>
    /// <typeparam name="TEnum">The enum used as the rubric.</typeparam>
    /// <param name="score">The expected score, which may fall between the integer levels.</param>
    /// <returns>The nearest member, or <c>default</c> when the enum declares none.</returns>
    public static TEnum Nearest<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>(double score)
        where TEnum : struct, Enum
    {
        var entries = EnumLabels<TEnum>.Entries;
        if (entries.Count == 0) return default;
        var rounded = Math.Round(score, MidpointRounding.AwayFromZero);
        // NaN compares false against both bounds, so it lands on the lowest level rather than throwing.
        var index = rounded >= entries.Count - 1 ? entries.Count - 1 : rounded > 0 ? (int)rounded : 0;
        return entries[index].Value;
    }

    private static TypeSafeException NotARubric<TEnum>()
        where TEnum : struct, Enum =>
        new($"Enum {typeof(TEnum).Name} must have contiguous values starting at 0 to be used as a score rubric.");
}
