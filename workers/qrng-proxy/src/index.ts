type AnchorPayload = {
  genesisHash: string;
  headHash: string;
  headSeq: number;
  protocolVersion: string;
};

type AnchorRecord = AnchorPayload & {
  receivedAt: string;
};

type KvKey = { name: string };
type AnchorKv = {
  get(key: string): Promise<string | null>;
  put(key: string, value: string): Promise<void>;
  list(options: { prefix: string }): Promise<{ keys: KvKey[] }>;
};

export type WorkerEnv = {
  ANCHOR_LOG: AnchorKv;
};

type WorkerClock = () => string;

const HASH_RE = /^[0-9a-f]{64}$/;
const ANCHOR_KEYS = ['genesisHash', 'headHash', 'headSeq', 'protocolVersion'] as const;

function json(value: unknown, status = 200): Response {
  return new Response(JSON.stringify(value), {
    status,
    headers: {
      'content-type': 'application/json; charset=utf-8',
      'cache-control': 'no-store',
      'access-control-allow-origin': '*',
    },
  });
}

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function parseAnchor(value: unknown): AnchorPayload | null {
  if (!isObject(value)) return null;
  const keys = Object.keys(value).sort();
  const expected = [...ANCHOR_KEYS].sort();
  if (keys.length !== expected.length || keys.some((key, index) => key !== expected[index])) return null;
  if (typeof value.genesisHash !== 'string' || !HASH_RE.test(value.genesisHash)) return null;
  if (typeof value.headHash !== 'string' || !HASH_RE.test(value.headHash)) return null;
  if (!Number.isInteger(value.headSeq) || (value.headSeq as number) < 1) return null;
  if (value.protocolVersion !== '2.1') return null;
  return {
    genesisHash: value.genesisHash,
    headHash: value.headHash,
    headSeq: value.headSeq as number,
    protocolVersion: value.protocolVersion,
  };
}

function anchorKey(payload: AnchorPayload): string {
  return `${payload.genesisHash}:${String(payload.headSeq).padStart(12, '0')}:${payload.headHash}`;
}

async function postAnchor(request: Request, env: WorkerEnv, now: WorkerClock): Promise<Response> {
  let raw: unknown;
  try {
    raw = await request.json();
  } catch {
    return json({ error: 'invalid_json' }, 400);
  }
  const payload = parseAnchor(raw);
  if (!payload) {
    return json({ error: 'invalid_anchor_payload', allowedKeys: ANCHOR_KEYS }, 400);
  }

  const key = anchorKey(payload);
  const existing = await env.ANCHOR_LOG.get(key);
  if (existing !== null) {
    return json({ ok: true, idempotent: true, anchor: JSON.parse(existing) });
  }

  const record: AnchorRecord = { ...payload, receivedAt: now() };
  await env.ANCHOR_LOG.put(key, JSON.stringify(record));
  return json({ ok: true, idempotent: false, anchor: record }, 201);
}

async function getAnchors(genesisHash: string, env: WorkerEnv): Promise<Response> {
  if (!HASH_RE.test(genesisHash)) return json({ error: 'invalid_genesis_hash' }, 400);
  const listed = await env.ANCHOR_LOG.list({ prefix: `${genesisHash}:` });
  const anchors: AnchorRecord[] = [];
  for (const key of listed.keys) {
    const value = await env.ANCHOR_LOG.get(key.name);
    if (value !== null) anchors.push(JSON.parse(value) as AnchorRecord);
  }
  anchors.sort((a, b) => a.headSeq - b.headSeq || a.receivedAt.localeCompare(b.receivedAt));
  return json({
    genesisHash,
    description: 'External chain-head anchors make later rewrites detectable for anchored periods; they do not make a local ledger tamper-proof.',
    anchors,
  });
}

export function createWorker(now: WorkerClock = () => new Date().toISOString()) {
  return {
    async fetch(request: Request, env: WorkerEnv): Promise<Response> {
      const url = new URL(request.url);
      if (request.method === 'OPTIONS') {
        return new Response(null, {
          status: 204,
          headers: {
            'access-control-allow-origin': '*',
            'access-control-allow-methods': 'GET,POST,OPTIONS',
            'access-control-allow-headers': 'content-type',
          },
        });
      }
      if (request.method === 'GET' && url.pathname === '/health') return json({ ok: true });
      if (request.method === 'POST' && url.pathname === '/anchors') return postAnchor(request, env, now);
      const match = /^\/anchors\/([0-9a-f]{64})$/.exec(url.pathname);
      if (request.method === 'GET' && match?.[1]) return getAnchors(match[1], env);
      return json({ error: 'not_found' }, 404);
    },
  };
}

export default createWorker();
