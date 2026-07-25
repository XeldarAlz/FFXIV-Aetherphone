using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet;

internal sealed class AethernetApi
{
    public AethernetApi(HttpService http, AethernetSession session, string appScope = "")
    {
        var net = new AethernetTransport(http, session, appScope);
        Auth = new AuthClient(net);
        Account = new AccountClient(net);
        Keys = new KeysClient(net);
        Contacts = new ContactsClient(net);
        Chats = new ChatClient(net);
        Social = new SocialClient(net);
        Grams = new GramClient(net);
        GramDm = new GramDmClient(net);
        Velvet = new VelvetClient(net);
        Media = new MediaClient(net);
        Safety = new SafetyClient(net);
        Feedback = new FeedbackClient(net);
        Dev = new DevClient(net);
        Polls = new PollsClient(net);
        Musters = new MusterClient(net);
        Ads = new YellowPagesClient(net);
    }

    public AuthClient Auth { get; }
    public AccountClient Account { get; }
    public KeysClient Keys { get; }
    public ContactsClient Contacts { get; }
    public ChatClient Chats { get; }
    public SocialClient Social { get; }
    public GramClient Grams { get; }
    public GramDmClient GramDm { get; }
    public VelvetClient Velvet { get; }
    public MediaClient Media { get; }
    public SafetyClient Safety { get; }
    public FeedbackClient Feedback { get; }
    public DevClient Dev { get; }
    public PollsClient Polls { get; }
    public MusterClient Musters { get; }
    public YellowPagesClient Ads { get; }
}
