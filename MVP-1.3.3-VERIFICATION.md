# RAC MVP 1.3.3 — Evidência de validação histórica pública

Data: 22/11/2026. Análise executada no utilitário isolado `RAC/analysis/walk-forward-smoke` com Binance Spot public klines 1m; nenhum acesso à conta ou ordem.

- `mode=FULL_TRADE_HORIZON_SHADOW_ONLY`, `count=12`, `paperModified=false`, `orderPlacementEnabled=false`, `promotionAllowed=false`.
- 12 símbolos, 999 candles fechados cada, **72 folds** com seleção pelo treino e testes cronológicos separados. Em todos os 72 folds foi selecionado um horizonte alternativo no treino e **nenhum holdout selecionado apresentou retorno positivo**.
- As médias abaixo são médias dos retornos (%) das seis janelas de teste de cada símbolo com 100 USDT reiniciados; não representam uma conta de operações concatenadas, previsão ou saldo real.

| Símbolo | Retorno médio horizonte 1m (%) | Retorno médio horizonte escolhido (%) | Trades encerrados nos testes selecionados | Stops ATR selecionados | Pior drawdown dos testes selecionados (%) |
|---|---:|---:|---:|---:|---:|
| BTCUSDT | -9,5307 | -2,0635 | 38 | 31 | 4,1734 |
| ETHUSDT | -8,8096 | -1,3084 | 25 | 15 | 2,4329 |
| SOLUSDT | -9,8080 | -1,4429 | 26 | 13 | 1,9912 |
| BNBUSDT | -8,2861 | -1,3266 | 28 | 18 | 2,2017 |
| XRPUSDT | -9,0698 | -1,9486 | 35 | 24 | 2,7540 |
| ADAUSDT | -9,5290 | -1,7698 | 29 | 21 | 2,8000 |
| DOGEUSDT | -7,7267 | -1,3624 | 24 | 16 | 2,4134 |
| LINKUSDT | -9,3899 | -1,2004 | 24 | 13 | 2,0634 |
| AVAXUSDT | -9,0292 | -1,7306 | 30 | 23 | 2,6795 |
| LTCUSDT | -8,5272 | -1,3536 | 25 | 16 | 2,0622 |
| DOTUSDT | -8,1587 | -1,8677 | 31 | 23 | 3,0411 |
| TRXUSDT | -11,2836 | -3,3731 | 64 | 53 | 4,8740 |

## Decisão de pesquisa

A seleção de horizontes 5/15/30 minutos reduziu consideravelmente perdas simuladas contra uma alternativa muito ativa de 1 minuto, mas **não produziu um resultado positivo em qualquer um dos 72 folds**. A magnitude da perda do horizonte 1 minuto reflete sua frequência de compra/venda all-in e custos repetidos; portanto não é comparável a taxas de retorno de uma carteira real.

**Não promover filtros ou horizontes ao PAPER.** Próximos estudos devem abranger mais dados e regimes, custos observáveis e um critério explícito de suporte mínimo e comparação com ausência de operações, antes de tentar elevar frequência ou risco.
