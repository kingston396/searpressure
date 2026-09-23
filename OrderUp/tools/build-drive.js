// Build drive-test.html: the Delivery Runs driving game on its own, for quick testing.
//   node OrderUp/tools/build-drive.js
// It copies the driving code and the few helpers it needs straight out of index.html,
// so rebuild after changing the game. All three runs are open, and nothing touches the main save.
const fs = require('fs'), path = require('path');
const DIR = path.join(__dirname, '..');
const src = fs.readFileSync(path.join(DIR, 'index.html'), 'utf8');
const js = src.split('<script>')[1].split('</script>')[0];

// Cut a balanced { } or [ ] block starting at `from`, up to and including its closing bracket.
function block(text, from) {
  let i = text.indexOf('{', from), j = text.indexOf('[', from);
  let open = i < 0 || (j >= 0 && j < i) ? j : i;
  const pairs = { '{': '}', '[': ']' }, stack = [];
  let q = null;
  for (let k = open; k < text.length; k++) {
    const ch = text[k];
    if (q) { if (ch === '\\') k++; else if (ch === q) q = null; continue; }
    if (ch === '"' || ch === "'" || ch === '`') { q = ch; continue; }
    if (ch === '{' || ch === '[') stack.push(pairs[ch]);
    else if (ch === '}' || ch === ']') { stack.pop(); if (!stack.length) return text.slice(from, k + 1); }
  }
  throw new Error('unbalanced at ' + from);
}
function fn(name) {
  const at = js.indexOf(`\nfunction ${name}(`);
  if (at < 0) throw new Error('missing function ' + name);
  return block(js, at + 1);
}
function decl(start) {
  const at = js.indexOf('\n' + start);
  if (at < 0) throw new Error('missing ' + start);
  return block(js, at + 1) + ';';
}
// Only the sprites the driving game borrows from the kitchen set.
function sprites(names) {
  const spr = decl('const SPR = {');
  return 'const SPR = {\n' + names.map(n => {
    const at = spr.indexOf(`\n  ${n}: [`);
    if (at < 0) throw new Error('missing sprite ' + n);
    return '  ' + block(spr, at + 3) + ',';
  }).join('\n') + '\n};';
}

// The driving module itself, minus the pieces that talk to the full game.
let drive = js.slice(js.indexOf('/* =========================================================================\n   Delivery Run'), js.indexOf('\nconst PLAYTEST'));
for (const name of ['driveOpen', 'newDriveRuns', 'driveResults', 'openDriveIntro']) drive = drive.replace(block(drive, drive.indexOf(`function ${name}(`)), '');

const parts = [
  decl('const PAL = {'),
  sprites(['coin', 'bag']),
  'const spriteCache = new Map();',
  fn('sprite'), fn('spr'), fn('hex'), fn('mix'), fn('mulberry32'),
  'let AC = null;', fn('audioInit'), fn('tone'), fn('noise'), decl('const SFX = {'), fn('sfx'), fn('buzz'),
  "const cvs = document.getElementById('game');\nconst mainCtx = cvs.getContext('2d');\nlet X = mainCtx;\nlet W = 0, H = 0, DPR = 1;\nconst safe = { t: 0, r: 0, b: 0, l: 0 };\nconst L = {};\nconst P = 1 / 16;",
  fn('resize'), fn('layout'), fn('stickSide'),
  decl('const input = {'), 'const JOY_R = 52;', decl('const MOVE_KEYS = {'), fn('dist'), fn('readMove'),
  fn('pixCircle'), fn('drawButton'), fn('fmtTime'), fn('starHTML'),
  drive,
];

const shell = fs.readFileSync(path.join(__dirname, 'drive-shell.html'), 'utf8');
const out = shell.replace('/*__GAME__*/', parts.join('\n\n'));
fs.writeFileSync(path.join(DIR, 'drive-test.html'), out);
console.log('wrote drive-test.html', (out.length / 1024).toFixed(0) + ' KB');
