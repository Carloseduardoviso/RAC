# RAC MVP 1.5.15 — Mapa fotorealista e recuperacao operacional local

Data: 23/11/2026. Host: `080001325ADPLAN`.

## Mapa e validacao

- Novo mapa com imagem WebP local, CSS fotorealista e responsivo, contadores obtidos de dados PAPER e estados acessiveis de 12 agentes e 3 supervisores.
- `tests/frontend/trading-floor-asset.test.js` valida integridade WebP, referencias de CSS, ids dos contadores e ausencia de CSS `!important`.
- 7 testes frontend aprovados, nenhum falhou.
- 258 testes .NET Release aprovados, nenhum falhou.
- `dotnet publish` Release API e Web concluidos em `%LOCALAPPDATA%\RAC\staging\mvp-1.5.15`.

## Backup e servicos

- Backup SQL com CHECKSUM: `%LOCALAPPDATA%\RAC\backups\RAC-pre-MVP1515-20261123-131557.bak`.
- `RESTORE VERIFYONLY WITH CHECKSUM` aprovado. Nenhuma exclusao ou migracao operacional.
- API: `127.0.0.1:5080`; Web: `127.0.0.1:5081`. Bind local sem exposicao externa.
- Primeira inicializacao da API foi bloqueada porque a variavel de conexao SQL nao estava configurada no novo processo. Ajustada configuracao *somente no ambiente do processo* para LocalDB RAC; segunda inicializacao respondeu HTTP 200.
- `GET /api/health` direto e via proxy retornou `status=ok`, `realOrdersEnabled=false`, `testnetOrderEnabled=false`.
- Web raiz, CSS fotorealista, CSS responsivo, WebP e pagina de diagnostico responderam HTTP 200.
- Script `tools/Start-RacPaperLocal.ps1` permite recuperar processos locais sem duplicar listener ou modificar trava de risco; valida as flags de ordens e o health.

## Estado operacional e bloqueio preservado

- No banco: PAPER `Running=0`, `RiskLocked=1`, 17 fills, 1 posicao registrada, Testnet `Enabled=0` e zero ordens Testnet.
- Razao persistida: `Carlos: erro de rede, dados ou persistencia. Revisao obrigatoria.` Evento `PAPER_RISK_LOCK` em 23/11/2026 16:14:13 UTC; alerta critico ainda aberto.
- A causa tecnica especifica desse erro ainda nao foi estabelecida.
- **A trava nao foi removida e o PAPER nao foi retomado.** A liberacao exige revisao humana e justificativa conforme `docs/OPERACAO-RISCO.md`.
- Nenhuma ordem real ou Testnet enviada, nenhuma credencial modificada, nenhuma alteracao nos dados de negociacao.
- A disponibilidade apos reinicio do Windows e a correspondencia visual final com imagens/video permanecem para homologacao separada.
