# RAC MVP 1.2.9 — Resultado da análise SHADOW dos 12 agentes

Coleta e cálculo: 22/11/2026, horário de Porto Velho. Fonte: SQL LocalDB `RAC`, candles `rac.MarketCandles` de 1 minuto, 392 candles por par, via `RacStrategyService.ShadowComparisonAsync` em programa local **somente leitura** fora do repositório. Não houve envio de ordens nem alteração do PAPER.

## Método e limites

- Cada série de 392 candles foi dividida em duas janelas cronológicas disjuntas, 196 candles por janela, sem consultar candles futuros para produzir sinais passados. Os indicadores foram aquecidos em cada janela individualmente.
- Duas hipóteses **simuladas**, usando sinais SMA 9/21, RSI14, ATR14 e stop 2x ATR no simulador experimental: referência sem cooldown e candidata com espera de cinco candles depois de uma saída.
- Capital fictício e independente de 100 USDT em cada teste, taxa hipotética 0,1%, slippage 0,05% e meio spread 0,01% por lado. `returnPct` é a variação percentual sobre 100 USDT. Stops são apenas os do cenário experimental e não reproduzem a execução e dimensionamento da carteira PAPER BRL.
- Trata-se de comparação de dois períodos da **mesma amostra observada**: os parâmetros não foram escolhidos exclusivamente usando um período de treino separado, tampouco passaram por um holdout prospectivo ou teste walk-forward completo. Não interpretar como comprovação de rentabilidade.

## Resultados individuais

Todos os números de retorno abaixo são **percentuais negativos**. A notação `referência → pausa` compara a ausência de cooldown à pausa de cinco candles.

| Par | Antigo: retorno base → pausa (%) | Recente: retorno base → pausa (%) | Stops ATR recente base → pausa | Entradas bloqueadas pela pausa recente | Conclusão histórica |
|---|---:|---:|---:|---:|---|
| BTCUSDT | -2,2041 → -1,6859 | -5,1767 → -2,9957 | 14 → 8 | 15 | Menor perda em ambos |
| ETHUSDT | -2,7212 → -1,9962 | -3,0614 → -1,8368 | 5 → 3 | 9 | Menor perda em ambos |
| SOLUSDT | -2,0182 → -2,7147 | -1,9505 → -1,5728 | 4 → 3 | 10 | Resultado misto |
| BNBUSDT | -4,0961 → -2,7448 | -3,1298 → -2,5457 | 8 → 6 | 17 | Menor perda em ambos |
| XRPUSDT | -4,8490 → -2,2855 | -2,9323 → -2,3904 | 6 → 5 | 13 | Menor perda em ambos |
| ADAUSDT | -3,6566 → -2,7868 | -3,6879 → -2,3504 | 7 → 6 | 23 | Menor perda em ambos |
| DOGEUSDT | -2,7904 → -2,7922 | -2,1180 → -3,9758 | 2 → 7 | 23 | **Piorou em ambos** |
| LINKUSDT | -2,4614 → -2,6004 | -2,4055 → -3,1844 | 3 → 7 | 21 | **Piorou em ambos** |
| AVAXUSDT | -4,2219 → -3,2547 | -3,3744 → -2,3612 | 6 → 5 | 12 | Menor perda em ambos |
| LTCUSDT | -3,8136 → -2,7806 | -3,0646 → -3,1957 | 5 → 5 | 23 | Resultado misto |
| DOTUSDT | -2,1376 → -1,7470 | -4,4360 → -2,8752 | 9 → 5 | 27 | Menor perda em ambos |
| TRXUSDT | -4,1645 → -2,9591 | -6,6145 → -3,6063 | 15 → 8 | 41 | Menor perda em ambos |

## Síntese

- **12 de 12** comparações possuíam histórico suficiente e produziram resultados históricos completos; nenhum cenário teve retorno positivo nas janelas observadas.
- **8 de 12** agentes apresentaram perdas menores com cooldown em ambas as janelas: BTC, ETH, BNB, XRP, ADA, AVAX, DOT e TRX.
- **9 de 12** apresentaram perdas menores no período recente, mas SOL só melhorou no recente, LTC apenas no antigo, e DOGE/LINK pioraram nos dois.
- Os stops ATR no período recente somaram **84 sem pausa** e **68 com pausa** (menos 16). Isso **não** significa vantagem financeira universal, pois DOGE e LINK tiveram mais stops e perdas com a variante.
- Os resultados desta amostra não demonstram lucratividade, robustez em condições diferentes, adequação à Binance Spot ou conformidade completa com riscos de produção.

## Situação operacional e decisão

Na verificação posterior ao cálculo: PAPER `Running=0`, `RiskLocked=1`, 33 fills históricos preservados, nenhuma ordem Testnet registrada. Permanecem o limite financeiro de 1% de perda diária e a parada de emergência da Binance; nenhum limite numérico de quantidade diária de negociações foi acrescentado.

**Decisão:** nenhuma estratégia é promovida ao PAPER; bloqueio financeiro permanece. Próxima pesquisa recomendada: coletar amostra histórica maior, reservar um conjunto fora da amostra e executar walk-forward com custos e filtros realistas, comparando especialmente DOGE, LINK, TRX e BTC. Só considerar ajustes depois de resultados consistentes, revisão do risco e autorização específica.
