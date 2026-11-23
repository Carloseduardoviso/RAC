# RAC MVP 1.3.5 — Comprovação do acervo mensal público

Data da execução: 22/11/2026. Origem: arquivo público mensal Binance Spot `2026-10` (outubro de 2026). Utilitário `RAC.HistoryResearch` executado separadamente na máquina DESKTOP-MI73RHO. Sem leitura nem escrita da carteira operacional.

## Integridade

- **12 arquivos ZIP oficiais baixados e validados integralmente**, um para cada par monitorado. Mês UTC 2026-10, 31 dias completos, **44.640 candles por par**, total **535.680 candles 1m**, 19,87 MiB de arquivos ZIP somados.
- Todos os arquivos CSV dentro dos ZIPs possuem OHLCV, número de colunas, close timestamps, ordem cronológica, número de candles e cobertura de dias validados. Timestamps da Binance foram processados em microssegundos.
- Manifesto de pesquisas com hashes SHA-256 por arquivo em `%LOCALAPPDATA%\RAC\research\monthly\2026-10\manifest.json`.
- Não houve qualquer requisição autenticada ou envio de ordens. O processo usa apenas dados de mercado públicos e não altera SQL.

## Regimes descritivos diários

UP significa variação diária da abertura ao fechamento >1%; DOWN significa <-1%; demais SIDEWAYS. Estes rótulos descrevem **movimento de preço**, não oportunidade de compra ou acurácia de estratégia.

| Par | UP | DOWN | SIDEWAYS | Total de dias |
|---|---:|---:|---:|---:|
| BTCUSDT | 6 | 6 | 19 | 31 |
| ETHUSDT | 6 | 9 | 16 | 31 |
| SOLUSDT | 6 | 6 | 19 | 31 |
| BNBUSDT | 8 | 7 | 16 | 31 |
| XRPUSDT | 9 | 9 | 13 | 31 |
| ADAUSDT | 9 | 13 | 9 | 31 |
| DOGEUSDT | 7 | 10 | 14 | 31 |
| LINKUSDT | 11 | 12 | 8 | 31 |
| AVAXUSDT | 6 | 9 | 16 | 31 |
| LTCUSDT | 7 | 10 | 14 | 31 |
| DOTUSDT | 9 | 15 | 7 | 31 |
| TRXUSDT | 2 | 2 | 27 | 31 |

## Situação operacional verificada depois da coleta

- PAPER: `Running=0`, `RiskLocked=1` (limite de perda diária de 1% ainda ativo); 33 fills, 1 posição aberta.
- Binance Testnet: `Enabled=0`, `CircuitLocked=1`, `EmergencyStopped=1`; zero ordens no banco.
- **Nenhum backtest de mês completo foi executado nesta etapa.** Os dados criam a base para pesquisas futuras e não constituem evidência de lucratividade, muito menos justificativa para operar.
