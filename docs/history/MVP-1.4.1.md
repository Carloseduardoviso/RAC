# RAC MVP 1.4.1 — Auditoria do simulador SHADOW de trades completos

Data: 23/11/2026. A implantação do RAC operacional (API/Web) não é modificada.

## Correções aplicadas

- Novo `PaperExecutionAudit`: módulo **puro** que centraliza cálculos explícitos de quantidade comprada, preço ajustado por slippage e meio spread, taxa de compra e venda e caixa líquido após a venda. Simula duas hipóteses predefinidas de custos, 1× e 2×, sem acessar carteira, SQL, chaves ou Binance privada.
- O motor `PaperTradeHorizonResearch.SimulateSlice` passa a usar esse módulo para entradas, saídas e stop conservador. Gap abaixo do stop é contabilizado no preço de abertura **pior do que o stop**; quando um stop é tocado dentro de uma vela, o preço do stop é usado, antes dos custos.
- Validação mais estrita de OHLC: preços de abertura e fechamento dentro de [Low, High], Low estritamente positivo, volumes e quantidade de trades não negativos, timestamp de fechamento posterior à abertura e não superior a um minuto.
- Para prevenir falsa execução no mesmo instante, o fechamento da vela de decisão deve ser **anterior** à abertura da vela seguinte. Se uma entrada for impossível de datar com segurança, a simulação é bloqueada (`BLOCKED`), inclusive para a alternativa NO_TRADE.
- Preservados: as regras de entrada, horizons 1/5/15/30min, custos definidos no próprio motor, filtros SHADOW, modo de não operar e decisão de pesquisa. Não houve mudança das estratégias operacionais.

## Testes

- Testes de reconciliação: orçamento da compra = taxa + custo do ativo; caixa da venda = valor bruto ajustado - taxa, reconciliação de ida e volta em cotação invariável, custos 2× mais caros que 1×, pior preço em abertura com gap, rejeição de preço/volume/horário impossíveis, e bloqueio de dados inválidos mesmo em NO_TRADE.
- Reexecutar `dotnet test RAC.slnx -c Release` e pesquisa SHADOW contra cópia verificável de outubro de 2026; registrar qualquer drift, não esconder alterações em resultados anteriores.

## Limitações ainda abertas

- Os cenários de taxa, spread e slippage permanecem **hipóteses fixas**; a simulação não sabe a liquidez real, posição no livro de ordens, latência, taxas por categoria do cliente ou fills parciais.
- Entradas e saídas de um minuto ainda se apoiam em OHLC: a trajetória intrabar real é desconhecida; stop conservador é uma aproximação, não garantia de execução.
- Esses testes **não demonstram retorno líquido positivo** e não autorizam promoção automática ao PAPER, Testnet ou operações reais.
- Replays por versão devem manter metadados do simulador antes de comparar números historicamente.
