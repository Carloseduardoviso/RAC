# RAC MVP 1.3.7 — Validação de três meses e estresse de custos

Data da execução: 22/11/2026, aplicação offline. Arquivos de candle público Binance Spot arquivados em `%LOCALAPPDATA%\RAC\research\monthly\{2026-08,2026-09,2026-10}`. Manifests por mês com SHA-256 usados para revalidar cada ZIP antes da análise.

## Amostra

- Três meses UTC completos consecutivos: **agosto (31 dias), setembro (30 dias) e outubro de 2026 (31 dias)**. São 92 dias por ativo, **1.104 janelas diárias de teste** para 12 agentes e **1.589.760 candles históricos** em total (92×1.440×12).
- Cada dia: 720 candles para seleção e 720 candles seguintes para teste de operações completas com 100 USDT fictícios reiniciados por janela. 1/5/15/30 minutos; horizonte escolhido apenas no treino do dia. Holdouts distintos e sem usar os resultados de teste na seleção.
- Dois cenários simulados separadamente: **BASE** com custos assumidos de 0,1% taxa, 0,05% slippage e 0,01% meio spread por lado; **STRESS** dobra cada parcela. A estratégia é escolhida novamente pelo treino no cenário STRESS, logo o desempenho STRESS não é apenas o BASE com taxas descontadas: os trades podem ser diferentes.
- **48.031 operações hipotéticas fechadas no BASE**; **7 de 1.104 janelas BASE positivas**, **12/12 agentes rejeitados**, nenhum promovido.
- Modo do resultado: `THREE_MONTH_COST_STRESS_SHADOW_ONLY`, `promotionAllowed=false`, `orderPlacementEnabled=false`, `paperModified=false` e `historyPersistedInOperationalDatabase=false`.

## Resultados consolidados

Retorno médio (%) é a **média ponderada pelos dias das médias de retornos por janela isolada**; *não* é retorno acumulado de uma carteira real ou estimativa de resultado futuro. O drawdown é o **pior teste entre BASE e STRESS**.

| Agente | Operações BASE | Dias BASE positivos / 92 | Retorno médio BASE (%) | Retorno médio STRESS (%) | Pior drawdown (%) | Gate |
|---|---:|---:|---:|---:|---:|---|
| BTCUSDT | 7.032 | 0 | -21,0289 | -57,0110 | 76,7558 | REJECTED_NO_EDGE |
| ETHUSDT | 5.135 | 1 | -15,4668 | -47,9435 | 76,6666 | REJECTED_NO_EDGE |
| SOLUSDT | 3.243 | 0 | -10,3539 | -37,8589 | 83,6617 | REJECTED_NO_EDGE |
| BNBUSDT | 6.172 | 0 | -18,6355 | -58,5173 | 79,3010 | REJECTED_NO_EDGE |
| XRPUSDT | 3.606 | 2 | -11,3279 | -37,7011 | 78,3651 | REJECTED_NO_EDGE |
| ADAUSDT | 2.406 | 1 | -8,1477 | -21,9801 | 58,4096 | REJECTED_NO_EDGE |
| DOGEUSDT | 3.117 | 1 | -10,0147 | -36,1136 | 75,8737 | REJECTED_NO_EDGE |
| LINKUSDT | 2.850 | 1 | -9,3784 | -30,6192 | 70,3415 | REJECTED_NO_EDGE |
| AVAXUSDT | 2.903 | 0 | -9,3228 | -32,6343 | 74,9843 | REJECTED_NO_EDGE |
| LTCUSDT | 3.009 | 0 | -9,7991 | -35,6353 | 79,4234 | REJECTED_NO_EDGE |
| DOTUSDT | 2.509 | 1 | -8,2479 | -21,6217 | 50,9370 | REJECTED_NO_EDGE |
| TRXUSDT | 6.049 | 0 | -18,9283 | -76,3359 | 87,8388 | REJECTED_NO_EDGE |

## Conclusão e limites

- Em ambos os cenários, **todos os retornos médios ficaram negativos**; a alternativa de não operar, 0%, foi superior em todos os ativos nesse recorte.
- A amostra de três meses consecutivos não garante variação macroeconômica independente. Ainda faltam microestrutura de ordens, custos variáveis, latência e teste Testnet específico e autorizado.
- Nunca utilizar retrospectivamente o fechamento do dia para selecionar estratégia: classificação de regimes de mercado é apenas descritiva e não entra na decisão.
- Resultado salvo no computador: `%LOCALAPPDATA%\RAC\research\comparisons\three-month-2026-08-to-2026-10.json`.
- **Decisão:** manter todas as estratégias de horizonte pesquisadas rejeitadas e PAPER bloqueado; não operar na Binance.
