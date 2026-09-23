// Summarise playtest runs logged by the published game (the "playtests" db collection).
//   node OrderUp/tools/playtest.js runs.json
// runs.json is an array of run docs (or {docs:[{data}]} as the db listing returns them).
// For each kitchen: runs, pass rate (1+ stars), average score against the star targets, and how
// testers rated it. The hint says which way to move that kitchen's pace or star targets.
const fs = require('fs');
const raw = JSON.parse(fs.readFileSync(process.argv[2] || 'runs.json', 'utf8'));
const runs = (Array.isArray(raw) ? raw : raw.docs || raw.items || []).map(r => r.data || r).filter(r => r && r.v === 1);
const by = new Map();
for (const r of runs) { if (!by.has(r.level)) by.set(r.level, []); by.get(r.level).push(r); }
const pct = (a, b) => b ? Math.round(100 * a / b) + '%' : '-';
console.log('kitchen'.padEnd(26), 'runs  pass  avg/1★  3★   easy right hard  hint');
for (const lvl of [...by.keys()].sort((a, b) => a - b)) {
  const rs = by.get(lvl), n = rs.length;
  const pass = rs.filter(r => r.stars >= 1).length, three = rs.filter(r => r.stars >= 3).length;
  const rel = rs.reduce((s, r) => s + r.score / r.targets[0], 0) / n;
  const rate = k => rs.filter(r => r.rating === k).length;
  const [easy, right, hard] = ['easy', 'right', 'hard'].map(rate);
  // A normal kitchen should pass for most testers and 3-star for some; the Judge's Table is meant to be ~2%.
  const judge = rs[0].kitchen === "The Judge's Table";
  let hint = '';
  if (n < 5) hint = 'need more runs';
  else if (judge) hint = pass / n > 0.1 ? 'too easy for the boss' : pass === 0 && n >= 30 ? 'maybe impossible' : 'ok';
  else if (hard > easy + right || pass / n < 0.5) hint = 'ease off: slower orders or lower targets';
  else if (easy > right + hard || three / n > 0.6) hint = 'push: faster orders or higher targets';
  else hint = 'ok';
  console.log(`${String(lvl).padStart(2)} ${rs[0].kitchen}`.slice(0, 26).padEnd(26), String(n).padStart(4), pct(pass, n).padStart(5),
    rel.toFixed(2).padStart(7), pct(three, n).padStart(4), String(easy).padStart(6), String(right).padStart(5), String(hard).padStart(4), ' ' + hint);
}
console.log(`\n${runs.length} runs. Player counts seen: ${[...new Set(runs.map(r => r.humans))].sort().join(', ') || '-'}.`);
