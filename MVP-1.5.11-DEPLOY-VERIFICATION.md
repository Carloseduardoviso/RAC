# RAC MVP 1.5.11 — implantação local verificada

Data: 23/11/2026. Host: `080001325ADPLAN`.

## Backup e preservação

- Backup SQL LocalDB `RAC`: `%LOCALAPPDATA%\RAC\backups\RAC-pre-MVP1511-20261123.bak` (10.014.720 bytes).
- `BACKUP DATABASE WITH CHECKSUM` e `RESTORE VERIFYONLY WITH CHECKSUM`: aprovados.
- Antes da implantação: PAPER `Running=1`, `RiskLocked=0`, 1 posição, 13 fills; Testnet `Enabled=0`, 0 ordens; 0 propostas e 0 auditorias documentais.
- PAPER pausado de forma controlada via alteração condicional de `Running`, depois do backup. Registros e posição preservados.

## Publicação e testes

- API anterior na porta 5080 e Web anterior na porta 5081 substituídas pelos builds Release `%LOCALAPPDATA%\RAC\staging\mvp-1.5.11\api` e `...\web`.
- Processos novos API PID 24580 e Web PID 24312, registrados após inicialização.
- API `/api/health` HTTP 200; Web `/` HTTP 200.
- API e proxy Web: `GET /api/risk/policies`, `POST /api/risk/policies` e `POST /api/risk/policies/1/review` responderam HTTP 401 sem sessão autenticada (controle de acesso confirmado; sem submissões de políticas).
- Arquivos `mvp1511-api.err.log` e `mvp1511-web.err.log` sem conteúdo na verificação.
- Login autenticado e interação real com formulários do dashboard não foram testados nesta implantação; exigem sessão do usuário.

## Estado final

- PAPER restaurado a `Running=1`, `RiskLocked=0`; 1 posição e 13 fills registrados na conferência final.
- Testnet `Enabled=0`, 0 ordens Testnet registradas; API confirma `realOrdersEnabled=false`, `testnetOrderEnabled=false`.
- Tabelas `rac.ProductionRiskPolicies` e `rac.ProductionRiskPolicyAudits`: 0 registros; nenhuma política ou revisão foi criada durante o deploy.
- Nenhuma ordem enviada à Binance. Nenhuma credencial alterada.
- Pasta local não rastreada `docs/` preservada.
