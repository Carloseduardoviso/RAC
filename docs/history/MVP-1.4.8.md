# RAC MVP 1.4.8 — Histórico SHADOW de agosto a outubro de 2026

Data: 23/11/2026. Computador de desenvolvimento `080001325ADPLAN`, repositório `C:\Users\05035144208\source\repos\RAC`.

## Escopo

- Importação e validação por SHA-256 dos arquivos públicos Binance Spot 1m de agosto e setembro de 2026 (12 símbolos). Outubro já tinha acervo local. Nenhuma credencial de corretora necessária; SQL operacional intocado.
- Execução manual do modo de pesquisa offline `--exit-constrained-backtest 2026-08` e `--exit-constrained-backtest 2026-09`, utilizando o mesmo modelo SHADOW de outubro, treino diário de 720 candles e teste cronologicamente posterior de 720 candles. Cada mês seleciona parâmetros apenas em treinamento; os resultados dos meses posteriores não são usados para escolher o gate de um mês anterior.
- Novo `RacShadowThreeMonthDashboard.Load`: exige os 3 relatórios mensais de modo `EXIT_CAPACITY_GATED_SHADOW_ONLY`, valida a projeção de 12 símbolos em cada mês, compara agosto/setembro/outubro em ordem cronológica, apresenta totais de dias sem operar, trades teóricos e saídas hipoteticamente não resolvidas. Relatório faltante não vira retorno zero e a comparação é marcada indisponível. Flags `paperModified`, `orderPlacementEnabled` e `promotionAllowed` seguem sempre falsas.
- Endpoint **autenticado**, apenas GET `/api/research/three-month-dashboard` com falha fechada em relatório inválido, incluído na allowlist fixa da Web. Não foram criadas novas rotas POST nem permissões de negociação.
- Nova seção do painel **Histórico comparativo SHADOW (agosto–outubro/2026)**: gráfico de contagem relativa por mês (saídas não resolvidas, trades hipotéticos, dias sem operar), tabela mensal e tabela de consistência dos 12 agentes. Indicador de elegibilidade apenas para **revisão científica**, sem aprovação de operação real.
- A soma das médias dos agentes não é resultado composto de uma carteira. Retornos por janela são fictícios, com 100 USDT reiniciados no simulador; NO_TRADE não é lucro. Ordens e profundidade do order book real não foram medidas.

## Reproduzir a pesquisa

Na raiz do repo, após obter os ZIPs e manifestos validados:

```powershell
dotnet run --project src/RAC.HistoryResearch -c Release -- --exit-constrained-backtest 2026-08
dotnet run --project src/RAC.HistoryResearch -c Release -- --exit-constrained-backtest 2026-09
dotnet run --project src/RAC.HistoryResearch -c Release -- --exit-constrained-backtest 2026-10
```

Os relatórios são isolados em `%LOCALAPPDATA%\RAC\research\monthly\YYYY-MM\exit-capacity-gated-backtest.json`. Nenhuma rotina de pesquisa é executada automaticamente pela API ou pela Web.

## Limitações

Estudo de três meses é insuficiente para demonstrar vantagem estatística futura. Comparativos são retrospectivos e não medem execução garantida de ordens; a capacidade é aproximada por volume OHLCV histórico e hipóteses de taxa/impacto. A disponibilidade de três arquivos JSON não equivale a uma prova criptográfica contra modificações locais posteriores — os ZIPs originais foram validados por hash durante cada execução manual.

Ver `MVP-1.4.8-VERIFICATION.md`.
