# RAC MVP 1.4.0 — Sensibilidade de filtros de entrada a custos duplicados

## Escopo

Pesquisa offline SHADOW, separada dos serviços operacionais. A coleta e a carteira PAPER não foram modificadas. O novo comando `--entry-gates-stress YYYY-MM` recalcula a seleção de filtros de entrada com custos fictícios **duplicados** (taxas 0,2%, slippage 0,1%, meio spread 0,02% por lado) usando os mesmos ZIPs públicos já validados por SHA-256. Apenas candles do treino de 720 minutos selecionam as regras; o teste posterior de 720 minutos fica separado. Os candidatos continuam limitados a 15/30 minutos, filtros predefinidos e a alternativa `NO_TRADE` (zero operações e retorno 0%).

A nova saída mensal `entry-quality-no-trade-double-cost.json` não sobrescreve `entry-quality-no-trade.json`. O comando `--entry-gates-stress-summary YYYY-MM YYYY-MM YYYY-MM` consolida três meses consecutivos completos, verifica datas, integridade e regras de NO_TRADE, valida `costMultiplier=2` e registra SHA-256 dos três relatórios mensais. Produz um JSON distinto `entry-quality-three-month-YYYY-MM-to-YYYY-MM-double-cost.json`. As três janelas estudadas são agosto, setembro e outubro de 2026.

As decisões seguem apenas a evidência da pesquisa e **não autorizam movimentar PAPER, modificar circuit breakers, habilitar Testnet ou enviar ordens**. Uma redução da exposição em cenários adversos não prova vantagem líquida nem lucro futuro. Custos duplicados são um teste hipotético de robustez, **não** estimativa de fill de mercado ou de spreads históricos variáveis.

## Segurança

- Sem alteração de banco SQL operacional, API ou Web.
- `paperModified=false`, `riskLimitsModified=false`, `orderPlacementEnabled=false`, `promotionAllowed=false` em todos os relatórios.
- Falha fechada para ausência de arquivos, símbolos duplicados, mês incorreto, checksum divergente, flags inseguras, custos não esperados ou contagens incoerentes.
- Modo simulado original 1x preservado; o cenário 2x usa relatório e caminho próprios.
- Consulte o relatório factual `MVP-1.4.0-VERIFICATION.md` para os resultados.
