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
  console.error('Usage: node .codex/tools/unity-ws-call.mjs <method> [paramsJson|-|@file] [--timeout ms]');
  process.exit(2);
}

const readParamsText = (value) => {
  if (value === '-') {
    return fs.readFileSync(0, 'utf8');
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

const requestId = `codex-${Date.now()}-${Math.random().toString(16).slice(2)}`;
const ws = new WebSocket(`ws://${options.host}:${options.port}/McpUnity`, {
  headers: { 'X-Client-Name': options.client },
  handshakeTimeout: Math.min(options.timeout, 10000),
});

let finished = false;
const finish = (exitCode, payload) => {
  if (finished) {
    return;
  }
  finished = true;
  clearTimeout(timer);
  try {
    ws.close();
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

ws.once('open', () => {
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
  finish(1, `Unity WebSocket error: ${error.message}`);
});

ws.once('close', (code, reasonBuf) => {
  if (finished) {
    return;
  }

  // 4001 = UnityCloseCode.PlayMode: the Editor closes clients when entering Play
  // mode. Surface that clearly instead of a generic "closed" error so the caller
  // knows to re-issue the request once Play mode has settled.
  if (code === 4001) {
    finish(1, 'Unity entered Play mode (close code 4001) before responding. Retry once Play mode has settled.');
    return;
  }

  const reason = reasonBuf && reasonBuf.length ? `: ${reasonBuf.toString()}` : '';
  finish(1, `Unity WebSocket closed before a response was received (code ${code}${reason})`);
});
