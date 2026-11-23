# RAC MVP 1.3.4 — Validação pública isolada de 48 horas

Verificado em 22/11/2026, máquina DESKTOP-MI73RHO, utilitário isolado `%LOCALAPPDATA%\RAC\analysis\extended-history-smoke`, usando Binance Spot public klines 1m. Não consulta a conta nem coloca ordens. Duração da consulta de dados + análise: **17,06 s** na execução aferida.

- `mode=EXTENDED_HISTORY_48H_SHADOW_ONLY`, `count=12`, `paperModified=false`, `orderPlacementEnabled=false`, `promotionAllowed=false`.
- Coleta de 2.880 candles fechados e cronologicamente contíguos por par: **34.560 candles** no total. São **10 folds** walk-forward para cada par, 120 holdouts no conjunto, de 240 minutos cada, com 360 minutos de treino anteriores. Treinos podem se sobrepor; períodos de teste não.
- A cobertura atravessou **3 datas UTC** por par, correspondentes a apenas 48 horas contínuas, não três dias completos de mercado.
- Critério de triagem para pesquisa: >=12 trades de teste, >=70% de folds positivos, retorno médio selecionado >0% e superior ao baseline, pior drawdown <=5%; mesmo se passasse, a decisão seria revisão manual apenas.

| Par | Folds positivos / 10 | Trades fechados | Média baseline 1m (%) | Média horizonte escolhido (%) | Pior drawdown (%) | Gate |
|---|---:|---:|---:|---:|---:|---|
| BTCUSDT | 0 | 102 | -17,5373 | -2,9428 | 4,8843 | REJECTED_NO_EDGE |
| ETHUSDT | 1 | 84 | -17,9594 | -2,3539 | 4,0487 | REJECTED_NO_EDGE |
| SOLUSDT | 0 | 80 | -20,1135 | -2,3821 | 4,7362 | REJECTED_NO_EDGE |
| BNBUSDT | 0 | 99 | -18,1651 | -2,7155 | 5,7733 | REJECTED_NO_EDGE |
| XRPUSDT | 0 | 90 | -18,1255 | -2,5631 | 3,8739 | REJECTED_NO_EDGE |
| ADAUSDT | 0 | 85 | -19,2743 | -2,4512 | 4,3977 | REJECTED_NO_EDGE |
| DOGEUSDT | 0 | 82 | -18,1839 | -2,2572 | 3,3732 | REJECTED_NO_EDGE |
| LINKUSDT | 1 | 92 | -20,3840 | -2,3600 | 3,8087 | REJECTED_NO_EDGE |
| AVAXUSDT | 0 | 98 | -18,8391 | -2,7625 | 4,7896 | REJECTED_NO_EDGE |
| LTCUSDT | 0 | 82 | -18,5710 | -2,5380 | 4,4162 | REJECTED_NO_EDGE |
| DOTUSDT | 0 | 98 | -19,2621 | -2,9720 | 6,7677 | REJECTED_NO_EDGE |
| TRXUSDT | 0 | 177 | -20,5609 | -5,4960 | 8,8847 | REJECTED_NO_EDGE |

## Interpretação

- As médias de retorno são **percentuais de capital hipotético de USDT 100 reiniciado por janela**, não retorno agregado de contas reais nem saldo PAPER.
- **2 de 120 folds** tiveram resultado positivo em suas janelas selecionadas; nenhum agente obteve média positiva. Todos os 12 foram rejeitados para promoção. A alternativa de não negociar (0%) superou as médias líquidas das alternativas simuladas neste recorte.
- Horizonte 1m é uma referência extremamente ativa com taxas e slippage hipotéticos a cada ida e volta. A redução de perda ao escolher horizontes maiores não constitui prova de vantagem financeira.
- Não implementar negociação automática a partir dessas evidências; coletar observações mais longas, regimes diferentes e custos/microestrutura reais antes de qualquer decisão sobre estratégia.
