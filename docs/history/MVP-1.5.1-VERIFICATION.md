# RAC MVP 1.5.1 — Validação de decisões congeladas

Data: 23/11/2026. Computador `080001325ADPLAN`.

## Evidências de pesquisa

- Arquivos locais de maio a outubro de 2026 foram avaliados separando **maio, junho e julho** (92 janelas por agente = **1.104** seleções de treinamento) de **agosto, setembro e outubro** (92 janelas por agente = **1.104** janelas posteriores).
- As 12 decisões majoritárias escolhidas **antes de agosto** foram `NO_TRADE`, com horizonte de placeholder igual a 1 minuto. Votos: BTC 91/92, ETH 91/92, SOL 90/92, BNB 91/92, XRP 91/92, ADA 88/92, DOGE 90/92, LINK 90/92, AVAX 84/92, LTC 91/92, DOT 89/92 e TRX 92/92.
- **12/12 agentes ficam sem negociar no modelo congelado**, 0 trades, retorno simulado de 0% por inatividade, 0 elegíveis para revisão e 0 com necessidade de replay ativo nesta amostra. Resultado não é prova de estratégia rentável.
- Relatório CLI: `%LOCALAPPDATA%\RAC\research\monthly\frozen-decision-2026-05-to-2026-10.json`; `mode=PRE_HOLDOUT_FROZEN_DECISION_SHADOW_ONLY`, `agents=12`, `trainingWindows=1104`, `holdoutWindows=1104`, `frozenNoTradeAgents=12`, `strategiesNeedingReplay=0`, `paperModified=false`, `orderPlacementEnabled=false`, `promotionAllowed=false`.

## Qualidade e segurança

- Testes cobrem seleção majoritária, empate conservador em NO_TRADE, recusa de enum e horizonte inválidos, entrada vazia e ausência de relatórios sem converter falta de evidência em retorno zero.
- `dotnet test RAC.slnx -c Release --nologo --verbosity quiet`: **201/201 testes aprovados**, zero falhas. `node --check` da interface aprovado.
- Implantação somente depois de verificar SQL: PAPER parado, Testnet desabilitada e 0 ordens Testnet; sem modificar contas, posições, credenciais, locks ou histórico operacional.

## Interpretação

Decisões de treino e desempenho posterior são mantidos separados. No caso NO_TRADE, o resultado 0% é trivial e não equivale a um backtest com transações executadas de uma estratégia congelada. Os parâmetros não se tornam elegíveis para negociação pela maioria observada.

## Implantação final — 23/11/2026

- **201/201 testes automatizados .NET Release aprovados**, zero falhas; `node --check` aprovado.
- API, Web e CLI publicados em `%LOCALAPPDATA%\RAC\staging\mvp-1.5.1\api`, `...\web`, `...\research`.
- Processos locais antigos substituídos somente após SQL confirmar PAPER `Running=0` e Testnet `Enabled=0`.
- API `http://localhost:5080/api/health` HTTP 200, `realOrdersEnabled=false`, `testnetOrderEnabled=false`.
- Painel `http://localhost:5081/` HTTP 200 com `frozenDecisionsPanel`; script JS HTTP 200 com `refreshFrozenDecisions`.
- Rota autenticada `/api/research/frozen-decisions` retorna HTTP 401 sem login tanto na API quanto pelo proxy.
- Logs `mvp151-api.err.log` e `mvp151-web.err.log`: **0 bytes**.
- Banco LocalDB após deploy: PAPER `Running=0`, Testnet `Enabled=0`, **0 ordens Testnet**; nenhum banco foi alterado pela pesquisa.
- Relatório SHADOW final: 12 agentes, 1104 escolhas no treino, 1104 janelas posteriores, 12 gates NO_TRADE congelados, zero aprovação, flags PAPER/ordens/promoção falsas.
- Poderá ser necessário recarregar a página e autenticar novamente após a substituição dos serviços.
