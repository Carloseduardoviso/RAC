# RAC MVP 1.5.17 — Homologação do dossiê de revisão de risco PAPER

Data: 23/11/2026. Computador: `080001325ADPLAN`.

## Contexto e escopo

- **Não confundir** `POST /api/risk/review`, que controla o simulador anterior, com a carteira independente `rac.PaperAccounts`.
- A trava PAPER observada ocorreu em `2026-11-23 16:14:13 UTC`, com motivo genérico `Carlos: erro de rede, dados ou persistência. Revisão obrigatória.`. O log da exceção original não foi encontrado; sua causa técnica permanece indeterminada.
- A implementação **não aprova** e **não desbloqueia** o PAPER. Nenhuma rota de desbloqueio PAPER foi criada.
- O modo de ordens real e Testnet permanece desabilitado; nenhuma chamada para envio de ordens foi executada.

## Implementação

- Serviço `PaperRiskReviewService` utiliza exclusivamente `AsNoTracking` e consultas SQL para reunir evidências: estado `Running/RiskLocked`, motivo persistido, último ciclo, caixa, último patrimônio, perdas relativas à referência diária e ao pico, fills, posição(ões) e incidentes de auditoria.
- O valor em BRL de cada posição utiliza **último preço persistido**, não cotação em tempo real. Sinaliza `valuationStale` se o ciclo for antigo ou ausente.
- Resposta sempre contém `mode=READ_ONLY_PAPER_RISK_REVIEW`, `reviewApproved=false`, `unlockPermitted=false`, `realOrdersEnabled=false` e `testnetOrdersEnabled=false`.
- Nova rota `GET /api/paper/risk-review` com política de autenticação `RacRiskReviewer`, `Cache-Control: no-store` e falha SQL fechada em HTTP 503.
- Proxy Web inclui **somente GET** da rota em lista explícita, sem POST de aprovação.
- Página `/paper-risk-review.html`, JS `/paper-risk-review.js`, link a partir de `/risk-security.html`. Os textos do SQL são renderizados via `textContent`, sem `innerHTML`, formulários de aprovação ou controles de negociação.
- Script de recuperação e watchdog (tarefa existente `RAC-MVP1516-Watchdog`) apontam, por padrão, para a publicação `mvp-1.5.17`.

## Homologação e dados preservados

- 9 testes de frontend aprovados, 0 falhas; inclui proibição de POST e conteúdo do dossiê.
- Suíte .NET Release: 270 testes aprovados, 0 falhas, incluindo prova SQL isolada (banco descartável) que preserva a trava, a posição, fill e auditoria após múltiplas consultas do serviço.
- Teste HTTP autenticado isolado, com usuários temporários e SQL descartável, validou: anônimo HTTP 401; Viewer HTTP 403; RiskReviewer e Administrator HTTP 200; tentativa POST na rota HTTP 405; resposta sem permissão de desbloqueio.
- Backup SQL antes do deploy: `%LOCALAPPDATA%\RAC\backups\RAC-pre-MVP1517-20261123-134712.bak`; `BACKUP WITH CHECKSUM` e `RESTORE VERIFYONLY WITH CHECKSUM` aprovados.
- `dotnet publish` Release da API e Web concluído em `%LOCALAPPDATA%\RAC\staging\mvp-1.5.17`. API e Web foram substituídos apenas após identificação das DLLs nas portas locais 5080/5081.
- API, proxy, página do dossiê, JS e painel de segurança responderam HTTP 200; rota do dossiê retornou HTTP 401 sem sessão, tanto direta como via proxy.
- Task watchdog desabilitada **somente durante a troca controlada dos processos**, reabilitada em seguida.
- Estado do SQL antes da publicação: PAPER `Running=0`, `RiskLocked=1`, `LastEquityBrl=99.556026848848`, 17 fills, 1 posição DOTUSDT, 0 ordens Testnet, Testnet `Enabled=0`.
- Nenhuma alteração de esquema, nenhuma transação de liberação ou ordem à Binance.
- Arquivo local `src/RAC.Web/wwwroot/qa-photoreal-preview.html` não rastreado pertence a outro trabalho e foi preservado fora do commit.

## Pendências / decisão humana separada

1. Revisar o dossiê autenticado no computador do RAC e investigar o motivo do incidente. O motivo genérico histórico não demonstra causa raiz conclusiva.
2. Avaliar a posição DOTUSDT, as cotações e a atualização dos dados antes de uma decisão futura sobre o PAPER.
3. Somente com **autorização humana explícita e separada**, projetar e homologar eventual workflow de liberação com justificativa, auditoria e controles de acesso. **Este MVP não libera a trava.**
4. Teste de navegação com usuário autenticado no navegador operacional continua pendente; o fluxo de autenticação foi homologado por HTTP em banco descartável.
