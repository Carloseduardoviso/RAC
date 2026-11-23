# RAC MVP 1.4.1 — Verificação da aritmética de execução SHADOW

Data: 23/11/2026. Escopo: somente simulação de trades completos, sem ordens Binance e sem modificação da conta PAPER.

## Auditoria implementada

- `PaperExecutionAudit.BuyAll` reconcilia o capital inicial como custo bruto de aquisição ajustado mais taxa de compra; `SellAll` reconcilia o produto bruto ajustado como taxa de venda mais caixa líquido.
- Teste de round-trip ao mesmo preço: custos fixos estimados produzem prejuízo, e custos 2× produzem prejuízo maior. Taxas de referência: 0,1% por lado, slippage 0,05% por lado, meio spread 0,01% por lado; cenário 2× dobra todas essas parcelas.
- `ConservativeStopFill`: preço do stop quando tocado sem gap e preço da abertura quando gap para baixo, nunca melhor que o stop.
- `SimulateSlice` agora rejeita abertura ou fechamento fora da faixa OHLC, Low não positivo, volumes/trades negativos e timestamps de decisão iguais ou posteriores ao próximo OpenUtc. São bloqueios de dados inválidos, inclusive para a alternativa NO_TRADE.
- As novas verificações são testadas sem serviços operacionais. A pesquisa mantém `promotionAllowed=false` e nenhuma execução privada.

## Suíte e reexecução

- **162/162 testes automatizados aprovados**, incluindo os novos testes de custos, caixa, stops e candles inválidos. Build Release da solução.
- Foi preservada a versão anterior do relatório de outubro de 2026 em `%LOCALAPPDATA%\RAC\analysis\mvp141\entry-quality-2026-10-before-mvp141.json` antes de reexecutar o comando isolado `--entry-gates-stress 2026-10` com as validações novas.
- Comparação determinística de **12/12 símbolos**, entre relatório anterior e atualizado: `REPLAY_CHANGED_SYMBOLS=0` para `noTradeDays`, `activeDays`, `selectedClosedTrades`, `positiveDays` e `netMeanPct`. Isso não certifica equivalência de todas as operações individuais ou livro de ordens real; verifica apenas os agregados indicados.
- **12/12 agentes permanecem rejeitados** (`REJECTED_OR_INSUFFICIENT_EDGE`), sem promoção (`PROMOTION=False`).

## Avaliação

As correções reforçam a integridade e auditabilidade da simulação; não demonstram vantagem financeira. Candles OHLC de um minuto não revelam todo o caminho intrabar; taxas e spreads ainda são estimativas fixas, e o simulador não modela preenchimento parcial, latência ou liquidez em ordem real. Sem aprovação de negociação na Testnet ou no mercado real.
