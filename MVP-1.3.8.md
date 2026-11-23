# RAC MVP 1.3.8 — Filtros de entrada e decisão de não operar (SHADOW)

Data: 22/11/2026.

## Entregue

- O simulador isolado `PaperTradeHorizonResearch.SimulateSlice` aceita opcionalmente gate de entrada `BASELINE`, `TREND_STRENGTH`, `ATR_COST`, `TREND_AND_ATR`, `NO_TRADE`. As opções mantêm custos por lado, stop ATR e saídas de trades completos; o gate afeta apenas se abre uma entrada hipotética. Valores desconhecidos são rejeitados.
- `NO_TRADE` é um candidato real com **0 operações, retorno 0% e drawdown 0%**; não é contabilizado como lucratividade. Candles inválidos continuam bloqueando inclusive este candidato.
- Gate `TREND_STRENGTH` usa SMA9-SMA21 >= 0,05% do fechamento; `ATR_COST` requer ATR14/candle_close em % >= metade do custo total presumido de ida e volta; `TREND_AND_ATR` combina ambos. Todos os indicadores são calculados exclusivamente no candle fechado antes da compra simulada.
- O novo parâmetro opt-in `entryGateResearch=true` de `MonthlyWalkForwardResearch.Run` usa as primeiras 12 horas UTC para selecionar entre 8 configurações predefinidas (horizontes 15/30 minutos e quatro gates) ou `NO_TRADE`; exige **ao menos três operações encerradas em treino e retorno líquido de treino estritamente positivo** para substituir a alternativa de não operar. Empates conservam NO_TRADE.
- Nos 720 candles seguintes do mesmo dia aplica somente a escolha congelada no treino. As janelas diárias não se sobrepõem. O estudo original de 1/5/15/30 minutos mantém seu comportamento anterior por padrão.
- Ferramenta CLI offline: `dotnet run --project src/RAC.HistoryResearch -c Release -- --entry-gates 2026-10` usa somente ZIPs de candles públicos do acervo já validado, exige manifesto e SHA-256, não envia pedidos privados a Binance nem opera na conta. Saída: `%LOCALAPPDATA%\RAC\research\monthly\YYYY-MM\entry-quality-no-trade.json`.
- O novo relatório permite comparar dias NO_TRADE, dias ativos, trades encerrados, ganhos médios líquidos e baseline antigo. Mesmo que algum mês atenda critérios de revisão, `promotionAllowed=false` sempre. PAPER e Testnet continuam bloqueados.

## Restrições e interpretação

- **Não existe motor de decisão operacional novo**; o gate está somente no utilitário de pesquisa SHADOW. Nenhuma modificação nos serviços API e Web implantados, conta PAPER, SQL, Testnet ou ordens Binance.
- O estudo tem custos fixos presumidos e capital fictício reiniciado a cada dia (100 USDT); médias não equivalem a retorno composto de portfólio. O resultado de muitos dias sem operação pode ser zero, mas isso não estabelece vantagem.
- O conjunto de candidatos é menor que a busca irrestrita, porém comparar oito opções em cada treino aumenta o risco de sobreajuste; os testes posteriores são indispensáveis. Ainda faltam taxas por volume, slippage variável, liquidez e validação multirregime.
- Resultados reais de três meses em `MVP-1.3.8-VERIFICATION.md`.
