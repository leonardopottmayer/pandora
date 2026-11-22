using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Notes.Tests;

public sealed class PageContentTests
{
    [Fact]
    public void Properties_read_rich_text_chips_and_nested_blocks_without_mutating_the_document()
    {
        var content = PageContent.Parse("""
            {"version":1,"blocks":[{"id":"p","type":"properties","props":{"rows":[
              {"key":[{"text":" Status ","marks":[{"type":"bold"}]}],"value":[{"status":{"label":"In progress"}}]},
              {"key":[{"text":"Owner"}],"value":[{"item":{"label":"Ana"}},{"text":" / "},{"text":"Bob"}]},
              {"key":[{"text":"Due"}],"value":[{"date":{"value":"2026-11-15"}}]},
              {"key":[{"text":"Formula"}],"value":[{"math":{"latex":"x+y"}}]},
              {"key":[{"text":"Empty"}],"value":[]},
              {"key":[{"text":"  "}],"value":[{"text":"Unnamed"}]}
            ]}},{"id":"c","type":"callout","children":[{"id":"nested","type":"properties","props":{"rows":[
              {"key":[{"text":"Status"}],"value":[{"text":"Review"}]}
            ]}}]}]}
            """);
        var original = content.Json;
        Assert.Equal(new[] {
            new PageProperty("Status", "In progress"), new PageProperty("Owner", "Ana / Bob"),
            new PageProperty("Due", "2026-11-15"), new PageProperty("Formula", "x+y"),
            new PageProperty("Empty", ""), new PageProperty("Status", "Review")
        }, content.Properties());
        Assert.Equal(original, content.Json);
    }

    [Fact]
    public void Properties_ignore_other_block_types_and_unusable_rows()
    {
        var content = PageContent.Parse("""
            {"version":1,"blocks":[{"id":"other","type":"table","props":{"rows":[
              {"key":[{"text":"Not a property"}],"value":[{"text":"No"}]}]}},
              {"id":"p","type":"properties","props":{"rows":[null,42,{},
              {"key":"Not inlines","value":[]},{"key":[{"text":"Valid"}]}]}}]}
            """);
        Assert.Equal([new PageProperty("Valid", "")], content.Properties());
    }

    [Fact]
    public void Templates_expand_visible_text_with_independent_nested_ids_and_no_comment_anchors()
    {
        var original = PageContent.Parse("""
            {"version":1,"blocks":[{"id":"parent","type":"callout","props":{"title":"{{title}}","url":"https://example.com/{{date}}"},
              "content":[{"text":"{{date}}: {{title}}","marks":[{"type":"bold"},{"type":"comment","attrs":{"thread":"old-thread"}}]}],
              "children":[{"id":"child","type":"table","props":{"rows":[[[{"text":"{{title}}"}]]]}}]}]}
            """);
        var copy = original.InstantiateTemplate("Review", new DateOnly(2026, 11, 7));
        var another = original.InstantiateTemplate("Other", new DateOnly(2026, 11, 8));

        Assert.Equal(2, copy.BlockFingerprints().Count);
        Assert.Empty(copy.BlockFingerprints().Keys.Intersect(original.BlockFingerprints().Keys));
        Assert.Empty(copy.BlockFingerprints().Keys.Intersect(another.BlockFingerprints().Keys));
        Assert.Contains("2026-11-07: Review", copy.Text);
        Assert.DoesNotContain("{{title}}", copy.Json);
        Assert.Contains("https://example.com/{{date}}", copy.Json);
        Assert.DoesNotContain("old-thread", copy.Json);
        Assert.Contains("bold", copy.Json);
        Assert.Contains("{{title}}", original.Json);
        Assert.Contains("old-thread", original.Json);
        Assert.True(PageContent.TryParse(copy.Json, out _, out _));
    }

    [Fact]
    public void AI_output_is_searched_and_read_as_text_without_searching_its_prompt_or_time()
    {
        var content = PageContent.Parse("""
            { "version": 1, "blocks": [ { "id": "ai1", "type": "ai", "props": {
              "prompt": "private instruction", "generatedAt": "2026-11-07T18:00:00Z",
              "output": [{ "text": "Apollo checklist\nReview tests" }]
            } } ] }
            """);
        Assert.Equal("Apollo checklist\nReview tests", content.Text);
        Assert.Equal("Apollo checklist\nReview tests", content.ToReadableText());
    }

    private static readonly Guid Linked = Guid.Parse("0198f6a2-0000-7000-8000-000000000001");
    private static readonly Guid Card = Guid.Parse("0198f6a2-0000-7000-8000-000000000002");
    private static readonly Guid Image = Guid.Parse("0198f6a2-0000-7000-8000-000000000003");

    private static readonly string Document = $$"""
        { "version": 1, "blocks": [
          { "id": "t", "type": "tags", "props": { "tags": ["DotNet", "dotnet", "Mensageria", "#123"] } },
          { "id": "h", "type": "heading", "props": { "level": 1 }, "content": [{ "text": "Outbox" }] },
          { "id": "p", "type": "paragraph", "content": [
            { "text": "Ver " }, { "mention": { "page": "{{Linked}}" } }, { "text": " hoje", "marks": [{ "type": "bold" }] } ] },
          { "id": "l", "type": "bulleted", "content": [{ "text": "Pai" }], "children": [
            { "id": "c", "type": "bulleted", "content": [{ "text": "Filho" }] } ] },
          { "id": "i", "type": "image", "props": { "mediaId": "{{Image}}", "caption": [{ "text": "Fluxo" }] } },
          { "id": "k", "type": "page-link", "props": { "pageId": "{{Card}}" } },
          { "id": "x", "type": "table", "props": { "rows": [[[{ "text": "Célula" }]]] } }
        ] }
        """;

    [Fact]
    public void Derives_text_links_tags_and_media_from_the_blocks()
    {
        Assert.True(PageContent.TryParse(Document, out var content, out var problem), problem);

        Assert.Equal("Outbox\nVer  hoje\nPai\nFilho\nFluxo\nCélula", content.Text);
        Assert.Equal([Linked], content.MentionedPageIds);
        Assert.Equal([Card], content.LinkedPageIds);
        Assert.Equal([Image], content.MediaIds);
        Assert.Equal(["dotnet", "mensageria"], content.Tags.Select(t => t.Slug));
        Assert.Equal("DotNet", content.Tags[0].Name);
    }

    [Fact]
    public void A_synced_copy_links_its_page_and_page_properties_are_searched()
    {
        var json = $$"""
            { "version": 1, "blocks": [
              { "id": "s", "type": "synced-ref", "props": { "pageId": "{{Card}}", "blockId": "b1" } },
              { "id": "p", "type": "properties", "props": { "rows": [{ "key": [{ "text": "Dono" }], "value": [{ "text": "Ana" }] }] } }
            ] }
            """;
        Assert.True(PageContent.TryParse(json, out var content, out var problem), problem);

        Assert.Equal([Card], content.LinkedPageIds);
        Assert.Equal("Dono\nAna", content.Text);
    }

    [Fact]
    public void Chips_read_as_their_words()
    {
        const string json = """
            { "version": 1, "blocks": [ { "id": "p", "type": "paragraph", "content": [
              { "text": "Entrega " }, { "date": { "value": "2026-12-01" } }, { "text": " " },
              { "status": { "label": "Bloqueado", "color": "red" } }, { "text": " " },
              { "item": { "module": "agenda", "kind": "task", "id": "x", "label": "Revisar contrato" } }, { "text": " " },
              { "math": { "latex": "x^2" } } ] } ] }
            """;
        Assert.True(PageContent.TryParse(json, out var content, out var problem), problem);
        Assert.Equal("Entrega 2026-12-01 Bloqueado Revisar contrato x^2", content.Text);
    }

    [Fact]
    public void Chips_in_a_property_value_or_a_table_cell_are_searched()
    {
        const string json = """
            { "version": 1, "blocks": [
              { "id": "p", "type": "properties", "props": { "rows": [
                { "key": [{ "text": "Quando" }], "value": [{ "date": { "value": "2026-11-28" } }, { "text": " a " }, { "date": { "value": "2026-11-30" } }] },
                { "key": [{ "text": "Status" }], "value": [{ "status": { "label": "Planejando", "color": "blue" } }] } ] } },
              { "id": "t", "type": "table", "props": { "rows": [[[{ "item": { "module": "finances", "kind": "card", "id": "c", "label": "Nubank" } }]]] } }
            ] }
            """;
        Assert.True(PageContent.TryParse(json, out var content, out var problem), problem);
        Assert.Equal("Quando\n2026-11-28 a 2026-11-30\nStatus\nPlanejando\nNubank", content.Text);
    }

    [Theory]
    [InlineData("""{ "blocks": "nope" }""", "blocks list")]
    [InlineData("""{ "blocks": [{ "type": "paragraph" }] }""", "needs an id")]
    [InlineData("""{ "blocks": [{ "id": "a", "type": "Bad Type" }] }""", "no valid type")]
    [InlineData("""{ "blocks": [{ "id": "a", "type": "p" }, { "id": "a", "type": "p" }] }""", "appears twice")]
    [InlineData("""{ "blocks": [{ "id": "a", "type": "p", "content": "text" }] }""", "list of objects")]
    [InlineData("not json", "not valid JSON")]
    public void Refuses_documents_with_the_wrong_shape(string json, string reason)
    {
        Assert.False(PageContent.TryParse(json, out _, out var problem));
        Assert.Contains(reason, problem);
    }

    [Fact]
    public void Empty_or_missing_content_is_an_empty_document()
    {
        Assert.True(PageContent.TryParse(null, out var content, out _));
        Assert.True(content.IsEmpty);
        Assert.Equal("""{"version":1,"blocks":[]}""", content.Json);
    }

    [Fact]
    public void Text_becomes_blocks_and_reads_back_the_same()
    {
        var content = PageContent.FromText("# Lista\n- leite\n- [x] pão\n1. um\n\n> citação\nfim");

        Assert.Equal("# Lista\n- leite\n- [x] pão\n1. um\n> citação\nfim", content.ToReadableText());
    }

    [Fact]
    public void Appending_keeps_both_documents_in_order()
    {
        var content = PageContent.FromText("um").Append(PageContent.FromText("dois"));

        Assert.Equal("um\ndois", content.Text);
    }
}
