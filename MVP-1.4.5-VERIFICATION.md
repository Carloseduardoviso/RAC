# RAC MVP 1.4.5 — Verificação local de saídas nos holdouts

Data: 23/11/2026. Máquina `080001325ADPLAN`, repo `C:\Users\05035144208\source\repos\RAC`. Pesquisa offline sobre outubro/2026, Binance Spot públicos, 12 pares × 31 dias, acervo novo validado integralmente por ZIP e SHA-256 (535.680 candles 1m).

## Execução e resultados

- Comando: `dotnet run --project src/RAC.HistoryResearch -c Release -- --exit-constrained-backtest 2026-10`.
- **372 janelas de teste**, **347 NO_TRADE**, 25 janelas ativas, **223 trades teóricos encerrados pelo simulador anterior**.
- **223 intenções teóricas de saída examinadas**, **20 saídas não completamente cobertas** em cinco candles de OHLCV; esses 20 eventos ocorreram em ADAUSDT (3), AVAXUSDT (2) e DOTUSDT (15). Uma parte pode decorrer de ausência de dados futuros suficientes ao fim de um holdout.
- **194 tentativas de compra barradas por volume hipotético**, **199 entradas simuladas parcialmente preenchidas**. Esses números são eventos do simulador, não ordens reais.
- **0 estratégias aprovadas para revisão**, nenhuma promovida para PAPER/Testnet/real. `promotionAllowed=false`, `orderPlacementEnabled=false`, `paperModified=false`.
- Retornos da simulação continuam hipotéticos e não foram transformados em dinheiro realmente realizado pela auditoria de saída. A verificação de capacidade somente impede classificação favorável quando faltam vendas simuladas completas.
- O relatório JSON está em `%LOCALAPPDATA%\RAC\research\monthly\2026-10\exit-capacity-gated-backtest.json`.

## Testes e ambiente local

- **178/178 testes automatizados .NET 10 em Release**, incluindo recusa de inventário sem volume, saída coberta sem autorização de ordens, falta de futuro observável, contagens de saída divergentes e preservação do comportamento anterior com auditoria desligada.
- Serviços web `http://localhost:5081/` e API `http://localhost:5080/api/health` iniciados e verificados HTTP 200, para acesso **no próprio computador**.
- Banco LocalDB `RAC` deste computador: PAPER pausado; Testnet desabilitada; nenhuma ordem Testnet observada. Não houve envio de ordens, desbloqueio de risco, habilitação de Testnet ou transferências.

## Limitações

Capacidade e preenchimentos são **hipóteses construídas com OHLCV**, não execuções da Binance. O resultado não prova rentabilidade futura e não substitui homologação separada.
