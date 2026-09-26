// Renderer: polls launcher.ps1 through the preload bridge and draws the state.

// What the user sees, in data-flow order. Each row sums up one or more
// launcher.ps1 components, so the technical pieces stay out of the UI.
const CHAIN = [
  { id: 'headset',  name: 'Headset',          parts: ['quest'] },
  { id: 'app',      name: 'App on headset',   parts: ['app'] },
  { id: 'software', name: 'Control software', parts: ['tcp', 'ros', 'bridge'] },
  { id: 'robot',    name: 'Robot',            parts: ['nic', 'robot'] },
];
const OBSERVERS = [
  { id: 'dash', name: 'Dashboard', parts: ['dash'] },
];
const STATE_LABEL = { on: 'On', off: 'Off', starting: 'Starting', stopping: 'Stopping', fault: 'Problem', waiting: 'Waiting' };

const $ = (s) => document.querySelector(s);
const bridge = window.mm ?? demoBridge();   // plain browser preview falls back to a local demo
let last = {};
let lastHint = '';
let phase = 'down';

function renderList(el, items) {
  el.innerHTML = items.map((c, i) => `
    <li class="node" data-id="${c.id}" style="--i:${i}">
      <span class="dot"><i></i></span>
      <span class="node-name">${c.name}</span>
      <span class="node-state">Off</span>
    </li>`).join('');
}
renderList($('#nodes'), CHAIN);
renderList($('#observers'), OBSERVERS);

function log(msg) {
  const t = new Date().toLocaleTimeString('de-AT', { hour12: false });
  const span = document.createElement('span');
  span.innerHTML = `<time>${t}</time>${msg}`;
  $('#log').prepend(span);
  while ($('#log').children.length > 8) $('#log').lastChild.remove();
}

// One state for a group of parts: worst case wins, a half-up group counts as starting.
function combine(list) {
  for (const s of ['fault', 'stopping', 'starting']) if (list.includes(s)) return s;
  if (list.every((s) => s === 'on')) return 'on';
  return list.some((s) => s === 'on') ? 'starting' : 'off';
}

function apply(res) {
  if (!res?.ok) { log(`Something went wrong: ${res?.error ?? 'no answer'}`); return; }
  $('#demoBadge').hidden = !res.demo;

  const states = Object.fromEntries(res.components.map((c) => [c.id, c.state]));
  lastStates = states;
  // Waiting for the operator at the pendant is not loading: no spinner, no pulsing dot.
  waitingOperator = !!PENDANT_PROMPTS[res.robotReason];
  const rows = {};
  for (const row of [...CHAIN, ...OBSERVERS]) {
    let st = combine(row.parts.map((p) => states[p] ?? 'off'));
    if (row.id === 'robot' && waitingOperator && st === 'starting') st = 'waiting';
    rows[row.id] = st;
    const node = document.querySelector(`.node[data-id="${row.id}"]`);
    // Touch the DOM only on a change: re-setting classes/text every poll restarts
    // animations and repaints for nothing (the lab PC renders in software).
    setClass(node, `node ${st}`);
    setText(node.querySelector('.node-state'), STATE_LABEL[st] ?? st);
    if (last[row.id] && last[row.id] !== st && (st === 'on' || st === 'off')) {
      log(`${row.name} ${st}`);
    }
  }
  last = rows;

  // Why the robot is not ready yet, straight from robot_status (e.g. "RAPID stopped").
  const robotNode = document.querySelector('.node[data-id="robot"] .node-state');
  if (robotNode && res.robotReason && rows.robot !== 'on') {
    setText(robotNode, `${STATE_LABEL[rows.robot] ?? rows.robot} · ${res.robotReason}`);
  }
  if (res.robotHint && res.robotHint !== lastHint) log(`Robot: ${res.robotHint}`);
  lastHint = res.robotHint;
  showPendantPrompt(res.robotReason);

  const up = CHAIN.filter((c) => rows[c.id] === 'on').length;
  setText($('#count'), `${up} of ${CHAIN.length} running`);

  const any = (s) => Object.values(states).includes(s);
  const next = any('stopping') ? 'stopping'
    : res.running && waitingOperator ? 'up'
    : res.running && any('starting') && states.robot !== 'on' ? 'starting'
    : res.running ? 'up' : 'down';
  // A click shows its phase at once (optimistic); the poll only overrides it once the
  // backend agrees or the grace time is over - otherwise the UI would jump back.
  if (next !== phase && !(pendingPhase && Date.now() < pendingUntil && next !== pendingPhase)) {
    setPhase(next);
  }
  if (next === pendingPhase) pendingPhase = null;
  paintCopy(states);
}

let pendingPhase = null, pendingUntil = 0, waitingOperator = false, lastStates = {};
function setPhase(p) {
  phase = p;
  if (document.body.dataset.phase !== p) document.body.dataset.phase = p;
}
function setText(el, t) { if (el && el.textContent !== t) el.textContent = t; }
function setClass(el, c) { if (el && el.className !== c) el.className = c; }

function paintCopy(states) {
  const h = $('#headline'), lbl = $('#powerLabel'), quest = $('#questBtn');
  // Usable whenever a headset is reachable (wireless adb or USB) - also to restart a
  // running or stuck app. Greyed out only without a headset or while it is opening.
  const dis = states.quest !== 'on' || states.app === 'starting';
  if (quest.disabled !== dis) quest.disabled = dis;
  const tip = states.quest !== 'on'
    ? 'No Quest reachable: plug it in (USB) once after a Quest restart, or open the app in the headset'
    : '';
  if (quest.title !== tip) quest.title = tip;
  setText($('#questLabel'), states.app === 'starting' ? 'Opening …' : 'Restart app');
  setText(h, phase === 'up' && waitingOperator ? 'Ready'
    : { down: 'Off', starting: 'Starting …', up: 'Running', stopping: 'Stopping …' }[phase]);
  setText(lbl, { down: 'Start', starting: 'Cancel', up: 'Stop', stopping: 'Stopping …' }[phase]);
}

let busy = false;
async function tick() {
  if (!busy) apply(await bridge.status());
  setTimeout(tick, phase === 'starting' || phase === 'stopping' ? 500 : 2000);
}

$('#power').addEventListener('click', async () => {
  if (phase === 'stopping') return;
  busy = true;
  const starting = phase === 'down';
  log(starting ? 'Starting' : 'Stopping');
  // Immediate feedback - the backend call takes seconds (RWS, rosbridge, adb).
  pendingPhase = starting ? 'starting' : 'stopping';
  pendingUntil = Date.now() + 15000;
  setPhase(pendingPhase);
  paintCopy(lastStates);
  const res = await (starting ? bridge.start() : bridge.stop());
  apply(res);
  if (starting) log(res?.questError ? `Headset app not started: ${res.questError}` : 'Headset app starting');
  busy = false;
});
$('#questBtn').addEventListener('click', async () => {
  log('Restarting the app on the headset');
  apply(await bridge.launchQuestApp());
});
$('#streamBtn').addEventListener('click', () => bridge.openStream());
document.querySelectorAll('[data-dash]').forEach((b) =>
  b.addEventListener('click', () => bridge.openDashboard(b.dataset.dash)));
document.querySelectorAll('[data-win]').forEach((b) =>
  b.addEventListener('click', () => bridge.win(b.dataset.win)));

// Operator action at the FlexPendant, shown only while robot_status waits for it.
const PENDANT_PROMPTS = {
  'RAPID stopped': { kind: 'play', title: 'Press Play on the FlexPendant', text: 'The robot program is ready. It starts when you press Play.' },
  'no EGM packets from the controller': { kind: 'play', title: 'Press Play on the FlexPendant', text: 'The robot is not answering yet. If Play is already on, check the robot cable.' },
};
let pendantMiss = 0;
function showPendantPrompt(reason) {
  const card = $('#pendant');
  const p = PENDANT_PROMPTS[reason];
  if (!p) {
    // Hide only after two polls without a reason: a single flicker would replay the
    // card's entrance animation every few seconds.
    if (++pendantMiss >= 2 && !card.hidden) card.hidden = true;
    return;
  }
  pendantMiss = 0;
  if (card.dataset.kind !== p.kind) card.dataset.kind = p.kind;
  setText($('#pendantTitle'), p.title);
  setText($('#pendantText'), p.text);
  if (card.hidden) card.hidden = false;
}
window.showPendantPrompt = showPendantPrompt;   // for design captures

log('Ready');
tick();

// Same demo timeline as launcher.ps1, for opening index.html in a plain browser.
function demoBridge() {
  const at = { nic: 0.6, quest: 1.4, ros: 2.2, tcp: 3.0, bridge: 4.0, robot: 5.2, dash: 6.0 };
  let started = null, stopping = null, quest = null;
  const status = () => {
    const now = Date.now();
    if (stopping && now - stopping > 3500) { started = stopping = null; }
    const ids = ['nic', 'ros', 'tcp', 'bridge', 'robot', 'quest', 'app', 'dash'];
    const components = ids.map((id, rank) => {
      let state = 'off';
      if (stopping) state = (now - stopping) / 1000 < (ids.length - rank) * 0.35 ? 'stopping' : 'off';
      else if (started) state = id === 'app'
        ? (quest ? ((now - quest) / 1000 > 2.5 ? 'on' : 'starting') : 'off')
        : ((now - started) / 1000 >= at[id] ? 'on' : 'starting');
      else if (id === 'quest') state = 'on';
      return { id, state };
    });
    return Promise.resolve({ ok: true, demo: true, running: !!started && !stopping, components });
  };
  return {
    status,
    start: () => { started = Date.now(); stopping = null; return status(); },
    stop: () => { stopping = Date.now(); quest = null; return status(); },
    launchQuestApp: () => { quest = Date.now(); return status(); },
    openDashboard: (sub) => window.open('http://localhost:8080' + (sub || '')),
    openStream: () => window.open('stream.html', '_blank', 'width=960,height=540'),
    win: () => {},
  };
}
