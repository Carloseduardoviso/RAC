# RAC MVP 1.3.1 — Verificação real em modo somente leitura

Data: 2026-11-22. Origem: consulta de candles públicos Binance Spot `data-api.binance.vision` pelo utilitário local isolado `%LOCALAPPDATA%\RAC\analysis\walk-forward-smoke`. Nenhuma credencial, ordem, gravação SQL ou desbloqueio PAPER.

## Evidência funcional

- `mode=WALK_FORWARD_RESEARCH_ONLY`, `count=12`, `paperModified=false`, `orderPlacementEnabled=false`, `historyPersisted=false`.
- Bloco `profitabilityDiagnostics`: `mode=READ_ONLY_SIGNAL_COST_DIAGNOSTICS`, `count=12`, `paperModified=false`, custo hipotético de ida e volta **0,3196%**.
- 12 agentes avaliados com 999 ou 1000 candles fechados, seis folds walk-forward por par; **0 folds selecionados com retorno positivo** na consulta observada.
- **4.160 sinais-candle** de compra no histórico atual. Apenas **13 movimentos abertura-fechamento do candle seguinte** ultrapassaram os custos hipotéticos quando comparados como compra e venda fictícia imediatas. Sinais consecutivos podem se sobrepor e não equivalem a trades reais, nem a ordens PAPER; não extrapolar 13/4.160 como win rate de uma estratégia.
- Todos os 12 agentes apresentaram média líquida hipotética negativa no movimento imediato do candle seguinte, variando de aproximadamente -0,315% a -0,325% segundo par e período.
- A volatilidade ATR de entrada, expressa em percentual do preço, ficou abaixo da referência hipotética de custo na ampla maioria dos sinais. Este é um diagnóstico descritivo de escala e não prova causal.

## Amostra por agente

| Par | Sinais-candle de compra | Próximo candle positivo | Hipotético líquido positivo | Movimento líquido médio (%) | Stops do holdout baseline / selecionado |
|---|---:|---:|---:|---:|---:|
| BTCUSDT | 317 | 145 | 0 | -0,3205 | 21 / 18 |
| ETHUSDT | 349 | 172 | 3 | -0,3150 | 13 / 11 |
| SOLUSDT | 372 | 166 | 2 | -0,3153 | 13 / 14 |
| BNBUSDT | 312 | 135 | 0 | -0,3232 | 19 / 18 |
| XRPUSDT | 318 | 122 | 1 | -0,3246 | 18 / 13 |
| ADAUSDT | 345 | 91 | 0 | -0,3222 | 18 / 14 |
| DOGEUSDT | 338 | 136 | 2 | -0,3199 | 12 / 13 |
| LINKUSDT | 392 | 184 | 1 | -0,3162 | 10 / 9 |
| AVAXUSDT | 348 | 132 | 2 | -0,3200 | 16 / 15 |
| LTCUSDT | 339 | 135 | 1 | -0,3204 | 13 / 11 |
| DOTUSDT | 342 | 51 | 1 | -0,3174 | 11 / 11 |
| TRXUSDT | 388 | 100 | 0 | -0,3187 | 43 / 26 |

## Limitações e decisão

- A medição do candle seguinte não é um backtest completo: sinais não representam ordens, movimentos podem ter sobreposição, e não modelamos filtros de tamanho, spread real, liquidez nem dimensionamento em BRL da carteira PAPER.
- Os stops citados pertencem exclusivamente aos cenários experimentais SHADOW/walk-forward e não correspondem a fills reais ou a stops PAPER.
- A coleta usou uma janela muito curta, na casa de 16-17 horas, e custos fixos assumidos. A observação justifica pesquisar horizontes, custos e condições de mercado, não promete lucro.
- **Não promover estratégia nem remover proteções financeiras**. O PAPER deve permanecer bloqueado até análise e autorização específica.
- Implementação: commit `edb0cb4`; testes automatizados Release **135/135 aprovados**. Versão preparada em `%LOCALAPPDATA%\RAC\staging\mvp-1.3.1`, ainda não implantada nos serviços.
