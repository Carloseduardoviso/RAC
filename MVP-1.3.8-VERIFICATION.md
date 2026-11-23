# RAC MVP 1.3.8 — Evidência de filtros conservadores e NO_TRADE

Data: 22/11/2026. Execução manual e isolada `RAC.HistoryResearch --entry-gates YYYY-MM` nos meses UTC 2026-08, 2026-09 e 2026-10, com 12 pares Binance Spot por mês, dados ZIP do acervo público previamente validado via manifesto SHA-256.

- Cada dia: primeiras 720 velas 1m **treino**, 720 velas 1m posteriores **teste**. A escolha de um dentre quatro gates (`BASELINE`, `TREND_STRENGTH`, `ATR_COST`, `TREND_AND_ATR`) com horizontes de 15/30m é feita **somente no treino**. Se não houver pelo menos três operações de treino encerradas com lucro líquido simulado estritamente positivo, `NO_TRADE` é escolhido (retorno e drawdown 0%). O teste pode ainda gerar perdas.
- Saída local por mês: `%LOCALAPPDATA%\RAC\research\monthly\YYYY-MM\entry-quality-no-trade.json`. Nenhuma gravação no banco operacional ou comunicação privada com Binance.
- Em 12 agentes e 92 dias UTC por agente: **1.104 janelas**, **1.072 janelas NO_TRADE (97,1014%)**, **32 janelas com trading simulado**, **364 operações hipotéticas fechadas** e **apenas 2 janelas positivas**. Em **0 de 36 relatórios símbolo-mês** o critério de revisão de estratégia foi atingido.
- Não interpretar o pequeno prejuízo médio em períodos majoritariamente sem operações como retorno rentável; a estratégia não superou persistentemente a alternativa de ficar sem operar.

| Símbolo | Dias NO_TRADE / 92 | Trades simulados nos testes | Retorno médio líquido diário dos filtros (%) | Retorno médio da referência antiga (%) |
|---|---:|---:|---:|---:|
| BTCUSDT | 91 | 4 | -0,0109 | -21,0289 |
| ETHUSDT | 92 | 0 | 0,0000 | -15,4668 |
| SOLUSDT | 90 | 12 | -0,0690 | -10,3539 |
| BNBUSDT | 89 | 8 | -0,0175 | -18,6355 |
| XRPUSDT | 87 | 68 | -0,1434 | -11,3279 |
| ADAUSDT | 89 | 51 | -0,1592 | -8,1477 |
| DOGEUSDT | 90 | 13 | -0,0389 | -10,0147 |
| LINKUSDT | 88 | 30 | -0,1131 | -9,3784 |
| AVAXUSDT | 87 | 82 | -0,2860 | -9,3228 |
| LTCUSDT | 90 | 25 | -0,0441 | -9,7991 |
| DOTUSDT | 87 | 71 | -0,2378 | -8,2479 |
| TRXUSDT | 92 | 0 | 0,0000 | -18,9283 |

Retornos são **médias aritméticas ponderadas pelos dias** de janelas simuladas que reiniciam com 100 USDT fictícios; não representam uma carteira composta, previsão de lucro ou resultados da conta PAPER. A referência antiga envolve seleção de horizontes e negocia com muito mais frequência; portanto a comparação mede comportamentos diferentes e não isola o efeito causal de um único filtro.

## Conclusão

- 36 relatórios completos, 12 pares × 3 meses; todos classificados como `REJECTED_OR_INSUFFICIENT_EDGE`.
- As condições de entrada estudadas continuam **SHADOW_ONLY**, e `promotionAllowed=false` no resultado JSON.
- Não habilitar ordens, reiniciar PAPER bloqueado nem elevar limites financeiros.
