# RAC MVP 1.4.2 — Auditoria hipotética de custos variáveis e capacidade

Data: 23/11/2026. Implementação SHADOW e isolada do RAC operacional.

## Implementação

- Novo `PaperLiquidityStressAudit.Assess`, uma função pura sem conexão com SQL, PAPER, Binance privada ou executor de ordens. Simula uma compra e uma venda ao **mesmo preço de referência**, aplicando a taxa fixa e o slippage/meio spread de 1x ou 2x, acrescido de penalidade hipotética associada à amplitude intraminuto.
- Penalidade extra de preço por lado: `min(2%, (high-low)/open * maxParticipation * 10)`; cenário matemático exploratório, não uma estimativa validada de spread de mercado.
- A capacidade hipotética de compra é `min(baseVolume * open, quoteVolume) * maxParticipation`, com taxas de participação predefinidas de 0,1% ou 1% do volume de um candle. Valor não preenchido permanece como caixa não aplicado. Caso não haja volume, o resultado é NO_FILL com zero custo atribuído a uma operação inexistente.
- Reconciliação explícita entre valor solicitado, valor simulado preenchido, saldo não preenchido, taxa de compra/venda e custo de ida e volta. Uma simulação de venda ao mesmo preço **não é backtest de lucratividade**.
- CLI manual `dotnet run --project src/RAC.HistoryResearch -c Release -- --liquidity-audit 2026-10`. Revalida SHA-256 e integridade integral de cada arquivo mensal e coleta amostras determinísticas na primeira vela de cada hora para os 12 pares. Não coleta livro de ordens e não acessa credenciais.
- O resultado está isolado em `%LOCALAPPDATA%\RAC\research\monthly\2026-10\liquidity-capacity-audit.json`, sem modificar o relatório de estratégias, conta PAPER ou regras de risco.

## Limitações

**O volume de um candle histórico não corresponde a liquidez executável disponível em um instante**. Por isso, os preenchimentos parciais são limites de capacidade simulados, não previsões de execução; o estresse de amplitude também não é um spread real. Não há modelagem de posição na fila, latência, fluxo de ordens, preço médio de múltiplos níveis nem execução de venda parcial ao final de um trade. Resultados são descritivos e não aprovam estratégias.

## Segurança

`paperModified=false`, `riskLimitsModified=false`, `orderPlacementEnabled=false`, `promotionAllowed=false` em todos os objetos; API/Web operacionais preservados. Dados inválidos, parâmetros fora do conjunto testado ou hashes divergentes bloqueiam a pesquisa. Nenhuma ordem Binance é enviada.

Consulte `MVP-1.4.2-VERIFICATION.md` para testes e observações reais.
