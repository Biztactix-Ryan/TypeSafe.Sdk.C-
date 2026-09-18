using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using TypeSafe.Internal;

namespace TypeSafe.Tests;

/// <summary>A payload serialized through source-generated metadata in the tests below.</summary>
internal sealed record TicketPayload(string Subject, int PriorityLevel, bool IsUrgent);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(TicketPayload))]
internal partial class ContentTestJsonContext : JsonSerializerContext;

public class ContentTests
{
    [Fact]
    public void DefaultAndNullAreAbsentContent()
    {
        Assert.Null(default(Content).Node);
        Assert.Null(Content.Null.Node);
        Assert.Equal(default, Content.Null);

        Content fromNullString = (string?)null;
        Content fromNullObject = (JsonObject?)null;
        Content fromNullArray = (JsonArray?)null;
        Content fromNullNode = new((JsonNode?)null);

        Assert.Null(fromNullString.Node);
        Assert.Null(fromNullObject.Node);
        Assert.Null(fromNullArray.Node);
        Assert.Null(fromNullNode.Node);
    }

    [Fact]
    public void StringConvertsToAStringValueNode()
    {
        Content content = "Contract renewal";

        Assert.Equal("\"Contract renewal\"", content.Node!.ToJsonString());
        Assert.Equal("Contract renewal", content.Node!.GetValue<string>());
    }

    [Fact]
    public void EmptyStringIsContentNotAbsence()
    {
        Content content = string.Empty;

        Assert.NotNull(content.Node);
        Assert.Equal(string.Empty, content.Node!.GetValue<string>());
    }

    [Fact]
    public void JsonObjectConvertsAndIsExposedUnchanged()
    {
        var value = new JsonObject { ["subject"] = "Renewal", ["urgent"] = true };

        Content content = value;

        Assert.Same(value, content.Node);
        Assert.Equal("""{"subject":"Renewal","urgent":true}""", content.Node!.ToJsonString());
    }

    [Fact]
    public void JsonArrayConvertsAndIsExposedUnchanged()
    {
        var value = new JsonArray("first", "second");

        Content content = value;

        Assert.Same(value, content.Node);
        Assert.Equal("""["first","second"]""", content.Node!.ToJsonString());
    }

    [Fact]
    public void ConstructorTakesAnyUnparentedNode()
    {
        var value = JsonValue.Create(42);

        var content = new Content(value);

        Assert.Same(value, content.Node);
        Assert.Equal("42", content.Node!.ToJsonString());
    }

    [Fact]
    public void ParentedObjectIsClonedSoItCanBeReusedTwice()
    {
        var envelope = new JsonObject { ["payload"] = new JsonObject { ["subject"] = "Renewal" } };
        var parented = (JsonObject)envelope["payload"]!;

        Content first = parented;
        Content second = parented;

        Assert.NotSame(parented, first.Node);
        Assert.NotSame(parented, second.Node);
        Assert.NotSame(first.Node, second.Node);
        Assert.Null(first.Node!.Parent);
        Assert.Null(second.Node!.Parent);

        // Both clones attach to fresh parents without System.Text.Json throwing.
        var one = new JsonObject { ["content"] = first.Node };
        var two = new JsonObject { ["content"] = second.Node };

        Assert.Equal("""{"content":{"subject":"Renewal"}}""", one.ToJsonString());
        Assert.Equal("""{"content":{"subject":"Renewal"}}""", two.ToJsonString());
        Assert.Equal("""{"payload":{"subject":"Renewal"}}""", envelope.ToJsonString());
    }

    [Fact]
    public void ParentedArrayAndValueAreAlsoCloned()
    {
        var envelope = new JsonObject
        {
            ["items"] = new JsonArray("a", "b"),
            ["label"] = "hello",
        };
        var parentedArray = (JsonArray)envelope["items"]!;
        var parentedValue = envelope["label"]!;

        Content array = parentedArray;
        var value = new Content(parentedValue);

        Assert.NotSame(parentedArray, array.Node);
        Assert.NotSame(parentedValue, value.Node);

        var reused = new JsonObject { ["items"] = array.Node, ["label"] = value.Node };

        Assert.Equal("""{"items":["a","b"],"label":"hello"}""", reused.ToJsonString());
    }

    [Fact]
    public void EqualityComparesTheWrappedNodeByReference()
    {
        var value = new JsonObject { ["a"] = 1 };

        Content first = value;
        Content second = value;

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.NotEqual(first, (Content)new JsonObject { ["a"] = 1 });
        Assert.NotEqual(first, Content.Null);
    }

    [Fact]
    public void TypedFromUsesSourceGeneratedMetadata()
    {
        var payload = new TicketPayload("Renewal", 2, true);

        var content = Content.From(payload, ContentTestJsonContext.Default.TicketPayload);

        Assert.Equal(
            """{"subject":"Renewal","priorityLevel":2,"isUrgent":true}""",
            content.Node!.ToJsonString());
    }

    [Fact]
    public void TypedFromUsesTheContextOptionsNotTheSdkCamelCaseDefault()
    {
        // The options carry only the source-generated resolver, so there is no reflection fallback to
        // fall back on, and their snake_case policy is one the SDK's own camelCase defaults could not
        // produce: the JSON below can only have come from the supplied JsonTypeInfo.
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            TypeInfoResolver = ContentTestJsonContext.Default,
        };
        var context = new ContentTestJsonContext(options);

        var content = Content.From(new TicketPayload("Renewal", 2, true), context.TicketPayload);

        Assert.Equal(
            """{"subject":"Renewal","priority_level":2,"is_urgent":true}""",
            content.Node!.ToJsonString());
    }

    [Fact]
    public void TypedFromWithNullValueIsAbsentContent()
    {
        var content = Content.From((TicketPayload?)null, ContentTestJsonContext.Default.TicketPayload!);

        Assert.Null(content.Node);
        Assert.Equal(Content.Null, content);
    }

    [Fact]
    public void TypedFromRejectsMissingTypeInfo()
    {
        Assert.Throws<ArgumentNullException>(
            () => Content.From(new TicketPayload("Renewal", 1, false), null!));
    }

    [Fact]
    public void ObjectFromSerializesAnonymousObjectsAsCamelCase()
    {
        var value = new { Subject = "Renewal", PriorityLevel = 2, IsUrgent = true };

        var content = Content.From(value);

        Assert.Equal(
            """{"subject":"Renewal","priorityLevel":2,"isUrgent":true}""",
            content.Node!.ToJsonString());
        Assert.Equal(JsonContent.From(value)!.ToJsonString(), content.Node!.ToJsonString());
    }

    [Fact]
    public void ObjectFromWithNullIsAbsentContent()
    {
        var content = Content.From((object?)null);

        Assert.Null(content.Node);
        Assert.Equal(Content.Null, content);
    }

    [Fact]
    public void ObjectFromHandlesStringsNodesAndContent()
    {
        var text = Content.From("Renewal");
        Assert.Equal("\"Renewal\"", text.Node!.ToJsonString());

        var node = new JsonObject { ["subject"] = "Renewal" };
        var fromNode = Content.From(node);
        Assert.Same(node, fromNode.Node);

        var original = new Content(new JsonArray("a", "b"));
        var fromContent = Content.From(original);
        Assert.Equal(original, fromContent);
        Assert.Same(original.Node, fromContent.Node);
    }

    [Fact]
    public void ObjectFromAndItsInternalHelperAreAnnotatedForTrimmingAndAot()
    {
        var publicOverload = typeof(Content).GetMethod(
            nameof(Content.From),
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            [typeof(object)],
            modifiers: null);
        var helper = typeof(JsonContent).GetMethod(
            nameof(JsonContent.From),
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            [typeof(object)],
            modifiers: null);

        Assert.NotNull(publicOverload);
        Assert.NotNull(helper);

        // Annotated code calling annotated code keeps the trim/AOT analyzer chain clean.
        foreach (var method in new[] { publicOverload!, helper! })
        {
            var trimming = method.GetCustomAttribute<RequiresUnreferencedCodeAttribute>();
            var aot = method.GetCustomAttribute<RequiresDynamicCodeAttribute>();

            Assert.NotNull(trimming);
            Assert.NotNull(aot);

            foreach (var message in new[] { trimming!.Message, aot!.Message })
            {
                Assert.Contains("From<T>", message);
                Assert.Contains("JsonSerializerContext", message);
            }
        }
    }
}
