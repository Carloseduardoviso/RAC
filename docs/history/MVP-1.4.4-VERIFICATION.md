# RAC MVP 1.4.4 — Verificação da liquidação parcial SHADOW

Data: 23/11/2026; dados públicos Binance Spot, mês UTC 2026-10, 12 pares previamente autorizados e 44.640 candles validados por par, manifestos SHA-256 conferidos.

## Resultado real da execução de pesquisa

- **8.928 cenários hipotéticos de liquidação**: 744 por símbolo, um a cada hora durante 31 dias.
- Inventário sintético equivalente a **100 USDT no preço inicial** de cada cenário, cinco candles consecutivos para tentar vender, participação máxima teórica de 0,1% do volume do candle, taxas e impacto de preço 1×/2×.
- **5.124 saídas integralmente simuladas** e **3.804 cenários com inventário residual após cinco minutos** (42,61%); os 3.804 têm vendas parcialmente simuladas e nenhum cenário apresentou zero de quantidade vendida ao longo da janela.
- Saídas pendentes permanecem identificadas como `UNRESOLVED_EXIT` e não são declaradas venda integral. As taxas e o dinheiro realizado incidem apenas nas frações teoricamente vendidas.
- A regra de capacidade é a mesma no estresse de custos 2×, então a quantidade residual e o total de janelas incompletas permanecem iguais; o cenário 2× gera caixa realizado menor ou igual para quantidades hipoteticamente idênticas.
- O relatório está em `%LOCALAPPDATA%\RAC\research\monthly\2026-10\exit-liquidity-audit.json`, `mode=FIVE_CANDLE_PARTIAL_EXIT_SHADOW_ONLY`, `count=12`, `paperModified=false`, `orderPlacementEnabled=false`, `promotionAllowed=false`.

## Qualidade

- **173/173 testes automatizados .NET Release aprovados**, cobrindo no-fill com volume zero, vendas parciais durante cinco candles, quantidade vendida + restante = quantidade inicial, custos duplicados, gap de stop, histórico inválido e flags de segurança.
- A ferramenta existe apenas no modo de pesquisa offline e não envia ordens, não opera no PAPER, não altera SQL ou os limites de risco. O relatório descreve **capacidade simulada**: não foi feita homologação de ordem na Binance.

## Conclusão

Não confundir `FULLY_SIMULATED_EXIT` com possibilidade garantida de execução em mercado. Os 3.804 casos incompletos dependem das hipóteses de participação e tempo fixadas antes da pesquisa, e mostram que a saída com preenchimento garantido **não deve ser assumida**. A investigação da integração dessa restrição aos holdouts permanece pendente.
