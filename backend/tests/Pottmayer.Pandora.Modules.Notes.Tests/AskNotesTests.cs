using System.Text.Json;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Application.Services;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Notes.Tests;

public sealed class AskNotesTests
{
    private static readonly NoteSourceDto Source = new(Guid.NewGuid(), Guid.NewGuid(), "Apollo", null, "Launch in March");

    [Fact]
    public void A_question_uses_safe_keyword_alternatives_without_changing_palette_search()
    {
        Assert.Equal("prazo:* | projeto:* | apollo:*", NoteQuestionSearch.ToTsQuery("Qual é o prazo do projeto Apollo?"));
        Assert.Equal("apollo:* | deadline:*", NoteQuestionSearch.ToTsQuery("What is my Apollo deadline?"));
        Assert.Equal("apollo:* | drop:* | table:*", NoteQuestionSearch.ToTsQuery("Apollo'); DROP TABLE --"));
        Assert.Equal("qual:* & prazo:*", PageSearch.ToTsQuery("qual prazo"));
        Assert.Equal("", NoteQuestionSearch.ToTsQuery("What are my notes?"));
    }

    [Fact]
    public void Excerpts_include_matches_beyond_the_opening_and_are_bounded()
    {
        var passage = NoteQuestionSearch.Excerpt(new string('x', 9000) + " Apollo launch in March. " + new string('z', 9000), "When is the Apollo launch?");
        Assert.Contains("Apollo launch in March", passage);
        Assert.InRange(passage.Length, 1, NoteQuestionSearch.ExcerptLength + 2);
        Assert.Equal("Short page", NoteQuestionSearch.Excerpt("Short page", "Apollo"));
    }

    [Fact]
    public void An_answer_returns_only_its_cited_consulted_pages()
    {
        var other = Source with { PageId = Guid.NewGuid(), Title = "Other" };
        var json = JsonSerializer.Serialize(new { answer = " Launch in March. ", pageIds = new[] { Source.PageId } });
        var result = NoteAnswerParser.Parse("```json\n" + json + "\n```", [Source, other]);
        Assert.True(result.IsSuccess);
        Assert.Equal("Launch in March.", result.Value!.Answer);
        Assert.Equal([Source], result.Value.Sources);
    }

    [Theory]
    [InlineData("not JSON")]
    [InlineData("[]")]
    [InlineData("{\"answer\":\"Unsupported assertion\",\"pageIds\":[]}")]
    [InlineData("{\"answer\":\"Text\",\"pageIds\":[\"not-an-id\"]}")]
    [InlineData("{\"answer\":null,\"pageIds\":[\"not-an-id\"]}")]
    [InlineData("{\"answer\":5,\"pageIds\":[]}")]
    public void Malformed_or_uncited_answers_are_never_displayed(string json)
        => Assert.Equal("Notes.InvalidAnswer", Assert.Single(NoteAnswerParser.Parse(json, [Source]).Errors).Code);

    [Fact]
    public void Invented_page_ids_are_rejected_even_when_another_source_is_valid()
    {
        var json = JsonSerializer.Serialize(new { answer = "Text", pageIds = new[] { Guid.NewGuid() } });
        Assert.True(NoteAnswerParser.Parse(json, [Source]).IsFailure);
    }

    [Fact]
    public void Insufficient_evidence_has_no_answer_or_sources()
    {
        var result = NoteAnswerParser.Parse("{\"answer\":null,\"pageIds\":[]}", [Source]);
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.Answer);
        Assert.Empty(result.Value.Sources);
    }
}
