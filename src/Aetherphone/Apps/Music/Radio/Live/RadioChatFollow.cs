namespace Aetherphone.Apps.Music.Radio.Live;

internal sealed class RadioChatFollow
{
    private const float ResizeEpsilon = 0.5f;

    private long lastTailId = -1;
    private float lastViewHeight;
    private bool jumpPending = true;

    public bool Pinned { get; private set; } = true;

    public int Unseen { get; private set; }

    public long SeenTailId { get; private set; } = -1;

    public void RequestJump()
    {
        jumpPending = true;
    }

    public void Reset()
    {
        lastTailId = -1;
        lastViewHeight = 0f;
        jumpPending = true;
        Pinned = true;
        Unseen = 0;
        SeenTailId = -1;
    }

    public bool Update(float scrollY, float scrollMax, float tolerance, float viewHeight, long tailId,
        int newerThanSeen)
    {
        var jump = jumpPending;
        jumpPending = false;
        Pinned = jump || scrollMax <= 0f || scrollY >= scrollMax - tolerance;
        var tailMoved = tailId != lastTailId;
        lastTailId = tailId;
        var resized = MathF.Abs(viewHeight - lastViewHeight) > ResizeEpsilon;
        lastViewHeight = viewHeight;
        if (!Pinned)
        {
            Unseen = newerThanSeen;
            return false;
        }

        SeenTailId = tailId;
        Unseen = 0;
        return jump || tailMoved || resized;
    }
}
