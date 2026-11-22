# RAC MVP 1.2.6 — Analise de desempenho PAPER

Data: 2026-11-22.

## Metricas (somente leitura)

- Exposicao virtual total em BRL e porcentagem do patrimonio marcado a mercado (equity). Exposicao individual por simbolo no quadro dos 12 agentes.
- Taxa de vendas lucrativas = quantidade de vendas com resultado realizado estritamente positivo / todas as vendas. Vendas empatadas ou com resultado nulo nao sao tratadas como lucrativas; zeros ficam registrados separadamente.
- Numeros de vendas lucrativas, com prejuizo e com resultado zero sao exibidos separadamente. Com zero vendas, o painel apresenta `Sem vendas fechadas` em vez de uma taxa enganosa.
- A curva de patrimonio mostra ate 120 amostras historicas de ate 14 dias, com minimo, maximo e ultimo valor nominal virtual. Aportes alteram a curva; ela nao representa retorno ajustado a aportes nem expectativa de lucro futuro.
- O resultado liquido virtual continua sendo patrimonio menos aportes, e o resultado realizado continua associado as vendas. Nao confundir exposicao com lucro.
- `PaperPerformanceMath` centraliza os calculos, inclusive casos sem vendas, patrimonio zero e exposicao maior que 100% para detectar inconsistencias sem esconder valores.

## Garantias

- Somente GET do monitoramento e exibicao; nenhuma mudanca no algoritmo de execucao, risco, contribuicoes, limite numerico diario, ordens ou persistencia de contas.
- Agregacoes SQL por simbolo evitam carregar todo o ledger para gerar o relatorio.
- Testes de matematica e de recuperacao em SQL descartavel incluem as novas propriedades. Testes Release: 113 aprovados; verificacao de sintaxe do JavaScript aprovada.
- Binance Testnet continua com habilitacao de ordens desligada e travas intactas. Nenhuma ordem real ou Testnet e enviada.

## Implantacao

O commit na main e a compilacao em staging nao reiniciam o PAPER. A ativacao em API/Web exige backup SQL verificado, rollback para MVP 1.2.5 e retomada autenticada da simulacao apos restart.
