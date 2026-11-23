# RAC MVP 1.5.1 — Auditoria temporal de decisões congeladas SHADOW

Data: 23/11/2026. Computador `080001325ADPLAN`, repo `C:\Users\05035144208\source\repos\RAC`.

## Implementação

- Divide rigidamente a pesquisa em **treinamento maio–julho de 2026**, 92 dias por símbolo, e **período posterior agosto–outubro de 2026**, 92 dias por símbolo. A escolha congelada jamais consulta seleções ou retornos posteriores.
- `RacFrozenDecisionAudit.Load`: primeiro valida os seis relatórios SHADOW com as verificações existentes e suas flags; depois, exclusivamente para maio, junho e julho, lê as decisões `SelectedEntryGate` e `ChosenHorizonMinutes` emitidas pelo processo de treino de cada dia. Confere datas, completude, ordem cronológica e enums permitidos. Escolhe o par (gate,horizonte) que apareceu mais vezes; empates favorecem `NO_TRADE`.
- Um gate majoritário **NO_TRADE** é matematicamente equivalente a zero execução e retorno fictício 0%, sem inferir lucro. Um gate diferente de NO_TRADE **não recebe retorno estimado**: seu valor de resultado fica nulo e o status `FIXED_STRATEGY_REPLAY_REQUIRED` impede uso dos retornos de modelos recalibrados a cada janela.
- A auditoria de decisões **não congela uma estratégia de compra ativa, não simula execução no book e não realiza ordens**. Como o resultado foi NO_TRADE para os 12 agentes, não existe neste conjunto uma estratégia ativa cuja rentabilidade tenha sido medida com parâmetros fixos.
- Novo endpoint autenticado GET `/api/research/frozen-decisions`, sem rotas POST; validação com falha fechada. A Web inclui seção **MVP 1.5.1 — Decisões congeladas antes do teste**, com tabela por agente, votos, janelas e interpretação. API e Web preservam as rotas anteriores.
- CLI manual `dotnet run --project src/RAC.HistoryResearch -c Release -- --frozen-decision-audit`. Resultado em `%LOCALAPPDATA%\RAC\research\monthly\frozen-decision-2026-05-to-2026-10.json`, fora da conta operacional.

## Limitações e próximos passos

- O critério de voto majoritário é uma **nova regra descritiva de congelamento**, não uma otimização comprovadamente melhor nem um sistema de negociação; a maioria NO_TRADE já era predominante no treino. A ausência de perdas decorre da ausência de operações e não prova vantagem de investimento.
- O estudo usa as decisões adaptativas antigas APENAS como fonte para eleger a regra fixa; a avaliação posterior no caso NO_TRADE é trivial. Quando for escolhido um gate ativo, será necessário construir e executar um replay genuinamente fixo usando somente candles posteriores e custos/risco de execução, sem consultar seu desempenho na seleção.
- Os relatórios e seus arquivos históricos foram previamente verificados na importação e pesquisa; a auditoria lê JSONs locais validados estruturalmente, não executa novo SHA256 dos ZIPs neste comando. Ainda não há experimento prospectivo nem avaliação de lucro de estratégia ativa congelada.
- Nenhuma promoção de estratégia, liberação PAPER/Testnet/real ou mudança nos circuit breakers é permitida.

Consulte `MVP-1.5.1-VERIFICATION.md`.
