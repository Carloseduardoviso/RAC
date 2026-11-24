'use strict';
/* Motion is an independent visualization layer. It NEVER invokes RAC commands.
 * Character dialogue and speech are scripted UI effects, not AI conversations. */
(function () {
  const core = window.RacMotionCore;
  const floor = document.getElementById('officePhotoreal');
  const scene = floor?.querySelector('.office-scene-shell');
  if (!core || !scene) return;
  const layer = document.createElement('div');
  layer.id = 'racMotionLayer';
  layer.className = 'rac-motion-layer';
  layer.setAttribute('aria-label', 'Personagens do escritório em movimento');
  scene.append(layer);
  const controls = document.createElement('div');
  controls.className = 'rac-motion-controls';
  const motionBtn = document.createElement('button');
  motionBtn.type = 'button';
  motionBtn.id = 'racMotionToggle';
  motionBtn.setAttribute('aria-pressed', 'true');
  motionBtn.textContent = 'Personagens animados: ligado';
  const voiceBtn = document.createElement('button');
  voiceBtn.type = 'button';
  voiceBtn.id = 'racMotionVoice';
  voiceBtn.setAttribute('aria-pressed', 'false');
  voiceBtn.textContent = 'Voz: desligada';
  const demoBtn = document.createElement('button');
  demoBtn.type = 'button';
  demoBtn.id = 'racMotionDemo';
  demoBtn.textContent = 'Testar movimentos (somente visual)';
  const notice = document.createElement('span');
  notice.className = 'rac-motion-disclaimer';
  notice.textContent = 'Movimentos e falas ilustrativos • dados PAPER';
  controls.append(motionBtn, voiceBtn, demoBtn, notice);
  floor.querySelector('.office-activity-strip')?.after(controls);
  const narration = document.createElement('div');
  narration.id = 'racMotionNarration';
  narration.className = 'rac-motion-narration';
  narration.setAttribute('role', 'status');
  narration.setAttribute('aria-live', 'polite');
  narration.textContent = 'Movimentação virtual sincronizada aos estados da simulação.';
  floor.querySelector('.office-floor-legend')?.before(narration);

  const actors = new Map();
  const reduce = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches === true;
  let active = !reduce;
  let voice = false;
  let latest = null;
  let busyVisit = false;
  let demoActive = false;
  const handled = new Set();
  const leaderNames = ['Ana', 'Rebeca', 'Carlos'];
  const genderFor = (id, leader = false) => (leader
    ? id === 'Carlos' ? 'male' : 'female'
    : Number(id) % 4 === 0 ? 'female' : 'male');

  function announce(text, actor) {
    const message = 'Conversa simulada: ' + String(text);
    narration.textContent = message;
    if (actor) {
      actor.bubble.textContent = text;
      actor.bubble.hidden = false;
      clearTimeout(actor.bubbleTimer);
      actor.bubbleTimer = setTimeout(() => { actor.bubble.hidden = true; }, 4500);
    }
    if (actor) {
      pose(actor, 'speaking');
      actor.el.classList.add('rac-motion-speaking');
      const speechToken = actor.token;
      setTimeout(() => {
        actor.el.classList.remove('rac-motion-speaking');
        if (actor.token !== speechToken || actor.motion) return;
        if (actor.zone === 'desk' || actor.zone === 'leader') pose(actor, 'typing');
        else if (actor.zone === 'lounge') pose(actor, 'resting'); else if (actor.zone) pose(actor, 'standing');
      }, 2400);
    }
    if (voice && 'speechSynthesis' in window &&
        typeof SpeechSynthesisUtterance === 'function') {
      window.speechSynthesis.cancel();
      const utterance = new SpeechSynthesisUtterance(String(text));
      utterance.lang = 'pt-BR';
      utterance.rate = 0.97;
      window.speechSynthesis.speak(utterance);
    }
  }
  function position(actor, point) {
    actor.el.style.left = point.x + '%';
    actor.el.style.top = point.y + '%';
    actor.point = {...point};
    actor.el.style.setProperty('--depth-scale', String(0.72 + point.y / 250));
    actor.el.style.zIndex = String(Math.round(point.y * 10));
  }
  function pose(actor, type) {
    if (type === 'resting' && actor.point.y >= 83 || type === 'resting' && actor.point.x >= 29) type = 'standing';
    actor.el.dataset.pose = type;
    actor.el.style.zIndex = String(Math.round(actor.point.y * 10));
    actor.sprite.dataset.pose = type;
  }
  function createActor(key, gender, point, label, leader) {
    const el = document.createElement('div');
    el.className = 'rac-motion-actor' + (leader ? ' rac-motion-leader' : '');
    el.dataset.gender = gender;
    el.dataset.pose = 'typing';
    el.dataset.key = key;
    const identity = Array.from(key).reduce((h,c) => (Math.imul(h,31)+c.charCodeAt(0))>>>0,0);
    el.style.setProperty("--avatar-hue", String((identity % 9 - 4) * 9)+"deg");
    const name = document.createElement("span");
    name.className = "rac-avatar-name";
    name.textContent = label;
    el.setAttribute('aria-label', label + ' — avatar ilustrativo');
    const sprite = document.createElement('div');
    sprite.className = 'rac-motion-sprite';
    sprite.dataset.pose = 'typing';
    const bubble = document.createElement('span');
    bubble.className = 'rac-motion-bubble';
    bubble.hidden = true;
    el.append(sprite, name, bubble);
    layer.append(el);
    const actor = {el, sprite, bubble, point, goal: null, zone: '',
      motion: null, token: 0, bubbleTimer: null, leader};
    position(actor, point);
    actors.set(key, actor);
    return actor;
  }
  function walk(actor, destination, targetZone, callback) {
    actor.token += 1;
    const token = actor.token;
    actor.motion?.cancel?.();
    clearTimeout(actor.riseTimer);
    const started = actor.point;
    actor.goal = targetZone;
    if (!active || reduce || !actor.el.animate) {
      position(actor, destination);
      actor.zone = targetZone;
      pose(actor, targetZone === 'desk' || targetZone === 'leader' ? 'typing' : targetZone === 'lounge' ? 'resting' : 'standing');
      callback?.();
      return;
    }
    pose(actor, 'rising');
    const frames = core.route(started, destination, targetZone)
      .map((pt, i, route) => ({left: pt.x + '%', top: pt.y + '%',
        offset: i / (route.length - 1)}));
    const distance = Math.hypot(started.x - destination.x, started.y - destination.y);
    const duration = Math.max(1300, Math.min(5200, distance * 105));
    actor.riseTimer = setTimeout(() => {
      if (actor.token !== token) return;
      pose(actor, 'walking');
      const movement = actor.el.animate(frames,
        {duration, easing: 'linear', fill: 'forwards'});
      actor.motion = movement;
      movement.finished.then(() => {
        if (actor.token !== token) return;
        actor.motion = null;
        movement.cancel();
        position(actor, destination);
        actor.zone = targetZone;
        pose(actor, targetZone === 'desk' || targetZone === 'leader' ? 'typing' : targetZone === 'lounge' ? 'resting' : 'standing');
        callback?.();
      }).catch(() => {});
    }, 430);
  }
  function moveAgent(actor, targetZone, targetPoint) {
    if (actor.goal === targetZone) return;
    if (!actor.zone) {
      position(actor, targetPoint);
      actor.zone = targetZone;
      actor.goal = targetZone;
      pose(actor, targetZone === 'desk' ? 'typing' : 'resting');
      return;
    }
    if (targetZone !== actor.zone || actor.goal !== targetZone) {
      announce(targetZone === 'lounge' ?
        actor.el.dataset.key.replace(/^agent:/, '') + ' foi para a área de lazer.' :
        actor.el.dataset.key.replace(/^agent:/, '') + ' voltou à mesa para observar.', actor);
      walk(actor, targetPoint, targetZone);
    }
  }
  function moveLeaderHome(actor, leader) {
    walk(actor, core.leaderCoords[leader], 'leader');
  }
  async function visit(symbol, leader) {
    if (!active || busyVisit) return;
    const target = actors.get('agent:' + symbol);
    const supervisor = actors.get('leader:' + leader);
    if (!target || !supervisor) return;
    busyVisit = true;
    const dest = {x: Math.max(33, target.point.x - 4), y: target.point.y + 7};
    announce(symbol + ': solicitando apoio de ' + leader + '.', target);
    const conversation = leader === 'Carlos'
      ? 'Vou verificar o bloqueio e os limites de risco.'
      : leader === 'Rebeca'
        ? 'Vou conferir as notícias e os dados disponíveis.'
        : 'Vamos revisar o estado e as prioridades do agente.';
    walk(supervisor, dest, 'visiting', () => {
      pose(supervisor, 'standing');
      announce(leader + ': ' + conversation, supervisor);
      setTimeout(() => {
        moveLeaderHome(supervisor, leader);
        busyVisit = false;
      }, 4100);
    });
  }
  function update(state) {
    if (!state || !Array.isArray(state.agents)) return;
    latest = state;
    if (demoActive) return;
    state.agents.slice(0, 12).forEach((agent, idx) => {
      const symbol = String(agent.symbol ?? 'Agente ' + (idx + 1));
      const key = 'agent:' + symbol;
      const zone = core.zoneFor(agent, state);
      const point = zone === 'desk' ? core.desk(idx) : core.lounge(idx);
      const actor = actors.get(key) ?? createActor(key, genderFor(idx), point, symbol, false);
      moveAgent(actor, zone, point);
    });
    for (const leader of leaderNames) {
      const record = (state.supervisors || []).find(s => s.name === leader);
      if (!record) continue;
      const key = 'leader:' + leader;
      const point = core.leaderCoords[leader];
      const actor = actors.get(key) ?? createActor(key, genderFor(leader, true),
        point, leader, true);
      if (!actor.zone) {
        actor.zone = 'leader';
        actor.goal = 'leader';
        pose(actor, 'typing');
      }
    }
    // Only true server requests trigger automatic visits; otherwise the existing
    // manual 'Solicitar visita' button starts the conversation.
    for (const agent of state.agents.slice(0, 12)) {
      const leader = core.requestedLeader(agent);
      if (!leader || busyVisit) continue;
      const symbol = String(agent.symbol);
      const requestId = String(agent.supervisorRequestId ?? agent.requestId ??
        symbol + ':' + leader + ':' + core.normalized(agent.status));
      if (!handled.has(requestId)) {
        handled.add(requestId);
        visit(symbol, leader);
      }
    }
    const currentKeys = new Set([
      ...state.agents.slice(0, 12).map((a, i) => 'agent:' + String(a.symbol ?? 'Agente ' + (i+1))),
      ...(state.supervisors || []).map(s => 'leader:' + s.name)
    ]);
    for (const [key, actor] of actors) {
      if (!currentKeys.has(key)) {
        actor.token++;
        actor.motion?.cancel?.();
        clearTimeout(actor.riseTimer);
        clearTimeout(actor.bubbleTimer);
        actor.el.remove(); actors.delete(key);
      }
    }
  }
  // The existing RAC "Solicitar visita" button remains authoritative.
  document.addEventListener('click', event => {
    const trigger = event.target.closest?.('#agents .office-visit-request');
    if (!trigger || trigger.disabled) return;
    const symbol = trigger.closest('.office-station')?.dataset.officeKey;
    const leader = document.querySelector('.office-visit-controls select')?.value;
    if (symbol && core.leaderCoords[leader]) visit(symbol, leader);
  }, true);
  motionBtn.addEventListener('click', () => {
    active = !active;
    motionBtn.setAttribute('aria-pressed', String(active));
    motionBtn.textContent = 'Personagens animados: ' + (active ? 'ligado' : 'desligado');
    floor.dataset.racMotion = active ? 'on' : 'off';
    for (const actor of actors.values()) {
      actor.token++;
      actor.motion?.cancel?.();
      clearTimeout(actor.riseTimer);
      actor.motion = null;
      actor.el.classList.toggle('motion-disabled', !active);
      if (!active) pose(actor, actor.zone === 'lounge' ? 'resting' : actor.zone === 'desk' || actor.zone === 'leader' ? 'typing' : 'standing');
    }
    if (active && latest) {
      actors.forEach(actor => { actor.zone = ''; actor.goal = null; });
      update(latest);
    }
  });
  // Preview is user-initiated and never affects the PAPER account.
  demoBtn.addEventListener('click', () => {
    if (!latest || !actors.size || demoActive) return;
    demoActive = true;
    demoBtn.disabled = true;
    floor.dataset.racDemo = 'on';
    narration.textContent = 'DEMONSTRAÇÃO VISUAL — não representa o estado real do PAPER.';
    const first = latest.agents[0];
    const second = latest.agents[1];
    const primary = first && actors.get('agent:' + String(first.symbol));
    if (primary) walk(primary, core.desk(0), 'desk');
    const secondary = second && actors.get('agent:' + String(second.symbol));
    if (secondary) walk(secondary, core.lounge(1), 'lounge');
    setTimeout(() => {
      if (demoActive && first && actors.has('leader:Ana')) {
        visit(String(first.symbol), 'Ana');
      }
    }, 5900);
    setTimeout(() => {
      demoActive = false;
      demoBtn.disabled = false;
      floor.dataset.racDemo = 'off';
      narration.textContent = 'Prévia concluída. Personagens sincronizados ao PAPER real.';
      for (const actor of actors.values()) actor.goal = null;
      if (latest) update(latest);
    }, 21500);
  });
  voiceBtn.addEventListener('click', () => {
    voice = !voice;
    voiceBtn.setAttribute('aria-pressed', String(voice));
    voiceBtn.textContent = 'Voz: ' + (voice ? 'ligada' : 'desligada');
    if (!voice) window.speechSynthesis?.cancel?.();
  });
  floor.dataset.racMotion = active ? 'on' : 'off';
  window.RacAgentMotion = Object.freeze({update, visit});
})();
