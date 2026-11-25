# RAC MVP 1.4.4 — Auditoria SHADOW de capacidade de saída parcial

Data: 23/11/2026.

## Implementação

- Novo módulo puro `PaperExitLiquidityAudit.Replay`: recebe quantidade positiva de ativo, 1 a 60 candles históricos públicos contíguos, participação hipotética máxima de 0,1% ou 1% do volume por candle e custos em 1×/2×. Não acessa contas, SQL nem Binance privada.
- A cada candle, vende hipoteticamente **somente** `min(quantidade restante, min(baseVolume, quoteVolume/open) * maxParticipation)`. Não contabiliza como dinheiro o saldo ainda não vendido. Contabiliza tentativas sem preenchimento, saídas parciais, quantidade residual e dinheiro realizado após taxas e custos hipotéticos.
- Stop opcional considera abertura pior em gap abaixo do stop. Aplica penalidade adicional de amplitude de preço (proxy), além do slippage e meio spread hipotéticos. Não presume fills reais, liquidez de livro ou execução garantida.
- Saldo residual de ativo é exibido separadamente com um **mark-to-market hipotético com haircut de 10%** para discussão de risco. Esse valor **não entra em `NetCashRealized`** e não constitui lucro realizado nem venda executada.
- Se ainda houver quantidade residual ao término do horizonte, o status é `UNRESOLVED_EXIT`. A conclusão integral é `FULLY_SIMULATED_EXIT`, que significa apenas que **o modelo** encontrou capacidade suficiente.
- Novo comando manual `dotnet run --project src/RAC.HistoryResearch -c Release -- --exit-liquidity-audit 2026-10`: revalida SHA-256 e integridade dos 12 arquivos oficiais do mês; testa, a cada hora, uma quantidade fictícia equivalente a 100 USDT no preço de abertura da primeira vela de uma janela fixa de cinco minutos. Usa 0,1% de participação por candle e cenário 1×/2× de custos. Grava JSON separado em `%LOCALAPPDATA%\RAC\research\monthly\2026-10\exit-liquidity-audit.json`.

## Limites e próximos passos

- O estudo é **de risco de liquidação de posições sintéticas**, não backtest estratégico de entradas e saídas. Não está conectado ao lucro líquido, ao gate de seleção do RAC nem aos resultados anteriores, portanto **não corrige automaticamente métricas de backtest**.
- Volume OHLCV não é profundidade instantânea do book; a capacidade disponível durante cada candle é estimada retrospectivamente, sem modelagem de fila, latência, ordens de outros participantes, spreads reais, lotes mínimos de venda ou falhas de conexão.
- Um cenário `FULLY_SIMULATED_EXIT` não comprova liquidação verdadeira; `UNRESOLVED_EXIT` significa incapacidade dentro das regras hipotéticas deste teste, não uma afirmação de que a Binance teria recusado a venda.
- Próxima etapa segura: ligar este diagnóstico às posições hipotéticas dos holdouts, exigindo venda simulada integral ou rejeitando resultados para revisão científica; manter resultados não realizados fora de ganhos operacionais.
- `paperModified=false`, `riskLimitsModified=false`, `orderPlacementEnabled=false`, `promotionAllowed=false`. Nenhuma mudança no banco RAC, painel, API, PAPER ou Binance.
