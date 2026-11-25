# RAC MVP 1.4.0 — Verificação real de estresse 2x dos filtros

Data: 23/11/2026, ambiente de pesquisa local, dados Binance Spot públicos de 2026-08, 2026-09 e 2026-10. Não houve uso de credenciais nem ordens Binance.

## Procedimento

- Reutilizados 36 ZIPs de 1m de 12 pares × 3 meses com verificação de manifesto mensal e SHA-256. Cada símbolo usa, por dia, 720 candles para treinar e 720 candles futuros disjuntos para testar.
- Candidatos: NO_TRADE (0%), horizontes 15/30min e quatro filtros de entrada. Cada seleção só substitui NO_TRADE quando o treino tem retorno líquido >0% e pelo menos três operações simuladas encerradas.
- O cenário de estresse **duplica cada custo por lado assumido**: taxa 0,2%; slippage 0,1%; meio spread 0,02%. Não são dados reais do livro de ordens nem custos personalizados da conta.
- Os arquivos de cenário estressado são distintos dos de custo base: `%LOCALAPPDATA%\RAC\research\monthly\YYYY-MM\entry-quality-no-trade-double-cost.json`. Consolidação cruzada em `%LOCALAPPDATA%\RAC\research\comparisons\entry-quality-three-month-2026-08-to-2026-10-double-cost.json`.

## Resultado consolidado observado

```text
CROSS_MONTH_GATE days=1104 noTrade=1103 active=1 trades=8 positive=0 rejected=12 review=0 orders=False
```

- **1.104 janelas diárias de avaliação**, **1.103 NO_TRADE**, **uma janela ativa**, **oito trades simulados encerrados** e **zero janelas positivas**.
- **12/12 agentes rejeitados**, nenhum autorizado sequer para revisão de pesquisa.
- O 0% de NO_TRADE representa **ficar sem exposição**, não ganho, lucro da conta, nem retorno antecipado.
- O cenário original de custo 1x permanece disponível e intacto (`entry-quality-three-month-2026-08-to-2026-10.json`), cuja execução anterior somou 1.072 janelas sem operações, 32 janelas ativas e 364 operações simuladas, com apenas duas janelas positivas.
- O estudo 2x seleciona os gates independentemente sob custos dobrados: portanto a diferença entre 1x e 2x não é uma comparação das mesmas operações. A seleção pode favorecer NO_TRADE em vez de aceitar mais custos.

## Segurança

- `Mode=THREE_MONTH_ENTRY_GATES_DOUBLE_COST_SHADOW_ONLY`, `CostMultiplier=2`, `PromotionAllowed=false`, `OrderPlacementEnabled=false`, `PaperModified=false`, `RiskLimitsModified=false` no agregado.
- Não alterar estratégias do PAPER ou executar ordens. Resultados reprovados não representam benefício financeiro ou evidência de edge.
