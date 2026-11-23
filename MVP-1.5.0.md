# RAC MVP 1.5.0 — Validação SHADOW longitudinal de seis meses

Data: 23/11/2026, computador `080001325ADPLAN`, repositório `C:\Users\05035144208\source\repos\RAC`.

## Execução e arquitetura

- Importados e validados por SHA-256 os 12 arquivos públicos Binance Spot 1m dos meses de maio, junho e julho de 2026. Já existiam agosto, setembro e outubro. Cada mês foi submetido ao mesmo CLI `--exit-constrained-backtest YYYY-MM`, que valida novamente o acervo e aplica treino cronológico diário de 720 minutos seguido de teste fora da amostra de 720 minutos; seleção de entrada, horizonte e filtros só usa o treino diário de cada respectivo mês.
- Novo `RacShadowSixMonthValidation.Load`: **exige seis relatórios completos** de maio a outubro de 2026 no modo `EXIT_CAPACITY_GATED_SHADOW_ONLY`, valida os 12 símbolos, quantidades, flags e reconcilia trades e saídas. Mês ausente interrompe a análise; não é preenchido por zeros. Não cruza treinamento entre meses e não atualiza os gates com conhecimento posterior a cada teste.
- Novo diagnóstico por símbolo: meses positivos, negativos e neutros; meses com trades; número de trades e saídas incompletas; pior retorno médio mensal; teste exato unilateral de sinais de meses não nulos e correção Bonferroni para 12 agentes. **Mesmo seis meses cronológicos não constituem seis resultados independentes**. Recusa agentes sem trades, com saídas não resolvidas, meses negativos, meses insuficientes ou correção estatística não significativa.
- Uma hipotética classificação `REVIEW_REQUIRED_NOT_APPROVED` só permite análise científica adicional, nunca envio de ordem; todos os campos `paperModified=false`, `orderPlacementEnabled=false`, `promotionAllowed=false` permanecem fixos.
- API read-only autenticada `GET /api/research/six-month-validation`, adicionada à allowlist fixa do proxy. Não existem novos endpoints POST ou permissões operacionais.
- Painel autenticado `RAC MVP 1.5.0 — Validação SHADOW de seis meses`, com indicadores, comparativo mensal e tabela dos 12 agentes; botão manual para atualizar em modo somente leitura.
- CLI manual `dotnet run --project src/RAC.HistoryResearch -c Release -- --six-month-validation`, que exporta JSON separado em `%LOCALAPPDATA%\RAC\research\monthly\six-month-validation-2026-05-to-2026-10.json`.

## Limitações explícitas

- Os filtros e parâmetros do estudo são recalibrados apenas dentro do treino de cada janela; esta implementação **não constitui um único modelo congelado prospectivamente para seis meses inteiros**. Não há prova de independência, ausência de overfitting ou retorno futuro.
- O estudo usa custos e capacidades hipotéticas derivados de OHLCV; não possui livro de ordens, latência de execução, fills reais, reconciliação de receita real ou capital composto. NO_TRADE representa ausência de operação e não rentabilidade.
- O teste de sinais e a correção para 12 agentes são apenas diagnósticos exploratórios; o universo de experimentos e suas dependências pode ser maior. Não deve ser interpretado como teste inferencial definitivo.
- A ferramenta de pesquisa e os JSONs ficam fora do banco operacional e não iniciam simulação PAPER, Testnet ou ordens reais.

Ver `MVP-1.5.0-VERIFICATION.md` para resultados e implantação.
