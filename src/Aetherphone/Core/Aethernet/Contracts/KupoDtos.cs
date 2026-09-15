namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record ConfessionDto(
    string Id,
    string Text,
    long CreatedAtUnix,
    long ExpiresAtUnix,
    int ResponseCount,
    int KudosCount,
    bool Mine,
    bool GaveKudos,
    ConfessionResponseDto[] Responses);

internal sealed record ConfessionResponseDto(
    string Id,
    string ConfessionId,
    string Text,
    long CreatedAtUnix,
    int LikeCount,
    bool Liked,
    bool Mine);

internal sealed record CreateConfessionRequest(string Text, long ExpiresAtUnix);

internal sealed record CreateConfessionResponseRequest(string Text);

internal sealed record ConfessionPage(ConfessionDto[] Items, string? NextCursor);

internal sealed record ConfessionResponsePage(ConfessionResponseDto[] Items, string? NextCursor);

internal sealed record KupoStatsDto(int WrittenCount, int ResponseCount, int KudosCount);
