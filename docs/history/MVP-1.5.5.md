# RAC MVP 1.5.5 — Stress de custos e risco no replay fixo

Data: 23/11/2026.

## Implementação

- Adicionada avaliação reutilizável `RacFixedHoldoutReplay.AssessWindow` para 720 candles de holdout sem reotimizar o gate ou horizonte congelados.
- Para estratégias ativas, replay comparativo com custos 1x e 2x, ambos com estresse de liquidez e avaliação de saída.
- O relatório distingue retorno hipotético com custos normais e dobrados, número de saídas não resolvidas sob 2x, drawdown máximo entre cenários e triagem conservadora de risco (limite de 5% herdado do estudo).
- Com evidência incompleta de saída, o retorno fica nulo; com drawdown acima do limite, o resultado recebe rejeição de risco. Nenhuma estratégia torna-se elegível para negociação.
- Testes incluem determinismo de janela ativa, rejeição de custo fora de 1x/2x, janela incompleta e configuração de gate inválida.

## Validação

- `dotnet test tests/RAC.Tests/RAC.Tests.csproj -c Release --no-restore --verbosity quiet`: 213/213 aprovados.
- Build Release da pesquisa: zero avisos/erros.
- CLI de replay histórico novamente executado: 36 cenários (12 agentes, agosto–outubro), todos NO_TRADE, 0 trades e 0 estratégias aprovadas.

## Limitações

- O conjunto atual não contém decisões congeladas ativas; o cenário de custos 2x foi coberto por testes sintéticos, não estimou retorno ativo nos 36 casos históricos.
- Os retornos de simulação são hipotéticos e não prometem rendimento futuro.
- Código isolado de pesquisa: não há envio de ordens, mudanças em PAPER, Binance, segredos ou SQL operacional.
- API e Web em execução não foram reiniciadas ou implantadas nesta etapa.
