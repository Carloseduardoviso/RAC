# RAC MVP 1.2.4 — correções de estabilidade antes do MVP 1.2.5

Data: 22/11/2026.

## Diagnóstico real do PAPER

Acompanhamento em SQL somente leitura encontrou `Running=1` e avanço periódico do `LastCycleUtc`, inclusive uma compra fictícia `DOTUSDT` nos logs da API. Portanto, não foi confirmada uma parada permanente do worker. Os alertas dos 12 agentes observados eram transitórios: ocorrências abertas em 20:22:08 UTC foram resolvidas em 20:23:08 UTC, enquanto o motor seguiu gerando ciclos. Alertas críticos de risco não foram alterados.

## Melhorias de diagnóstico

Novo `PaperCycleDiagnostics` distingue `UNINITIALIZED`, `PAUSED`, `RISK_LOCKED`, `STARTING`, `RUNNING`, `STALLED` e `CLOCK_SKEW`; calcula a idade do último ciclo persistido e descreve o motivo. O endpoint autenticado `/api/paper/monitoring` passa a retornar `cycleHealth`, e o painel exibe estado do motor e idade do último ciclo. Nunca confundir `Running=1` sozinho com execução efetiva.

Alertas `AGENT_<SYMBOL>` e `AGENTS_NOT_READY` agora exigem duas observações com pelo menos 50 segundos de intervalo e no máximo três minutos entre elas, para ignorar oscilações de apenas um ciclo; observações interrompidas são descartadas. `PAPER_CYCLE_STALE`, `MARKET_STALE`, `RISK_LOCKED`, perdas, drawdown e inconsistência de saldo continuam imediatos. O debounce é memória volátil do watchdog e não altera registros financeiros, não apaga alertas históricos nem reativa travas.

## Segurança

PAPER preservado, 12 símbolos mantidos, sem limites de quantidade diária, limites financeiros intactos, Testnet/real não habilitados, nenhuma ordem enviada. A integração SQL descartável foi estendida para verificar o estado `PAUSED` após restauração. Testes adicionais cobrem estado do motor, defasagem de relógio, período de aquecimento e debounce.

## Publicação

Código e testes na branch `main`; **não implantar diretamente sem reinício controlado**. Para manter o PAPER em andamento, a instância em execução permanece na release MVP 1.2.4 até nova janela de ativação autorizada, com backup SQL, rollback para 1.2.4 e retomada PAPER via endpoint autenticado.
