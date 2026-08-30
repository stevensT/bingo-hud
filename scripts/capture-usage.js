#!/usr/bin/env node
'use strict';

// Captures a live response from the undocumented quota endpoint and writes a scrubbed
// fixture for the parser tests. The access token is read into memory, used for the one
// request, and never printed, logged, or written to disk.
//
//   node scripts/capture-usage.js --label five-hour-normal
//   node scripts/capture-usage.js --label rate-limited --raw-dir /some/scratch/dir
//
// Re-run this whenever the contract test fails. A failing contract test means the payload
// moved; the fix is a fresh fixture, never a looser parser.

const fs = require('fs');
const os = require('os');
const path = require('path');
const { execSync } = require('child_process');

const ENDPOINT = 'https://api.anthropic.com/api/oauth/usage';
const FALLBACK_USER_AGENT = 'claude-code/2.1.83';
const SECONDS_MILLIS_BOUNDARY = 10000000000;

function arg(name, fallback) {
  const i = process.argv.indexOf(name);
  return i >= 0 && process.argv[i + 1] ? process.argv[i + 1] : fallback;
}

const label = arg('--label', 'capture');
const outDir = arg('--out', path.join(process.cwd(), 'tests', 'fixtures', 'usage'));
const rawDir = arg('--raw-dir', null);

// Sends a deliberately invalid bearer token instead of the real one, to record the shape of
// an auth failure. The error taxonomy depends on that body, and it cannot be observed by
// waiting for a real token to expire.
const authFailureProbe = process.argv.includes('--auth-failure-probe');

// --- credentials -----------------------------------------------------------------------

function findAccessToken(node, depth) {
  depth = depth || 0;
  if (!node || typeof node !== 'object' || depth > 6) return null;
  for (const [k, v] of Object.entries(node)) {
    if (/^access_?token$/i.test(k) && typeof v === 'string' && v.length > 0) {
      return { token: v, container: node, path: k };
    }
  }
  for (const [k, v] of Object.entries(node)) {
    if (v && typeof v === 'object') {
      const found = findAccessToken(v, depth + 1);
      if (found) return { token: found.token, container: found.container, path: k + '.' + found.path };
    }
  }
  return null;
}

function describeExpiry(container) {
  const raw = container.expiresAt !== undefined ? container.expiresAt : container.expires_at;
  if (typeof raw !== 'number') return 'expiresAt: absent or non-numeric';
  const storedIn = raw < SECONDS_MILLIS_BOUNDARY ? 'seconds' : 'milliseconds';
  const ms = raw < SECONDS_MILLIS_BOUNDARY ? raw * 1000 : raw;
  const delta = ms - Date.now();
  const state = delta > 0
    ? 'valid for ' + Math.round(delta / 60000) + ' min'
    : 'EXPIRED ' + Math.round(-delta / 60000) + ' min ago';
  return 'expiresAt: stored in ' + storedIn + ' -> ' + new Date(ms).toISOString() + ' (' + state + ')';
}

// --- scrubbing -------------------------------------------------------------------------

const EMAIL = /[\w.+-]+@[\w-]+\.[\w.-]+/;
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const OPAQUE = /^[A-Za-z0-9_-]{24,}$/;
const SENSITIVE_KEY = /(email|token|secret|api_?key|account|organi[sz]ation|^org|user|uuid|customer|subscriber|session)/i;
const SAFE_ENUM = /^[a-z][a-z0-9_]{0,23}$/;

function scrubScalar(key, value) {
  if (typeof value !== 'string') return value;
  if (EMAIL.test(value)) return '<redacted:email>';
  if (UUID.test(value)) return '<redacted:uuid>';
  if (SENSITIVE_KEY.test(key) && !SAFE_ENUM.test(value)) return '<redacted>';
  if (OPAQUE.test(value) && !SAFE_ENUM.test(value)) return '<redacted:opaque>';
  return value;
}

function scrub(node, key) {
  key = key || '';
  if (Array.isArray(node)) return node.map((v) => scrub(v, key));
  if (node && typeof node === 'object') {
    const out = {};
    for (const [k, v] of Object.entries(node)) out[k] = scrub(v, k);
    return out;
  }
  return scrubScalar(key, node);
}

// Key paths and value types only. Safe to paste anywhere.
function shapeLines(node) {
  const lines = [];
  const walk = (n, p) => {
    if (Array.isArray(n)) {
      lines.push(p + '  array(' + n.length + ')');
      if (n.length) walk(n[0], p + '[0]');
    } else if (n && typeof n === 'object') {
      const entries = Object.entries(n);
      if (!entries.length) lines.push(p + '  object(empty)');
      for (const [k, v] of entries) walk(v, p ? p + '.' + k : k);
    } else {
      lines.push(p + '  ' + (n === null ? 'null' : typeof n));
    }
  };
  walk(node, '');
  return lines;
}

// Dropped outright: these can carry credential material.
const DROP_HEADERS = /^(set-cookie|authorization|proxy-authorization)$/i;

// Kept as a key with the value redacted, so a fixture still records that the header was
// present without tying the capture to an account, workspace, or single request. This
// repository is public.
const REDACT_HEADERS = /(organi[sz]ation|workspace|account|user|request|correlation|trace)[-_]?id$|^cf-ray$/i;

// --- main ------------------------------------------------------------------------------

async function main() {
  const credPath = path.join(os.homedir(), '.claude', '.credentials.json');
  if (!fs.existsSync(credPath)) {
    console.error('No credentials file at ' + credPath + '. Sign in with Claude Code first.');
    process.exit(2);
  }

  let creds;
  try {
    creds = JSON.parse(fs.readFileSync(credPath, 'utf8'));
  } catch (e) {
    console.error('Credentials file is not valid JSON: ' + e.message);
    process.exit(2);
  }

  const found = findAccessToken(creds);
  if (!found) {
    console.error('No accessToken found in the credentials file.');
    console.error('Key paths present (names only, no values):');
    for (const line of shapeLines(scrub(creds))) console.error('  ' + line);
    process.exit(2);
  }

  let userAgent = FALLBACK_USER_AGENT;
  try {
    const v = execSync('claude --version', {
      encoding: 'utf8',
      timeout: 15000,
      stdio: ['ignore', 'pipe', 'ignore'],
    });
    const m = v.match(/\d+\.\d+\.\d+/);
    if (m) userAgent = 'claude-code/' + m[0];
  } catch (e) {
    // Fall through to the pinned default.
  }

  console.log('Credential source : ' + credPath);
  console.log('Token found at    : ' + found.path + ' (value not shown)');
  console.log('Token length      : ' + found.token.length + ' chars');
  console.log(describeExpiry(found.container));
  console.log('User-Agent        : ' + userAgent);
  console.log('');

  const bearer = authFailureProbe ? 'sk-ant-oat01-not-a-real-token' : found.token;
  if (authFailureProbe) {
    console.log('MODE: auth-failure probe — sending an invalid token, not yours.');
    console.log('');
  }

  let res, bodyText;
  try {
    res = await fetch(ENDPOINT, {
      headers: {
        Authorization: 'Bearer ' + bearer,
        'anthropic-beta': 'oauth-2025-04-20',
        Accept: 'application/json',
        'User-Agent': userAgent,
      },
    });
    bodyText = await res.text();
  } catch (e) {
    console.error('Network failure: ' + e.message);
    process.exit(3);
  }

  const headers = {};
  for (const [k, v] of res.headers.entries()) {
    if (DROP_HEADERS.test(k)) continue;
    headers[k] = REDACT_HEADERS.test(k) ? '<redacted>' : scrubScalar(k, v);
  }

  const now = new Date();
  const day = now.toISOString().slice(0, 10);
  const stamp = now.toISOString().replace(/[:.]/g, '-');

  if (rawDir) {
    fs.mkdirSync(rawDir, { recursive: true });
    const rawFile = path.join(rawDir, stamp + '-' + label + '.raw.txt');
    fs.writeFileSync(rawFile, bodyText, 'utf8');
    console.log('Raw body (UNSCRUBBED, keep out of the repo): ' + rawFile);
  }

  fs.mkdirSync(outDir, { recursive: true });

  let parsed = null;
  try {
    parsed = JSON.parse(bodyText);
  } catch (e) {
    // A non-JSON body is itself a finding worth keeping.
  }

  const bodyFile = path.join(outDir, day + '-' + label + (parsed ? '.json' : '.txt'));
  const scrubbed = parsed ? scrub(parsed) : bodyText;
  fs.writeFileSync(bodyFile, parsed ? JSON.stringify(scrubbed, null, 2) + '\n' : bodyText, 'utf8');

  const metaFile = path.join(outDir, day + '-' + label + '.meta.json');
  fs.writeFileSync(metaFile, JSON.stringify({
    capturedAt: now.toISOString(),
    endpoint: ENDPOINT,
    userAgent: userAgent,
    status: res.status,
    statusText: res.statusText,
    bodyBytes: Buffer.byteLength(bodyText, 'utf8'),
    bodyIsJson: parsed !== null,
    headers: headers,
  }, null, 2) + '\n', 'utf8');

  console.log('HTTP ' + res.status + ' ' + res.statusText + '  (' + Buffer.byteLength(bodyText, 'utf8') + ' bytes)');
  console.log('Scrubbed fixture  : ' + bodyFile);
  console.log('Capture metadata  : ' + metaFile);
  console.log('');
  console.log('Response shape (key paths and types only):');
  if (parsed) {
    for (const line of shapeLines(parsed)) console.log('  ' + line);
  } else {
    console.log('  body did not parse as JSON');
  }
}

main();
