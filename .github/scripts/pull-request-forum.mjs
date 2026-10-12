import { readFileSync } from "node:fs";

const DiscordApiBaseUrl = process.env.DISCORD_API_BASE_URL ?? "https://discord.com/api/v10";
const GitHubApiBaseUrl = process.env.GITHUB_API_URL ?? "https://api.github.com";
const UserAgent = "Aetherphone pull request forum (https://github.com/XeldarAlz/FFXIV-Aetherphone)";
const RequestTimeoutMilliseconds = 20000;
const RateLimitAttempts = 4;
const PageSize = 100;
const MaxPages = 20;

// Discord channel type 15 is GUILD_FORUM.
const ForumChannelType = 15;
const ThreadNameLimit = 100;
const EmbedTitleLimit = 256;
const StarterBodyLimit = 1200;
const CommentBodyLimit = 900;
const CommitLineLimit = 6;
const BotLogins = new Set(["dependabot[bot]", "github-actions[bot]"]);

const ArchivedThreadPageLimit = 100;
const ArchivedThreadPageCap = 30;
const SnowflakePattern = /\d{15,}/g;
const MentionTokenPattern = /<@&?\d+>/g;
const MentionPattern = /(?:^|[^\w`/@])@([A-Za-z0-9](?:[A-Za-z0-9-]{0,38}))/g;
const DashPattern = new RegExp(`[${String.fromCharCode(0x2013)}${String.fromCharCode(0x2014)}]`, "g");
const HtmlCommentPattern = /<!--[\s\S]*?-->/g;
const ExcessBlankLinesPattern = /\n{3,}/g;

const Status = Object.freeze({
    Draft: "draft",
    Open: "open",
    ChangesRequested: "changes_requested",
    Approved: "approved",
    Merged: "merged",
    Closed: "closed",
});

const StatusTags = Object.freeze({
    [Status.Draft]: { name: "Draft", emoji: "📝" },
    [Status.Open]: { name: "Open", emoji: "🟢" },
    [Status.ChangesRequested]: { name: "Changes requested", emoji: "🔁" },
    [Status.Approved]: { name: "Approved", emoji: "✅" },
    [Status.Merged]: { name: "Merged", emoji: "🟣" },
    [Status.Closed]: { name: "Closed", emoji: "⚫" },
});

const StatusColors = Object.freeze({
    [Status.Draft]: 0x6E7781,
    [Status.Open]: 0x2DA44E,
    [Status.ChangesRequested]: 0xCF222E,
    [Status.Approved]: 0x1A7F37,
    [Status.Merged]: 0x8250DF,
    [Status.Closed]: 0x6E7781,
});

const ReviewStateColors = Object.freeze({
    APPROVED: StatusColors[Status.Approved],
    CHANGES_REQUESTED: StatusColors[Status.ChangesRequested],
    COMMENTED: 0x0969DA,
});

const NoMentions = Object.freeze({ text: "", userIds: [], roleIds: [] });

function readConfig() {
    return {
        token: (process.env.DISCORD_BOT_TOKEN ?? "").trim(),
        forumChannelId: (process.env.DISCORD_PULL_REQUEST_FORUM_ID ?? "").trim(),
        maintainersRoleId: (process.env.DISCORD_MAINTAINERS_ROLE_ID ?? "").trim(),
        userMap: parseUserMap(process.env.DISCORD_GITHUB_USERS ?? ""),
        repository: process.env.GITHUB_REPOSITORY ?? "",
        githubToken: process.env.GITHUB_TOKEN ?? "",
    };
}

function parseUserMap(raw) {
    const trimmed = raw.trim();
    if (trimmed.length === 0) {
        return new Map();
    }
    let parsed;
    try {
        parsed = JSON.parse(trimmed);
    } catch (error) {
        warn(`DISCORD_GITHUB_USERS is not valid JSON (${error.message}); nobody will be pinged by name.`);
        return new Map();
    }
    const map = new Map();
    for (const [login, discordId] of Object.entries(parsed)) {
        if (typeof discordId !== "string" || !/^\d{15,22}$/.test(discordId)) {
            warn(`DISCORD_GITHUB_USERS entry for ${login} is not a Discord user id; skipping it.`);
            continue;
        }
        map.set(login.toLowerCase(), discordId);
    }
    return map;
}

// Workflow logs of a public repository are public, so nothing that identifies the staff server
// (snowflake ids, mention tokens) may reach stdout.
function redact(text) {
    return text.replace(MentionTokenPattern, "@...").replace(SnowflakePattern, "<id>");
}

function warn(message) {
    console.log(`::warning::${redact(message)}`);
}

function sleep(milliseconds) {
    return new Promise(resolve => setTimeout(resolve, milliseconds));
}

async function discordRequest(config, method, path, body) {
    for (let attempt = 0; attempt < RateLimitAttempts; attempt++) {
        const response = await fetch(`${DiscordApiBaseUrl}${path}`, {
            method,
            headers: {
                Authorization: `Bot ${config.token}`,
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
            throw new Error(`Discord ${method} ${path} failed with ${response.status}: ${text.slice(0, 400)}`);
        }
        return text.length === 0 ? null : JSON.parse(text);
    }
    throw new Error(`Discord ${method} ${path} stayed rate limited after ${RateLimitAttempts} attempts.`);
}

async function githubRequest(config, method, path, body) {
    const response = await fetch(`${GitHubApiBaseUrl}${path}`, {
        method,
        headers: {
            Authorization: `Bearer ${config.githubToken}`,
            Accept: "application/vnd.github+json",
            "X-GitHub-Api-Version": "2022-11-28",
            "Content-Type": "application/json",
            "User-Agent": UserAgent,
        },
        body: body === undefined ? undefined : JSON.stringify(body),
        signal: AbortSignal.timeout(RequestTimeoutMilliseconds),
    });
    const text = await response.text();
    if (!response.ok) {
        throw new Error(`GitHub ${method} ${path} failed with ${response.status}: ${text.slice(0, 400)}`);
    }
    return text.length === 0 ? null : JSON.parse(text);
}

async function githubPaginate(config, path) {
    const items = [];
    const separator = path.includes("?") ? "&" : "?";
    for (let page = 1; page <= MaxPages; page++) {
        const batch = await githubRequest(config, "GET", `${path}${separator}per_page=${PageSize}&page=${page}`);
        items.push(...batch);
        if (batch.length < PageSize) {
            break;
        }
    }
    return items;
}

function normalizeTagName(name) {
    return name.trim().toLowerCase().replace(/[\s_-]+/g, " ");
}

async function loadForum(config) {
    const channel = await discordRequest(config, "GET", `/channels/${config.forumChannelId}`);
    if (channel.type !== ForumChannelType) {
        throw new Error(`Channel ${config.forumChannelId} (${channel.name}) is not a forum channel; DISCORD_PULL_REQUEST_FORUM_ID must point at a Forum.`);
    }
    const tagIds = resolveTagIds(channel.available_tags ?? []);
    const missing = Object.keys(StatusTags).filter(status => tagIds[status] === undefined);
    if (missing.length > 0) {
        await createMissingTags(config, channel, tagIds, missing);
    }
    return { channel, guildId: channel.guild_id, tagIds };
}

function resolveTagIds(availableTags) {
    const tagIds = {};
    for (const [status, tag] of Object.entries(StatusTags)) {
        const wanted = normalizeTagName(tag.name);
        const match = availableTags.find(candidate => normalizeTagName(candidate.name) === wanted);
        if (match !== undefined) {
            tagIds[status] = match.id;
        }
    }
    return tagIds;
}

async function createMissingTags(config, channel, tagIds, missing) {
    const existing = (channel.available_tags ?? []).map(tag => ({
        id: tag.id,
        name: tag.name,
        moderated: tag.moderated,
        emoji_id: tag.emoji_id,
        emoji_name: tag.emoji_name,
    }));
    const additions = missing.map(status => ({
        name: StatusTags[status].name,
        moderated: true,
        emoji_name: StatusTags[status].emoji,
    }));
    try {
        const updated = await discordRequest(config, "PATCH", `/channels/${channel.id}`, { available_tags: [...existing, ...additions] });
        Object.assign(tagIds, resolveTagIds(updated.available_tags ?? []));
        console.log(`Created forum tags: ${additions.map(tag => tag.name).join(", ")}.`);
    } catch (error) {
        warn(`Forum tags missing (${additions.map(tag => tag.name).join(", ")}) and the bot could not create them: ${error.message}. Create them by hand in the forum settings; posts go out without a status tag until then.`);
    }
}

function scrubText(text) {
    return (text ?? "")
        .replace(HtmlCommentPattern, "")
        .replace(DashPattern, "-")
        .replace(/\r\n/g, "\n")
        .replace(ExcessBlankLinesPattern, "\n\n")
        .trim();
}

function truncate(text, limit) {
    if (text.length <= limit) {
        return text;
    }
    return `${text.slice(0, limit - 3).trimEnd()}...`;
}

function threadName(pullRequest) {
    const title = scrubText(pullRequest.title).replace(/\s+/g, " ");
    return truncate(`#${pullRequest.number} · ${title}`, ThreadNameLimit);
}

function userAuthor(user) {
    return { name: user.login, url: user.html_url, icon_url: user.avatar_url };
}

function footer(config) {
    return { text: `Aetherphone · ${config.repository}` };
}

function boldLogin(login) {
    return `**${login}**`;
}

function renderTargets(context, logins) {
    const userIds = [];
    const parts = [];
    for (const login of logins) {
        const discordId = context.config.userMap.get(login.toLowerCase());
        if (discordId === undefined) {
            parts.push(boldLogin(login));
            continue;
        }
        userIds.push(discordId);
        parts.push(`<@${discordId}>`);
    }
    return { text: parts.join(" "), userIds };
}

function withoutActor(context, logins) {
    const unique = [];
    for (const login of logins) {
        if (login === context.actorLogin || unique.includes(login)) {
            continue;
        }
        unique.push(login);
    }
    return unique;
}

function latestReviewStates(pullRequest, reviews) {
    const states = new Map();
    const chronological = reviews
        .filter(review => review.state !== "PENDING" && review.submitted_at)
        .sort((left, right) => left.submitted_at.localeCompare(right.submitted_at));
    for (const review of chronological) {
        const login = review.user?.login;
        if (!login || login === pullRequest.user.login) {
            continue;
        }
        if (review.state === "COMMENTED") {
            if (!states.has(login)) {
                states.set(login, "COMMENTED");
            }
            continue;
        }
        if (review.state === "DISMISSED") {
            states.set(login, "COMMENTED");
            continue;
        }
        states.set(login, review.state);
    }
    return states;
}

function determineStatus(pullRequest, reviewStates) {
    if (pullRequest.state === "closed") {
        return pullRequest.merged ? Status.Merged : Status.Closed;
    }
    if (pullRequest.draft) {
        return Status.Draft;
    }
    const values = [...reviewStates.values()];
    if (values.includes("CHANGES_REQUESTED")) {
        return Status.ChangesRequested;
    }
    if (values.includes("APPROVED")) {
        return Status.Approved;
    }
    return Status.Open;
}

function reviewerLogins(context) {
    const logins = (context.pullRequest.requested_reviewers ?? []).map(user => user.login);
    for (const login of context.reviewStates.keys()) {
        logins.push(login);
    }
    return withoutActor(context, logins.filter(login => login !== context.pullRequest.user.login));
}

function counterpartLogins(context) {
    const authorLogin = context.pullRequest.user.login;
    if (context.actorLogin !== authorLogin) {
        return [authorLogin];
    }
    return reviewerLogins(context);
}

function openingMentions(context) {
    const requested = withoutActor(context, (context.pullRequest.requested_reviewers ?? []).map(user => user.login))
        .filter(login => login !== context.pullRequest.user.login);
    if (requested.length > 0) {
        const rendered = renderTargets(context, requested);
        return { text: `Review requested from ${rendered.text}`, userIds: rendered.userIds, roleIds: [] };
    }
    if (context.config.maintainersRoleId.length > 0) {
        return { text: `<@&${context.config.maintainersRoleId}>`, userIds: [], roleIds: [context.config.maintainersRoleId] };
    }
    return { text: "", userIds: [], roleIds: [] };
}

function allowedMentions(mentions) {
    return { parse: [], users: mentions.userIds, roles: mentions.roleIds };
}

function starterEmbed(context) {
    const pullRequest = context.pullRequest;
    const description = scrubText(pullRequest.body);
    return {
        author: userAuthor(pullRequest.user),
        title: truncate(`#${pullRequest.number} ${scrubText(pullRequest.title)}`, EmbedTitleLimit),
        url: pullRequest.html_url,
        description: description.length === 0 ? "No description." : truncate(description, StarterBodyLimit),
        fields: [
            { name: "Branch", value: `\`${pullRequest.head.label}\` into \`${pullRequest.base.ref}\``, inline: true },
            { name: "Changes", value: `+${pullRequest.additions} -${pullRequest.deletions} in ${plural(pullRequest.changed_files, "file")}`, inline: true },
        ],
        color: StatusColors[context.status],
        footer: footer(context.config),
        timestamp: pullRequest.created_at,
    };
}

function plural(count, word) {
    return `${count} ${word}${count === 1 ? "" : "s"}`;
}

function threadPrefix(pullRequest) {
    return `#${pullRequest.number} ·`;
}

function matchesPullRequest(thread, forumChannelId, prefix) {
    return thread.parent_id === forumChannelId && typeof thread.name === "string" && thread.name.startsWith(prefix);
}

function earliestThread(threads) {
    let earliest = null;
    for (const thread of threads) {
        if (earliest === null || BigInt(thread.id) < BigInt(earliest.id)) {
            earliest = thread;
        }
    }
    return earliest;
}

// Posts are found by name on the Discord side so that nothing about the staff server (no link,
// no id, no comment) is ever written to the public pull request.
async function findThreads(context) {
    const prefix = threadPrefix(context.pullRequest);
    const forumChannelId = context.config.forumChannelId;
    const found = [];
    const active = await discordRequest(context.config, "GET", `/guilds/${context.forum.guildId}/threads/active`);
    for (const thread of active.threads ?? []) {
        if (matchesPullRequest(thread, forumChannelId, prefix)) {
            found.push(thread);
        }
    }
    if (found.length > 0) {
        return found;
    }
    let before = "";
    for (let page = 0; page < ArchivedThreadPageCap; page++) {
        const query = before.length === 0
            ? `?limit=${ArchivedThreadPageLimit}`
            : `?limit=${ArchivedThreadPageLimit}&before=${encodeURIComponent(before)}`;
        const archived = await discordRequest(context.config, "GET", `/channels/${forumChannelId}/threads/archived/public${query}`);
        const threads = archived.threads ?? [];
        for (const thread of threads) {
            if (matchesPullRequest(thread, forumChannelId, prefix)) {
                found.push(thread);
            }
        }
        if (found.length > 0 || !archived.has_more || threads.length === 0) {
            break;
        }
        before = threads[threads.length - 1].thread_metadata?.archive_timestamp ?? "";
        if (before.length === 0) {
            break;
        }
    }
    return found;
}

async function createThread(context, mentions) {
    const starterLine = `New pull request by ${boldLogin(context.pullRequest.user.login)}`;
    const content = mentions.text.length === 0 ? starterLine : `${mentions.text}\n${starterLine}`;
    const tagId = context.forum.tagIds[context.status];
    return discordRequest(context.config, "POST", `/channels/${context.config.forumChannelId}/threads`, {
        name: threadName(context.pullRequest),
        applied_tags: tagId === undefined ? [] : [tagId],
        message: {
            content,
            embeds: [starterEmbed(context)],
            allowed_mentions: allowedMentions(mentions),
        },
    });
}

// Two runs for the same pull request can race (an opened event and a review request fire together),
// and GitHub's concurrency groups drop queued runs instead of serializing them. Both runs create a
// post, then the one whose post is not the oldest deletes its own again.
async function ensureThread(context, starterMentions) {
    const existing = earliestThread(await findThreads(context));
    if (existing !== null) {
        return existing.id;
    }
    const created = await createThread(context, starterMentions);
    const winner = earliestThread(await findThreads(context)) ?? created;
    if (winner.id === created.id) {
        console.log(`Created the forum post for #${context.pullRequest.number}.`);
        return created.id;
    }
    console.log(`Another run already created the forum post for #${context.pullRequest.number}; removing this run's duplicate.`);
    await discordRequest(context.config, "DELETE", `/channels/${created.id}`);
    return winner.id;
}

async function postThreadMessage(context, threadId, message) {
    await discordRequest(context.config, "POST", `/channels/${threadId}/messages`, {
        content: message.content,
        embeds: message.embed === null ? [] : [message.embed],
        allowed_mentions: allowedMentions(message.mentions),
    });
}

function sameIds(left, right) {
    if (left.length !== right.length) {
        return false;
    }
    const sortedLeft = [...left].sort();
    const sortedRight = [...right].sort();
    for (let index = 0; index < sortedLeft.length; index++) {
        if (sortedLeft[index] !== sortedRight[index]) {
            return false;
        }
    }
    return true;
}

async function applyThreadState(context, threadId) {
    const thread = await discordRequest(context.config, "GET", `/channels/${threadId}`);
    const metadata = thread.thread_metadata ?? {};
    const desiredName = threadName(context.pullRequest);
    const tagId = context.forum.tagIds[context.status];
    const desiredTags = tagId === undefined ? [] : [tagId];
    const shouldClose = context.pullRequest.state === "closed";

    const edit = {};
    if (thread.name !== desiredName) {
        edit.name = desiredName;
    }
    if (!sameIds(thread.applied_tags ?? [], desiredTags)) {
        edit.applied_tags = desiredTags;
    }
    let archived = metadata.archived === true;
    let locked = metadata.locked === true;
    if (Object.keys(edit).length > 0) {
        await discordRequest(context.config, "PATCH", `/channels/${threadId}`, { ...edit, archived: false, locked: false });
        archived = false;
        locked = false;
    }
    if (archived !== shouldClose || locked !== shouldClose) {
        await discordRequest(context.config, "PATCH", `/channels/${threadId}`, { archived: shouldClose, locked: shouldClose });
    }
    console.log(`The forum post for #${context.pullRequest.number} is tagged ${StatusTags[context.status].name}${shouldClose ? ", locked and archived" : ""}.`);
}

function resolvePullRequestNumber(eventName, event) {
    switch (eventName) {
        case "pull_request_target":
        case "pull_request_review":
            return event.pull_request?.number ?? null;
        case "issue_comment":
            return event.issue?.pull_request ? event.issue.number : null;
        default:
            return null;
    }
}

function actorLine(context, emoji, sentence) {
    return `${emoji} ${boldLogin(context.actorLogin)} ${sentence}`;
}

function messageWithTargets(context, emoji, sentence, logins, embed) {
    const targets = withoutActor(context, logins);
    const rendered = renderTargets(context, targets);
    const line = actorLine(context, emoji, sentence);
    return {
        content: rendered.text.length === 0 ? line : `${rendered.text}\n${line}`,
        embed,
        mentions: { userIds: rendered.userIds, roleIds: [] },
    };
}

function messageWithOpeningMentions(context, emoji, sentence) {
    const mentions = openingMentions(context);
    const line = actorLine(context, emoji, sentence);
    return {
        content: mentions.text.length === 0 ? line : `${mentions.text}\n${line}`,
        embed: null,
        mentions,
    };
}

async function describePush(context, event) {
    const compare = await loadCompare(context, event.before, event.after);
    if (compare === null) {
        return messageWithTargets(context, "📦", "force-pushed the branch", counterpartLogins(context), null);
    }
    const commits = compare.commits ?? [];
    const lines = commits.slice(-CommitLineLimit).map(commit => {
        const subject = scrubText(commit.commit.message.split("\n")[0]);
        return `[\`${commit.sha.slice(0, 7)}\`](${commit.html_url}) ${truncate(subject, 110)}`;
    });
    if (commits.length > CommitLineLimit) {
        lines.unshift(`*... and ${commits.length - CommitLineLimit} more*`);
    }
    const embed = {
        title: plural(compare.total_commits ?? commits.length, "new commit"),
        url: compare.html_url,
        description: lines.join("\n"),
        color: StatusColors[context.status],
        footer: footer(context.config),
        timestamp: new Date().toISOString(),
    };
    return messageWithTargets(context, "📦", `pushed ${plural(compare.total_commits ?? commits.length, "commit")}`, counterpartLogins(context), embed);
}

async function loadCompare(context, before, after) {
    if (!before || !after) {
        return null;
    }
    try {
        return await githubRequest(context.config, "GET", `/repos/${context.config.repository}/compare/${before}...${after}`);
    } catch (error) {
        console.log(`Compare ${before.slice(0, 7)}...${after.slice(0, 7)} is unavailable (${error.message}); treating the push as a force push.`);
        return null;
    }
}

async function describePullRequestEvent(context, event) {
    const pullRequest = context.pullRequest;
    switch (event.action) {
        case "opened":
        case "edited":
            return null;
        case "reopened":
            return messageWithOpeningMentions(context, "♻️", "reopened the pull request");
        case "ready_for_review":
            return messageWithOpeningMentions(context, "✅", "marked the pull request ready for review");
        case "converted_to_draft":
            return { content: actorLine(context, "📝", "converted the pull request to a draft"), embed: null, mentions: NoMentions };
        case "review_requested": {
            if (event.requested_reviewer) {
                const rendered = renderTargets(context, withoutActor(context, [event.requested_reviewer.login]));
                return {
                    content: actorLine(context, "👀", `requested a review from ${rendered.text}`),
                    embed: null,
                    mentions: { userIds: rendered.userIds, roleIds: [] },
                };
            }
            const teamName = event.requested_team?.name ?? "a team";
            return { content: actorLine(context, "👀", `requested a review from ${boldLogin(teamName)}`), embed: null, mentions: NoMentions };
        }
        case "synchronize":
            return describePush(context, event);
        case "closed": {
            if (pullRequest.merged) {
                return messageWithTargets(context, "🎉", `merged the pull request into \`${pullRequest.base.ref}\``, [pullRequest.user.login], null);
            }
            return messageWithTargets(context, "🚫", "closed the pull request without merging", [pullRequest.user.login], null);
        }
        default:
            return null;
    }
}

async function describeReview(context, event) {
    const review = event.review;
    const inlineComments = await githubPaginate(context.config, `/repos/${context.config.repository}/pulls/${context.pullRequest.number}/reviews/${review.id}/comments`);
    const body = scrubText(review.body);
    const state = review.state.toUpperCase();
    if (state === "COMMENTED" && body.length === 0 && inlineComments.length === 0) {
        return null;
    }
    const headlines = {
        APPROVED: ["✅", "approved the pull request", "Approved"],
        CHANGES_REQUESTED: ["🔁", "requested changes", "Changes requested"],
        COMMENTED: ["💬", "reviewed the pull request", "Review"],
    };
    const [emoji, sentence, title] = headlines[state] ?? ["💬", "reviewed the pull request", "Review"];
    const descriptionParts = [];
    if (body.length > 0) {
        descriptionParts.push(truncate(body, CommentBodyLimit));
    }
    if (inlineComments.length > 0) {
        descriptionParts.push(`*${plural(inlineComments.length, "inline comment")}*`);
    }
    const embed = {
        author: userAuthor(review.user),
        title,
        url: review.html_url,
        description: descriptionParts.join("\n\n"),
        color: ReviewStateColors[state] ?? ReviewStateColors.COMMENTED,
        footer: footer(context.config),
        timestamp: review.submitted_at ?? new Date().toISOString(),
    };
    return messageWithTargets(context, emoji, sentence, counterpartLogins(context), embed);
}

function mentionedLogins(text) {
    const logins = [];
    for (const match of text.matchAll(MentionPattern)) {
        logins.push(match[1]);
    }
    return logins;
}

function describeComment(context, event) {
    const comment = event.comment;
    if (comment.user?.type === "Bot") {
        return null;
    }
    const body = scrubText(comment.body);
    if (body.length === 0) {
        return null;
    }
    const embed = {
        author: userAuthor(comment.user),
        title: "Comment",
        url: comment.html_url,
        description: truncate(body, CommentBodyLimit),
        color: ReviewStateColors.COMMENTED,
        footer: footer(context.config),
        timestamp: comment.created_at,
    };
    const targets = [...counterpartLogins(context), ...mentionedLogins(body)];
    return messageWithTargets(context, "💬", "commented", targets, embed);
}

async function describeEvent(context, eventName, event) {
    switch (eventName) {
        case "pull_request_target":
            return describePullRequestEvent(context, event);
        case "pull_request_review":
            return describeReview(context, event);
        case "issue_comment":
            return describeComment(context, event);
        default:
            return null;
    }
}

async function buildContext(config, forum, pullRequestNumber, actorLogin) {
    const pullRequest = await githubRequest(config, "GET", `/repos/${config.repository}/pulls/${pullRequestNumber}`);
    const reviews = await githubPaginate(config, `/repos/${config.repository}/pulls/${pullRequestNumber}/reviews`);
    const reviewStates = latestReviewStates(pullRequest, reviews);
    return {
        config,
        forum,
        pullRequest,
        reviewStates,
        status: determineStatus(pullRequest, reviewStates),
        actorLogin,
    };
}

async function mirrorEvent(config, forum, eventName, event) {
    const pullRequestNumber = resolvePullRequestNumber(eventName, event);
    if (pullRequestNumber === null) {
        console.log(`Nothing to mirror for a ${eventName} event without a pull request.`);
        return;
    }
    const context = await buildContext(config, forum, pullRequestNumber, event.sender?.login ?? "github");
    if (BotLogins.has(context.pullRequest.user.login)) {
        console.log(`#${pullRequestNumber} was opened by ${context.pullRequest.user.login}; bot pull requests are not mirrored.`);
        return;
    }
    const opensThread = eventName === "pull_request_target" && event.action === "opened" && !context.pullRequest.draft;
    const threadId = await ensureThread(context, opensThread ? openingMentions(context) : NoMentions);
    const message = await describeEvent(context, eventName, event);
    if (message !== null) {
        await postThreadMessage(context, threadId, message);
        console.log(`Posted to the forum post for #${context.pullRequest.number}: ${redact(message.content.split("\n").pop())}`);
    }
    await applyThreadState(context, threadId);
}

async function syncPullRequest(config, forum, pullRequestNumber) {
    const context = await buildContext(config, forum, pullRequestNumber, "github");
    const threadId = await ensureThread(context, NoMentions);
    await applyThreadState(context, threadId);
}

async function checkConnection(config, forum) {
    await discordRequest(config, "GET", "/users/@me");
    console.log("Bot token: ok");
    console.log(`Forum channel: ok (${forum.channel.type === ForumChannelType ? "forum" : "not a forum"})`);
    for (const [status, tag] of Object.entries(StatusTags)) {
        console.log(`Tag ${tag.name}: ${forum.tagIds[status] === undefined ? "MISSING" : "ok"}`);
    }
    if (config.maintainersRoleId.length > 0) {
        const roles = await discordRequest(config, "GET", `/guilds/${forum.guildId}/roles`);
        const role = roles.find(candidate => candidate.id === config.maintainersRoleId);
        console.log(`Maintainers role: ${role === undefined ? "MISSING (DISCORD_MAINTAINERS_ROLE_ID is not a role in this server)" : "ok"}`);
    } else {
        console.log("Maintainers role: not configured (new pull requests ping nobody unless a reviewer is requested).");
    }
    if (config.userMap.size === 0) {
        console.log("User map: empty (DISCORD_GITHUB_USERS is unset, so nobody is pinged by name).");
    }
    for (const [login, discordId] of config.userMap) {
        try {
            await discordRequest(config, "GET", `/guilds/${forum.guildId}/members/${discordId}`);
            console.log(`User ${login}: member`);
        } catch {
            warn(`User ${login}: not a member of the server, or the id is wrong.`);
        }
    }
}

async function main() {
    const config = readConfig();
    if (config.token.length === 0 || config.forumChannelId.length === 0) {
        console.log("DISCORD_BOT_TOKEN or DISCORD_PULL_REQUEST_FORUM_ID is not set; skipping the Discord forum mirror.");
        return;
    }
    const eventName = process.env.GITHUB_EVENT_NAME ?? "";
    const event = JSON.parse(readFileSync(process.env.GITHUB_EVENT_PATH, "utf8"));
    const forum = await loadForum(config);
    if (eventName === "workflow_dispatch") {
        const requested = (process.env.PULL_REQUEST_NUMBER ?? "").trim();
        if (requested.length === 0) {
            await checkConnection(config, forum);
            return;
        }
        await syncPullRequest(config, forum, Number(requested));
        return;
    }
    await mirrorEvent(config, forum, eventName, event);
}

main().catch(error => {
    console.log(`::error::${redact(error.message)}`);
    process.exitCode = 1;
});
