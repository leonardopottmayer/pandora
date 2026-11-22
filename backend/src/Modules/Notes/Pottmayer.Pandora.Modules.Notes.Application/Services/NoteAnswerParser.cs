using System.Text.Json;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Notes.Application.Services;

/// <summary>Accepts only structured answers citing page ids actually supplied in this request.</summary>
public static class NoteAnswerParser
{
    public static Result<NoteAnswerDto> Parse(string text, IReadOnlyList<NoteSourceDto> sources)
    {
        var json = text.Trim();
        if (json.StartsWith("```json", StringComparison.Ordinal) && json.EndsWith("```", StringComparison.Ordinal))
            json = json[7..^3].Trim();
        else if (json.StartsWith("```", StringComparison.Ordinal) && json.EndsWith("```", StringComparison.Ordinal) && json.Length >= 6)
            json = json[3..^3].Trim();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("answer", out var answer) ||
                !root.TryGetProperty("pageIds", out var ids) || ids.ValueKind != JsonValueKind.Array || ids.GetArrayLength() > sources.Count)
                return Result<NoteAnswerDto>.Failure(NoteAiErrors.InvalidAnswer);
            if (answer.ValueKind == JsonValueKind.Null && ids.GetArrayLength() == 0)
                return Result<NoteAnswerDto>.Success(new NoteAnswerDto(null, []));
            if (answer.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(answer.GetString()) || ids.GetArrayLength() == 0)
                return Result<NoteAnswerDto>.Failure(NoteAiErrors.InvalidAnswer);

            var cited = new HashSet<Guid>();
            foreach (var id in ids.EnumerateArray())
            {
                if (id.ValueKind != JsonValueKind.String || !Guid.TryParse(id.GetString(), out var pageId) ||
                    !sources.Any(source => source.PageId == pageId))
                    return Result<NoteAnswerDto>.Failure(NoteAiErrors.InvalidAnswer);
                cited.Add(pageId);
            }
            return Result<NoteAnswerDto>.Success(new NoteAnswerDto(answer.GetString()!.Trim(),
                sources.Where(source => cited.Contains(source.PageId)).ToList()));
        }
        catch (JsonException)
        {
            return Result<NoteAnswerDto>.Failure(NoteAiErrors.InvalidAnswer);
        }
    }
}
