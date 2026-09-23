// Recompute each kitchen's order pacing and star targets so difficulty rises smoothly.
//   node OrderUp/tools/tune.js          print the table
//   node OrderUp/tools/tune.js --write  write the new intervals and stars into index.html
// Pressure = orders per minute x work per dish. Target: 13 + 1.2 per stop, +5 from a stop's
// first kitchen to its last. Stars: 35/55/75% (+0.5% per stop) of every possible coin at that
// pace. The Judge's Table and tutorial are tuned by hand.
const fs = require('fs');
const FILE = require('path').join(__dirname, '..', 'index.html');
let src = fs.readFileSync(FILE, 'utf8');
const js = src.split('<script>')[1].split('</script>')[0];
const pre = 'const document={getElementById:()=>({getContext:()=>({}),style:{},addEventListener(){}}),createElement:()=>({getContext:()=>({}),style:{}}),addEventListener(){},fonts:null};const window={addEventListener(){},devicePixelRatio:1,innerWidth:400,innerHeight:800};const localStorage={getItem:()=>null,setItem(){}};const navigator={};const getComputedStyle=()=>({});const requestAnimationFrame=()=>{};const performance={now:()=>0};const location={hash:""};';
const { LEVELS, RECIPES, POT_RECIPES, HEAT, BLENDS } = new Function(pre + js.split('/* =========================================================================\n   Audio')[0] + ';return {LEVELS,RECIPES,POT_RECIPES,HEAT,BLENDS};')();

// Work in one dish: a step per ingredient, +1 per prep/cook step, + long cooks count extra.
const cookSecs = kind => { for (const h of Object.values(HEAT)) if (h.foods[kind]) return h.foods[kind][1]; return 0; };
function itemEffort(k) {
  const [kind, st] = k.split(':');
  const pr = POT_RECIPES.find(r => r.result === k);
  if (pr) return pr.needs.reduce((n, x) => n + itemEffort(x), 0) + 1 + 9 / 6;
  if (BLENDS.find(b => b.result.join(':') === k)) return 3;
  let e = 1;
  if (st === 'chopped' || st === 'sliced' || st === 'stretched') e += 1;
  if (st === 'cooked' || st === 'sliced') { const s = cookSecs(kind); if (s) e += 1 + Math.max(0, s - 6) / 6; }
  if (kind === 'pizza' || kind === 'pepperonipizza') e += 3;          // stretch + sauce + cheese (+ pepperoni)
  return e;
}
const dishEffort = r => { const R = RECIPES[r]; let e = R.items.reduce((n, k) => n + itemEffort(k), 0); if (R.extras) e += (R.pick[0] + R.pick[1]) / 2 * 1.6; return e; };

const out = [];
LEVELS.forEach((lv, i) => {
  if (lv.judge) return;
  const k = LEVELS.filter((l, j) => l.stop === lv.stop && j < i).length;            // position within the stop
  const n = LEVELS.filter(l => l.stop === lv.stop).length;
  const target = 13 + 1.2 * lv.stop + (n > 1 ? 5 * k / (n - 1) : 0);              // pressure goal: +5 from first to last
  const eff = lv.recipes.reduce((n, r) => n + dishEffort(r), 0) / lv.recipes.length;
  const rate = target / eff;                                                       // orders per minute
  const avg = Math.min(32, Math.max(6, 60 / rate));
  const interval = [Math.round(avg * 0.8), Math.round(avg * 1.2)];
  const reward = lv.recipes.reduce((n, r) => { const R = RECIPES[r]; return n + R.reward + (R.extras ? (R.pick[0] + R.pick[1]) / 2 * R.extraReward : 0); }, 0) / lv.recipes.length;
  const possible = lv.time / 60 * (60 / avg) * (reward + 5);                          // every ticket served, average tip
  const f = [0.35, 0.55, 0.75].map(x => x + 0.005 * lv.stop);
  const stars = f.map(x => Math.round(possible * x / 10) * 10);
  out.push({ i, name: lv.name, stop: lv.stop, k, eff: eff.toFixed(1), was: lv.interval.join('-'), interval, starsWas: lv.stars.join('/'), stars, pressure: (60 / avg * eff).toFixed(1) });
  // write back: the level's own interval and stars
  const nameLit = lv.name.includes("'") ? `"${lv.name}"` : `'${lv.name}'`;
  const at = src.indexOf(`name: ${nameLit},`);
  if (at < 0) throw new Error('level not found ' + lv.name);
  const lineEnd = src.indexOf('\n', at);
  let line = src.slice(at, lineEnd);
  line = line.replace(/stars: \[[^\]]*\]/, `stars: [${stars.join(', ')}]`).replace(/interval: \[[^\]]*\]/, `interval: [${interval.join(', ')}]`);
  src = src.slice(0, at) + line + src.slice(lineEnd);
});
if (process.argv[2] === '--write') fs.writeFileSync(FILE, src);
console.log('No Kitchen               stop eff   interval (was)     stars (was)                 pressure');
for (const o of out) console.log(String(o.i + 1).padStart(2), o.name.slice(0, 20).padEnd(20), String(o.stop).padStart(4), o.eff.padStart(5), (o.interval.join('-') + ' (' + o.was + ')').padStart(15), (o.stars.join('/') + ' (' + o.starsWas + ')').padStart(28), o.pressure.padStart(8));
