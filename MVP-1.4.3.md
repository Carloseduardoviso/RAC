# RAC MVP 1.4.3 — Backtest SHADOW com restrição hipotética de liquidez

Data: 23/11/2026.

## Implementação

- Opção **opt-in** `liquidityStress=true` no `PaperTradeHorizonResearch.SimulateSlice`. Os backtests históricos anteriores continuam iguais por padrão. O motor passa a contabilizar tentativas de entrada barradas, entradas hipotéticas parcialmente preenchidas e soma de valores não aplicados.
- Na entrada, aplica o modelo `PaperLiquidityStressAudit.Assess` ao candle público seguinte ao sinal fechado: capital disponível até USDT 100 por janela, limite **hipotético** de 0,1% do volume monetário do candle, taxas e impactos 1x ou 2x, limite de entrada preenchida ≥USDT 5. Se a estimativa preenchível não alcançar USDT 5, a tentativa é rejeitada sem contabilizar trade. Se houver preenchimento parcial, o saldo não aplicado permanece em caixa; a quantidade comprada é proporcional ao valor hipoteticamente preenchido.
- Na saída, aplica taxas e slippage/spread estimados, com **penalidade adversa adicional** de amplitude intraminuto, limitada a 2% por lado. Stop por ATR e saída por horizonte mantidos. A saída ainda pressupõe liquidação integral da quantidade adquirida: **não há simulação de capacidade de saída, fila, fill parcial da venda nem book real**.
- O modelo da execução acessa o OHLCV integral do candle seguinte somente para calcular a execução hipotética *a posteriori*. A seleção de filtros e o sinal de compra continuam usando **apenas candles fechados anteriormente**. A amplitude/volume futuros não são usados para prever ou selecionar a estratégia, mas os resultados não reproduzem uma decisão de execução observável no início do candle.
- Novo modo opt-in `liquidityStressResearch=true` na pesquisa mensal com treino diário de 720 minutos e teste seguinte de 720 minutos, mantendo regras de NO_TRADE e apenas promoção à revisão científica se cumpridos os critérios estabelecidos. O padrão anterior é preservado.
- Comando offline `dotnet run --project src/RAC.HistoryResearch -c Release -- --liquidity-backtest 2026-10`. Revalida manifesto SHA-256 e integridade de arquivos públicos antes de analisar 12 pares; grava em `%LOCALAPPDATA%\RAC\research\monthly\2026-10\entry-quality-liquidity-stress.json` sem sobrescrever o backtest original.
- O JSON traz `liquidityBlockedEntries`, `partialEntryFills` e `unfilledNotionalUsdt` por agente e por janela. **O valor não preenchido soma intenções/ocasiões de entrada, podendo contar repetidas vezes o mesmo capital, e não equivale a perda de dinheiro nem a saldo de uma conta.**

## Limitações e segurança

- Volumes públicos de candles não equivalem a profundidade disponível no order book. Preenchimentos e impacto são cenários hipotéticos, sem slippage real, spread variável observado, taxas da conta, filtros de Binance por par, latência, fills fracionados na saída ou simulação de fila. O valor USDT 5 não é a validação dos filtros reais de notional por símbolo.
- As médias de retorno são de janelas SHADOW com USDT 100 reiniciados e não equivalem a PnL composto nem desempenho operacional.
- `paperModified=false`, `riskLimitsModified=false`, `orderPlacementEnabled=false`, `promotionAllowed=false`. A API, o painel, SQL e a infraestrutura de PAPER/Binance não são alterados.
- Resultados de outubro e verificações em `MVP-1.4.3-VERIFICATION.md`.
