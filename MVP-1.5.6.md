# RAC MVP 1.5.6 — Diagnóstico de decisões congeladas e avaliação posterior

Data: 23/11/2026.

## Implementação

- Introduzido `RacFixedCandidateDiagnostics.Compare`, que relaciona os votos de treinamento maio–julho às três avaliações fixas agosto–outubro para os 12 agentes.
- As porcentagens de voto medem estabilidade de escolhas de treinamento, **não** retorno de treinamento nem rentabilidade comprovada.
- Para decisões ativas com evidências completas, exibe médias mensais hipotéticas 1x/2x, diferença de custos, trades e pior drawdown. Retém os retornos se falta evidência ou triagem de risco falha.
- Para decisões NO_TRADE, o relatório explicita abstenção, sem inventar retornos de estratégias ativas.
- Falha fechada com mês faltante, mudança do gate congelado, trades ou retorno falsos em NO_TRADE, flags de ordem/promoção ativas.
- O comando `--fixed-holdout-replay` inclui o objeto `diagnostic` no JSON histórico separado da conta operacional.

## Validação

- `dotnet test tests/RAC.Tests/RAC.Tests.csproj -c Release --no-restore --verbosity quiet`: 216/216 aprovados; 0 falhas.
- Build Release da pesquisa: 0 erros e 0 avisos.
- Replay de agosto a outubro: 36/36 cenários NO_TRADE, sem negociações, relatório produzido com dados de diagnóstico.

## Restrições

- Dados históricos e custos de execução continuam hipotéticos. Nenhum desempenho ativo foi demonstrado para o conjunto congelado atual.
- Sem liberação de estratégias, ordens Binance Testnet/real, alteração de PAPER, credenciais ou SQL operacional.
- API e Web locais não foram reiniciadas ou implantadas nesta etapa.
