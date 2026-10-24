using System.Diagnostics;
using System.Net;
using Bookside.Core.Vocabulary;
using Bookside.Core.Vocabulary.Lookup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace Bookside.Integrations.Tests;

/// <summary>Goes through the production registration (typed client + resilience) with a stubbed primary handler.</summary>
public sealed class DictionaryApiClientTests : IDisposable
{
    private const string NotFoundBody =
        """{"title":"No Definitions Found","message":"Sorry pal, we couldn't find definitions for the word you were looking for.","resolution":"You can try the search again at later time or head to the web instead."}""";

    private readonly StubHttpMessageHandler _handler = new();
    private readonly ServiceProvider _services;

    public DictionaryApiClientTests()
    {
        var services = new ServiceCollection();
        var builder = services.AddDictionaryApiClient(o => o.BaseAddress = new Uri("https://dictionary.test/api/v2/"))
            .ConfigurePrimaryHttpMessageHandler(() => _handler);

        // Keep retry tests fast; the production timeouts are kept.
        services.Configure<HttpStandardResilienceOptions>($"{builder.Name}-standard", o => o.Retry.Delay = TimeSpan.Zero);
        _services = services.BuildServiceProvider();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IDictionaryLookupService Client => _services.GetRequiredService<IDictionaryLookupService>();

    public void Dispose() => _services.Dispose();

    [Fact]
    public async Task Found_word_maps_phonetic_audio_and_meanings()
    {
        const string body = """
            [{"word":"serendipity","phonetic":"/ˌsɛ.ɹən.ˈdɪ.pɪ.ti/",
              "phonetics":[{"text":"/ˌsɛ.ɹən.ˈdɪ.pɪ.ti/","audio":"https://api.dictionaryapi.dev/media/pronunciations/en/serendipity-au.mp3"}],
              "meanings":[{"partOfSpeech":"noun","definitions":[
                {"definition":"A happy accident.","example":"It was pure serendipity.","synonyms":[]},
                {"definition":"A fortunate discovery.","synonyms":["luck"]}]}]}]
            """;
        _handler.Reply(HttpStatusCode.OK, body);

        var result = await Client.LookupAsync("serendipity", cancellationToken: Ct);

        Assert.Equal(DictionaryLookupStatus.Found, result.Status);
        Assert.Equal("/ˌsɛ.ɹən.ˈdɪ.pɪ.ti/", result.Phonetic);
        Assert.Equal("https://api.dictionaryapi.dev/media/pronunciations/en/serendipity-au.mp3", result.AudioUrl);
        Assert.Equal(body, result.RawResponse);
        var meaning = Assert.Single(result.Meanings);
        Assert.Equal("noun", meaning.PartOfSpeech);
        Assert.Equal(
            [("A happy accident.", "It was pure serendipity."), ("A fortunate discovery.", null)],
            meaning.Definitions.Select(d => (d.Text, d.Example)));
        Assert.Equal(new Uri("https://dictionary.test/api/v2/entries/en/serendipity"), Assert.Single(_handler.Requests));
    }

    [Fact]
    public async Task Multiple_entries_are_merged_by_part_of_speech_without_duplicates()
    {
        _handler.Reply(HttpStatusCode.OK, """
            [{"word":"bank","phonetics":[{"text":"","audio":""}],"meanings":[
                {"partOfSpeech":"noun","definitions":[{"definition":"A financial institution."}]},
                {"partOfSpeech":"verb","definitions":[{"definition":"To deposit money."}]}]},
             {"word":"bank","phonetics":[{"text":"/bæŋk/","audio":"//ssl.gstatic.com/dictionary/bank.mp3"}],"meanings":[
                {"partOfSpeech":"Noun","definitions":[{"definition":"The edge of a river."},{"definition":"A financial institution."}]},
                {"partOfSpeech":"adjective","definitions":[{"definition":"  "}]}]}]
            """);

        var result = await Client.LookupAsync("bank", cancellationToken: Ct);

        Assert.Equal("/bæŋk/", result.Phonetic);
        Assert.Equal("https://ssl.gstatic.com/dictionary/bank.mp3", result.AudioUrl);
        Assert.Collection(
            result.Meanings,
            m => Assert.Equal(["A financial institution.", "The edge of a river."], m.Definitions.Select(d => d.Text)),
            m => Assert.Equal(("verb", 1), (m.PartOfSpeech, m.Definitions.Count)));
    }

    [Fact]
    public async Task Term_and_language_are_escaped_in_the_path()
    {
        _handler.Reply(HttpStatusCode.NotFound, NotFoundBody);

        await Client.LookupAsync("ice cream/cone?", "en", Ct);

        Assert.Equal("/api/v2/entries/en/ice%20cream%2Fcone%3F", Assert.Single(_handler.Requests).AbsolutePath);
    }

    [Fact]
    public async Task Api_404_is_a_cacheable_not_found_and_is_not_retried()
    {
        _handler.Reply(HttpStatusCode.NotFound, NotFoundBody);

        var result = await Client.LookupAsync("qwzxhjk", cancellationToken: Ct);

        Assert.Equal(DictionaryLookupStatus.NotFound, result.Status);
        Assert.Empty(result.Meanings);
        Assert.Equal(NotFoundBody, result.RawResponse);
        Assert.Single(_handler.Requests);
    }

    [Fact]
    public async Task A_404_without_the_api_json_body_is_a_failure_not_a_not_found()
    {
        _handler.Reply(HttpStatusCode.NotFound, "<html>Not Found</html>", "text/html");

        await Assert.ThrowsAsync<DictionaryLookupException>(() => Client.LookupAsync("word", cancellationToken: Ct));
    }

    [Fact]
    public async Task Empty_array_or_no_definitions_is_not_found()
    {
        _handler.Reply(HttpStatusCode.OK, """[{"word":"x","meanings":[{"partOfSpeech":"noun","definitions":[]}]}]""");

        var result = await Client.LookupAsync("x", cancellationToken: Ct);

        Assert.Equal(DictionaryLookupStatus.NotFound, result.Status);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData((HttpStatusCode)530)] // Cloudflare "origin unreachable", seen from this API.
    public async Task Transient_status_is_retried_then_succeeds(HttpStatusCode status)
    {
        _handler.Reply(status, "error code: 1033", "text/plain")
            .Reply(HttpStatusCode.OK, """[{"word":"hello","meanings":[{"partOfSpeech":"noun","definitions":[{"definition":"A greeting."}]}]}]""");

        var result = await Client.LookupAsync("hello", cancellationToken: Ct);

        Assert.Equal(DictionaryLookupStatus.Found, result.Status);
        Assert.Equal(2, _handler.Requests.Count);
    }

    [Fact]
    public async Task Persistent_server_errors_throw_after_two_retries()
    {
        _handler.Reply(HttpStatusCode.InternalServerError, "boom", "text/plain");

        var ex = await Assert.ThrowsAsync<DictionaryLookupException>(() => Client.LookupAsync("hello", cancellationToken: Ct));

        Assert.Contains("500", ex.Message, StringComparison.Ordinal);
        Assert.Equal(3, _handler.Requests.Count);
    }

    [Fact]
    public async Task Network_errors_throw_lookup_exception()
    {
        _handler.Throw(new HttpRequestException("DNS failure"));

        await Assert.ThrowsAsync<DictionaryLookupException>(() => Client.LookupAsync("hello", cancellationToken: Ct));
    }

    [Fact]
    public async Task Malformed_json_throws_lookup_exception()
    {
        _handler.Reply(HttpStatusCode.OK, """{"unexpected":true}""");

        await Assert.ThrowsAsync<DictionaryLookupException>(() => Client.LookupAsync("hello", cancellationToken: Ct));
    }

    [Fact]
    public async Task Caller_cancellation_is_not_wrapped()
    {
        _handler.Hang();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client.LookupAsync("hello", cancellationToken: cts.Token));

        Assert.IsNotType<DictionaryLookupException>(ex);
    }

    [Fact]
    public async Task Hanging_api_times_out_within_the_total_budget()
    {
        _handler.Hang();
        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAsync<DictionaryLookupException>(() => Client.LookupAsync("hello", cancellationToken: Ct));

        Assert.InRange(stopwatch.Elapsed, TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(10));
    }
}
