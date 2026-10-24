using Bookside.Core.Common;
using Bookside.Core.Vocabulary;

namespace Bookside.Data.Tests.Services;

/// <summary>Two providers: <see cref="ServiceTestBase.Dictionary"/> ("fake") first, then <see cref="Second"/>.</summary>
public sealed class DefinitionFallbackTests() : ServiceTestBase("second")
{
    private FakeDictionaryLookupService Second => Fallbacks[0];

    [Fact]
    public async Task First_provider_that_finds_the_word_wins_and_later_ones_are_not_called()
    {
        Dictionary.Define("ennui", "Listlessness.");
        Second.Define("ennui", "Something else.");

        var definition = Success(await Definitions.GetDefinitionAsync("ennui", cancellationToken: Ct));

        Assert.Equal("fake", definition.Source);
        Assert.Equal("Listlessness.", definition.Meanings[0].Definitions[0].Text);
        Assert.Empty(Second.Calls);
    }

    [Fact]
    public async Task Not_found_moves_on_to_the_next_provider()
    {
        Second.Define("skibidi", "A new word.");

        var definition = Success(await Definitions.GetDefinitionAsync("skibidi", cancellationToken: Ct));

        Assert.Equal(DictionaryLookupStatus.Found, definition.Status);
        Assert.Equal("second", definition.Source);
        Assert.Equal(["skibidi"], Dictionary.Calls);
    }

    [Fact]
    public async Task Failure_moves_on_to_the_next_provider()
    {
        Dictionary.Unavailable = true;
        Second.Define("ennui", "Listlessness.");

        var definition = Success(await Definitions.GetDefinitionAsync("ennui", cancellationToken: Ct));

        Assert.Equal(("second", DictionaryLookupStatus.Found), (definition.Source, definition.Status));
    }

    [Fact]
    public async Task Not_found_everywhere_is_cached_with_the_first_providers_source()
    {
        var definition = Success(await Definitions.GetDefinitionAsync("qwzx", cancellationToken: Ct));
        Success(await Definitions.GetDefinitionAsync("qwzx", cancellationToken: Ct));

        Assert.Equal(("fake", DictionaryLookupStatus.NotFound), (definition.Source, definition.Status));
        Assert.Single(Dictionary.Calls);
        Assert.Single(Second.Calls);
    }

    [Fact]
    public async Task Not_found_plus_a_failure_is_unavailable_and_not_cached()
    {
        Second.Unavailable = true;

        Failure(await Definitions.GetDefinitionAsync("skibidi", cancellationToken: Ct), ErrorType.Unavailable);

        // The provider that was down gets another chance on the next lookup.
        Second.Unavailable = false;
        Second.Define("skibidi", "A new word.");
        var definition = Success(await Definitions.GetDefinitionAsync("skibidi", cancellationToken: Ct));
        Assert.Equal("second", definition.Source);
    }

    [Fact]
    public async Task Every_provider_failing_is_unavailable_with_each_reason()
    {
        Dictionary.Unavailable = true;
        Second.Unavailable = true;

        var error = Failure(await Definitions.GetDefinitionAsync("ennui", cancellationToken: Ct), ErrorType.Unavailable);

        Assert.Equal(ErrorCodes.DictionaryUnavailable, error.Code);
        Assert.Contains("Simulated fake outage.", error.Message, StringComparison.Ordinal);
        Assert.Contains("Simulated second outage.", error.Message, StringComparison.Ordinal);
        Assert.Single(Dictionary.Calls);
        Assert.Single(Second.Calls);
    }

    [Fact]
    public async Task Refresh_reruns_the_whole_chain_from_the_first_provider()
    {
        Second.Define("ennui", "From the second.");
        var cached = Success(await Definitions.GetDefinitionAsync("ennui", cancellationToken: Ct));
        Assert.Equal("second", cached.Source);

        Dictionary.Define("ennui", "From the first.");
        var refreshed = Success(await Definitions.GetDefinitionAsync("ennui", refresh: true, cancellationToken: Ct));

        Assert.Equal(cached.Id, refreshed.Id);
        Assert.Equal("fake", refreshed.Source);
        Assert.Equal("From the first.", refreshed.Meanings[0].Definitions[0].Text);
    }

    [Fact]
    public async Task Refresh_with_a_provider_down_keeps_the_cached_entry()
    {
        Second.Define("ennui", "From the second.");
        Success(await Definitions.GetDefinitionAsync("ennui", cancellationToken: Ct));

        Second.Unavailable = true;
        Failure(await Definitions.GetDefinitionAsync("ennui", refresh: true, cancellationToken: Ct), ErrorType.Unavailable);

        Second.Unavailable = false;
        var cached = Success(await Definitions.GetDefinitionAsync("ennui", cancellationToken: Ct));
        Assert.Equal(("second", DictionaryLookupStatus.Found), (cached.Source, cached.Status));
    }
}
