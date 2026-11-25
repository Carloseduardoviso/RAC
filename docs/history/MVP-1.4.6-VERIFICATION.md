# RAC MVP 1.4.6 — Validação do painel de pesquisas SHADOW

Data: 23/11/2026. Ambiente Windows local `080001325ADPLAN`.

## Resultado da fonte histórica

- Fonte: relatório do MVP 1.4.5 gerado em outubro de 2026, **12 pares × 31 dias**, 372 janelas de teste.
- **347 janelas NO_TRADE**, 25 janelas com operações, **223 trades hipotéticos**, **20 saídas sem cobertura** em cinco candles e **12/12 agentes rejeitados**. Não há agentes aprovados para revisão.
- Os três agentes com rejeição explícita por liquidez de saída são ADAUSDT, AVAXUSDT e DOTUSDT; os outros nove seguem rejeitados por evidência insuficiente de retorno líquido.
- A visualização somente lê esses resultados e não converte `NO_TRADE` em lucro realizado.

## Testes esperados da implementação

- `dotnet test RAC.slnx -c Release --nologo --verbosity quiet` para testes de integridade, ausência de relatório, casos de flags de promoção inseguras e rejeição de mês inválido.
- `node --check src/RAC.Web/wwwroot/app.js` para validação sintática JavaScript.
- Verificar HTTP 200 do painel e de `/api/health` no localhost; a rota `/api/research/shadow-dashboard` requer login.
- Confirmar no SQL local PAPER `Running=0`, Testnet `Enabled=0`, 0 ordens Testnet, sem alterações de credenciais ou dados durante essa etapa.

## Limitações

- A UI exibe **pesquisa offline** e não representa uma estratégia em produção. Não oferece botões de promoção ou de execução real.
- Resultados e capacidade são hipotéticos, baseados em candles históricos e custos presumidos; não existe demonstração de rentabilidade futura.
- A sessão de login pode precisar ser renovada após reinício dos serviços locais para ativar a versão de painel atualizada.

## Execução concluída em 23/11/2026

- **181/181 testes automatizados aprovados** (`dotnet test RAC.slnx -c Release`).
- `node --check src/RAC.Web/wwwroot/app.js` passou sem erro.
- API e Web publicados em `%LOCALAPPDATA%\RAC\staging\mvp-1.4.6\api` e `...\web`, respectivamente, e **iniciados neste computador** nas portas locais 5080 e 5081.
- API `http://localhost:5080/api/health`: HTTP 200, `environment=SIMULACAO_LOCAL`, `realOrdersEnabled=false`, `testnetOrderEnabled=false`.
- Interface `http://localhost:5081/`: HTTP 200, com `researchShadowPanel` presente no HTML.
- Consulta **sem autenticação** a `/api/research/shadow-dashboard`: **HTTP 401**, comprovando proteção de acesso. Não foram usadas nem expostas credenciais de login durante o teste.
- Logs `mvp146-api.err.log` e `mvp146-web.err.log`: **0 bytes**.
- SQL operacional deste computador após a implantação: PAPER `Running=0`, Testnet `Enabled=0`, **0 ordens Testnet**. As regras e o histórico deste computador foram preservados.
- Relatório SHADOW histórico de outubro/2026 previamente validado: 372 janelas, 347 NO_TRADE, 223 trades teóricos, 20 saídas sem cobertura, 12 rejeitados. A UI apresenta uma projeção somente leitura desse relatório; não há migração de banco, operações ou aprovação de estratégias.
- Após o reinício da API/Web, o usuário poderá precisar atualizar a página e efetuar login novamente.
