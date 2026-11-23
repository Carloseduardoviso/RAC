# RAC MVP 1.3.2 — Verificação com candles públicos reais

Data da verificação: 22/11/2026. Ferramenta isolada `%LOCALAPPDATA%\RAC\analysis\walk-forward-smoke` (código analítico local, nenhuma conexão ao SQL e nenhuma ordem). A coleta foi realizada apenas pela API pública `data-api.binance.vision`.

## Validações

- `mode=ENTRY_FILTER_WALK_FORWARD_SHADOW_ONLY`, `count=12`, `paperModified=false`, `orderPlacementEnabled=false`, `promotionAllowed=false`.
- 12 pares com **999 candles fechados por par**, cada um com **6 folds de treino/teste** e métricas de comparação de movimentos no candle seguinte.
- As escolhas de filtro são feitas apenas com 180 candles anteriores de treino e avaliadas em 120 candles seguintes, separados. Os testes não se sobrepõem **dentro de cada par**; os treinos podem sobrepor-se.
- Preço de abertura para fechamento do candle seguinte, com custos de execução **hipotéticos**; sinais repetidos não são operações reais e não produzem rentabilidade realizável por soma.

## Resumo da última amostra

| Par | Folds que escolheram filtro | Sinais baseline no teste | Sinais com escolha no teste | Média líquida hipotética baseline (%) | Média líquida hipotética selecionada (%) |
|---|---:|---:|---:|---:|---:|
| BTCUSDT | 2 | 154 | 123 | -0,3247 | -0,3249 |
| ETHUSDT | 2 | 168 | 140 | -0,3172 | -0,3188 |
| SOLUSDT | 4 | 178 | 112 | -0,3164 | -0,3097 |
| BNBUSDT | 2 | 152 | 102 | -0,3216 | -0,3285 |
| XRPUSDT | 4 | 165 | 101 | -0,3259 | -0,3260 |
| ADAUSDT | 1 | 169 | 151 | -0,3225 | -0,3214 |
| DOGEUSDT | 2 | 133 | 111 | -0,3246 | -0,3222 |
| LINKUSDT | 2 | 172 | 123 | -0,3195 | -0,3218 |
| AVAXUSDT | 3 | 171 | 127 | -0,3230 | -0,3255 |
| LTCUSDT | 2 | 179 | 151 | -0,3217 | -0,3217 |
| DOTUSDT | 1 | 167 | 148 | -0,3178 | -0,3216 |
| TRXUSDT | 0 | 224 | 224 | -0,3181 | -0,3181 |

## Interpretação e decisão

- **Apenas SOL, ADA e DOGE** apresentaram média líquida menos negativa após o filtro, nesta amostra; outros pares pioraram, tiveram empate arredondado ou permaneceram na baseline.
- Todos os 12 pares mantiveram retorno médio estimado negativo no horizonte de **um único candle**, mesmo com os filtros. Não existem resultados que justifiquem promover uma estratégia operacional.
- O walk-forward de estratégia completa (MVP 1.3.0) ainda observou **0 folds com retorno positivo** nessa amostra. Não misturar suas métricas com o diagnóstico de sinais de um candle.
- Os valores não medem o lucro da conta PAPER, não consideram oportunidade perdida, capital ocioso nem a microestrutura da Binance; preços reais, spread e profundidade podem variar.
- **Decisão operacional: SHADOW somente**, PAPER pausado, bloqueio financeiro mantido, Testnet desabilitada, ordens Binance zero.
- Próximas pesquisas úteis: período de observação mais longo, análise de sinais com entrada/saída completa, regimes de mercado diferentes e custos observados, sempre com holdout e monitoramento da taxa de sinais descartados.
