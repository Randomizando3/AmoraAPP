import * as functions from "firebase-functions/v1";
import * as admin from "firebase-admin";
import * as nodemailer from "nodemailer";

// IMPORTANT: este import evita o "never" no NodeNext ao chamar config()
import functionsRoot = require("firebase-functions");

admin.initializeApp();

const db = admin.database();
const REGION = "southamerica-east1";

/* =========================
   Utils
========================= */
function asString(v: unknown): string {
    return (v ?? "").toString();
}

function isValidEmail(email: string): boolean {
    const e = email.trim();
    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(e);
}

/* =========================
   SMTP (simples, via functions.config().smtp.*)
========================= */
type SmtpCfg = {
    enabled?: string | boolean;
    host?: string;
    port?: string | number;
    user?: string;
    pass?: string;
    from_email?: string;
    from_name?: string;
};

function cfgBool(v: unknown, def = false): boolean {
    if (typeof v === "boolean") return v;
    const s = asString(v).trim().toLowerCase();
    if (!s) return def;
    return s === "1" || s === "true" || s === "yes" || s === "on";
}

function getSmtpCfg(): SmtpCfg {
    // Chama config() via any para não dar TS2349/never no NodeNext
    const rootCfg = ((functionsRoot as any).config?.() ?? {}) as any;
    return (rootCfg.smtp ?? {}) as SmtpCfg;
}

let _transporter: nodemailer.Transporter | null = null;

function getTransporter(): nodemailer.Transporter {
    if (_transporter) return _transporter;

    const cfg = getSmtpCfg();
    if (!cfgBool(cfg.enabled, false)) {
        throw new Error("[smtp] disabled (smtp.enabled=0)");
    }

    const host = asString(cfg.host).trim();
    const port = Number(cfg.port ?? 587) || 587;
    const user = asString(cfg.user).trim();
    const pass = asString(cfg.pass).trim();

    if (!host || !user || !pass) {
        throw new Error("[smtp] missing host/user/pass");
    }

    const secure = port === 465; // 465 SSL, 587 STARTTLS

    _transporter = nodemailer.createTransport({
        host,
        port,
        secure,
        auth: { user, pass },
    });

    return _transporter;
}

async function getUserEmail(uid: string): Promise<string> {
    // Ajuste aqui SE seu schema não usa /users/{uid}/email
    try {
        const snap = await db.ref(`/users/${uid}/email`).get();
        return asString(snap.val()).trim();
    } catch {
        return "";
    }
}

async function sendEmailToUser(uid: string, subject: string, text: string): Promise<void> {
    try {
        const cfg = getSmtpCfg();
        if (!cfgBool(cfg.enabled, false)) return;

        const to = (await getUserEmail(uid)).trim();
        if (!isValidEmail(to)) {
            functions.logger.info(`[smtp] Sem e-mail válido para uid=${uid}`);
            return;
        }

        const transporter = getTransporter();

        const fromEmail = asString(cfg.from_email || cfg.user).trim();
        const fromName = asString(cfg.from_name || "Amora").trim();
        const from = fromName ? `"${fromName}" <${fromEmail}>` : fromEmail;

        await transporter.sendMail({
            from,
            to,
            subject,
            text,
        });

        functions.logger.info(`[smtp] sent uid=${uid} to=${to} subject="${subject}"`);
    } catch (err: any) {
        functions.logger.warn(`[smtp] fail uid=${uid} err=${err?.message || err}`);
    }
}

/* =========================
   Push helpers
========================= */

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
    for (const k of Object.keys(data || {})) {
        dataPayload[k] = asString((data as Record<string, unknown>)[k]);
    }

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

/** Push + Email juntos (email só se smtp.enabled=1) */
async function notifyUser(
    uid: string,
    title: string,
    body: string,
    data: Record<string, string>
): Promise<void> {
    await Promise.allSettled([
        sendPushToUser(uid, title, body, data),
        sendEmailToUser(uid, title, body),
    ]);
}

/* =========================
   LIKE (v1)
   RTDB: likes/{fromUid}/{targetUid} = true
========================= */
export const onLikeReceived = functions
    .region(REGION)
    .database.ref("/likes/{fromUid}/{targetUid}")
    .onCreate(async (_snap, context) => {
        const fromUid = context.params.fromUid;
        const targetUid = context.params.targetUid;

        functions.logger.info(`[like] onCreate from=${fromUid} target=${targetUid}`);

        if (!fromUid || !targetUid || fromUid === targetUid) return null;

        const fromName = await getDisplayName(fromUid);

        await notifyUser(targetUid, "Nova curtida", `${fromName} curtiu você.`, {
            type: "like",
            fromUid,
        });

        return null;
    });

/* =========================
   MATCH (v1)
   RTDB: matches/{uid}/{otherUid} = true (dos dois lados)
   Dedup: uid < otherUid
========================= */
export const onMatchCreated = functions
    .region(REGION)
    .database.ref("/matches/{uid}/{otherUid}")
    .onCreate(async (_snap, context) => {
        const uid = context.params.uid;
        const otherUid = context.params.otherUid;

        functions.logger.info(`[match] onCreate uid=${uid} other=${otherUid}`);

        if (!uid || !otherUid || uid === otherUid) return null;

        if (uid >= otherUid) {
            functions.logger.info(`[match] skip duplicate side uid=${uid} other=${otherUid}`);
            return null;
        }

        const nameA = await getDisplayName(uid);
        const nameB = await getDisplayName(otherUid);

        await Promise.all([
            notifyUser(uid, "É um match!", `Você e ${nameB} combinaram.`, {
                type: "match",
                fromUid: otherUid,
            }),
            notifyUser(otherUid, "É um match!", `Você e ${nameA} combinaram.`, {
                type: "match",
                fromUid: uid,
            }),
        ]);

        return null;
    });

/* =========================
   FRIEND REQUEST (v1)
   RTDB: friendRequests/{targetUid}/{fromUid} = true
========================= */
export const onFriendRequestCreated = functions
    .region(REGION)
    .database.ref("/friendRequests/{targetUid}/{fromUid}")
    .onCreate(async (_snap, context) => {
        const targetUid = context.params.targetUid;
        const fromUid = context.params.fromUid;

        functions.logger.info(`[friendRequest] onCreate from=${fromUid} target=${targetUid}`);

        if (!fromUid || !targetUid || fromUid === targetUid) return null;

        const fromName = await getDisplayName(fromUid);

        await notifyUser(targetUid, "Solicitação de amizade", `${fromName} enviou uma solicitação.`, {
            type: "friend_request",
            fromUid,
        });

        return null;
    });

/* =========================
   NEW MESSAGE (v1)
   RTDB: chatMessages/{chatId}/{messageId} = { senderId, ... }
========================= */
type ChatMessage = {
    senderId?: string;
    receiverId?: string;
    text?: string;
    message?: string;
    body?: string;
    type?: string;
};

function inferRecipientUid(chatId: string, senderId: string, msg: ChatMessage): string | null {
    const receiverId = asString(msg.receiverId).trim();
    if (receiverId) return receiverId;

    const parts = chatId.split("_").map((s) => s.trim()).filter(Boolean);
    if (parts.length === 2) {
        const [a, b] = parts;
        if (senderId === a) return b;
        if (senderId === b) return a;
    }
    return null;
}

function buildMessagePreview(msg: ChatMessage): string {
    const t = asString(msg.text || msg.message || msg.body).trim();
    if (t) return t;

    const kind = asString(msg.type).trim().toLowerCase();
    if (kind === "image" || kind === "photo") return "Foto";
    if (kind === "audio" || kind === "voice") return "Áudio";
    if (kind === "video") return "Vídeo";
    if (kind === "file") return "Arquivo";

    return "Você recebeu uma nova mensagem.";
}

export const onChatMessageCreated = functions
    .region(REGION)
    .database.ref("/chatMessages/{chatId}/{messageId}")
    .onCreate(async (snap, context) => {
        const chatId = context.params.chatId;
        const messageId = context.params.messageId;

        const raw = snap.val() as unknown;
        const msg = (raw && typeof raw === "object" ? (raw as ChatMessage) : {}) as ChatMessage;

        const senderId = asString(msg.senderId).trim();

        functions.logger.info(`[chat] onCreate chatId=${chatId} messageId=${messageId} sender=${senderId}`);

        if (!chatId || !messageId || !senderId) return null;

        const recipientUid = inferRecipientUid(chatId, senderId, msg);
        if (!recipientUid) {
            functions.logger.warn(`[chat] recipientUid not inferred chatId=${chatId} sender=${senderId}`);
            return null;
        }
        if (recipientUid === senderId) return null;

        const fromName = await getDisplayName(senderId);
        const preview = buildMessagePreview(msg);

        await notifyUser(recipientUid, fromName, preview, {
            type: "message",
            fromUid: senderId,
            chatId,
            messageId,
        });

        return null;
    });
