using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace TypeSafe.Tests;

/// <summary>
/// Guards the shape of the public surface: callers pass <see cref="Content"/>, never a loosely typed
/// <c>object?</c>. <see cref="Content.From(object?)"/> is the one sanctioned escape hatch.
/// </summary>
public class PublicSurfaceTests
{
    private const BindingFlags Declared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    /// <summary>The types the story names explicitly, so a rename cannot make the scan pass vacuously.</summary>
    private static readonly Type[] MustBeScanned =
    [
        typeof(Content), typeof(Question), typeof(NoulCriteria), typeof(SystemOneRequest),
        typeof(TypeSafeClient), typeof(ITypeSafeClient),
    ];

    [Fact]
    public void NoPublicMemberTakesObjectExceptContentFrom()
    {
        var types = typeof(Content).Assembly.GetExportedTypes();
        foreach (var required in MustBeScanned) Assert.Contains(required, types);

        var offenders = new List<string>();
        var exempt = new List<string>();
        foreach (var type in types)
        {
            foreach (var ctor in type.GetConstructors(Declared))
                if (IsVisible(ctor)) Inspect(type, ctor, ".ctor", offenders, exempt);

            foreach (var method in type.GetMethods(Declared))
                if (IsVisible(method) && !IsAccessor(method) && !IsOverride(method))
                    Inspect(type, method, method.Name, offenders, exempt);

            foreach (var property in type.GetProperties(Declared))
            {
                if (!IsVisible(property.GetMethod) && !IsVisible(property.SetMethod)) continue;
                if (IsObjectTyped(property.PropertyType))
                    offenders.Add($"{Describe(type)}.{property.Name} is a public property of type {Name(property.PropertyType)}");
                foreach (var index in property.GetIndexParameters())
                    if (IsObjectTyped(index.ParameterType))
                        offenders.Add($"{Describe(type)}.{property.Name}[{index.Name}] is an indexer parameter of type {Name(index.ParameterType)}");
            }
        }

        Assert.True(offenders.Count == 0, Report(offenders));

        // The scan really does walk parameters: the one exempt member is the only one it found.
        Assert.Equal(["TypeSafe.Content.From(object value)"], exempt);
    }

    /// <summary>
    /// Transport now deserializes through <c>TypeSafeJsonContext</c>, so the hand-rolled parsing layer is gone:
    /// no <c>Wire</c> helper, no <c>ResponseFieldException</c>, and no static <c>Parse</c> on either response type.
    /// </summary>
    [Fact]
    public void HandRolledResponseParsingIsGone()
    {
        var types = typeof(SystemOneResponse).Assembly.GetTypes();

        // The scan sees internals (InternalsVisibleTo), so a missing type really means deleted, not hidden.
        Assert.Contains(typeof(SystemOneResponse), types);
        Assert.Contains(typeof(TypeSafe.Internal.JsonContent), types);

        var deleted = types
            .Where(type => type.Name is "Wire" or "ResponseFieldException")
            .Select(Describe)
            .ToArray();
        Assert.Empty(deleted);

        foreach (var type in new[] { typeof(SystemOneResponse), typeof(ListModelsResponse) })
        {
            var parse = type.GetMethods(Declared).Where(method => method.Name == "Parse").Select(Describe).ToArray();
            Assert.Empty(parse);
        }
    }

    /// <summary>
    /// The library is built with <c>IsAotCompatible</c>, which turns the trim, single-file and AOT
    /// analyzers on. That marker is an MSBuild property rather than an assembly attribute, so what this
    /// test can observe is its consequence: the reflection-based object conversion is the only annotated
    /// path, and nothing else in the assembly declares that it needs unreferenced or dynamic code.
    /// </summary>
    [Fact]
    public void OnlyTheReflectionConversionRequiresUnreferencedOrDynamicCode()
    {
        string[] expected =
        [
            "TypeSafe.Content.From(Object)",
            "TypeSafe.Internal.JsonContent.From(Object)",
            "TypeSafe.Internal.JsonContent.get_SerializerOptions()",
        ];

        // The scan sees internals (InternalsVisibleTo), so the conversion helper is really in range.
        var types = typeof(Content).Assembly.GetTypes();
        Assert.Contains(typeof(TypeSafe.Internal.JsonContent), types);

        var annotated = new List<string>();
        foreach (var type in types)
        {
            if (RequiresUnsafeCode(type)) annotated.Add(Describe(type));
            foreach (var member in type.GetMembers(Declared))
            {
                if (member is Type) continue; // Nested types are visited by the outer loop.
                if (RequiresUnsafeCode(member)) annotated.Add(DescribeAnnotated(type, member));
            }
        }

        annotated.Sort(StringComparer.Ordinal);
        var unexpected = annotated.Except(expected, StringComparer.Ordinal).ToArray();
        Assert.True(unexpected.Length == 0, ReportAnnotated(unexpected));
        Assert.Equal(expected, annotated);
    }

    /// <summary>
    /// The <c>Questions : Dictionary&lt;string, Question&gt;</c> collection type is gone: callers pass any
    /// <c>IEnumerable&lt;KeyValuePair&lt;string, Question&gt;&gt;</c>, so a plain dictionary works and the SDK
    /// no longer owns a collection type of its own.
    /// </summary>
    [Fact]
    public void TheQuestionsDictionaryTypeIsGone()
    {
        // The scan sees internals (InternalsVisibleTo), so a missing type really means deleted, not hidden.
        var types = typeof(Question).Assembly.GetTypes();
        Assert.Contains(typeof(Question), types);

        var survivors = types.Where(type => type.Name == "Questions").Select(Describe).ToArray();
        Assert.Empty(survivors);

        // The overload that used to take Questions now takes the interface any dictionary satisfies.
        var overloads = typeof(ITypeSafeClient)
            .GetMethods()
            .Where(method => method.Name == nameof(ITypeSafeClient.SystemOneAsync))
            .ToArray();
        // One state-and-questions overload takes a plain sequence; the named-question ones take a span.
        var keyed = Assert.Single(
            overloads,
            method => method.GetParameters() is { Length: > 3 } parameters
                && parameters[1].ParameterType is { IsGenericType: true } questions
                && questions.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        Assert.Equal(
            typeof(IEnumerable<KeyValuePair<string, Question>>),
            keyed.GetParameters()[1].ParameterType);
    }

    /// <summary>Whether a member opts out of trim or AOT safety analysis for its callers.</summary>
    private static bool RequiresUnsafeCode(MemberInfo member) =>
        member.IsDefined(typeof(RequiresUnreferencedCodeAttribute), inherit: false)
        || member.IsDefined(typeof(RequiresDynamicCodeAttribute), inherit: false);

    /// <summary>Name an annotated member, with parameter types so overloads stay distinguishable.</summary>
    private static string DescribeAnnotated(Type type, MemberInfo member) =>
        member is MethodBase method
            ? $"{Describe(type)}.{member.Name}({string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.Name))})"
            : $"{Describe(type)}.{member.Name}";

    private static string ReportAnnotated(string[] unexpected)
    {
        var report = new StringBuilder(
            "Only the reflection-based object conversion may carry RequiresUnreferencedCode/RequiresDynamicCode. Unexpected:");
        foreach (var member in unexpected) report.Append(Environment.NewLine).Append("  - ").Append(member);
        return report.ToString();
    }

    private static string Describe(MethodInfo method) => $"{Describe(method.DeclaringType!)}.{method.Name}";

    /// <summary>Record the members of <paramref name="member"/> that take an <c>object</c>, bar the one exemption.</summary>
    private static void Inspect(Type type, MethodBase member, string name, List<string> offenders, List<string> exempt)
    {
        if (member.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)) return;
        foreach (var parameter in member.GetParameters())
        {
            if (!IsObjectTyped(parameter.ParameterType)) continue;
            if (IsContentFrom(type, member, name))
            {
                exempt.Add($"{Describe(type)}.{name}({Name(parameter.ParameterType)} {parameter.Name})");
                continue;
            }
            offenders.Add($"{Describe(type)}.{name}({Name(parameter.ParameterType)} {parameter.Name}) takes {Name(parameter.ParameterType)}");
        }
    }

    /// <summary>The single allowed escape hatch: <c>Content.From(object?)</c>.</summary>
    private static bool IsContentFrom(Type type, MethodBase member, string name) =>
        type == typeof(Content)
        && name == nameof(Content.From)
        && member is MethodInfo { IsStatic: true }
        && member.GetParameters() is [{ ParameterType: var only }]
        && only == typeof(object);

    private static bool IsObjectTyped(Type type) => type == typeof(object) || type == typeof(object[]);

    /// <summary>Public or protected, i.e. part of the surface a caller or subclass can see.</summary>
    private static bool IsVisible(MethodBase? member) =>
        member is not null && (member.IsPublic || member.IsFamily || member.IsFamilyOrAssembly);

    /// <summary>Property and event accessors are covered by the property scan instead.</summary>
    private static bool IsAccessor(MethodInfo method) =>
        method.IsSpecialName
        && (method.Name.StartsWith("get_", StringComparison.Ordinal)
            || method.Name.StartsWith("set_", StringComparison.Ordinal)
            || method.Name.StartsWith("add_", StringComparison.Ordinal)
            || method.Name.StartsWith("remove_", StringComparison.Ordinal));

    /// <summary>An override of a base member (<c>Equals(object)</c>, <c>ToString</c>) is the base type's contract.</summary>
    private static bool IsOverride(MethodInfo method) => method.GetBaseDefinition() != method;

    private static string Describe(Type type) => type.FullName ?? type.Name;

    private static string Name(Type type) =>
        type == typeof(object) ? "object" : type == typeof(object[]) ? "object[]" : type.Name;

    private static string Report(List<string> offenders)
    {
        var report = new StringBuilder("Public members must take Content, not object. Only Content.From(object?) is exempt. Offenders:");
        foreach (var offender in offenders) report.Append(Environment.NewLine).Append("  - ").Append(offender);
        return report.ToString();
    }
}
