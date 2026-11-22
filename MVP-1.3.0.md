# RAC MVP 1.3.0 — Amostra publica ampliada e walk-forward SHADOW

Implementacao: 22/11/2026.

## O que foi implementado

- `GET /api/strategy/walk-forward` **autenticado**, permitido explicitamente pela allowlist restrita do proxy Web, e novo botao no painel.
- Coleta sob demanda, exclusivamente publica e sem credenciais: `GET https://data-api.binance.vision/api/v3/klines?symbol=<par>&interval=1m&limit=1000` para os 12 pares. Apenas candles **fechados** passam pela validacao existente da Binance.
- O historico expandido e mantido apenas **em memoria** para a analise, sem adicionar candles artificiais ou gravar dados no SQL operacional. A rotina independente de atualizacao publica da carteira continua inalterada. Nova pesquisa exige o botao; nenhuma requisicao de historico e agendada.
- Evita rajadas repetidas: cache em memoria de 60 segundos e exclusao mutua por analise. Em erros publicos ou rate limit a consulta falha; nao envia ordens, nao troca de endpoint, nao contorna o limite Binance.
- `PaperStrategyWalkForward` verifica consistencia de 1 minuto, ultimo candle recente e ao menos 420 candles continuos. Serie >1000 e recusada, sem truncamento conveniente.
- Cada fold usa **180 candles passados para treino** e **120 candles posteriores para avaliacao**. Avanco de 120 candles por fold: testes futuros nao se sobrepoem; as janelas de treino podem se sobrepor (rolling window).
- O criterio de selecao usa **somente o resultado do treino**, sem consultar o teste: duas hipoteses predefinidas (`BASELINE` com stop 2xATR e `COOLDOWN_5` com o mesmo stop e 5 candles de espera). Em caso de empate, preserva a baseline.
- Cada fold zera o capital ficticio de 100 USDT para treino e teste, com as mesmas premissas hipoteticas de taxa 0,1%, slippage 0,05% e meio spread 0,01% por lado. A metrica de retorno e uma media de resultados percentuais independentes, nao uma curva de capital composta.
- O painel exibe quantidade de folds, escolhas de cooldown em treino, testes posteriores positivos e retorno medio de baseline versus politica selecionada.

## Limitacoes essenciais

- No maximo ~16h40 de dados 1m por chamada Binance (1000 candles por par), **ainda e uma amostra curta** para aferir regime e custos reais. Walk-forward com folds historicos reduz contaminacao treino/teste, mas nao elimina vieses e overfitting em escolhas futuras.
- O experimento nao implementa o mesmo dimensionamento, stop operacional, liquidez, filtros, ordens minimas ou limites financeiros do PAPER BRL. Nenhum ganho futuro esta demonstrado.
- Os folds de treino sobrepoem-se, logo nao sao amostras estatisticamente independentes. O snapshot tambem pode ser atualizado a cada nova consulta.
- Nunca promover automaticamente. Campos de seguranca: `mode=WALK_FORWARD_RESEARCH_ONLY`, `paperModified=false`, `riskLimitsModified=false`, `historyPersisted=false`, `orderPlacementEnabled=false`, `promotionAllowed=false`.
- PAPER pausado por limite de perda diaria **permanece bloqueado**; nenhuma mudanca no numero de operacoes por dia, circuit breakers, contas, credenciais, ordem real ou testnet.

## Validacao e implantacao

- Testes de amostra curta, lacunas, candles antigos, selecao sem vazamento, janelas de teste sem sobreposicao, integridade da rota publica HTTP GET e rejeicao de par nao monitorado.
- `dotnet test RAC.slnx -c Release`, `node --check src/RAC.Web/wwwroot/app.js` e `git diff --check` devem ser verdes antes do commit/push.
- Publicacao preparada em `%LOCALAPPDATA%\RAC\staging\mvp-1.3.0` sem reiniciar o RAC. A futura ativacao exige backup validado, retorno seguro a `mvp-1.2.9` e bloqueios financeiros/Testnet preservados.
