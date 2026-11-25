# RAC MVP 1.2.8 — Validacao exploratoria de estrategias PAPER

Data: 2026-11-22.

## Entrega

- `GET /api/strategy/validation` autenticado retorna exatamente os 12 pares suportados, sem envio de ordens e sem alterar SQL: candles publicos 1m fechados, ate 1000 por par, ultimo trecho continuo, indicador `RacStrategyEvaluator` (SMA9/21, RSI14, ATR14) e backtest historico `RacBacktest`.
- Simulacao ilustrativa long-only com capital independente de 100 USDT por simbolo, entrada no **open do candle seguinte** ao sinal, sem preenchimento na barra do sinal.
- Custos hipoteticos por lado: taxa 0,1%, slippage 0,05% e meio spread 0,01%. O spread e uma premissa fixa, nao foi extraido do livro real de ofertas.
- Metricas: trades encerrados, retorno historico, drawdown, numero de vendas com lucro ou prejuizo e maior sequencia de perdas fechadas; comparativo lado a lado com contagens de vendas e lucro/prejuizo realizado PAPER (BRL).
- Caso candles sejam insuficientes, recentes gaps reduzam o trecho continuo ou haja dados invalidos, status `BLOCKED`, nunca resultados simulados como se fossem completos. Ausencia de trades fechados tem classificacao propria.
- Painel adiciona um botao explicito de validacao e tabela dos 12 agentes; mantem o backtest individual anterior.

## Restricoes e interpretacao

- **Nao e replay identico do PAPER**: PAPER usa capital BRL, limite de risco por operacao, exposicao, stops, aportes, taxas e horario de execucao diferentes; o backtest ilustrativo usa 100 USDT all-in por simbolo. Nunca comparar os dois lucros nominais como equivalentes.
- Somente teste **in-sample**, nao e walk-forward nem validacao out-of-sample, nem valida liquidez, filtro minNotional, spread real ou execucoes Binance.
- O backtest nao aplica os limites financeiros completos do motor PAPER; reporta drawdown, mas nao certifica risco de producao.
- Nenhum limite numerico de trades por dia e adicionado. Riscos financeiros, circuit breakers, dados PAPER e parada de emergencia permanecem inalterados.
- Nenhum endpoint privado Binance consultado e nenhuma ordem real ou Testnet enviada.

## Testes

- 119/119 testes Release aprovados, incluindo SQL descartavel: endpoint de validacao retorna 12 agentes, protege modo somente leitura e nao fabrica trades quando nao existem candles.
- Testes de determinismo, custo assumido, metricas de risco nao-negativas, controle de lacunas, falta de candles e sintaxe JavaScript aprovados.

## Operacao

- Commit/push na branch `main` e release preparada em `%LOCALAPPDATA%\RAC\staging\mvp-1.2.8`; nao reiniciar PAPER durante desenvolvimento.
- Ativacao da nova API e Web requer backup SQL validado, restart controlado, rollback para MVP 1.2.7 e retomada PAPER por autenticacao.
