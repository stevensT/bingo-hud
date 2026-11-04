#!/usr/bin/env node
'use strict';

// Spike script for specs/codex-quota/spikes/codex-endpoint.md, deleted when that spike closes.
// Asks Codex's usage endpoint for the account's rate limits, using the sign-in Codex keeps in
// auth.json, and prints the windows. The access token and account id are read into memory, used
// for the one request, and never printed, logged, or written to disk. auth.json is only read;
// nothing here refreshes the token, because refresh tokens are single-use and a refresh Codex
// did not make would sign Codex out.
//
//   node scripts/capture-codex-usage.js

const fs = require('fs');
const os = require('os');
const path = require('path');

const ENDPOINT = 'https://chatgpt.com/backend-api/wham/usage';
const USER_AGENT = 'bingo-hud/0.5.0';

function codexHome() {
  return process.env.CODEX_HOME || path.join(os.homedir(), '.codex');
}

// Reads the expiry and issue time from the JWT's payload. Nothing else in it is returned,
// since the payload also carries the account's identity.
function jwtTimes(token) {
  try {
    const payload = JSON.parse(Buffer.from(token.split('.')[1], 'base64url').toString('utf8'));
    return { iat: payload.iat, exp: payload.exp };
  } catch (e) {
    return {};
  }
}

function describeAge(ms) {
  const minutes = Math.round(Math.abs(ms) / 60000);
  const text = minutes < 120 ? minutes + ' min' : (minutes / 60).toFixed(1) + ' h';
  return ms >= 0 ? 'in ' + text : text + ' ago';
}

function describeTime(unixSeconds) {
  if (typeof unixSeconds !== 'number') return 'absent';
  const ms = unixSeconds * 1000;
  return new Date(ms).toLocaleString() + ' (' + describeAge(ms - Date.now()) + ')';
}

// Key paths and value types only, so an error body can be described without quoting it.
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

function printWindow(name, w) {
  if (!w) {
    console.log('  ' + name + ': absent');
    return;
  }
  const hours = typeof w.limit_window_seconds === 'number'
    ? (w.limit_window_seconds / 3600) + ' h window'
    : 'window length absent';
  const left = typeof w.used_percent === 'number' ? 100 - w.used_percent : '?';
  console.log('  ' + name + ': ' + w.used_percent + '% used (' + left + '% left), ' + hours
    + ', resets ' + describeTime(w.reset_at));
}

async function main() {
  const authPath = path.join(codexHome(), 'auth.json');
  if (!fs.existsSync(authPath)) {
    console.error('No auth.json at ' + authPath + '. Sign in to Codex first.');
    process.exit(2);
  }
  const writtenBefore = fs.statSync(authPath).mtime;

  let auth;
  try {
    auth = JSON.parse(fs.readFileSync(authPath, 'utf8'));
  } catch (e) {
    console.error('auth.json is not valid JSON.');
    process.exit(2);
  }
  const accessToken = auth.tokens && auth.tokens.access_token;
  const accountId = auth.tokens && auth.tokens.account_id;
  if (typeof accessToken !== 'string' || !accessToken) {
    console.error('No tokens.access_token in auth.json. Is Codex signed in with ChatGPT?');
    process.exit(2);
  }

  const times = jwtTimes(accessToken);
  console.log('Run at            : ' + new Date().toLocaleString());
  console.log('Machine           : ' + os.hostname());
  console.log('auth.json written : ' + writtenBefore.toLocaleString());
  console.log('last_refresh      : ' + (auth.last_refresh
    ? new Date(auth.last_refresh).toLocaleString() + ' (' + describeAge(new Date(auth.last_refresh) - Date.now()) + ')'
    : 'absent'));
  console.log('Token issued      : ' + describeTime(times.iat));
  console.log('Token expires     : ' + describeTime(times.exp));
  console.log('Account id        : ' + (accountId ? 'present (not shown)' : 'absent'));
  console.log('User-Agent        : ' + USER_AGENT);
  console.log('');

  const headers = {
    Authorization: 'Bearer ' + accessToken,
    Accept: 'application/json',
    'User-Agent': USER_AGENT,
  };
  if (accountId) headers['chatgpt-account-id'] = accountId;

  let res, bodyText;
  try {
    res = await fetch(ENDPOINT, { headers });
    bodyText = await res.text();
  } catch (e) {
    console.error('Network failure: ' + e.message);
    process.exit(3);
  }

  console.log('HTTP ' + res.status + ' ' + res.statusText);

  let body = null;
  try {
    body = JSON.parse(bodyText);
  } catch (e) {
    console.log('Body is not JSON (' + Buffer.byteLength(bodyText, 'utf8') + ' bytes, not shown).');
  }

  if (body && res.ok) {
    const limit = body.rate_limit || {};
    console.log('Plan              : ' + body.plan_type);
    console.log('Allowed           : ' + limit.allowed + ', limit reached: ' + limit.limit_reached);
    printWindow('primary  ', limit.primary_window);
    printWindow('secondary', limit.secondary_window);
    for (const extra of body.additional_rate_limits || []) {
      // Per-model limits. Their shape is not confirmed yet; the response shape below shows it.
      console.log('  additional limit present: ' + Object.keys(extra).join(', '));
    }
  }
  if (body) {
    console.log('');
    console.log('Response shape (key paths and types only):');
    for (const line of shapeLines(body)) console.log('  ' + line);
  }

  const writtenAfter = fs.statSync(authPath).mtime;
  console.log('');
  console.log('auth.json unchanged by this run: ' + (writtenAfter.getTime() === writtenBefore.getTime()));
}

main();
