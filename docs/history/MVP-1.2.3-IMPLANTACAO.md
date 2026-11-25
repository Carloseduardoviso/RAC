# RAC MVP 1.2.3 — release preparada sem reiniciar o PAPER

## Release local validada

Publicações Release da API e do painel estão em `%LOCALAPPDATA%\RAC\staging\mvp-1.2.3\api` e `...\web`, produzidas a partir de `c78c7da`. A suíte Release teve 93 testes aprovados, e `node --check` passou.

## Backup e preflight

Criado backup SQL `RAC-pre-MVP123-*.bak` em `%LOCALAPPDATA%\RAC\backups`; `RESTORE VERIFYONLY WITH CHECKSUM` confirmou que o backup pode ser lido e que o checksum é válido. Execute `powershell -NoProfile -File tools\Preflight-Mvp123.ps1` para confirmar artefatos, saúde da API, processos e travas PAPER/Testnet. Esse script é somente leitura.

## Ativação ainda pendente

A versão em execução ainda é a release `unlimited-daily-entries`. Trocar a API atualmente exige reiniciar o processo. O serviço PAPER é restaurado intencionalmente pausado após reinício e um segundo executor concorrente não é seguro. Portanto, esta preparação deliberadamente NÃO mata processos, NÃO força reinício, NÃO escreve no SQL operacional, NÃO retoma a simulação por SQL e NÃO envia ordens à Binance. A operação continua ilimitada em quantidade, com limites financeiros preservados.

Antes de uma ativação futura, deve existir uma janela de interrupção explicitamente aceita, procedimento documentado de retomada via API autenticada, verificações pós-reinício e rollback para a release anterior, nunca reexecutando automaticamente o antigo script que altera `Running` diretamente no SQL.
