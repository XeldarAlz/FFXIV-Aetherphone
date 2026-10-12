const DiscordApiBaseUrl = process.env.DISCORD_API_BASE_URL ?? "https://discord.com/api/v10";
const UserAgent = "Aetherphone staff role sync (https://github.com/XeldarAlz/FFXIV-Aetherphone)";
const RequestTimeoutMilliseconds = 20000;
const RateLimitAttempts = 6;
const MemberPageLimit = 1000;
const MemberPageCap = 200;
const AdministratorPermission = 1n << 3n;
const ManageRolesPermission = 1n << 28n;
const RemovalBrakeMinimum = 5;
const RemovalBrakeShare = 0.5;
const ReportChunkLimit = 1900;
const AuditReason = "Staff role sync";
const SnowflakePattern = /\d{15,}/g;
const MentionTokenPattern = /<@&?\d+>/g;

// Workflow logs of a public repository are public, so stdout carries counts and role names only;
// who gained or lost what goes to the report channel on the staff server.
function redact(text) {
    return text.replace(MentionTokenPattern, "@...").replace(SnowflakePattern, "<id>");
}

class ReportedError extends Error {}

function fail(message) {
    throw new Error(message);
}

function failReported(message) {
    throw new ReportedError(message);
}

function plural(count, word) {
    return `${count} ${word}${count === 1 ? "" : "s"}`;
}

function readConfig() {
    const eventName = process.env.GITHUB_EVENT_NAME ?? "";
    const dispatched = eventName === "workflow_dispatch";
    return {
        token: (process.env.DISCORD_BOT_TOKEN ?? "").trim(),
        staffGuildId: (process.env.DISCORD_STAFF_GUILD_ID ?? "").trim(),
        communityGuildId: (process.env.DISCORD_COMMUNITY_GUILD_ID ?? "").trim(),
        roleMapSource: (process.env.DISCORD_ROLE_SYNC_MAP ?? "").trim(),
        reportChannelId: (process.env.DISCORD_ROLE_SYNC_REPORT_CHANNEL_ID ?? "").trim(),
        apply: dispatched ? process.env.SYNC_APPLY === "true" : process.env.DISCORD_ROLE_SYNC_ARMED === "true",
        force: dispatched && process.env.SYNC_FORCE === "true",
        reportDryRun: dispatched,
    };
}

function parseRoleMap(source) {
    let parsed;
    try {
        parsed = JSON.parse(source);
    } catch (error) {
        fail(`DISCORD_ROLE_SYNC_MAP is not valid JSON: ${error.message}`);
    }
    const entries = [];
    for (const [communityRoleName, staffRoleNames] of Object.entries(parsed)) {
        const names = Array.isArray(staffRoleNames) ? staffRoleNames : [staffRoleNames];
        if (names.length === 0 || names.some(name => typeof name !== "string" || name.trim().length === 0)) {
            fail(`DISCORD_ROLE_SYNC_MAP entry "${communityRoleName}" must list one or more staff role names.`);
        }
        entries.push({ communityRoleName, staffRoleNames: names });
    }
    if (entries.length === 0) {
        fail("DISCORD_ROLE_SYNC_MAP maps nothing.");
    }
    return entries;
}

function sleep(milliseconds) {
    return new Promise(resolve => setTimeout(resolve, milliseconds));
}

async function discordRequest(config, method, path, body, reason) {
    for (let attempt = 0; attempt < RateLimitAttempts; attempt++) {
        const headers = {
            Authorization: `Bot ${config.token}`,
            "Content-Type": "application/json",
            "User-Agent": UserAgent,
        };
        if (reason !== undefined) {
            headers["X-Audit-Log-Reason"] = reason;
        }
        const response = await fetch(`${DiscordApiBaseUrl}${path}`, {
            method,
            headers,
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

function normalizeName(name) {
    return name.trim().toLowerCase();
}

function findRole(roles, name) {
    const wanted = normalizeName(name);
    return roles.find(role => normalizeName(role.name) === wanted);
}

function resolveMappings(entries, staffRoles, communityRoles) {
    const mappings = [];
    const unresolved = [];
    for (const entry of entries) {
        const communityRole = findRole(communityRoles, entry.communityRoleName);
        if (communityRole === undefined) {
            unresolved.push(`community role "${entry.communityRoleName}"`);
        }
        const staffRoleIds = new Set();
        for (const staffRoleName of entry.staffRoleNames) {
            const staffRole = findRole(staffRoles, staffRoleName);
            if (staffRole === undefined) {
                unresolved.push(`staff role "${staffRoleName}"`);
                continue;
            }
            staffRoleIds.add(staffRole.id);
        }
        if (communityRole !== undefined) {
            mappings.push({ communityRole, staffRoleIds });
        }
    }
    if (unresolved.length > 0) {
        fail(`Role map names not found, nothing was changed: ${unresolved.join(", ")}.`);
    }
    return mappings;
}

async function loadBotStanding(config, communityRoles) {
    const me = await discordRequest(config, "GET", "/users/@me");
    const member = await discordRequest(config, "GET", `/guilds/${config.communityGuildId}/members/${me.id}`);
    const everyone = communityRoles.find(role => role.id === config.communityGuildId);
    let permissions = BigInt(everyone?.permissions ?? "0");
    let topPosition = 0;
    for (const roleId of member.roles) {
        const role = communityRoles.find(candidate => candidate.id === roleId);
        if (role === undefined) {
            continue;
        }
        permissions |= BigInt(role.permissions);
        topPosition = Math.max(topPosition, role.position);
    }
    return { botId: me.id, permissions, topPosition };
}

function assertBotCanManage(standing, mappings) {
    const administrator = (standing.permissions & AdministratorPermission) !== 0n;
    if (!administrator && (standing.permissions & ManageRolesPermission) === 0n) {
        fail("The bot has no Manage Roles permission on the community server.");
    }
    const blocked = mappings
        .filter(mapping => mapping.communityRole.position >= standing.topPosition)
        .map(mapping => mapping.communityRole.name);
    if (blocked.length > 0) {
        fail(`The bot's role must sit above these community roles before it can manage them: ${blocked.join(", ")}.`);
    }
}

async function loadMembers(config, guildId) {
    const members = new Map();
    let after = "0";
    for (let page = 0; page < MemberPageCap; page++) {
        const batch = await discordRequest(config, "GET", `/guilds/${guildId}/members?limit=${MemberPageLimit}&after=${after}`);
        for (const member of batch) {
            members.set(member.user.id, member);
        }
        if (batch.length < MemberPageLimit) {
            break;
        }
        after = batch[batch.length - 1].user.id;
    }
    return members;
}

function planChanges(mappings, staffMembers, communityMembers, botId) {
    const changes = [];
    const holders = new Map();
    for (const member of communityMembers.values()) {
        if (member.user.bot === true || member.user.id === botId) {
            continue;
        }
        const staffRoleIds = new Set(staffMembers.get(member.user.id)?.roles ?? []);
        const add = [];
        const remove = [];
        for (const mapping of mappings) {
            const holds = member.roles.includes(mapping.communityRole.id);
            if (holds) {
                holders.set(mapping.communityRole.id, (holders.get(mapping.communityRole.id) ?? 0) + 1);
            }
            let entitled = false;
            for (const staffRoleId of mapping.staffRoleIds) {
                if (staffRoleIds.has(staffRoleId)) {
                    entitled = true;
                    break;
                }
            }
            if (entitled && !holds) {
                add.push(mapping.communityRole);
            }
            if (!entitled && holds) {
                remove.push(mapping.communityRole);
            }
        }
        if (add.length > 0 || remove.length > 0) {
            changes.push({ member, add, remove });
        }
    }
    return { changes, holders };
}

function brakeReasons(changes, holders, mappings) {
    const reasons = [];
    for (const mapping of mappings) {
        let removals = 0;
        for (const change of changes) {
            if (change.remove.includes(mapping.communityRole)) {
                removals++;
            }
        }
        const held = holders.get(mapping.communityRole.id) ?? 0;
        if (removals > RemovalBrakeMinimum && removals > held * RemovalBrakeShare) {
            reasons.push(`${removals} of ${held} ${mapping.communityRole.name} holders would lose it`);
        }
    }
    return reasons;
}

function countChanges(changes) {
    let additions = 0;
    let removals = 0;
    for (const change of changes) {
        additions += change.add.length;
        removals += change.remove.length;
    }
    return { additions, removals };
}

function describeChange(change) {
    const parts = [];
    for (const role of change.add) {
        parts.push(`+${role.name}`);
    }
    for (const role of change.remove) {
        parts.push(`-${role.name}`);
    }
    return `<@${change.member.user.id}> ${parts.join(" ")}`;
}

async function applyChanges(config, changes) {
    let applied = 0;
    const failures = [];
    for (const change of changes) {
        const memberPath = `/guilds/${config.communityGuildId}/members/${change.member.user.id}/roles`;
        for (const role of change.add) {
            try {
                await discordRequest(config, "PUT", `${memberPath}/${role.id}`, undefined, AuditReason);
                applied++;
            } catch (error) {
                failures.push(`<@${change.member.user.id}> +${role.name}: ${error.message}`);
            }
        }
        for (const role of change.remove) {
            try {
                await discordRequest(config, "DELETE", `${memberPath}/${role.id}`, undefined, AuditReason);
                applied++;
            } catch (error) {
                failures.push(`<@${change.member.user.id}> -${role.name}: ${error.message}`);
            }
        }
    }
    return { applied, failures };
}

async function postReport(config, title, lines) {
    if (config.reportChannelId.length === 0) {
        return;
    }
    const chunks = [];
    let current = `**${title}**`;
    for (const line of lines) {
        const candidate = `${current}\n${line}`;
        if (candidate.length > ReportChunkLimit) {
            chunks.push(current);
            current = line;
            continue;
        }
        current = candidate;
    }
    chunks.push(current);
    for (const chunk of chunks) {
        await discordRequest(config, "POST", `/channels/${config.reportChannelId}/messages`, {
            content: chunk,
            allowed_mentions: { parse: [] },
        });
    }
}

async function synchronize(config) {
    const entries = parseRoleMap(config.roleMapSource);
    const [staffRoles, communityRoles] = await Promise.all([
        discordRequest(config, "GET", `/guilds/${config.staffGuildId}/roles`),
        discordRequest(config, "GET", `/guilds/${config.communityGuildId}/roles`),
    ]);
    const mappings = resolveMappings(entries, staffRoles, communityRoles);
    const standing = await loadBotStanding(config, communityRoles);
    assertBotCanManage(standing, mappings);
    const staffMembers = await loadMembers(config, config.staffGuildId);
    if (staffMembers.size === 0) {
        fail("The staff member list came back empty; refusing to sync against nothing.");
    }
    const communityMembers = await loadMembers(config, config.communityGuildId);
    const { changes, holders } = planChanges(mappings, staffMembers, communityMembers, standing.botId);
    if (changes.length === 0) {
        console.log("In sync: nothing to change.");
        return;
    }
    const { additions, removals } = countChanges(changes);
    const lines = changes.map(describeChange);
    const brakes = brakeReasons(changes, holders, mappings);
    const brakeNote = brakes.length === 0 ? "" : ` (the removal brake would stop a real run: ${brakes.join("; ")})`;
    if (!config.apply) {
        console.log(`Dry run: ${additions} additions and ${removals} removals planned${brakes.length === 0 ? "" : "; the removal brake would trip"}.`);
        if (config.reportDryRun) {
            await postReport(config, `Staff role sync, dry run: ${additions} to add, ${removals} to remove${brakeNote}`, lines);
        }
        return;
    }
    if (brakes.length > 0 && !config.force) {
        await postReport(config, `Staff role sync stopped by the removal brake, nothing changed: ${brakes.join("; ")}. Run it by hand with force to apply.`, lines);
        failReported(`Removal brake: ${brakes.join("; ")}. Nothing was changed; dispatch with force to apply anyway.`);
    }
    const result = await applyChanges(config, changes);
    console.log(`Applied ${plural(result.applied, "role change")}, ${result.failures.length} failed.`);
    await postReport(
        config,
        `Staff role sync: applied ${plural(result.applied, "role change")}${result.failures.length === 0 ? "" : `, ${result.failures.length} failed`}`,
        [...lines, ...result.failures.map(failure => `failed: ${failure}`)],
    );
    if (result.failures.length > 0) {
        failReported(`${plural(result.failures.length, "role change")} failed; see the report channel.`);
    }
}

async function main() {
    const config = readConfig();
    if (config.token.length === 0 || config.staffGuildId.length === 0 || config.communityGuildId.length === 0 || config.roleMapSource.length === 0) {
        console.log("DISCORD_BOT_TOKEN, DISCORD_STAFF_GUILD_ID, DISCORD_COMMUNITY_GUILD_ID or DISCORD_ROLE_SYNC_MAP is not set; skipping the role sync.");
        return;
    }
    try {
        await synchronize(config);
    } catch (error) {
        if (!(error instanceof ReportedError)) {
            await postReport(config, `Staff role sync aborted: ${error.message}`, []).catch(() => {});
        }
        // An unarmed scheduled run applies nothing, so a setup problem there is a warning; once armed
        // (or when dispatched by hand) it fails the run so GitHub's failure notice reaches a maintainer.
        if (!config.apply && !config.reportDryRun) {
            console.log(`::warning::${redact(error.message)}`);
            return;
        }
        console.log(`::error::${redact(error.message)}`);
        process.exitCode = 1;
    }
}

await main();
