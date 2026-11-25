# RAC MVP 1.5.7 — Diagnóstico de robustez SHADOW

Data: 23/11/2026.

## Implementação

- `RacCandidateRobustnessScreen.Evaluate` consome somente a escolha congelada de maio–julho e os relatórios fixos de agosto–outubro já validados pelo diagnóstico anterior.
- Para estratégia ativa com evidências completas nos três meses, apresenta meses positivos, pior e melhor média mensal hipotética, amplitude dos resultados, médias com custos 1x/2x, penalidade de custos e pior drawdown.
- Marca `ACTIVE_ROBUSTNESS_WARNING` quando houver mês não positivo, retorno com custos 2x não positivo ou penalidade anômala; marca `ACTIVE_EVIDENCE_INSUFFICIENT` quando houver mês sem trades, retorno indisponível, saída não resolvida ou triagem de risco reprovada.
- `NO_TRADE` não recebe rentabilidade ativa: `NO_TRADE_NO_ACTIVE_EVIDENCE` e métricas de retorno iguais a null.
- A saída do CLI `--fixed-holdout-replay` agora inclui `robustness` no JSON de pesquisa local, sem afetar contas ou dados operacionais.

## Validação

- Testes .NET Release: **218/218 aprovados**, zero falhas.
- Build Release do projeto de pesquisa: zero avisos e zero erros.
- CLI reexecutada: 36/36 avaliações históricas NO_TRADE, 0 operações, 0 elegíveis para negociação, exit code 0.
- Testes sintéticos cobrem mês de perda sob estratégia ativa, sensibilidade de custos e rejeição por evidência incompleta.

## Limites e segurança

- A robustez não é um critério de aprovação e três meses não representam prova estatística de eficácia. Não há estratégia ativa elegível neste histórico.
- Nenhuma autorização de ordens Testnet/reais, mudanças na simulação PAPER, chaves Binance, circuit breakers, SQL operacional ou serviço API/Web.
