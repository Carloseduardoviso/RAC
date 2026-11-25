# RAC MVP 1.4.9 — Diagnóstico de consistência e risco de overfitting

Data: 23/11/2026. Computador `080001325ADPLAN`, repo `C:\Users\05035144208\source\repos\RAC`.

## Objetivo

Evitar que três meses de resultados SHADOW retrospectivos sejam confundidos com uma demonstração confiável de vantagem de trading, e expor motivos objetivos de cautela, incluindo falta de operações, meses com médias negativas e saídas teóricas incompletas.

## Implementação

- `RacShadowOverfitDiagnostics.Assess` recebe o relatório **já validado** por `RacShadowThreeMonthDashboard`, preserva a ordem agosto–setembro–outubro de 2026 e reconcilia trades e saídas agregadas.
- Para cada agente, contabiliza meses com retorno médio por janela positivo, negativo ou exatamente zero, meses com trades, pior e melhor média mensal, trades simulados e saídas sem cobertura de liquidação.
- Calcula um **teste exato unilateral de sinais** nas médias mensais **não nulas**, sob uma hipótese idealizada de sinais positivos e negativos equiprováveis e independentes. Empates são descartados somente para o cálculo do p-valor; não são tratados como ganhos. Ajusta o p-valor por Bonferroni para as **12 comparações** (`min(1, 12 * p)`, alfa nominal de 5%).
- Exige, como **filtro exploratório mínimo**, pelo menos **seis meses com média não nula** para sequer sinalizar uma candidatura à revisão. Com apenas três meses no acervo, **nenhum agente pode superar esse filtro**, independentemente do p-valor. Mesmo em meses adicionais, o p-valor não substitui validação de independência, negociação, custos reais, modelo econômico ou avaliação fora da amostra.
- Status informativos: `UNRESOLVED_EXIT_RISK`, `NO_TRADES`, `NEGATIVE_MONTHS`, `INSUFFICIENT_MONTHS`, `NOT_SIGNIFICANT_AFTER_12_TESTS` e `RESEARCH_ONLY_REVIEW` (somente revisão científica, não execução).
- Novo endpoint **autenticado** e read-only `/api/research/overfit-screen`, via proxy web GET allowlist, sem nova rota de controle nem integração privada Binance. Relatório ausente/inválido resulta em indisponibilidade e nenhuma aprovação.
- Nova seção web **MVP 1.4.9 — Consistência estatística e risco de overfitting**: quatro indicadores, tabela dos 12 agentes com meses positivos/negativos/neutros, saídas sem cobertura, valores do teste de sinais e diagnóstico; botão manual de recarregar.
- CLI manual de pesquisa `dotnet run --project src/RAC.HistoryResearch -c Release -- --overfit-screen`, que salva `%LOCALAPPDATA%\RAC\research\monthly\overfit-screen-2026-08-to-2026-10.json` sem escrever no banco operacional.

## Limitações metodológicas

**Este teste não constitui prova formal de ausência ou presença de overfitting.** São apenas **três médias mensais**, com decisões de estratégias recalculadas em treinamento a cada janela; não está demonstrada independência temporal, estacionariedade, ausência de viés de seleção, adequação de Bonferroni a todo o universo real de tentativas, nem probabilidade de retorno futuro. O mínimo de seis meses é um **limiar interno exploratório**, não tamanho amostral suficiente por si só.

O retorno analisado é de pesquisa SHADOW com capital fictício reiniciado por janela, sem execução real, e as regras de liquidez são hipóteses com dados OHLCV. Valores `NO_TRADES` e médias 0% em meses sem operação não demonstram rentabilidade. Nenhuma ordem autorizada.

## Segurança

Não ativa PAPER, Testnet ou ordens reais, não altera saldos, posições, circuito de risco ou credenciais. O painel atual é atualizado somente após teste e confirmação de serviços pausados. Ver `MVP-1.4.9-VERIFICATION.md`.
