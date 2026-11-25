# RAC MVP 1.4.7 — Verificação do comparativo visual SHADOW

Data: 23/11/2026, ambiente `080001325ADPLAN`.

## Testes e implantação

- `dotnet test RAC.slnx -c Release --nologo --verbosity quiet`: **181/181 testes aprovados**, zero falhas.
- `node --check src/RAC.Web/wwwroot/app.js`: sintaxe JavaScript aprovada.
- O HTML contém `researchShadowChartMetric`, seletor de cinco indicadores predefinidos. A função JS `renderShadowBars` apresenta 12 barras com números não negativos e escala relativa e rejeita indicadores inesperados. CSS dedicado `.research-bar-row` suporta telas menores.
- Atualizados somente `index.html`, `app.js` e `styles.css` do website publicado em `%LOCALAPPDATA%\RAC\staging\mvp-1.4.6\web\wwwroot`, com cópias anteriores de segurança em `%LOCALAPPDATA%\RAC\staging\mvp-1.4.6\*.before-147`.
- **HTTP 200** no `http://localhost:5081/` (controle presente), `/app.js` (renderizador presente), `/styles.css` (novos estilos presentes). A API em `http://localhost:5080/api/health` segue `status=ok`, `realOrdersEnabled=false`, `testnetOrderEnabled=false`.
- SQL LocalDB deste computador: PAPER `Running=0`, Testnet `Enabled=0`, **0 ordens Testnet** após a aplicação. Sem migração de banco, envio de ordens ou alteração da carteira.

## Evidência exibida

O relatório local de pesquisa de outubro tem 12 agentes, 372 janelas, 347 NO_TRADE, 223 trades hipotéticos e 20 saídas sem cobertura; três agentes com rejeição explícita por saída (ADAUSDT, AVAXUSDT, DOTUSDT), **zero aprovados**. Os gráficos não alteram esses valores, apenas oferecem comparação visual.

## Limitação

Os gráficos são de **um único mês**, não uma evolução mensal. A capacidade de execução baseada em OHLCV não equivale a liquidez real. Para conferir a tela, atualizar o navegador (Ctrl+F5) enquanto autenticado, localizar a seção SHADOW e alternar o indicador.
