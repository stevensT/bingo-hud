#!/usr/bin/env node
'use strict';

// Spike script for specs/codex-quota/spikes/session-log-probe.md. Throwaway: deleted when the
// spike closes, with its result recorded in that document.
//
// Reads the rate-limit records Codex writes into its own session logs and prints a summary of
// their shapes, values, gaps, and reset behaviour. Read-only; writes no files.
//
//   node scripts/codex-ratelimit-probe.js
//
// The logs hold the user's conversations. A line is parsed only if it looks like a session
// header or a token count, and only these fields are kept from it: the line's timestamp, the
// rate_limits object, and the header's cli_version and originator. Nothing else is printed.
// The rate_limits object can carry a plan type, which is about the account rather than the
// conversation; it is reported because a change in it would explain a change in the windows.

const fs = require('fs');
const os = require('os');
const path = require('path');

const SECONDS_MILLIS_BOUNDARY = 10000000000;
const WINDOWS = ['primary', 'secondary'];

const codexHome = process.env.CODEX_HOME || path.join(os.homedir(), '.codex');
const logFolders = ['sessions', 'archived_sessions'].map((name) => path.join(codexHome, name));
const now = new Date();

// --- reading -----------------------------------------------------------------------------

function findLogs(folder) {
  if (!fs.existsSync(folder)) {
    return [];
  }
  const found = [];
  for (const entry of fs.readdirSync(folder, { withFileTypes: true })) {
    const full = path.join(folder, entry.name);
    if (entry.isDirectory()) {
      found.push(...findLogs(full));
    } else if (/^rollout-.*\.jsonl$/.test(entry.name)) {
      found.push(full);
    }
  }
  return found;
}

// A cheap text test first, so conversation lines are never parsed at all. The structural test
// after parsing is what decides: a conversation can contain the words "rate_limits" or
// "session_meta" without being either record.
function readLog(file) {
  const log = { file: path.basename(file), originator: null, cliVersion: null, records: [] };
  const lines = fs.readFileSync(file, 'utf8').split('\n');

  for (const line of lines) {
    const mayBeHeader = line.includes('"session_meta"');
    const mayBeCount = line.includes('"token_count"') && line.includes('"rate_limits"');
    if (!mayBeHeader && !mayBeCount) {
      continue;
    }

    let parsed;
    try {
      parsed = JSON.parse(line);
    } catch {
      continue;
    }
    const payload = parsed && parsed.payload;
    if (!payload) {
      continue;
    }

    if (parsed.type === 'session_meta') {
      log.originator = typeof payload.originator === 'string' ? payload.originator : null;
      log.cliVersion = typeof payload.cli_version === 'string' ? payload.cli_version : null;
    } else if (parsed.type === 'event_msg' && payload.type === 'token_count' && 'rate_limits' in payload) {
      log.records.push({
        at: new Date(parsed.timestamp),
        limits: payload.rate_limits,
        file: log.file,
      });
    }
  }
  return log;
}

// --- describing ----------------------------------------------------------------------------

// The shape of a value: key names and value types, nested, with keys sorted so that two
// records differing only in key order count as one shape.
function shapeOf(value) {
  if (value === null) return 'null';
  if (Array.isArray(value)) return `[${value.map(shapeOf).join(',')}]`;
  if (typeof value === 'object') {
    const keys = Object.keys(value).sort();
    return `{${keys.map((k) => `${k}:${shapeOf(value[k])}`).join(',')}}`;
  }
  return typeof value;
}

function resetTime(window) {
  if (!window || typeof window.resets_at !== 'number') return null;
  const seconds = window.resets_at > SECONDS_MILLIS_BOUNDARY ? window.resets_at / 1000 : window.resets_at;
  return new Date(seconds * 1000);
}

function local(date) {
  return date ? date.toLocaleString() : '(none)';
}

function age(from) {
  const minutes = Math.floor((now - from) / 60000);
  if (minutes < 60) return `${minutes}m`;
  const hours = minutes / 60;
  if (hours < 48) return `${hours.toFixed(1)}h`;
  return `${(hours / 24).toFixed(1)}d`;
}

function count(map, key, extra) {
  const entry = map.get(key) || { count: 0, extra: new Set() };
  entry.count += 1;
  if (extra) entry.extra.add(extra);
  map.set(key, entry);
}

function heading(text) {
  console.log(`\n== ${text} ==`);
}

// --- report --------------------------------------------------------------------------------

const files = logFolders.flatMap(findLogs);
const logs = files.map(readLog);
const records = logs
  .flatMap((log) => log.records.map((r) => ({ ...r, version: `${log.originator} ${log.cliVersion}` })))
  .filter((r) => !Number.isNaN(r.at.getTime()))
  .sort((a, b) => a.at - b.at);

console.log(`Codex rate-limit probe, run ${local(now)}`);
console.log(`Logs read from ${codexHome}: ${files.length} files, ${records.length} rate-limit records`);

heading('Logs by originator and version');
const byVersion = new Map();
for (const log of logs) {
  const key = `${log.originator} ${log.cliVersion}`;
  const entry = byVersion.get(key) || { files: 0, records: 0 };
  entry.files += 1;
  entry.records += log.records.length;
  byVersion.set(key, entry);
}
for (const [key, entry] of byVersion) {
  console.log(`  ${key}: ${entry.files} files, ${entry.records} records`);
}

heading('Shapes of the rate_limits object');
const shapes = new Map();
for (const r of records) count(shapes, shapeOf(r.limits), r.version);
let shapeNumber = 0;
for (const [shape, entry] of shapes) {
  shapeNumber += 1;
  console.log(`  Shape ${shapeNumber}: ${entry.count} records, from ${[...entry.extra].join('; ')}`);
  console.log(`    ${shape}`);
}

heading('Values seen');
const values = new Map();
for (const r of records) {
  const l = r.limits || {};
  count(values, `limit_id = ${JSON.stringify(l.limit_id)}`);
  count(values, `limit_name = ${JSON.stringify(l.limit_name)}`);
  count(values, `plan_type = ${JSON.stringify(l.plan_type)}`);
  count(values, `rate_limit_reached_type = ${JSON.stringify(l.rate_limit_reached_type)}`);
  for (const name of WINDOWS) {
    count(values, `${name}.window_minutes = ${JSON.stringify(l[name] && l[name].window_minutes)}`);
  }
}
for (const [value, entry] of [...values].sort()) console.log(`  ${value}: ${entry.count}`);

heading('Records missing something');
let gaps = 0;
for (const r of records) {
  const problems = [];
  if (!r.limits) problems.push('rate_limits is null');
  for (const name of WINDOWS) {
    const w = r.limits && r.limits[name];
    if (!w) problems.push(`${name} missing`);
    else {
      if (typeof w.used_percent !== 'number') problems.push(`${name}.used_percent not a number`);
      if (typeof w.resets_at !== 'number') problems.push(`${name}.resets_at not a number`);
      else if (w.resets_at > SECONDS_MILLIS_BOUNDARY) problems.push(`${name}.resets_at looks like milliseconds`);
    }
  }
  if (problems.length) {
    gaps += 1;
    console.log(`  ${local(r.at)} ${r.file}: ${problems.join(', ')}`);
  }
}
if (!gaps) console.log('  none');

heading('Records per log');
for (const log of logs) {
  const times = log.records.map((r) => r.at).filter((t) => !Number.isNaN(t.getTime())).sort((a, b) => a - b);
  if (!times.length) {
    console.log(`  ${log.file}: no records`);
    continue;
  }
  let longest = 0;
  for (let i = 1; i < times.length; i += 1) longest = Math.max(longest, times[i] - times[i - 1]);
  console.log(`  ${log.file}: ${times.length} records, ${local(times[0])} to ${local(times[times.length - 1])}, ` +
    `longest gap ${(longest / 60000).toFixed(1)}m`);
}

// For each window and each reset time it reported: the last figure taken before that reset,
// and the first taken after it. A reset field that can be trusted shows the figure falling,
// and the record after it naming a later reset.
heading('Behaviour across resets');
let resetsSeen = 0;
for (const name of WINDOWS) {
  const resetTimes = new Map();
  for (const r of records) {
    const t = resetTime(r.limits && r.limits[name]);
    if (t) resetTimes.set(t.getTime(), t);
  }
  for (const t of [...resetTimes.values()].sort((a, b) => a - b)) {
    if (t > now) continue;
    resetsSeen += 1;
    const before = records.filter((r) => r.at < t && r.limits && r.limits[name]).pop();
    const after = records.find((r) => r.at >= t && r.limits && r.limits[name]);
    const beforeText = before ? `${before.limits[name].used_percent}% at ${local(before.at)}` : 'no record before';
    if (!after) {
      console.log(`  ${name} reset ${local(t)}: ${beforeText}; no record after it yet`);
      continue;
    }
    const w = after.limits[name];
    const fell = before ? (w.used_percent < before.limits[name].used_percent ? 'FELL' : 'DID NOT FALL') : '';
    const nextReset = resetTime(w);
    const moved = nextReset && nextReset > t ? 'next reset later' : 'NEXT RESET NOT LATER';
    console.log(`  ${name} reset ${local(t)}: ${beforeText}; then ${w.used_percent}% at ${local(after.at)} — ${fell}, ${moved}`);
  }
}
if (!resetsSeen) console.log('  no reset has passed yet');

// Principle 6: a window whose reset has passed since this reading gets no figure, only the
// fact that it has reset.
heading('Latest reading');
const latest = records[records.length - 1];
if (!latest || !latest.limits) {
  console.log('  none');
} else {
  console.log(`  taken ${local(latest.at)}, ${age(latest.at)} ago, ${latest.version}, limit_id ${JSON.stringify(latest.limits.limit_id)}`);
  for (const name of WINDOWS) {
    const w = latest.limits[name];
    const t = resetTime(w);
    if (!w) console.log(`  ${name}: not in this record`);
    else if (t && t <= now) console.log(`  ${name}: reset at ${local(t)}, since this reading; no current figure`);
    else console.log(`  ${name} (${w.window_minutes}m): ${w.used_percent}% used, resets ${local(t)}`);
  }
}
