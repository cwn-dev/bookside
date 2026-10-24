using System.Net;
using Bookside.Core.Vocabulary;
using Bookside.Core.Vocabulary.Lookup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace Bookside.Integrations.Tests;

/// <summary>Goes through the production registration (typed client + resilience) with a stubbed primary handler.</summary>
public sealed class FreeDictionaryApiClientTests : IDisposable
{
    private const string NotFoundBody =
        """{"word":"qwzx","entries":[],"source":{"url":"https://en.wiktionary.org","license":{"name":"CC BY-SA 4.0","url":"https://creativecommons.org/licenses/by-sa/4.0/"}}}""";

    private readonly StubHttpMessageHandler _handler = new();
    private readonly ServiceProvider _services;

    public FreeDictionaryApiClientTests()
    {
        var services = new ServiceCollection();
        var builder = services.AddFreeDictionaryApiClient(o => o.BaseAddress = new Uri("https://free.test/api/v1/"))
            .ConfigurePrimaryHttpMessageHandler(() => _handler);

        services.Configure<HttpStandardResilienceOptions>($"{builder.Name}-standard", o => o.Retry.Delay = TimeSpan.Zero);
        _services = services.BuildServiceProvider();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IDictionaryLookupService Client => _services.GetRequiredService<IDictionaryLookupService>();

    public void Dispose() => _services.Dispose();

    [Fact]
    public async Task Found_word_maps_phonetic_and_meanings_without_audio()
    {
        const string body = """
            {"word":"ice cream","entries":[{"language":{"code":"en","name":"English"},"partOfSpeech":"noun",
              "pronunciations":[{"type":"ipa","text":"/ˌaɪsˈkɹiːm/","tags":["Received Pronunciation"]},
                                {"type":"enpr","text":"īs-krēm","tags":[]},
                                {"type":"ipa","text":"/ˈaɪs ˌkɹim/","tags":[]}],
              "forms":[],"synonyms":[],"antonyms":[],
              "senses":[{"definition":"(uncountable) A frozen dessert.","tags":["uncountable"],"examples":["We had ice cream."],
                         "quotes":[],"synonyms":[],"antonyms":[],"subsenses":[]}]}],
             "source":{"url":"https://en.wiktionary.org/wiki/ice_cream"}}
            """;
        _handler.Reply(HttpStatusCode.OK, body);

        var result = await Client.LookupAsync("ice cream", cancellationToken: Ct);

        Assert.Equal(DictionaryLookupStatus.Found, result.Status);
        Assert.Equal("/ˈaɪs ˌkɹim/", result.Phonetic); // untagged IPA before regional ones; other notations ignored
        Assert.Null(result.AudioUrl);
        Assert.Equal(body, result.RawResponse);
        var meaning = Assert.Single(result.Meanings);
        Assert.Equal("noun", meaning.PartOfSpeech);
        Assert.Equal([("(uncountable) A frozen dessert.", "We had ice cream.")], meaning.Definitions.Select(d => (d.Text, d.Example)));
        Assert.Equal(new Uri("https://free.test/api/v1/entries/en/ice%20cream"), Assert.Single(_handler.Requests));
    }

    [Fact]
    public async Task Subsenses_follow_their_parent_and_short_quotes_stand_in_for_missing_examples()
    {
        var longQuote = new string('x', 301);
        _handler.Reply(HttpStatusCode.OK, $$"""
            {"word":"head","entries":[{"partOfSpeech":"noun","pronunciations":[],"senses":[
              {"definition":"Top part of the body.","examples":[" "],"quotes":[{"text":"Off with his head!","reference":"Carroll"}],
               "subsenses":[{"definition":"Mental aptitude.","examples":[],"quotes":[{"text":"{{longQuote}}","reference":"r"}],
                             "subsenses":[{"definition":"Aptitude for numbers.","examples":["A head for figures."],"quotes":[],"subsenses":[]}]}]},
              {"definition":"Leader.","examples":[],"quotes":[],"subsenses":[]}]}]}
            """);

        var result = await Client.LookupAsync("head", cancellationToken: Ct);

        Assert.Equal(
            [
                ("Top part of the body.", "Off with his head!"),
                ("Mental aptitude.", null),
                ("Aptitude for numbers.", "A head for figures."),
                ("Leader.", null),
            ],
            Assert.Single(result.Meanings).Definitions.Select(d => (d.Text, d.Example)));
    }

    [Fact]
    public async Task Entries_are_merged_by_part_of_speech_without_duplicates()
    {
        _handler.Reply(HttpStatusCode.OK, """
            {"word":"bank","entries":[
              {"partOfSpeech":"noun","senses":[{"definition":"A financial institution."}]},
              {"partOfSpeech":"verb","senses":[{"definition":"To deposit money."}]},
              {"partOfSpeech":"Noun","senses":[{"definition":"The edge of a river."},{"definition":"A financial institution."}]},
              {"partOfSpeech":"adjective","senses":[{"definition":"  "}]}]}
            """);

        var result = await Client.LookupAsync("bank", cancellationToken: Ct);

        Assert.Null(result.Phonetic);
        Assert.Collection(
            result.Meanings,
            m => Assert.Equal(["A financial institution.", "The edge of a river."], m.Definitions.Select(d => d.Text)),
            m => Assert.Equal(("verb", 1), (m.PartOfSpeech, m.Definitions.Count)));
    }

    [Fact]
    public async Task Empty_entries_is_a_cacheable_not_found_and_is_not_retried()
    {
        _handler.Reply(HttpStatusCode.OK, NotFoundBody);

        var result = await Client.LookupAsync("qwzx", cancellationToken: Ct);

        Assert.Equal(DictionaryLookupStatus.NotFound, result.Status);
        Assert.Equal(NotFoundBody, result.RawResponse);
        Assert.Single(_handler.Requests);
    }

    [Fact]
    public async Task Entries_with_no_definitions_are_not_found()
    {
        _handler.Reply(HttpStatusCode.OK, """{"word":"x","entries":[{"partOfSpeech":"noun","senses":[]}]}""");

        var result = await Client.LookupAsync("x", cancellationToken: Ct);

        Assert.Equal(DictionaryLookupStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task A_404_is_a_failure_not_a_not_found()
    {
        _handler.Reply(HttpStatusCode.NotFound, NotFoundBody);

        await Assert.ThrowsAsync<DictionaryLookupException>(() => Client.LookupAsync("qwzx", cancellationToken: Ct));
    }

    [Theory]
    [InlineData("""{"unexpected":true}""")]
    [InlineData("""[{"word":"x"}]""")]
    [InlineData("<html>Bad gateway</html>")]
    public async Task Unexpected_body_throws_lookup_exception(string body)
    {
        _handler.Reply(HttpStatusCode.OK, body);

        await Assert.ThrowsAsync<DictionaryLookupException>(() => Client.LookupAsync("x", cancellationToken: Ct));
    }

    [Fact]
    public async Task Transient_status_is_retried_then_succeeds()
    {
        _handler.Reply((HttpStatusCode)530, "error code: 1033", "text/plain")
            .Reply(HttpStatusCode.OK, """{"word":"hello","entries":[{"partOfSpeech":"noun","senses":[{"definition":"A greeting."}]}]}""");

        var result = await Client.LookupAsync("hello", cancellationToken: Ct);

        Assert.Equal(DictionaryLookupStatus.Found, result.Status);
        Assert.Equal(2, _handler.Requests.Count);
    }

    [Fact]
    public async Task Network_errors_throw_lookup_exception()
    {
        _handler.Throw(new HttpRequestException("DNS failure"));

        await Assert.ThrowsAsync<DictionaryLookupException>(() => Client.LookupAsync("hello", cancellationToken: Ct));
    }
}
