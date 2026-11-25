# RAC MVP 1.4.9 — Verificação do teste exploratório de sinais

Data: 23/11/2026. Computador `080001325ADPLAN`.

## Evidência de pesquisa local

- Fonte: arquivos de pesquisa de **agosto, setembro e outubro de 2026**, três meses de candles públicos Binance Spot e 12 relatórios de agentes por mês, previamente verificados com manifests SHA-256 antes dos backtests.
- **12 agentes avaliados**, **0 positivos nos três meses**, **5 agentes com alguma saída não resolvida**, **0 com evidência estatística suficiente** e **0 elegíveis para revisão científica**.
- Agentes com saídas incompletas: XRPUSDT (1), ADAUSDT (4), AVAXUSDT (8), LTCUSDT (3), DOTUSDT (24); total **40**. Essas são **saídas simuladas não cobertas pelo modelo**, não ordens reais fracassadas.
- BTCUSDT, SOLUSDT, BNBUSDT, DOGEUSDT e LINKUSDT tiveram pelo menos uma média mensal negativa. ETHUSDT e TRXUSDT não realizaram trades no estudo, e portanto os três meses neutros de cada um NÃO evidenciam lucro.
- ADAUSDT foi o único com **uma média mensal positiva** e não foi aprovado: outras duas médias negativas e saídas incompletas. Nenhum p-valor Bonferroni passou o filtro de significância.
- Relatório gerado pela CLI manual em `%LOCALAPPDATA%\RAC\research\monthly\overfit-screen-2026-08-to-2026-10.json`, flags `paperModified=false`, `orderPlacementEnabled=false` e `promotionAllowed=false`.

## Qualidade

- Testes de binomial exata unilateral, correção Bonferroni, três meses positivos insuficientes, saídas incompletas bloqueantes, casos NO_TRADES e relatório faltante.
- `dotnet test RAC.slnx -c Release --nologo --verbosity quiet` e `node --check src/RAC.Web/wwwroot/app.js` devem passar antes de publicar.

## Observação essencial

Esta é uma **triagem conservadora de amostra pequena**. Três meses não comprovam nem refutam rigorosamente overfitting; o teste unilateral presume independência e equiprobabilidade dos sinais que **não foram verificadas**. Nenhuma estratégia obtém permissão de operar por um p-valor.

## Implantação final em 23/11/2026

- **194/194 testes automatizados aprovados**, zero falhas; `node --check` aprovado.
- API, Web e CLI de pesquisa publicados separadamente em `%LOCALAPPDATA%\RAC\staging\mvp-1.4.9\api`, `...\web`, `...\research`.
- Serviços antigos das portas 5080 e 5081 substituídos após confirmação SQL de operação pausada.
- API `http://localhost:5080/api/health`: HTTP 200, status `ok`, `realOrdersEnabled=false`, `testnetOrderEnabled=false`.
- Painel `http://localhost:5081/`: HTTP 200, seção `overfitScreenPanel` presente; `/app.js` HTTP 200 com `refreshOverfitScreen`.
- Endpoint privado `/api/research/overfit-screen` exige login: **HTTP 401 anônimo** na API e na Web.
- Logs `mvp149-api.err.log` e `mvp149-web.err.log`: **0 bytes**.
- SQL após a atualização: PAPER `Running=0`, Testnet `Enabled=0`, **0 ordens Testnet**. Dados existentes preservados.
- Relatório offline: modo `THREE_MONTH_OVERFIT_SCREEN_SHADOW_ONLY`, 3 meses, 12 agentes, 0 positivos nos três meses, 5 com saídas incompletas, 0 com evidência estatística suficiente, 0 elegíveis para revisão, `orderPlacementEnabled=false`, `promotionAllowed=false`.
- A API não possui rota de ativação para esta pesquisa. Possível necessidade de renovar a sessão no navegador após reinício dos serviços.
