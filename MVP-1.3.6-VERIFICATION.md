# RAC MVP 1.3.6 — Verificação do walk-forward mensal

Data: 22/11/2026; dados públicos Binance Spot de **outubro de 2026**, 31 dias. Execução offline usando `%LOCALAPPDATA%\RAC\research\monthly\2026-10\manifest.json` e 12 ZIPs previamente verificados por SHA-256. Sem acesso ao banco operacional nem a qualquer método de ordens.

## Amostra e segregação

- **535.680 candles 1m**, 44.640 por símbolo, 31 dias UTC.
- **372 holdouts** (12 agentes × 31 dias): treino de 720 minutos na manhã UTC de cada dia e avaliação nos 720 minutos seguintes do mesmo dia. O horizonte 1/5/15/30 minutos é selecionado somente no treino. Cada holdout começa com 100 USDT fictícios.
- **15.582 operações simuladas encerradas**, com **896 operações vencedoras**, nos 372 holdouts. Isso não representa fills do PAPER ou operações Binance. Como as operações podem se repetir e os parâmetros são simplificados, os eventos não têm independência estatística garantida.
- **0 dias de teste positivos no conjunto de 372**, **0 agentes aprovados**. Todos foram classificados como `REJECTED_NO_EDGE`.
- Modo JSON: `OFFLINE_MONTHLY_WALK_FORWARD_SHADOW_ONLY`; `count=12`, `paperModified=false`, `orderPlacementEnabled=false`, `promotionAllowed=false`.

## Resultados por símbolo

Os números de retorno são **médias aritméticas dos retornos percentuais de 31 holdouts isolados**, e não o desempenho acumulado de um portfólio de 31 dias.

| Agente | Operações simuladas encerradas | Dias positivos / 31 | Retorno líquido médio por teste (%) | Pior drawdown diário (%) | Gate |
|---|---:|---:|---:|---:|---|
| BTCUSDT | 2.471 | 0 | -21,9295 | 45,9931 | REJECTED_NO_EDGE |
| ETHUSDT | 1.693 | 0 | -15,4574 | 45,5763 | REJECTED_NO_EDGE |
| SOLUSDT | 1.058 | 0 | -10,3467 | 24,9897 | REJECTED_NO_EDGE |
| BNBUSDT | 2.187 | 0 | -19,5959 | 41,4330 | REJECTED_NO_EDGE |
| XRPUSDT | 1.204 | 0 | -11,6053 | 38,6953 | REJECTED_NO_EDGE |
| ADAUSDT | 790 | 0 | -8,1279 | 12,4061 | REJECTED_NO_EDGE |
| DOGEUSDT | 936 | 0 | -9,2121 | 23,7457 | REJECTED_NO_EDGE |
| LINKUSDT | 897 | 0 | -8,8356 | 14,7807 | REJECTED_NO_EDGE |
| AVAXUSDT | 848 | 0 | -8,1755 | 12,9935 | REJECTED_NO_EDGE |
| LTCUSDT | 893 | 0 | -8,8780 | 13,2363 | REJECTED_NO_EDGE |
| DOTUSDT | 803 | 0 | -7,9473 | 13,0977 | REJECTED_NO_EDGE |
| TRXUSDT | 1.802 | 0 | -16,9001 | 29,2962 | REJECTED_NO_EDGE |

## Regimes descritivos

O JSON detalha a média de cada agente em dias históricos de alta (>1%), baixa (<-1%) e lateralização (demais). **Todas as médias por regime calculadas nesta execução permaneceram negativas**. As categorias são formadas usando o retorno do dia completo, depois do teste — válidas somente para análise retrospectiva e **proibidas como entrada de decisão operacional**.

## Evidência em disco

- `%LOCALAPPDATA%\RAC\research\monthly\2026-10\walk-forward-monthly.json` — resultado completo por agente, por dia e por regime.
- `%LOCALAPPDATA%\RAC\research\monthly\2026-10\manifest.json` — proveniência dos arquivos com hashes SHA-256.

## Decisão

**Rejeitar todos os horizontes testados para uso no PAPER.** A alternativa de não negociar (0%) superou a média líquida hipotética de todos os agentes. Nenhuma ordem foi enviada, nenhuma estratégia aprovada, nenhuma trava desabilitada.
