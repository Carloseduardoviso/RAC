# Política de Risco Operacional Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Registrar as regras financeiras atuais do RAC em uma política operacional versionada, sem habilitar operações reais.

**Architecture:** Criar um documento Markdown independente em `docs/` que seja referência humana para o simulador e para futuras fases. Não alterar código, banco, configurações ou integrações externas.

**Tech Stack:** Markdown, Git.

**Spec:** `docs/superpowers/specs/2026-11-23-politica-risco-operacional-design.md`

## Global Constraints

- O RAC continuará em simulação local.
- `RealOrdersEnabled` e `TestnetOrdersEnabled` permanecerão desativados.
- Os valores documentados devem refletir a configuração existente, sem criar autorização para operar dinheiro real.
- A política deve exigir parada e revisão humana após os limites de risco.

## Review Focus

- Confusão entre capital simulado e capital real: o documento deve distinguir os modos explicitamente.
- Aporte mascarando prejuízo: o drawdown deve continuar sendo calculado separadamente dos aportes.
- Reinício após bloqueio: nenhum aporte ou reinício automático deve liberar a trava.
- Escopo de operação: a política não deve autorizar margem, alavancagem ou saques.
- Divergência entre documento e configuração: os valores devem coincidir com `src/RAC.Api/appsettings.json`.

### Task 1: Criar política operacional definitiva

**Files:**
- Create: `docs/OPERACAO-RISCO.md`
- Reference: `src/RAC.Api/appsettings.json`
- Reference: `SEGURANCA.md`
- Reference: `README.md`

**Interfaces:**
- Consumes: limites de risco existentes e comportamento de bloqueio documentado no RAC.
- Produces: uma política operacional humana, sem mudança de runtime.

- [ ] **Step 1: Criar `docs/OPERACAO-RISCO.md`** com objetivo, escopo, modos permitidos, valores de risco, travas, critérios de parada, responsabilidades e checklist antes de qualquer futura produção.
- [ ] **Step 2: Conferir que a política declara explicitamente simulação local, ausência de ordens reais e ausência de autorização para produção.**
- [ ] **Step 3: Executar `git diff --check` para validar a documentação.**
- [ ] **Step 4: Executar `dotnet build RAC.slnx --no-restore` e `dotnet test RAC.slnx --no-build` para garantir que a documentação não alterou o software.**

## Self-review

- Cobertura: todos os itens do desenho aprovado estão representados na Task 1.
- Escopo: nenhum código, migration, dependência ou segredo será alterado.
- Riscos: os cinco casos mais prováveis de interpretação incorreta estão no Review Focus.
- Proporção: uma tarefa documental é suficiente para esta etapa.
