# RAC MVP 1.3.4 — Validação histórica ampliada de 48h, modo SHADOW

Data: 22/11/2026.

## Implementação

- Endpoint **GET autenticado `/api/strategy/extended-history`**, distinto do walk-forward curto, com coleta pública Binance Spot `data-api.binance.vision` de **2.880 candles fechados de 1 minuto por um dos 12 pares**.
- Coleta limitada a **até 3 requisições públicas GET por símbolo**, com paginação retroativa por `endTime`, sem chave de API nem credenciais; rejeita páginas sem candles, lacunas ou duplicatas, e não salva no SQL. O coletor PAPER original não foi alterado.
- A execução só ocorre ao clicar em **Analisar 48 horas dos 12 agentes**; cache de 15 minutos impede bursts repetidos. O proxy Web expõe apenas a rota conhecida na allowlist, mantém autenticação e usa timeout distinto de 150 s, preservando 45 s nos outros endpoints.
- Para cada símbolo, `PaperExtendedHistoryResearch` usa **360 candles de treino** e **240 candles de teste posteriores** (10 folds em 2.880 candles), com holdouts não sobrepostos por par. Treinos podem se sobrepor; seleção por melhor retorno no treino, entre 1/5/15/30 minutos, mantendo baseline 1 minuto em empate e rejeitando candidato sem trades no treino.
- Simulação experimental existente de trades completos: compra no candle seguinte após sinal fechado, stop de 2x ATR na entrada, saída pelo horizonte ou fechamento da janela, USDT 100 fictícios por fold, custos fixos assumidos de taxa, slippage e spread.
- O relatório inclui número de dias UTC representados, quantidade de folds, operações encerradas, vitórias/perdas, stops ATR, retorno médio da baseline e horizonte escolhido, pior drawdown e gate de pesquisa.
- Condições para **apenas revisão manual de pesquisa**: pelo menos 12 trades de teste encerrados, 70% das janelas positivas, média de retorno selecionada positiva e superior à baseline, e pior drawdown <=5%. Se não atingir, o gate rejeita; dados insuficientes bloqueiam análise. O retorno hipotético da alternativa **não operar** é 0%. Mesmo que passe no gate, **nenhuma estratégia é promovida**.
- Indicadores de segurança `paperModified=false`, `historyPersisted=false`, `riskLimitsModified=false`, `orderPlacementEnabled=false`, `promotionAllowed=false`.

## Limitações

- 48 horas é apenas uma ampliação inicial, **não representa validação em diferentes regimes de mercado ao longo de meses**. A inclusão de dois ou três dias UTC de calendário não garante dois dias de negociação completos com regimes diferentes.
- Treinos se sobrepõem; dados e parâmetros de custo fixo são simplificações. Filtros reais de liquidez, tamanho de lote, latência, taxas escalonadas e spread variável ainda não são simulados.
- Retornos reiniciados a USDT 100 por fold não compõem retorno de uma carteira contínua, e a operação PAPER em BRL pode usar regras diferentes.
- Os critérios de aprovação são **somente triagem para pesquisa**, não aprovação de Testnet ou ordem real. PAPER mantém suas travas de risco.

## Qualidade e deploy

- Testes de paginação pública de 3 páginas sem credenciais, sequência cronológica e rejeição de símbolos inválidos, dados atrasados, trades inexistentes e holdouts consecutivos.
- `dotnet test RAC.slnx -c Release`, `node --check` e `git diff --check`; evidência da consulta pública isolada no documento `MVP-1.3.4-VERIFICATION.md`.
- Release compilada separadamente em `%LOCALAPPDATA%\RAC\staging\mvp-1.3.4` e implantação posterior com backup SQL e rollback para 1.3.3. Nenhum restart foi autorizado neste desenvolvimento.
