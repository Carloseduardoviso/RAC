# RAC MVP 1.2.7 — Comparativo historico dos agentes PAPER

Data: 2026-11-22.

## Escopo

- Relatorio autenticado `/api/paper/monitoring` expõe `comparisonWindow` e `historicalComparison`, com os 12 simbolos, inclusive os que nao operaram.
- Compara janelas moveis UTC de sete dias (`[agora-14d, agora-7d)` e `[agora-7d, agora)`), evitando dupla contagem no instante limite. As datas exatas sao devolvidas ao painel.
- Para cada agente: compras, vendas, percentual de vendas com lucro estritamente positivo, resultado realizado, taxas e diferenca em BRL do resultado realizado entre as janelas.
- `realizedPnlChangePct` fica `null` quando a base anterior e zero; sem percentual artificioso de crescimento infinito.
- Agrupamento SQL por simbolo; todos os registros de fills sao preservados e a quantidade de trades por dia nao e limitada.
- Painel inclui tabela de periodos sem modificar as tabelas anteriores.

## Interpretacao e seguranca

- Relatorio reflete resultado realizado de vendas de posicoes ficticias; NAO e retorno total ajustado a aportes, nao inclui lucro nao realizado e nao preve retornos futuros.
- Nao ha alteracoes no motor PAPER, riscos financeiros, agentes, ordens, credenciais nem schema SQL.
- Binance Testnet permanece desabilitada; bloqueio de circuito e parada de emergencia permanecem acionados.
- Testes automatizados Release: 119/119, incluindo integracao SQL descartavel e fronteiras de janela/variacao; `node --check` passou.

## Ativacao

Codigo commitado e enviado a branch main. A compilacao em staging nao altera a API em execucao. Ativar exige backup SQL verificado, janela de reinicio e retomada do PAPER via sessao autenticada. Rollback: MVP 1.2.6.
