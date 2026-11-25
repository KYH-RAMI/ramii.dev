import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { once } from 'node:events';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';

const web = process.argv[2];
const profile = await mkdtemp(join(tmpdir(), 'ramii-edge-'));
const app = spawn(join(web, 'bin/Debug/net11.0/ramii.dev.exe'),
  ['--urls', 'http://127.0.0.1:0'], { cwd: web, windowsHide: true });
let log = '';
let browser;
let socket;
app.stdout.on('data', chunk => { log += chunk; });
app.stderr.on('data', chunk => { log += chunk; });
app.on('error', error => { log += error.message; });

async function waitFor(check, label) {
  for (let attempt = 0; attempt < 100; attempt++) {
    const result = await check();
    if (result) return result;
    await delay(150);
  }
  throw new Error('Timed out waiting for ' + label);
}

const pending = new Map();
const events = [];
let nextId = 0;

function command(method, params = {}) {
  const id = ++nextId;
  const reply = new Promise((resolve, reject) => pending.set(id, { resolve, reject }));
  socket.send(JSON.stringify({ id, method, params }));
  return reply;
}

async function evaluate(expression) {
  const reply = await command('Runtime.evaluate', { expression, returnByValue: true });
  assert.ok(!reply.exceptionDetails, reply.exceptionDetails?.text);
  return reply.result.value;
}

async function stop(child) {
  if (!child?.pid || child.exitCode !== null || child.signalCode !== null) return;
  const exited = once(child, 'exit');
  child.kill();
  await Promise.race([exited, delay(2000)]);
}

try {
  const base = await waitFor(
    () => log.match(/Now listening on: (http:\/\/127\.0\.0\.1:\d+)/)?.[1],
    'local app');
  const issued = await fetch(base + '/__test/issue-cookie', { method: 'POST' });
  assert.equal(issued.status, 204);
  const header = issued.headers.getSetCookie().find(value => value.startsWith('ramii_auth='));
  assert.ok(header, 'Expected the app authentication cookie.');
  const original = header.split(';', 1)[0].slice('ramii_auth='.length);
  assert.ok(original.length > 20, 'Expected a protected authentication ticket.');
  const index = Math.floor(original.length / 2);
  const altered = original.slice(0, index) +
    (original[index] === 'A' ? 'B' : 'A') + original.slice(index + 1);

  const edge = process.env.RAMII_TEST_EDGE ??
    'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe';
  browser = spawn(edge, ['--headless=new', '--no-first-run', '--no-default-browser-check',
    '--remote-debugging-port=0', '--user-data-dir=' + profile, 'about:blank'],
    { windowsHide: true, stdio: 'ignore' });
  let browserError;
  browser.on('error', error => { browserError = error; });
  const debugPort = await waitFor(async () => {
    if (browserError) throw browserError;
    const file = await readFile(join(profile, 'DevToolsActivePort'), 'utf8').catch(() => '');
    return file.split(/\r?\n/)[0];
  }, 'Edge');
  const targets = await (await fetch('http://127.0.0.1:' + debugPort + '/json/list')).json();
  socket = new WebSocket(targets.find(target => target.type === 'page').webSocketDebuggerUrl);
  await new Promise((resolve, reject) => {
    socket.addEventListener('open', resolve, { once: true });
    socket.addEventListener('error', reject, { once: true });
  });
  socket.addEventListener('message', ({ data }) => {
    const message = JSON.parse(data);
    if (message.id) {
      const request = pending.get(message.id);
      pending.delete(message.id);
      if (message.error) request.reject(new Error(message.error.message));
      else request.resolve(message.result);
    } else if (message.method.startsWith('Network.')) {
      events.push(message);
    }
  });
  await command('Network.enable');

  const routes = [
    ['/backstage', 'COOKIE-TEST-INBOX'],
    ['/backstage/admins', 'COOKIE-TEST-ADMIN'],
  ];
  for (const [label, value, allowed] of [['original', original, true], ['altered', altered, false]]) {
    for (const [route, marker] of routes) {
      const response = await fetch(base + route, {
        redirect: 'manual', headers: { Cookie: 'ramii_auth=' + value },
      });
      assert.equal(response.status, allowed ? 200 : 302, label + ' direct ' + route);
      assert.equal((await response.text()).includes(marker), allowed, label + ' direct data ' + route);
      if (!allowed) {
        const destination = new URL(response.headers.get('location'), base);
        assert.equal(destination.pathname, '/login');
        assert.equal(destination.searchParams.get('ReturnUrl'), route);
      }
      console.log('PASS', label, 'direct', route, response.status);

      await command('Network.clearBrowserCookies');
      const cookie = await command('Network.setCookie', {
        name: 'ramii_auth', value, url: base, path: '/', httpOnly: true, sameSite: 'Strict',
      });
      assert.equal(cookie.success, true);
      const home = base + '/?cookie-test=' + crypto.randomUUID();
      const beforeHome = events.length;
      await command('Page.navigate', { url: home });
      const circuit = await waitFor(() => {
        const current = events.slice(beforeHome);
        const opened = current.find(event => event.method === 'Network.webSocketCreated' &&
          new URL(event.params.url).pathname === '/_blazor');
        return opened && current.some(event => event.method === 'Network.webSocketFrameReceived' &&
          event.params.requestId === opened.params.requestId) && opened.params.requestId;
      }, 'new Blazor circuit');
      // Let the new circuit finish rendering before clicking.
      await delay(750);

      const token = crypto.randomUUID();
      const beforeClick = events.length;
      await evaluate(`(() => {
        if (location.href !== ${JSON.stringify(home)}) throw Error('Expected the public home page.');
        window.cookieTestDocument = ${JSON.stringify(token)};
        const link = document.createElement('a');
        link.href = ${JSON.stringify(route)};
        document.body.append(link);
        link.click();
      })()`);
      const expectedText = allowed ? marker : 'Logga in för att öppna Backstage.';
      const page = await waitFor(async () => {
        const state = await evaluate(
          '({ path: location.pathname, token: window.cookieTestDocument, text: document.body.innerText })');
        return state.path === route && state.text.includes(expectedText) && state;
      }, label + ' interactive ' + route);
      assert.equal(page.token, token, 'Navigation must stay in the original document.');
      assert.equal(page.text.includes(marker), allowed, label + ' interactive data ' + route);
      const afterClick = events.slice(beforeClick);
      assert.ok(afterClick.some(event => event.method === 'Network.webSocketFrameSent' &&
        event.params.requestId === circuit), 'Navigation must use the same Blazor circuit.');
      assert.ok(!afterClick.some(event => event.method === 'Network.requestWillBeSent' &&
        event.params.type === 'Document'), 'Navigation must not load a new document.');
      console.log('PASS', label, 'interactive', route, '(same Blazor circuit)');
    }
  }
} finally {
  socket?.close();
  await stop(browser);
  await stop(app);
  await rm(profile, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 }).catch(() => {});
}

