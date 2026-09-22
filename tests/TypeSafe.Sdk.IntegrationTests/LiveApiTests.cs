using System.Net;

namespace TypeSafe.IntegrationTests;

/// <summary>
/// Tests that talk to the real TypeSafe AI API. They run only when <c>TYPESAFE_API_KEY</c> is set and
/// report themselves as skipped, with a reason, when it is not — see <see cref="LiveApiFixture"/>,
/// which also records that the passing path is unverified.
/// </summary>
public sealed class LiveApiTests
{
    [SkippableFact]
    public async Task Models_list_returns_at_least_one_model_with_a_name()
    {
        LiveApiFixture.SkipUnlessConfigured();
        using var cts = LiveApiFixture.CreateTimeout();
        using var client = LiveApiFixture.CreateClient();

        var models = await client.Models.ListAsync(cancellationToken: cts.Token);

        Assert.NotEmpty(models);
        Assert.All(models, model => Assert.False(string.IsNullOrWhiteSpace(model.Name)));
    }

    [SkippableFact]
    public async Task System_one_answers_a_noul_a_choice_and_a_score_question()
    {
        LiveApiFixture.SkipUnlessConfigured();
        using var cts = LiveApiFixture.CreateTimeout();
        using var client = LiveApiFixture.CreateClient();

        var billing = Question.Named.Noul(
            "billing",
            "Is this message about billing?",
            "The message is about billing.",
            "The message is about something else.");
        var tone = Question.Named.Choice("tone", "What is the tone of the message?", "calm", "angry", "confused");
        var urgency = Question.Named.Score(
            "urgency",
            "How urgent is this message?",
            "Not urgent at all.",
            "Somewhat urgent.",
            "Extremely urgent.");

        var response = await client.SystemOneAsync(
            "I was charged twice for my subscription this month and I need a refund today.",
            [billing, tone, urgency],
            cancellationToken: cts.Token);

        var noulAnswer = response.Get(billing);
        Assert.InRange(noulAnswer.Noul, 0d, 1d);

        var choiceAnswer = response.Get(tone);
        Assert.False(string.IsNullOrWhiteSpace(choiceAnswer.Choice));
        Assert.Equal(1d, choiceAnswer.Probabilities.Values.Sum(), 0.01d);

        var scoreAnswer = response.Get(urgency);
        Assert.Equal(1d, scoreAnswer.Probabilities.Values.Sum(), 0.01d);
    }

    /// <summary>
    /// A key that cannot be valid must come back as <see cref="TypeSafeAuthenticationException"/> carrying
    /// the request id from the response headers. Unlike its two neighbours this test needs no valid
    /// credential, so it is the one part of this project that has been run against the live API: a bogus
    /// key draws a 401 with a request id, exactly as asserted here.
    /// </summary>
    [SkippableFact]
    public async Task A_bad_api_key_is_rejected_as_an_authentication_error()
    {
        LiveApiFixture.SkipUnlessConfigured();
        using var cts = LiveApiFixture.CreateTimeout();
        using var client = LiveApiFixture.CreateClientWithBadKey();

        var exception = await Assert.ThrowsAsync<TypeSafeAuthenticationException>(
            () => client.Models.ListAsync(cancellationToken: cts.Token));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.False(
            string.IsNullOrWhiteSpace(exception.RequestId),
            "the API's 401 response must carry a request id header");
    }
}
