# RAC MVP 1.5.8 — implantação local validada

Data: 23/11/2026. Computador: `080001325ADPLAN`.

## Preservação

- Backup do SQL LocalDB (`RAC`): `%LOCALAPPDATA%\RAC\backups\RAC-pre-MVP158-20261123.bak`.
- `BACKUP DATABASE ... WITH CHECKSUM` e `RESTORE VERIFYONLY ... WITH CHECKSUM` concluídos com sucesso.
- Estado inicial: PAPER Running=1, RiskLocked=0; 1 posição e 5 fills no banco; Testnet Enabled=0; 0 ordens Testnet.
- PAPER temporariamente pausado por atualização condicional do campo `Running` no SQL, após backup; uma posição e cinco fills preservados durante a troca.

## Implantação

- Processos antigos `RAC.Api.dll` e `RAC.Web.dll` nas portas 5080/5081 substituídos pelos artefatos Release preparados em `%LOCALAPPDATA%\RAC\staging\mvp-1.5.8\api` e `...\web`.
- API local HTTP 200 (`/api/health`); Web HTTP 200 (`/`); JavaScript HTTP 200.
- `/api/research/robustness` na API e no proxy Web responde HTTP 401 sem sessão, confirmando rota ativa e protegida.
- Logs `mvp158-api.err.log` e `mvp158-web.err.log`: zero bytes durante a validação.
- Relatório local `fixed-replay-2026-08-to-2026-10.json`: 12 agentes, 12 NO_TRADE, `orderPlacementEnabled=false`.

## Estado final

- PAPER Running=1, RiskLocked=0 (estado inicial restaurado); 1 posição ainda registrada.
- Binance Spot Testnet Enabled=0; 0 ordens Testnet registradas.
- Endpoint de saúde confirma `realOrdersEnabled=false` e `testnetOrderEnabled=false`.
- Nenhuma ordem foi enviada à Binance neste procedimento. Não foram alteradas credenciais nem habilitados executores reais.

## Observação

- A área visual autenticada foi verificada por presença no HTML e rota protegida; não foi feita navegação autenticada em navegador nesta implantação.
- A pasta local não rastreada `docs/` foi mantida intacta.
