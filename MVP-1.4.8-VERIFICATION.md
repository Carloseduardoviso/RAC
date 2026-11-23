# RAC MVP 1.4.8 — Evidência de pesquisa comparativa e implantação

Data: 23/11/2026, computador `080001325ADPLAN`.

## Histórico importado e pesquisa SHADOW

- Fontes públicas Binance Spot ZIP 1m mensais de agosto, setembro e outubro de 2026, 12 pares, cada ZIP revalidado por SHA-256 contra o manifesto em modo offline antes de cada backtest.
- Resultados mensais sob `%LOCALAPPDATA%\RAC\research\monthly\YYYY-MM\exit-capacity-gated-backtest.json`, distintos do banco operacional.
- **Agosto**: 372 janelas, 357 NO_TRADE, 165 trades teóricos, 10 saídas sem cobertura no horizonte de cinco candles, 0 agentes elegíveis para revisão; média aritmética fictícia dos 12 retornos por janela: -0,0696%.
- **Setembro**: 360 janelas, 345 NO_TRADE, 216 trades teóricos, 10 saídas sem cobertura, 0 elegíveis; média: -0,1061%.
- **Outubro**: 372 janelas, 347 NO_TRADE, 223 trades teóricos, 20 saídas sem cobertura, 0 elegíveis; média: -0,0685%.
- **Total: 1.104 janelas de agente, 1.049 dias NO_TRADE, 604 trades hipotéticos e 40 saídas incompletas.** Todos os 12 agentes permanecem sem elegibilidade nos três meses. O total NÃO representa execução de uma carteira nem rentabilidade composta.

## Testes automatizados

- `dotnet test RAC.slnx -c Release --nologo --verbosity quiet`: **184/184 testes aprovados**, incluindo testes de histórico faltante, ordenação cronológica, reconciliação de três meses, ausência de promoção, falha fechada em arquivos faltantes e rejeição de reporte inseguro pelos validadores existentes.
- `node --check src/RAC.Web/wwwroot/app.js` sem erro.
- A nova rota exige sessão RAC e é GET-only; o client bloqueia modo inesperado, contagens inválidas, relatórios incompletos e autorização de operações.

## Implantação

- Publicar a API e Web em staging separado, verificar as portas 5080 e 5081 e não reiniciar serviços se PAPER ou Testnet estiverem operando. Confirmar 401 da rota sem login, 200 da Web, health da API, estado do SQL e logs de erro.
- Não houve alteração em saldo, posição ou risco operacional, ordens não foram ativadas e nenhuma estratégia foi promovida.

## Implantação concluída no computador 080001325ADPLAN

- **184/184 testes .NET Release aprovados**; `node --check` sem erros.
- `dotnet publish` da API e Web realizado em `%LOCALAPPDATA%\RAC\staging\mvp-1.4.8\api` e `...\web`, separadamente dos serviços anteriores.
- Processos locais antigos das portas 5080/5081 substituídos somente após confirmação SQL de PAPER `Running=0` e Testnet `Enabled=0`.
- `http://localhost:5080/api/health` HTTP 200 com `realOrdersEnabled=false`, `testnetOrderEnabled=false`.
- `http://localhost:5081/` HTTP 200, seção `researchThreeMonthPanel` presente; `/app.js` HTTP 200 com renderizador `renderThreeMonthBars`.
- Rota `GET /api/research/three-month-dashboard` rejeita acesso anônimo com **HTTP 401** tanto na API quanto no proxy web.
- Logs `mvp148-api.err.log` e `mvp148-web.err.log`: zero bytes.
- SQL após deploy: PAPER `Running=0`, Testnet `Enabled=0`, **0 ordens Testnet**.
- O navegador pode pedir novo login após a substituição de processos. Nenhuma ativação de ordens ou risco foi autorizada.
