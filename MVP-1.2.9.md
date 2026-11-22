# RAC MVP 1.2.9 - Comparacao SHADOW de stops e reentradas

Data de desenvolvimento: 2026-11-22.

## Implementado

- Comparador historico isolado `PaperStrategyShadowComparison.Run` com a mesma estrategia deterministica SMA9/21 + RSI14 + ATR14 observacional utilizada pelo RAC.
- Dois intervalos cronologicos **disjuntos**, cada um com indicadores aquecidos apenas pelos proprios candles anteriores; separacao 50/50 e nenhum dado posterior participa dos sinais anteriores.
- Cenario baseline: uma posicao long-only, stop 2xATR congelado na entrada e nenhuma espera entre fechamentos e novas entradas.
- Cenario SHADOW: mesmas regras de referencia, mas com espera de **5 candles** entre venda e proxima compra. A espera e hipotetica e **nao altera o motor PAPER**.
- Sinal no fechamento de candle confirmado e ordem simulada apenas na abertura do candle seguinte; stop observado pelo minimo do candle, com gaps executados no preco de abertura pior, nunca preenchimento otimista. Se a entrada sofre stop no mesmo candle de abertura, o stop conservador e considerado.
- Taxa de 0,1%, slippage de 0,05% e meio spread fixo hipotetico de 0,01% por lado; capital isolado de 100 USDT para cada par e periodo. **Nao replica** o dimensionamento de risco financeiro e stops da carteira BRL PAPER nem a liquidez real.
- API autenticada `GET /api/strategy/shadow-comparison` retorna exatamente 12 pares, metricas por periodo (retorno, drawdown, vendas, stops ATR, saídas por sinal, perdas/vitorias, entradas evitadas) e protecoes `paperModified=false`, `orderPlacementEnabled=false`, `promotionAllowed=false`.
- Rota incluida na allowlist fixa do proxy Web e painel com botao de comparacao SHADOW. Historico curto, gaps ou dados desatualizados retornam `INSUFFICIENT_DATA` sem resultados validos falsos.

## Criterios e cautelas

- As duas metades sao ensaios historicos, **nao** prova fora da amostra: o cooldown foi escolhido como exemplo, nao treinado exclusivamente num periodo anterior nem testado em amostra independente.
- **Nao e comparacao de resultado financeiro com o PAPER real**; baseline e candidato sao dois modelos hipoteticos consistentes entre si, ambos long-only all-in; falta avaliar slippage real, liquidez, minimo de ordens, alavancagem (nao usada), regime de mercado, mais dias e walk-forward.
- Nao existe promocao automatica. Alterar risco ou estrategia de ordens exigira estudos adicionais e autorizacao separada.
- Nao foram alterados limites financeiros, limite numerico de entradas (continua sem limite por contagem diaria), circuit breakers, parada de emergencia ou dados existentes. Binance Testnet e ordens reais permanecem proibidas.

## Operacao

- Testes .NET Release, SQL descartavel, JavaScript e diff Git verificados antes do commit.
- Publicacao da API/Web preparada na pasta `%LOCALAPPDATA%\RAC\staging\mvp-1.2.9` sem reiniciar o PAPER ou alterar o bloqueio financeiro atualmente ativo.
- Implantacao futura exige backup SQL validado, rollback, verificacao do estado `PAPER=0|1` e Binance `TESTNET=0|1|1` e autenticacao no painel.
