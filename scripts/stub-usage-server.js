// Throwaway stand-in for the usage endpoint, for forcing the error states on screen.
// See specs/quota-hud/spikes/error-states-onscreen.md. Deleted in the commit after the one that
// records that spike's result.
// Needs BINGO_CREDENTIALS_PATH set as well as the endpoint; a Debug build refuses the endpoint
// override on its own.
//
// Usage: node scripts/stub-usage-server.js <mode-file>
// Point a Debug build at it with BINGO_USAGE_ENDPOINT=http://localhost:8765/usage.
// The mode file is re-read on every request, so the state can change while the app runs:
//   ok          200, the baseline capture with its reset times moved into the future
//   warning     200, as ok with the session window at 80% used
//   critical    200, as ok with the session window at 95% used
//   rejected    200, as ok with the session window at 100% and the server's "rejected"
//   unreadable  200, a body naming no window
//   scoped      200, only a per-model window
//   401         401, the recorded authentication_error body
//   404         404, an endpoint this account cannot use
//   500         500, a transient server failure

const fs = require('fs');
const http = require('http');
const path = require('path');

const modeFile = process.argv[2];
if (!modeFile) {
  console.error('usage: node scripts/stub-usage-server.js <mode-file>');
  process.exit(1);
}

const fixtures = path.join(__dirname, '..', 'tests', 'fixtures', 'usage');
const read = (name) => fs.readFileSync(path.join(fixtures, name), 'utf8');
const inHours = (h) => new Date(Date.now() + h * 3600e3).toISOString();

function baseline(session = {}) {
  const body = JSON.parse(read('2026-08-30-baseline.json'));
  for (const limit of body.limits) {
    limit.resets_at = limit.kind === 'session' ? inHours(3) : inHours(72);
    if (limit.kind === 'session') Object.assign(limit, session);
  }
  return JSON.stringify(body);
}

const answers = {
  ok: () => [200, baseline()],
  warning: () => [200, baseline({ percent: 80 })],
  critical: () => [200, baseline({ percent: 95 })],
  rejected: () => [200, baseline({ percent: 100, severity: 'rejected' })],
  unreadable: () => [200, JSON.stringify({ tangelo: null })],
  scoped: () => [200, JSON.stringify({
    limits: [{
      kind: 'weekly_scoped', group: 'weekly', percent: 40, severity: 'normal',
      resets_at: inHours(72), scope: 'claude-opus', is_active: true,
    }],
  })],
  401: () => [401, read('2026-08-30-auth-failure.json')],
  404: () => [404, '{"error":{"type":"not_found_error"}}'],
  500: () => [500, '{"error":{"type":"api_error"}}'],
};

http.createServer((req, res) => {
  const mode = fs.readFileSync(modeFile, 'utf8').trim();
  const answer = answers[mode];
  const [status, body] = answer ? answer() : [500, `unknown mode ${mode}`];
  console.log(new Date().toISOString(), req.method, req.url, mode, status);
  res.writeHead(status, { 'content-type': 'application/json' });
  res.end(body);
}).listen(8765, '127.0.0.1', () => console.log('stub listening on http://localhost:8765/usage'));
