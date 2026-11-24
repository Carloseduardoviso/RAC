# Agent Motion 2.5D Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Tornar os personagens do escritório mais vivos com animações 2.5D de digitação, micro-movimentos, conversa e caminhada, sem alterar o PAPER.

**Architecture:** Manter a separação entre `trading-floor-motion-core.js` (regras puras), `trading-floor-motion.js` (estado e transições) e `trading-floor-motion.css` (renderização). A melhoria será feita sobre os WebP atuais, usando classes/data attributes e animações CSS com fallback para movimento reduzido/desligado.

**Tech Stack:** JavaScript browser vanilla, CSS animations/Web Animations API, Node test runner, .NET test/build existente.

**Spec:** `docs/superpowers/specs/2026-11-23-agent-motion-2.5d-design.md`

## Global Constraints

- O PAPER permanece pausado e bloqueado: `Running=0`, `RiskLocked=1`.
- A camada visual não faz chamadas de rede, não envia ordens e não modifica banco ou serviços.
- O cenário permanece pré-renderizado e sem câmera 3D livre.
- `prefers-reduced-motion`, botão de desligar e fallback dos WebP devem continuar funcionando.

## Review Focus

- Agente permanece na mesma zona: animação não deve reiniciar ou acumular timers.
- PAPER pausado/bloqueado: personagens devem ir apenas para a representação visual de lazer.
- Movimento desligado ou `prefers-reduced-motion`: nenhum loop visual deve continuar.
- Visita da liderança durante caminhada: movimento anterior deve ser cancelado sem personagem duplicado.
- Assets ausentes ou navegador sem `Element.animate`: personagem deve continuar visível em pose estática.

### Task 1: Fixar o comportamento visual com testes frontend

**Files:**
- Modify: `tests/frontend/trading-floor-motion.test.js`
- Test: `src/RAC.Web/wwwroot/trading-floor-motion-core.js`, `src/RAC.Web/wwwroot/trading-floor-motion.js`, `src/RAC.Web/wwwroot/trading-floor-motion.css`

**Interfaces:**
- Consumes: regras e seletores existentes da camada de movimento.
- Produces: testes que protegem as transições e os estados visuais novos.

- [ ] **Step 1:** Adicionar testes para as classes/data attributes de digitação, repouso, caminhada e fala, além de verificar que a proteção de risco continua presente.
- [ ] **Step 2:** Executar `node --test tests/frontend/trading-floor-motion.test.js` e confirmar que os novos testes falham antes da implementação.

### Task 2: Implementar micro-movimentos e estados de pose

**Files:**
- Modify: `src/RAC.Web/wwwroot/trading-floor-motion.js`
- Modify: `src/RAC.Web/wwwroot/trading-floor-motion.css`

**Interfaces:**
- Consumes: `pose(actor, type)`, `walk(actor, destination, targetZone, callback)` e os sprites WebP atuais.
- Produces: estados `typing`, `standing`, `rising`, `walking` e `speaking` visualmente distintos, sem mudar o contrato público `window.RacAgentMotion`.

- [ ] **Step 1:** Ajustar a criação/alteração de pose para usar classes estáveis e não reiniciar a animação quando o agente continua na mesma zona.
- [ ] **Step 2:** Adicionar ciclos CSS leves para digitação, postura, olhar e conversa; manter duração curta e sutil para não competir com a interface.
- [ ] **Step 3:** Garantir que desligar movimento remova loops e que `prefers-reduced-motion` mantenha pose estática.
- [ ] **Step 4:** Executar `node --test tests/frontend/trading-floor-motion.test.js` e confirmar aprovação.

### Task 3: Verificar integração e segurança operacional

**Files:**
- Modify: `src/RAC.Web/wwwroot/index.html` only if cache-busting versions require update.
- Modify: `MVP-1.5.18-AGENT-MOTION.md` only if behavior differs from the documented MVP.

**Interfaces:**
- Consumes: camada 2.5D implementada nas tarefas anteriores.
- Produces: validação de sintaxe, testes e build sem alteração de trading.

- [ ] **Step 1:** Executar `node --check src/RAC.Web/wwwroot/trading-floor-motion-core.js` e `node --check src/RAC.Web/wwwroot/trading-floor-motion.js`.
- [ ] **Step 2:** Executar `dotnet test --no-restore`.
- [ ] **Step 3:** Executar o build Release da solução/projeto web.
- [ ] **Step 4:** Conferir no navegador com Ctrl+F5 que os personagens digitam, respiram e caminham; conferir que os indicadores PAPER/RiskLocked permanecem inalterados.

