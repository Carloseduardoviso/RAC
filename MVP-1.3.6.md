# RAC MVP 1.3.6 — Backtest mensal walk-forward SHADOW, isolado

Data: 22/11/2026.

## Entrega

- Novo `MonthlyWalkForwardResearch` em `RAC.Application` com leitura de arquivos mensais públicos offline, **somente após a mesma validação integral de integridade do MVP 1.3.5**.
- Comando de execução manual, sem navegador, API ou worker: `dotnet run --project src/RAC.HistoryResearch -c Release -- --backtest 2026-10`.
- O comando exige o manifesto `manifest.json` do acervo offline e valida **SHA-256 de cada ZIP**, nome e cobertura dos 12 símbolos, antes de permitir a pesquisa; arquivos modificados ou ausentes são rejeitados.
- Cada um dos 31 dias UTC usa **primeiras 720 velas de 1 minuto para treino** e **últimas 720 para teste cronológico**. O horizonte de entrada/saída é selecionado pelo retorno simulado **somente durante o treino** (alternativas fixas de 1/5/15/30 minutos), e a mesma escolha é executada no teste posterior do próprio dia.
- Trades fictícios completos com stop de 2x ATR, compra após candle fechado, custos fixos estimados por lado de 0,1% taxa, 0,05% slippage e 0,01% meio spread. Cada teste começa com 100 USDT simulados e termina liquidando posição aberta. Reporta trades encerrados, vitórias, perdas, stops, retorno líquido e drawdown.
- **372 períodos de teste no total**, 31 por agente, sem sobreposição temporal entre treino e teste em cada dia. A escolha do próximo dia não reutiliza o desempenho do teste anterior, apenas o treino do próprio dia.
- Resumo descritivo de resultados por regimes `UP`, `DOWN`, `SIDEWAYS`, com rótulos baseados na abertura e fechamento **do dia inteiro**. São rótulos **retrospectivos exclusivamente para relato**; não servem como filtros conhecidos em tempo real, nem como dados de treino ou decisão.
- Gate de pesquisa (nunca de trading): requer >=30 trades fechados, >=70% dias de teste positivos, retorno médio selecionado positivo e superior ao baseline 1m e pior drawdown <=5%. Se não atender, `REJECTED_NO_EDGE`; se insuficiente, `INSUFFICIENT_TRADES`. Mesmo `RESEARCH_REVIEW_ONLY` não autoriza promoção.
- Comparador explícito de **não operar: 0%**.
- O resultado JSON contém `mode=OFFLINE_MONTHLY_WALK_FORWARD_SHADOW_ONLY`, `paperModified=false`, `riskLimitsModified=false`, `historyPersistedInOperationalDatabase=false`, `orderPlacementEnabled=false`, `promotionAllowed=false` e a lista de 31 dias e resultados por regime de cada agente.

## Limitações

- Este é um backtest **offline e exploratório de um mês**, não validação de vários meses, regimes independentes, order book, fills de verdade ou taxas operacionais verificadas.
- Os retornos são **médias de porcentagens dos 31 holdouts** com 100 USDT fictícios reiniciados a cada dia, e não retornos acumulados de uma carteira ao longo do mês. Não extrapolar resultados para PAPER/BRL, Testnet ou mercado real.
- O baseline de 1 minuto tem churn extremo e alto custo; uma redução da perda versus baseline **não equivale a lucro**. Os preços e regimes são históricos, não previsões.
- A parte pesquisada não modifica API, Web, SQL operacional, coletores, workers, estratégias ativas, filtros de risco, credenciais ou ordens Binance. Não haverá desbloqueio de PAPER ou Testnet.

## Qualidade

- Testes automatizados validam um mês completo fictício, regime descritivo, ausência de resultado inventado sem trades e rejeição de sequência com lacunas.
- Comando integrado na ferramenta isolada criada no MVP 1.3.5. Resultados de outubro de 2026 estão em `MVP-1.3.6-VERIFICATION.md`.
- Release independente em `%LOCALAPPDATA%\RAC\staging\mvp-1.3.6\research`, sem necessidade de reiniciar o aplicativo operacional MVP 1.3.4.
