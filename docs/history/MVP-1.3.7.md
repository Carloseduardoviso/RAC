# RAC MVP 1.3.7 — Três meses de walk-forward offline e stress de custos

Data: 22/11/2026.

## Entrega

- Expansão do acervo isolado Binance Spot para **agosto, setembro e outubro de 2026**: 31 + 30 + 31 dias de candles públicos de 1 minuto para os 12 agentes, com controle mensal por manifesto e SHA-256 por ZIP.
- Nova opção manual do executável de pesquisa: `dotnet run --project src/RAC.HistoryResearch -c Release -- --three-months 2026-08 2026-09 2026-10`. Exige **três meses consecutivos, integralmente encerrados**, cada qual importado/validado previamente.
- Rejeita dados ausentes, hashes divergentes, símbolos repetidos, dados incompletos, resultado operacional habilitado ou cenário não suportado.
- Cada dia: treino de 720 candles (primeiras 12h UTC), teste posterior de 720 candles, seleção fixa entre horizontes 1/5/15/30min **exclusivamente no treino**. Sem escolha baseada no teste.
- Dois cenários re-simulados de forma independente a partir dos mesmos candles:
  - Base: taxa de 0,1%, slippage de 0,05% e meio spread de 0,01% por lado.
  - Estresse: **dobro de cada custo**, taxa de 0,2%, slippage de 0,1% e meio spread de 0,02% por lado.
- O horizonte é selecionado novamente pelo treino em cada cenário; portanto os dois cenários **não representam necessariamente as mesmas operações**, e suas diferenças não isolam um único custo marginal.
- Relatório por símbolo: meses, janelas de teste, dias positivos, trades encerrados, retorno médio líquido na base e sob estresse, pior drawdown e decisão de triagem. Retornos são médias de períodos com **USDT 100 fictícios reiniciados a cada dia**, e **não** retorno composto de carteira real.
- Gate rigoroso **somente para revisão científica**: >=90 operações simuladas, três meses com médias positivas na base e no estresse, >=70% de dias positivos em três meses, médias positivas em ambos cenários e drawdown de estresse <=5%. Mesmo com `RESEARCH_REVIEW_ONLY`, **NUNCA promover automaticamente ao PAPER ou Binance**.
- Compara explicitamente com a alternativa de **não operar = 0%**.
- Output JSON: `mode=THREE_MONTH_COST_STRESS_SHADOW_ONLY`; `paperModified=false`, `riskLimitsModified=false`, `historyPersistedInOperationalDatabase=false`, `orderPlacementEnabled=false`, `promotionAllowed=false`.

## Limitações

- Meses consecutivos representam **períodos distintos**, mas não são independentes do ponto de vista de regimes macroeconômicos, nem comprovam comportamento futuro.
- As classes UP/DOWN/SIDEWAYS por dia são **descritivas e retrospectivas**. Proibido usar o fechamento futuro do dia para escolher trades.
- Não modela order book real, fracionamento, filtros de lote, fill probabilístico, indisponibilidade de API, spreads dinâmicos nem slippage real; multiplicadores são cenários hipotéticos.
- Esse avanço não modifica API, painel, banco SQL operacional, serviços PAPER, controle de risco nem executor Binance. É um utilitário offline de pesquisa.

## Evidência

- Testes do simulador com custo de 1x/2x, rejeição de fatores inválidos, recusa de três meses incompletos/não consecutivos e flags de segurança.
- `MVP-1.3.7-VERIFICATION.md` documenta a execução dos três meses e as verificações do PAPER.
