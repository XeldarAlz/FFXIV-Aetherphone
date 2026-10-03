namespace Aetherphone.Core.Radio;

internal interface ILiveChatFeed
{
    int Version { get; }

    int Count { get; }

    RadioChatEntry At(int index);
}
