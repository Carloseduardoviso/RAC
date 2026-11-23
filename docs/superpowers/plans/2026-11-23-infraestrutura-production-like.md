# Preparação de Infraestrutura Production-like Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Criar um runbook versionado para preparar o RAC para uma futura operação segura, sem publicar o sistema ou habilitar ordens.

**Architecture:** Documentar a separação entre Web, API, SQL Server e observabilidade, com configuração externa e segredos fora do repositório. O runbook cobrirá desenvolvimento controlado, Testnet e produção futura, sem assumir provedor ou domínio.

**Tech Stack:** Markdown, ASP.NET Core/.NET 10, SQL Server, Docker como referência de empacotamento.

**Spec:** `docs/superpowers/specs/2026-11-23-infraestrutura-production-like-design.md`

## Global Constraints

- Não publicar o RAC nem abrir portas na Internet.
- Não alterar código de execução de ordens, banco ou autenticação.
- Não armazenar segredos no repositório ou em `appsettings.json`.
- Produção e ordens reais continuarão desabilitadas por padrão.
- Não executar migrations de produção nem criar chaves da Binance.
- O runbook deve exigir backup/restauração testada, TLS validado, parada externa e preservação do bloqueio após reinício.

## Review Focus

- Exposição acidental: instruções devem manter endpoints locais e impedir publicação sem HTTPS.
- Segredos: nenhum exemplo pode conter chave, senha ou valor sensível real.
- Banco: backup sem teste de restauração não deve ser tratado como prontidão.
- Reinício: o bloqueio de risco deve permanecer após reinício manual ou automático.
- Docker: limitações do Dockerfile atual devem ser descritas sem alegar que ele entrega produção completa.

### Task 1: Criar runbook de infraestrutura

**Files:**
- Create: `docs/INFRAESTRUTURA-PRODUCTION-LIKE.md`
- Reference: `Dockerfile`
- Reference: `COMO-EXECUTAR.md`
- Reference: `SQL-SERVER.md`
- Reference: `SEGURANCA.md`

**Interfaces:**
- Consumes: arquitetura e restrições atuais do RAC.
- Produces: runbook humano com pré-requisitos, ambientes, configuração, operação, backup, monitoramento, kill switch, rollback e gates de produção.

- [ ] **Step 1: Criar o runbook** com matriz de ambientes local/Testnet/produção futura, sem autorizar produção nesta fase.
- [ ] **Step 2: Documentar configuração externa e secrets** sem inserir valores reais ou recomendar secrets em `appsettings.json`.
- [ ] **Step 3: Documentar SQL Server, TLS, backup/restauração, sincronização de horário, logs, alertas, parada externa e rollback.**
- [ ] **Step 4: Registrar as limitações do Dockerfile atual** e os itens necessários para um deploy futuro com Web, API, banco e HTTPS.
- [ ] **Step 5: Adicionar checklist de prontidão e critérios de bloqueio**, incluindo ordens reais desabilitadas, chave sem saque, Testnet validada e aprovação humana.
- [ ] **Step 6: Executar `git diff --check`, `dotnet build RAC.slnx --no-restore` e `dotnet test RAC.slnx --no-build`.**

## Self-review

- Cobertura: cada seção do desenho aprovado possui uma etapa no Task 1.
- Escopo: o único artefato novo de produto será documentação; nenhum runtime será modificado.
- Riscos: exposição, secrets, banco, reinício e Docker estão explicitamente no Review Focus.
- Proporção: um runbook único é suficiente enquanto provedor e domínio não foram escolhidos.
