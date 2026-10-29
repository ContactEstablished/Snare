using Snare.Core.Captures;
using Snare.Core.Search;

namespace Snare.Core.Tests;

public class SearchQueryParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_IsEverything(string? input)
    {
        Assert.Same(SearchQuery.All, SearchQueryParser.Parse(input));
    }

    [Fact]
    public void Words_BecomePrefixTermsAndedTogether()
    {
        var query = SearchQueryParser.Parse("invoice  err");

        Assert.Equal(["invoice", "err"], query.Terms);
        Assert.Equal("\"invoice\"* \"err\"*", query.ToFtsExpression());
    }

    [Fact]
    public void Filters_AreSeparatedFromTerms()
    {
        var query = SearchQueryParser.Parse("tag:bug app:chrome kind:video before:2026-10-01 after:2026-09-01 crash TAG:urgent");

        Assert.Equal(["crash"], query.Terms);
        Assert.Equal(["bug", "urgent"], query.Tags);
        Assert.Equal("chrome", query.App);
        Assert.Equal(CaptureKind.Video, query.Kind);
        Assert.Equal(new DateOnly(2026, 10, 1), query.Before);
        Assert.Equal(new DateOnly(2026, 9, 1), query.After);
    }

    [Fact]
    public void QuotedPhrases_StayTogether()
    {
        var query = SearchQueryParser.Parse("\"payment failed\" tag:\"needs review\"");

        Assert.Equal(["payment failed"], query.Terms);
        Assert.Equal(["needs review"], query.Tags);
    }

    [Theory]
    [InlineData("kind:banana")]
    [InlineData("before:yesterday")]
    [InlineData("url:example.com")]
    [InlineData("12:30")]
    public void UnknownOrInvalidFilters_ArePlainTerms(string input)
    {
        var query = SearchQueryParser.Parse(input);

        Assert.Equal([input], query.Terms);
    }

    [Fact]
    public void FtsExpression_EscapesQuotes()
    {
        var query = new SearchQuery { Terms = ["say \"hi\""] };

        Assert.Equal("\"say \"\"hi\"\"\"*", query.ToFtsExpression());
    }

    [Fact]
    public void FiltersOnly_HaveNoFtsExpression()
    {
        Assert.Null(SearchQueryParser.Parse("tag:bug").ToFtsExpression());
    }

    [Fact]
    public void TagPalette_WrapsAndCycles()
    {
        Assert.Equal(TagPalette.Colors[0], TagPalette.ForIndex(TagPalette.Colors.Count));
        Assert.Equal(TagPalette.Colors[1], TagPalette.Next(TagPalette.Colors[0]));
        Assert.Equal(TagPalette.Colors[0], TagPalette.Next("#123456"));
        Assert.Equal(TagPalette.Colors[0], TagPalette.Next(TagPalette.Colors[^1]));
    }
}
