import { createRequire } from 'node:module';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const projectRoot = path.resolve(scriptDir, '..', '..');

// Reuse the 'ws' module vendored by the MCP Unity package so this tool needs no
// dependencies of its own. On a fresh clone node_modules may be missing until
// Unity runs the server install, so fail with an actionable message instead of
// a raw MODULE_NOT_FOUND stack trace.
let WebSocket;
try {
  WebSocket = require(path.join(
    projectRoot,
    'Packages',
    'com.gamelovers.mcp-unity',
    'Server~',
    'node_modules',
    'ws',
  ));
} catch (error) {
  console.error(
    "Could not load the 'ws' module from the MCP Unity package.\n" +
    'Open the project in Unity once (it installs the Node server automatically), ' +
    "or run 'npm install' in Packages/com.gamelovers.mcp-unity/Server~.\n" +
    `Underlying error: ${error.message}`,
  );
  process.exit(2);
}

const options = {
  host: process.env.UNITY_HOST || '127.0.0.1',
  port: Number(process.env.UNITY_PORT || 8090),
  timeout: Number(process.env.UNITY_REQUEST_TIMEOUT_MS || 60000),
  client: process.env.UNITY_CLIENT_NAME || 'Codex Direct Unity WS',
};

const positional = [];
for (let index = 2; index < process.argv.length; index += 1) {
  const arg = process.argv[index];
  if (arg === '--host') {
    options.host = process.argv[++index];
  } else if (arg === '--port') {
    options.port = Number(process.argv[++index]);
  } else if (arg === '--timeout') {
    options.timeout = Number(process.argv[++index]);
  } else if (arg === '--client') {
    options.client = process.argv[++index];
  } else {
    positional.push(arg);
  }
}

const [method, paramsJson = '{}'] = positional;
if (!method) {
  console.error(
    'Usage: node .claude/tools/unity-ws-call.mjs <method> [paramsJson|-|@file] [--timeout ms]\n' +
    'Waits out a bridge restart (Play mode, domain reload) for UNITY_RECONNECT_WINDOW_MS (default 30000).',
  );
  process.exit(2);
}

const readParamsText = (value) => {
  if (value === '-') {
    return fs.readFileSync(0, 'utf8').replace(/^\uFEFF/, '');
  }

  if (value.startsWith('@')) {
    return fs.readFileSync(path.resolve(projectRoot, value.slice(1)), 'utf8');
  }

  return value;
};

let params;
try {
  params = JSON.parse(readParamsText(paramsJson));
} catch (error) {
  console.error(`Invalid params JSON: ${error.message}`);
  process.exit(2);
}

const requestId = `agent-${Date.now()}-${Math.random().toString(16).slice(2)}`;

// Entering Play mode takes the bridge down for a few seconds: the Editor closes clients with
// code 4001, the play-mode domain reload wipes the server instance, and McpUnityAutoStart brings
// it back. A call that starts inside that window used to fail outright. Waiting through it is
// correct - but only while the request is still unsent. Once the payload has gone out we cannot
// know whether Unity ran it before the socket dropped, and silently re-running something like
// execute_menu_item would be worse than reporting the truth.
const reconnectWindowMs = Number(process.env.UNITY_RECONNECT_WINDOW_MS || 30000);
const reconnectDeadline = Date.now() + reconnectWindowMs;
const reconnectDelayMs = 750;

let ws = null;
let requestSent = false;
let finished = false;

const finish = (exitCode, payload) => {
  if (finished) {
    return;
  }
  finished = true;
  clearTimeout(timer);
  try {
    ws?.close();
  } catch {
    // Ignore close races during process shutdown.
  }

  // Set the exit code up front so the process exits correctly even if it drains
  // naturally. Do NOT call process.exit() before the output is flushed: when
  // stdout is a pipe (the usual case here) writes are async, and exiting
  // immediately truncates large payloads (health reports, console log dumps).
  process.exitCode = exitCode;

  if (payload === undefined) {
    return;
  }

  const text = typeof payload === 'string' ? payload : JSON.stringify(payload, null, 2);
  const stream = exitCode === 0 ? process.stdout : process.stderr;
  stream.write(`${text}\n`, () => {
    process.exit(exitCode);
  });
};

const timer = setTimeout(() => {
  finish(1, `Unity WebSocket request timed out after ${options.timeout}ms`);
}, options.timeout);

/**
 * Retries only while the request is still unsent, and only until the reconnect window closes.
 * Returns false when the caller should give up and report `failure` instead.
 */
const retryUnlessSent = (failure) => {
  if (finished) {
    return true;
  }

  if (requestSent || Date.now() >= reconnectDeadline) {
    finish(1, failure);
    return true;
  }

  setTimeout(connect, reconnectDelayMs);
  return true;
};

function connect() {
  if (finished) {
    return;
  }

  ws = new WebSocket(`ws://${options.host}:${options.port}/McpUnity`, {
    headers: { 'X-Client-Name': options.client },
    handshakeTimeout: Math.min(options.timeout, 10000),
  });

  // A failed socket emits 'error' AND 'close'; without this guard one attempt would schedule
  // two reconnects and the backoff would double every round.
  let settled = false;
  const failAttempt = (failure) => {
    if (settled) {
      return;
    }
    settled = true;
    retryUnlessSent(failure);
  };

  ws.once('open', () => {
    requestSent = true;
    ws.send(JSON.stringify({ method, params, id: requestId }));
  });

  ws.on('message', (raw) => {
    let message;
    try {
      message = JSON.parse(raw.toString());
    } catch {
      return;
    }

    if (message.id !== requestId) {
      return;
    }

    if (message.error) {
      finish(1, message.error);
      return;
    }

    finish(0, message.result);
  });

  ws.once('error', (error) => {
    // The usual case while Unity restarts the bridge: ECONNREFUSED on a port nobody listens on.
    failAttempt(`Unity WebSocket error: ${error.message}`);
  });

  ws.once('close', (code, reasonBuf) => {
    if (finished) {
      return;
    }

    // 4001 = UnityCloseCode.PlayMode: the Editor closes clients when entering Play mode.
    if (code === 4001) {
      failAttempt(
        'Unity entered Play mode (close code 4001) after the request was sent, so it may or may not ' +
        'have run. Re-issue it once Play mode has settled.',
      );
      return;
    }

    const reason = reasonBuf && reasonBuf.length ? `: ${reasonBuf.toString()}` : '';
    failAttempt(`Unity WebSocket closed before a response was received (code ${code}${reason})`);
  });
}

connect();
