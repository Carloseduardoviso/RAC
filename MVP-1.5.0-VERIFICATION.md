# RAC MVP 1.5.0 — Validação longitudinal SHADOW e instalação local

Data: 23/11/2026, computador `080001325ADPLAN`. Seis acervos históricos públicos Binance Spot 1m de maio a outubro 2026, 12 pares por mês; arquivos ZIP e manifests SHA-256 verificados antes da execução manual de cada pesquisa.

## Resultado consolidado

| Mês 2026 | Janelas | NO_TRADE | Trades hipotéticos | Saídas não resolvidas |
|---|---:|---:|---:|---:|
| Maio | 372 | 368 | 12 | 0 |
| Junho | 360 | 342 | 225 | 25 |
| Julho | 372 | 368 | 40 | 2 |
| Agosto | 372 | 357 | 165 | 10 |
| Setembro | 360 | 345 | 216 | 10 |
| Outubro | 372 | 347 | 223 | 20 |
| **Total** | **2208** | **2127** | **881** | **67** |

- **Zero agentes com evidência estatística suficiente** e **zero elegíveis para revisão científica**; o estudo permanece SHADOW, sem envio de ordens nem liberação de estratégia.
- O CLI `--six-month-validation` gravou `%LOCALAPPDATA%\RAC\research\monthly\six-month-validation-2026-05-to-2026-10.json` com `agents=12`, `agentDays=2208`, `noTradeDays=2127`, `hypotheticalTrades=881`, `unresolvedExits=67`, `statisticallySupportedAgents=0`, `agentsEligibleForResearchReview=0`, flags de operação e promoção falsas.
- `NO_TRADE` e médias fictícias não são lucro realizado. Saídas não resolvidas são somente falhas dentro do modelo hipotético de capacidade baseado em OHLCV, não ordens executadas.

## Segurança e qualidade

- Casos automatizados: histórico faltante, ordenação cronológica de seis meses, reconciliação de 12 agentes/72 avaliações mensais, falso positivo evitado em seis meses positivos sem significância após Bonferroni, risco de saída bloqueante e arquivos com flags de promoção inseguras.
- Testes da solução .NET Release, verificação sintática do JS e deploy das publicações separadas para API/Web/CLI.
- Endpoint autenticado apenas GET; ausência de novas rotas de controle.
- Antes do reinício do serviço, confirmar no SQL `Running=0` (PAPER), `Enabled=0` (Testnet), 0 ordens Testnet. Não substituir dados, não remover locks e não executar ordens.

## Limitações

A pesquisa reconstrói decisões de treino/teste por janelas diárias e usa hipóteses de custo. Não constitui experimento prospectivo congelado, prova estatística de rentabilidade ou validação da execução em livro de ordens real.

## Implantação final — 23/11/2026

- **197/197 testes automatizados .NET Release aprovados**; `node --check` também passou.
- Publicações separadas no `%LOCALAPPDATA%\RAC\staging\mvp-1.5.0\api`, `...\web`, `...\research`; não houve escrita em banco operacional.
- Serviços locais da versão anterior foram substituídos apenas após SQL confirmar PAPER `Running=0` e Testnet `Enabled=0`.
- API health `http://localhost:5080/api/health`: HTTP 200 `status=ok`, `realOrdersEnabled=false`, `testnetOrderEnabled=false`.
- Painel `http://localhost:5081/`: HTTP 200 e elemento `sixMonthValidationPanel` presente; JS HTTP 200 e renderizador `refreshSixMonthValidation` presente.
- A rota `/api/research/six-month-validation` responde **HTTP 401 sem login** tanto pela API quanto pelo proxy web.
- Logs `mvp150-api.err.log` e `mvp150-web.err.log`: **0 bytes**.
- SQL após a publicação: PAPER `Running=0`, Testnet `Enabled=0`, **0 ordens Testnet**, sem ativar PAPER nem retirar travas.
- JSON final do estudo de seis meses conferido: `mode=SIX_MONTH_WALK_FORWARD_RISK_SHADOW_ONLY`, `agents=12`, `agentDays=2208`, `noTradeDays=2127`, `hypotheticalTrades=881`, `unresolvedExits=67`, `agentsEligibleForResearchReview=0`, `orderPlacementEnabled=false`, `promotionAllowed=false`.
- O acesso web poderá exigir atualizar a página e renovar a sessão após o reinício da API.
