# RAC MVP 1.3.0 — Verificação de dados públicos e walk-forward

Data: 2026-11-22, consulta pública somente leitura diretamente de `data-api.binance.vision`, fora do servidor RAC. Programa isolado no diretório local `%LOCALAPPDATA%\RAC\analysis\walk-forward-smoke`; sem conexão ao banco, sem credenciais privadas e sem envio de ordens.

## Comprovação funcional

- `RacWalkForwardService.AnalyzeAsync` retornou `mode=WALK_FORWARD_RESEARCH_ONLY`, `count=12`, `paperModified=false`, `orderPlacementEnabled=false`, `historyPersisted=false`.
- Binance devolveu até 1.000 candles por par; os 12 pares tiveram **999 candles completos** após exclusão do candle corrente.
- Todos os **12 agentes** tiveram dados consecutivos recentes e **6 folds** de teste por par; total de 72 folds não sobrepostos dentro de cada par.
- Treino de 180 candles e avaliação posterior de 120 candles por fold. As janelas de treino têm sobreposição; portanto, resultados sucessivos não são independentes estatisticamente.

## Retornos históricos médios por janela de teste

Percentuais sobre capital fictício de 100 USDT por período. `Selecionado` é a política escolhida apenas pelos retornos do treino anterior. **Todos os resultados foram negativos**, mesmo em casos de melhora relativa.

| Agente | Folds | Treinos que escolheram cooldown | Testes positivos | Baseline média (%) | Selecionado média (%) |
|---|---:|---:|---:|---:|---:|
| BTCUSDT | 6 | 6 | 0 | -1,8891 | -1,4396 |
| ETHUSDT | 6 | 4 | 0 | -1,1926 | -1,0998 |
| SOLUSDT | 6 | 3 | 0 | -1,3804 | -1,3538 |
| BNBUSDT | 6 | 5 | 0 | -1,8382 | -1,4946 |
| XRPUSDT | 6 | 5 | 0 | -2,0242 | -1,7036 |
| ADAUSDT | 6 | 4 | 0 | -1,6569 | -1,4653 |
| DOGEUSDT | 6 | 4 | 0 | -1,5330 | -1,5577 |
| LINKUSDT | 6 | 2 | 0 | -0,9160 | -0,9637 |
| AVAXUSDT | 6 | 4 | 0 | -1,4805 | -1,4042 |
| LTCUSDT | 6 | 6 | 0 | -1,7568 | -1,3933 |
| DOTUSDT | 6 | 5 | 0 | -1,5826 | -1,3293 |
| TRXUSDT | 6 | 6 | 0 | -3,2738 | -1,9793 |

## Conclusão

- A política escolhida com base no treino reduziu a perda histórica média em **10 de 12 pares** frente à baseline, mas piorou em DOGEUSDT e LINKUSDT.
- **0 dos 72 folds selecionados foram lucrativos**. Isso **não aprova** a estratégia e não constitui evidência suficiente para seu uso em negociações reais ou Testnet.
- Amostra curta (menos de 17 horas), custos fixos, capital independente por fold e ausência de reprodução do dimensionamento de risco PAPER limitam substancialmente a validade externa.
- Sem promoção, sem mudanças no motor PAPER, no limite de perda de 1%, no número de entradas diário, nos circuit breakers ou nas configurações Binance.
- Código e release podem ser preparados, mas a implantação e o uso na interface só ocorrem após ativação controlada da nova API e Web.
