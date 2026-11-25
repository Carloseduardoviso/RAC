# RAC MVP 1.4.5 — Gate SHADOW de capacidade de saída nos holdouts

Data: 23/11/2026. Implementado no computador `080001325ADPLAN` e repositório `C:\Users\05035144208\source\repos\RAC`.

## Implementação

- Modo de pesquisa **opt-in** `exitCapacityAudit=true` do `MonthlyWalkForwardResearch.Run`, sem mudar as simulações tradicionais nem o executor operacional.
- `PaperTradeHorizonResearch.SimulateSlice` pode coletar `ExitIntent(NextCandleIndex, RequestedQuantity)` quando explicitamente solicitado. Por padrão o registro de saídas fica desligado e a igualdade estrutural dos resultados anteriores é preservada. A posição vendida hipoteticamente no simulador anterior **não é reclassificada como lucro realizado**.
- `PaperExitCapacityGate.Assess` confronta cada saída teórica com os **cinco candles posteriores** do holdout, utilizando `PaperExitLiquidityAudit.Replay`, hipótese de participação máxima de 0,1% do volume por vela e custos estimados 1× ou 2×. Para stop intrabar, a avaliação começa no candle **seguinte** àquele onde o stop foi disparado; saídas sem cinco candles seguintes são marcadas como `MissingFutureCandles` e **rejeitadas**.
- O diagnóstico registra saídas cobertas, saídas sem preenchimento completo, quantidade ainda não vendida e janelas sem dados futuros suficientes. Uma janela `NO_TRADE` não é contada como lucro nem prova capacidade de execução.
- Qualquer saída incompleta/desconhecida resulta em `EXIT_UNRESOLVED_REJECTED` e torna a estratégia **inelegível até mesmo para revisão científica**. Se todas as saídas teóricas forem cobertas, isso **não** autoriza promoção ou execução em exchange: `promotionAllowed=false` sempre.
- CLI manual `dotnet run --project src/RAC.HistoryResearch -c Release -- --exit-constrained-backtest 2026-10`, com revalidação SHA-256 dos ZIPs públicos, 12 pares, 31 dias completos de outubro de 2026, 720 minutos para treinamento e 720 minutos posteriores fora da amostra. Saída isolada em `%LOCALAPPDATA%\RAC\research\monthly\2026-10\exit-capacity-gated-backtest.json`.

## Limitações

- **Auditoria retrospectiva de capacidade**, não simulação precisa de saídas parciais em tempo real. O backtest de retorno ainda contabiliza liquidação teórica integral; os dados de capacidade são uma camada de rejeição, e NÃO são usados para ajustar o retorno financeiro ou criar ordens.
- Candles OHLCV agregados não representam livro de ordens, fila de execução, custos reais, fill probabilístico ou profundidade disponível no instante da venda. O horizonte de cinco candles e 0,1% de participação são hipóteses de estresse pré-definidas. Uma saída marcada coberta apenas o é **dentro do modelo**.
- O patrimônio fictício de 100 USDT é reiniciado a cada janela de pesquisa. Resultados não equivalem à rentabilidade acumulada de uma carteira real.
- API e Web operacionais são executados separadamente; nenhuma nova rota de negociação ou configuração de ativação foi adicionada.
- PAPER, Testnet e ordens reais seguem sob controles preexistentes. Nesta máquina as contas e flags são locais e **não são cópias do ambiente anterior**.

Veja `MVP-1.4.5-VERIFICATION.md` para evidências.
