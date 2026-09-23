# Sear Pressure relay

A small WebSocket server that lets phones play Sear Pressure online (co-op and versus).
The web version connects phones directly with PeerJS/WebRTC; the Unity build talks to this
relay instead. The host's phone still runs the kitchen. The relay only makes rooms and passes
messages along: guest to host, and host to one guest or to every guest.

- The host gets a 4-letter room code (letters from `ABCDEFGHJKLMNPQRSTUVWXYZ`).
- Up to 3 guests join with that code. A 5th player gets `full`, and a code with no room gets `nocode`.
- When the host leaves, the room closes and its guests are told. When a guest leaves, the host is told.
- Every socket is pinged every 15 s. A socket that misses a pong is closed. The game also sends its own
  `ping` every 10 s and hangs up after 30 s of silence.

## Run it locally

```sh
cd SearPressureUnity/Server
npm install
npm start               # listens on port 8080, or on $PORT
```

`GET /` returns `ok N rooms`, which you can use for health checks. In the game, the platform's
`CreateTransport()` returns `new WsRelayTransport("ws://<your-ip>:8080")`.

## Deploy

Any host that runs Node 18 or later and accepts WebSocket connections works. Set the start command to
`node relay.js` (or `npm start`). The server reads `PORT` from the environment. Free options:

- **Render**: create a new Web Service from the repo, set the root directory to `SearPressureUnity/Server`,
  the build command to `npm install` and the start command to `npm start`. Free instances go to sleep when
  idle, so the first connection after a quiet spell takes a few seconds.
- **Fly.io**: run `fly launch` in this folder (it detects Node), then `fly deploy`.
- **Railway**: create a new project from the repo, set the root directory to `SearPressureUnity/Server`, and it starts with `npm start`.

These hosts put TLS in front of your server. **Release builds must use `wss://`**, for example
`wss://sear-relay.onrender.com`. iOS and Android block plain `ws://` by default, and many mobile networks
break unencrypted WebSockets. Plain `ws://` is only for local testing.

The relay keeps rooms in memory. Run one instance only, because two instances would not know each
other's rooms. Restarting it drops every game in progress.

## Protocol

JSON text frames.

| phone -> relay | answer / effect |
| --- | --- |
| `{op:'host'}` | `{op:'hosted', code}` |
| `{op:'join', code}` | `{op:'joined', id}` to the guest and `{op:'peer-join', id}` to the host, or `{op:'error', reason:'nocode'\|'full'}` |
| `{op:'send', to?, d}` | host: `{op:'msg', d}` to guest `to`, or to every guest when `to` is missing. guest: `{op:'msg', from:id, d}` to the host |
| `{op:'kick', id}` | host only: closes that guest's socket, after anything already sent to them |
| `{op:'ping'}` | `{op:'pong'}` |

The relay sends `{op:'peer-leave', id}` to the host when a guest goes, and `{op:'host-left'}` to the
guests when the host goes. `d` holds the game's own messages (`fit`, `snap`, `start` and so on), which
the relay doesn't read.
