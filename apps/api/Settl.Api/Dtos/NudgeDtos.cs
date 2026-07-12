namespace Settl.Api.Dtos;

public sealed record NudgeActionDto(string Label, string Kind, Guid TargetId);

public sealed record NudgeDto(
    string Kind,
    string Title,
    string Body,
    string When,
    IReadOnlyList<NudgeActionDto> Actions);
