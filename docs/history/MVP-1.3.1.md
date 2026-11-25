# RAC MVP 1.3.1 — Diagnóstico observacional de rentabilidade

Data da implementação: 2026-11-22.

## Escopo e segurança

- Diagnóstico **somente leitura** acoplado à consulta autenticada `GET /api/strategy/walk-forward`; **não existe chamada adicional** à Binance para o diagnóstico: os mesmos candles públicos, fechados e temporários já obtidos pelo MVP 1.3.0 são reutilizados.
- Novo objeto JSON `profitabilityDiagnostics`, com 12 registros e proteções `paperModified=false`, `orderPlacementEnabled=false`, `promotionAllowed=false`; painel Web exibe tabela na mesma execução do botão de análise walk-forward.
- Nenhuma conexão SQL adicional, gravação em `RAC`, credencial Binance, alteração de estratégia, ordem, aumento de limite operacional ou reinício do PAPER é necessário.

## Métricas e interpretação correta

- **Sinais de compra por candle** e sinais de saída observacionais — um sinal em candles consecutivos não é uma ordem, entrada nem execução PAPER.
- Para cada candle fechado que gerou um sinal de compra, observar o movimento **da abertura ao fechamento do candle seguinte**: total de movimentos positivos, número que venceria custos hipotéticos e média bruta. **Isto não é um backtest de operações efetivamente executadas**, e os movimentos podem se sobrepor para sinais consecutivos.
- Estimativa de movimento líquido de uma hipotética compra na abertura seguinte e venda no fechamento seguinte, pagando entrada e saída com taxa de **0,1% por lado**, slippage de **0,05% por lado** e meio spread de **0,01% por lado**. A estimativa pressupõe execução integral, sem filtros de lote, profundidade nem impactos reais. Os custos são calculados com multiplicação/razão, evitando somar incorretamente percentuais.
- Regime observacional de volatilidade: sinais de compra classificados pelo ATR14/preço de fechamento relativo ao custo total fictício. Isto é apenas um indicador aproximado de escala de volatilidade, não avaliação de regime por aprendizado de máquina.
- Stops ATR e trades históricos nos testes de walk-forward são extraídos do **simulador experimental SHADOW**, nas janelas de teste separadas. Eles não correspondem diretamente às saídas, posições ou fills da conta PAPER em reais.
- Dados insuficientes, desatualizados ou não contíguos e ausência de walk-forward completo bloqueiam os indicadores ao invés de fabricar taxas de sucesso; médias sem observações ficam nulas.

## Resultados e próximos controles

- Comparar os indicadores de entrada/custos nos 12 agentes com os 72 testes holdout anteriores, reconhecendo que na última amostra **0 dos 72 folds selecionados foram lucrativos**.
- Esta etapa diagnostica um problema, **não valida nem otimiza automaticamente** uma estratégia rentável. Para qualquer alteração operacional ainda são necessários histórico mais amplo, diferentes regimes, custos e filtros reais, validação estatística e autorização separada.
- A trava financeira PAPER de 1% e a parada de emergência Binance continuam ativas; não foi criado limite numérico diário de entradas.

## Verificações

- Bateria .NET Release, testes de ausência de sinais, validade de dados, custos aproximados, idempotência/determinismo, separação de períodos, `node --check` e verificação Git de whitespace.
- Publicação de código em `main` e build isolado em `%LOCALAPPDATA%\RAC\staging\mvp-1.3.1`; a implantação em serviços exige etapa controlada própria, com backup SQL e rollback para a versão 1.3.0.
