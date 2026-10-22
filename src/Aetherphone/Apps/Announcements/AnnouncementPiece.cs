namespace Aetherphone.Apps.Announcements;

internal enum AnnouncementPieceKind : byte
{
    Line,
    Image,
    Link,
}

internal enum AnnouncementGap : byte
{
    None,
    Paragraph,
    Block,
}

internal readonly record struct AnnouncementPiece(
    AnnouncementPieceKind Kind,
    AnnouncementGap Gap,
    string Text,
    string Url,
    float Aspect,
    int ImageIndex,
    string Key);
