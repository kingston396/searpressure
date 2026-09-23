// Sear Pressure relay: rooms by 4-letter code, one host and up to three guests each.
// It doesn't know the game: it only forwards JSON messages guest -> host and host -> guests.
//
//   phone -> relay: {op:'host'} | {op:'join', code} | {op:'send', to?, d} | {op:'kick', id} | {op:'ping'}
//   relay -> phone: {op:'hosted', code} | {op:'joined', id} | {op:'error', reason} | {op:'msg', from?, d}
//                   {op:'peer-join', id} | {op:'peer-leave', id} | {op:'host-left'} | {op:'pong'}
//
// PORT (default 8080) sets the port. A plain HTTP GET answers "ok" for health checks.
'use strict';
const http = require('http');
const { WebSocketServer, WebSocket } = require('ws');

const PORT = +process.env.PORT || 8080;
const CODE_CHARS = 'ABCDEFGHJKLMNPQRSTUVWXYZ';
const MAX_PLAYERS = 4;              // the host and three guests
const MAX_ROOMS = 5000;
const HEARTBEAT_MS = 15000;         // ping every socket; miss one pong and it's gone
const MAX_MSGS_PER_SEC = 200;       // per socket; a guest's inputs are ~60/s at most
const MAX_BUFFERED = 4 << 20;       // a socket this far behind is dead weight

const rooms = new Map();            // code -> { code, host, guests: Map<id, ws>, nextId }

function randomCode() {
  for (let tries = 0; tries < 1000; tries++) {
    let c = '';
    for (let i = 0; i < 4; i++) c += CODE_CHARS[Math.floor(Math.random() * CODE_CHARS.length)];
    if (!rooms.has(c)) return c;
  }
  return null;
}

function send(ws, obj) {
  if (!ws || ws.readyState !== WebSocket.OPEN) return;
  if (ws.bufferedAmount > MAX_BUFFERED) { ws.terminate(); return; }
  ws.send(typeof obj === 'string' ? obj : JSON.stringify(obj));
}

// A socket leaves its room: the host closes it for everyone, a guest just tells the host.
function leave(ws) {
  const room = ws.room;
  if (!room) return;
  ws.room = null;
  if (room.host === ws) {
    rooms.delete(room.code);
    for (const g of room.guests.values()) { g.room = null; send(g, { op: 'host-left' }); g.close(1000, 'host left'); }
    room.guests.clear();
  } else if (room.guests.get(ws.id) === ws) {
    room.guests.delete(ws.id);
    send(room.host, { op: 'peer-leave', id: ws.id });
  }
}

function onMessage(ws, data) {
  // A simple flood guard.
  const now = Date.now();
  if (now - ws.rateT > 1000) { ws.rateT = now; ws.rateN = 0; }
  if (++ws.rateN > MAX_MSGS_PER_SEC) return;

  let m;
  try { m = JSON.parse(data); } catch (e) { return; }
  if (!m || typeof m !== 'object') return;
  const room = ws.room;

  switch (m.op) {
    case 'ping': send(ws, { op: 'pong' }); return;
    case 'host': {
      if (room) return;
      const code = rooms.size < MAX_ROOMS ? randomCode() : null;
      if (!code) { send(ws, { op: 'error', reason: 'busy' }); ws.close(1013, 'busy'); return; }
      const r = { code, host: ws, guests: new Map(), nextId: 1 };
      rooms.set(code, r);
      ws.room = r;
      send(ws, { op: 'hosted', code });
      return;
    }
    case 'join': {
      if (room) return;
      const code = String(m.code || '').trim().toUpperCase();
      const r = /^[A-Z]{4}$/.test(code) ? rooms.get(code) : null;
      if (!r) { send(ws, { op: 'error', reason: 'nocode' }); ws.close(1000, 'nocode'); return; }
      if (r.guests.size >= MAX_PLAYERS - 1) { send(ws, { op: 'error', reason: 'full' }); ws.close(1000, 'full'); return; }
      ws.id = r.nextId++;
      ws.room = r;
      r.guests.set(ws.id, ws);
      send(ws, { op: 'joined', id: ws.id });
      send(r.host, { op: 'peer-join', id: ws.id });
      return;
    }
    case 'send': {
      if (!room || m.d === undefined) return;
      if (room.host === ws) {
        const out = JSON.stringify({ op: 'msg', d: m.d });
        if (m.to == null) { for (const g of room.guests.values()) send(g, out); }
        else send(room.guests.get(+m.to), out);
      } else send(room.host, { op: 'msg', from: ws.id, d: m.d });
      return;
    }
    case 'kick': {
      if (!room || room.host !== ws) return;
      const g = room.guests.get(+m.id);
      if (!g) return;
      room.guests.delete(g.id);
      g.room = null;
      g.close(1000, 'kicked');   // anything already sent to them goes first
      return;
    }
  }
}

const server = http.createServer((req, res) => {
  res.writeHead(200, { 'content-type': 'text/plain' });
  res.end(`ok ${rooms.size} rooms\n`);
});
const wss = new WebSocketServer({ server, maxPayload: 256 * 1024 });

wss.on('connection', ws => {
  ws.alive = true; ws.room = null; ws.id = 0; ws.rateT = 0; ws.rateN = 0;
  ws.on('pong', () => { ws.alive = true; });
  ws.on('message', (data, isBinary) => { if (!isBinary) onMessage(ws, data.toString()); });
  ws.on('close', () => leave(ws));
  ws.on('error', () => { leave(ws); ws.terminate(); });
});

// Heartbeats: sockets that stop answering pings (phone asleep, network gone) are closed,
// which closes their room or tells their host.
const beat = setInterval(() => {
  for (const ws of wss.clients) {
    if (!ws.alive) { leave(ws); ws.terminate(); continue; }
    ws.alive = false;
    try { ws.ping(); } catch (e) {}
  }
}, HEARTBEAT_MS);
wss.on('close', () => clearInterval(beat));

server.listen(PORT, () => console.log(`Sear Pressure relay on :${PORT}`));

function shutdown() {
  for (const ws of wss.clients) ws.close(1001, 'server restarting');
  server.close(() => process.exit(0));
  setTimeout(() => process.exit(0), 2000).unref();
}
process.on('SIGTERM', shutdown);
process.on('SIGINT', shutdown);
