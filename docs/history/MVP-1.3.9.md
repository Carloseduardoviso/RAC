# RAC MVP 1.3.9 — Consolidação auditável de filtros NO_TRADE em três meses

Data: 22/11/2026.

## O que foi implementado

- Um agregador puro `EntryGateCrossMonthSummary` em RAC.Application e comando de pesquisa **manual** `dotnet run --project src/RAC.HistoryResearch -c Release -- --entry-gates-summary 2026-08 2026-09 2026-10`.
- Lê unicamente os relatórios SHADOW `entry-quality-no-trade.json` previamente gerados em três meses UTC consecutivos concluídos, sem acesso à API do RAC, SQL, chaves de Binance, PAPER ou executores de ordens.
- Verifica identificação do modo de pesquisa, mês, exatamente 12 agentes por mês sem repetição, flags de segurança, cobertura de dias do mês, contagem e unicidade das datas, totais de operações e resultados comparados a cada janela. Exige que um dia `NO_TRADE` tenha zero operações, zero retorno e zero drawdown. Calcula SHA-256 dos três relatórios de entrada e registra os hashes no arquivo consolidado.
- Consolida 36 relatórios símbolo-mês, ponderando retornos médios pela quantidade de dias em cada mês; portanto estes valores não são rentabilidade composta de uma conta.
- Gate exclusivamente de **revisão de pesquisa**, que exige que TODOS os três meses tenham médias líquidas positivas, >=30 dias efetivamente ativos, >=90 trades simulados, >=70% de todos os dias símbolo positivos e média global líquida positiva superior à referência anterior. **Mesmo se esse gate passar, `promotionAllowed=false` e `orderPlacementEnabled=false` sempre**.
- Produz `%LOCALAPPDATA%\RAC\research\comparisons\entry-quality-three-month-YYYY-MM-to-YYYY-MM.json`, com rastreabilidade dos três relatórios via SHA-256.

## Segurança e limites

- Não altera a aplicação operacional, API, Web, banco RAC, alertas, carteira PAPER, travas de perda, Testnet e ordens privadas Binance.
- O sistema de pesquisa é deliberadamente **fail-closed** se faltarem meses, agentes, relatórios ou surgirem contagens incoerentes. Não usa rótulos retrospectivos de regime para operar.
- O resultado deve ser tratado como avaliação retrospectiva de baixa frequência, não recomendação de investimento nem previsão de retornos. O comparador de não operar permanece 0%.
- Ainda dependemos de uma estratégia com rentabilidade positiva genuína em dados fora da amostra, auditoria de custos variáveis e testes de segurança por separado antes de discutir ativação operacional.

## Validação

- Foram acrescentados 3 testes automatizados para consistência dos meses, modo de recusa de relatórios incompletos, resultados NO_TRADE iguais a zero e ausência permanente de promoção.
- Resultado executado dos 3 meses e indicadores de integridade disponíveis no `MVP-1.3.9-VERIFICATION.md`.
