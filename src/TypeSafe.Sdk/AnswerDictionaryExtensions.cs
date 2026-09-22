namespace TypeSafe;

/// <summary>
/// The per-type views of a map of answers — <c>Nouls</c>, <c>Choices</c> and <c>Scores</c> — as
/// extension members on any <c>IReadOnlyDictionary&lt;string, Answer&gt;</c>, so they are available on a
/// map a caller assembled as well as on <see cref="SystemOneResponse.Answers"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SystemOneResponse"/> declares <c>Nouls</c>, <c>Choices</c> and <c>Scores</c> as instance
/// properties of its own that forward here and cache the result; an instance member wins over an
/// extension member of the same name, so <c>response.Nouls</c> is the cached one and there is no
/// ambiguity to resolve.
/// </para>
/// <para>
/// Each view is built once per call, eagerly, and keeps the order of the map it came from: filtering a
/// <see cref="SystemOneResponse.Answers"/> map preserves wire order. Answers whose type this SDK version
/// does not model — bare <see cref="Answer"/> records — match none of the views, as they match no derived
/// record. The match is a runtime type test, not a lookup by the <see cref="Answer.Type"/> discriminator,
/// so no reflection is involved.
/// </para>
/// </remarks>
public static class AnswerDictionaryExtensions
{
    extension(IReadOnlyDictionary<string, Answer> answers)
    {
        /// <summary>The yes/no answers in the map, keyed by question name, in the map's own order.</summary>
        /// <exception cref="ArgumentNullException">The map is <c>null</c>.</exception>
        public IReadOnlyDictionary<string, NoulAnswer> Nouls => Filter<NoulAnswer>(answers);

        /// <summary>The choice answers in the map, keyed by question name, in the map's own order.</summary>
        /// <exception cref="ArgumentNullException">The map is <c>null</c>.</exception>
        public IReadOnlyDictionary<string, ChoiceAnswer> Choices => Filter<ChoiceAnswer>(answers);

        /// <summary>The score answers in the map, keyed by question name, in the map's own order.</summary>
        /// <exception cref="ArgumentNullException">The map is <c>null</c>.</exception>
        public IReadOnlyDictionary<string, ScoreAnswer> Scores => Filter<ScoreAnswer>(answers);
    }

    /// <summary>The answers of one type, in the order <paramref name="answers"/> enumerates them.</summary>
    /// <typeparam name="TAnswer">The answer record to keep.</typeparam>
    /// <param name="answers">The map to filter.</param>
    private static OrderedDictionary<string, TAnswer> Filter<TAnswer>(IReadOnlyDictionary<string, Answer> answers)
        where TAnswer : Answer
    {
        ArgumentNullException.ThrowIfNull(answers);
        var filtered = new OrderedDictionary<string, TAnswer>(StringComparer.Ordinal);
        foreach (var (name, answer) in answers)
        {
            if (answer is TAnswer typed) filtered[name] = typed;
        }
        return filtered;
    }
}
