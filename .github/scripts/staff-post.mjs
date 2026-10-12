const DiscordApiBaseUrl = process.env.DISCORD_API_BASE_URL ?? "https://discord.com/api/v10";
const UserAgent = "Aetherphone staff post (https://github.com/XeldarAlz/FFXIV-Aetherphone)";
const RequestTimeoutMilliseconds = 20000;
const RateLimitAttempts = 6;
const EmbedChunkLimit = 3900;
const TitleLimit = 256;
const EmbedColor = 0x8d54e3;
const SnowflakePattern = /\d{15,}/g;
const MentionTokenPattern = /<@&?\d+>/g;

// Workflow logs of a public repository are public, so ids never reach stdout.
function redact(text) {
    return text.replace(MentionTokenPattern, "@...").replace(SnowflakePattern, "<id>");
}

function sleep(milliseconds) {
    return new Promise(resolve => setTimeout(resolve, milliseconds));
}

async function discordRequest(token, method, path, body) {
    for (let attempt = 0; attempt < RateLimitAttempts; attempt++) {
        const response = await fetch(`${DiscordApiBaseUrl}${path}`, {
            method,
            headers: {
                Authorization: `Bot ${token}`,
                "Content-Type": "application/json",
                "User-Agent": UserAgent,
            },
            body: body === undefined ? undefined : JSON.stringify(body),
            signal: AbortSignal.timeout(RequestTimeoutMilliseconds),
        });
        if (response.status === 429) {
            const payload = await response.json().catch(() => ({}));
            const retryAfterSeconds = Number(payload.retry_after ?? response.headers.get("retry-after") ?? 1);
            await sleep(retryAfterSeconds * 1000 + 250);
            continue;
        }
        const text = await response.text();
        if (!response.ok) {
            throw new Error(`Discord ${method} ${path} failed with ${response.status}: ${text.slice(0, 300)}`);
        }
        return text.length === 0 ? null : JSON.parse(text);
    }
    throw new Error(`Discord ${method} ${path} stayed rate limited after ${RateLimitAttempts} attempts.`);
}

function splitBody(body, limit) {
    const chunks = [];
    let current = "";
    for (const rawLine of body.replace(/\r/g, "").split("\n")) {
        const line = rawLine.length > limit ? rawLine.slice(0, limit) : rawLine;
        const candidate = current.length === 0 ? line : `${current}\n${line}`;
        if (candidate.length > limit) {
            chunks.push(current);
            current = line;
            continue;
        }
        current = candidate;
    }
    if (current.trim().length > 0) {
        chunks.push(current);
    }
    return chunks;
}

async function main() {
    const token = process.env.DISCORD_BOT_TOKEN ?? "";
    const channelId = process.env.DISCORD_STAFF_POST_CHANNEL_ID ?? "";
    const title = (process.env.POST_TITLE ?? "").trim().slice(0, TitleLimit);
    const body = (process.env.POST_BODY ?? "").replace(/\\n/g, "\n");
    if (token.length === 0 || channelId.length === 0) {
        console.log("::error::DISCORD_BOT_TOKEN or DISCORD_STAFF_POST_CHANNEL_ID is not set.");
        process.exitCode = 1;
        return;
    }
    const chunks = splitBody(body, EmbedChunkLimit);
    if (chunks.length === 0) {
        console.log("::error::The message is empty.");
        process.exitCode = 1;
        return;
    }
    try {
        for (let index = 0; index < chunks.length; index++) {
            const embed = { description: chunks[index], color: EmbedColor };
            if (index === 0 && title.length > 0) {
                embed.title = title;
            }
            if (chunks.length > 1) {
                embed.footer = { text: `${index + 1} of ${chunks.length}` };
            }
            await discordRequest(token, "POST", `/channels/${channelId}/messages`, {
                embeds: [embed],
                allowed_mentions: { parse: [] },
            });
        }
        console.log(`Posted ${chunks.length} message(s).`);
    } catch (error) {
        console.log(`::error::${redact(error.message)}`);
        process.exitCode = 1;
    }
}

await main();
