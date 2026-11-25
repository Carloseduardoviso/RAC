# RAC MVP 1.4.3 — Verificação de backtest SHADOW com capacidade

Data: 23/11/2026, fonte ZIPs públicos Binance Spot de outubro de 2026 (12 pares, 31 dias completos por par) previamente validados por SHA-256.

## Resultados observados

- **169/169 testes .NET Release aprovados**, incluindo simulação com volume zero, NO_TRADE preservado, restrição proporcional de capacidade e impossibilidade de promoção ao mercado.
- **372 janelas cronológicas** (31 por cada um de 12 agentes), com 720 candles de treino e 720 seguintes de teste por dia; 347 janelas selecionaram **NO_TRADE**, 25 escolheram estratégia de entrada para simular.
- Nas 25 janelas ativas: **223 trades hipotéticos encerrados**, **194 tentativas de compra barradas** por estimativa de capacidade abaixo de USDT 5, **199 preenchimentos parciais estimados** e soma de **34.953,27 USDT de intenções de entrada não preenchidas**. Essa soma inclui tentativas repetidas e **não** é valor perdido ou capital único.
- **Uma janela positiva**, nenhum agente qualificado para revisão, **12/12 classificados `REJECTED_OR_INSUFFICIENT_EDGE`**.
- Relatório `%LOCALAPPDATA%\RAC\research\monthly\2026-10\entry-quality-liquidity-stress.json`, com modo `ENTRY_QUALITY_LIQUIDITY_STRESS_SHADOW_ONLY`, flags de segurança desabilitando PAPER, ordens e promoção.

## Ressalvas

- Os filtros de compra e a seleção de horizonte usam apenas o treino; as características OHLCV do candle seguinte são usadas para estimar *após o fato* se a entrada seria preenchível, não para um sinal em tempo real.
- A capacidade e os custos são **hipóteses de estresse**. Como não há livro de ordens, histórico de fills nem capacidade de saída modelada, o resultado não valida a execução real das entradas ou saídas.
- Quando o resultado é 0% porque NO_TRADE foi escolhido, isso representa ausência de negociação, **não lucro**. Nenhuma estratégia é aprovada para operações reais.
