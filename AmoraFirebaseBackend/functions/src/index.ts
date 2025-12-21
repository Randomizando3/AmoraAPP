import * as functions from "firebase-functions/v1";
import * as admin from "firebase-admin";

admin.initializeApp();

const db = admin.database();
const REGION = "southamerica-east1";

function asString(v: unknown): string {
    return (v ?? "").toString();
}

/**
 * Lê tokens do RTDB em /pushTokens/{uid}
 *
 * Suporta:
 * A) pushTokens/{uid}/{platform} = "TOKEN"
 * B) pushTokens/{uid}/{platform}/{token} = true
 * C) pushTokens/{uid}/{platform} = { token: "TOKEN", updatedAtUtcMs: 123, platform: "android" }
 */
async function getUserTokens(uid: string): Promise<string[]> {
    const snap = await db.ref(`/pushTokens/${uid}`).get();
    if (!snap.exists()) return [];

    const val = snap.val() as unknown;
    const tokens: string[] = [];

    if (!val || typeof val !== "object") return [];

    const perPlatform = val as Record<string, unknown>;

    for (const platform of Object.keys(perPlatform)) {
        const p = perPlatform[platform];

        // A) "TOKEN"
        if (typeof p === "string" && p.trim()) {
            tokens.push(p.trim());
            continue;
        }

        if (p && typeof p === "object") {
            const obj = p as Record<string, unknown>;

            // C) { token: "TOKEN", ... }
            if (typeof obj.token === "string" && obj.token.trim()) {
                tokens.push(obj.token.trim());
                continue;
            }

            // B) { "TOKEN": true } ou { "TOKEN": { ... } }
            for (const key of Object.keys(obj)) {
                if (key === "token" || key === "platform" || key === "updatedAtUtcMs") continue;

                // token como chave
                if (typeof key === "string" && key.trim().length > 20) {
                    tokens.push(key.trim());
                    continue;
                }

                const v = obj[key];
                if (v && typeof v === "object") {
                    const vObj = v as Record<string, unknown>;
                    if (typeof vObj.token === "string" && vObj.token.trim()) {
                        tokens.push(vObj.token.trim());
                    }
                }
            }
        }
    }

    return Array.from(new Set(tokens.filter(Boolean)));
}

async function getDisplayName(uid: string): Promise<string> {
    try {
        const snap = await db.ref(`/users/${uid}/displayName`).get();
        const name = asString(snap.val()).trim();
        return name || "Alguém";
    } catch {
        return "Alguém";
    }
}

async function sendPushToUser(
    uid: string,
    title: string,
    body: string,
    data: Record<string, string>
): Promise<void> {
    const tokens = await getUserTokens(uid);

    if (!tokens.length) {
        functions.logger.info(`[push] Sem tokens para uid=${uid}`);
        return;
    }

    const dataPayload: Record<string, string> = {};
    for (const k of Object.keys(data || {})) dataPayload[k] = asString((data as any)[k]);

    const message: admin.messaging.MulticastMessage = {
        tokens,
        notification: { title, body },
        data: dataPayload,
        android: {
            priority: "high",
            notification: {
                channelId: "amora_default",
                sound: "default",
            },
        },
        apns: { payload: { aps: { sound: "default" } } },
    };

    const res = await admin.messaging().sendEachForMulticast(message);

    functions.logger.info(
        `[push] uid=${uid} tokens=${tokens.length} ok=${res.successCount} fail=${res.failureCount}`
    );

    if (res.failureCount > 0) {
        res.responses.forEach((r, i) => {
            if (!r.success) {
                const tokenPreview = (tokens[i] ?? "").slice(0, 18);
                functions.logger.warn(
                    `[push] token_fail uid=${uid} tokenPrefix=${tokenPreview} err=${r.error?.message}`
                );
            }
        });
    }
}

/**
 * LIKE (v1)
 *
 * RTDB atual (pelo seu print):
 * likes/{fromUid}/{targetUid} = true
 *
 * Notifica o targetUid dizendo que fromUid curtiu.
 */
export const onLikeReceived = functions
    .region(REGION)
    .database.ref("/likes/{fromUid}/{targetUid}")
    .onCreate(
        async (
            _snap: functions.database.DataSnapshot,
            context: functions.EventContext<{ fromUid: string; targetUid: string }>
        ) => {
            const fromUid = context.params.fromUid;
            const targetUid = context.params.targetUid;

            functions.logger.info(`[like] onCreate from=${fromUid} target=${targetUid}`);

            if (!fromUid || !targetUid || fromUid === targetUid) return null;

            const fromName = await getDisplayName(fromUid);

            await sendPushToUser(targetUid, "Nova curtida", `${fromName} curtiu você.`, {
                type: "like",
                fromUid,
            });

            return null;
        }
    );

/**
 * MATCH (v1)
 *
 * RTDB (pelo seu print):
 * matches/{uid}/{otherUid} = true (gravado dos dois lados)
 *
 * Dedup: processa só quando uid < otherUid (ordem lexicográfica do UID)
 */
export const onMatchCreated = functions
    .region(REGION)
    .database.ref("/matches/{uid}/{otherUid}")
    .onCreate(
        async (
            _snap: functions.database.DataSnapshot,
            context: functions.EventContext<{ uid: string; otherUid: string }>
        ) => {
            const uid = context.params.uid;
            const otherUid = context.params.otherUid;

            functions.logger.info(`[match] onCreate uid=${uid} other=${otherUid}`);

            if (!uid || !otherUid || uid === otherUid) return null;

            // evita duplicar (se grava pros 2 lados)
            if (uid >= otherUid) {
                functions.logger.info(`[match] skip duplicate side uid=${uid} other=${otherUid}`);
                return null;
            }

            const nameA = await getDisplayName(uid);
            const nameB = await getDisplayName(otherUid);

            await Promise.all([
                sendPushToUser(uid, "É um match!", `Você e ${nameB} combinaram.`, {
                    type: "match",
                    fromUid: otherUid,
                }),
                sendPushToUser(otherUid, "É um match!", `Você e ${nameA} combinaram.`, {
                    type: "match",
                    fromUid: uid,
                }),
            ]);

            return null;
        }
    );
