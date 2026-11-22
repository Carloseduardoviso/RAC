# RAC MVP 1.2.4 — Desempenho individual dos 12 agentes PAPER

Data: 22/11/2026.

## Entrega

O endpoint autenticado `GET /api/paper/monitoring` acrescenta a coleção `agentPerformance` para os 12 símbolos suportados, inclusive os que ainda não possuem nenhuma operação. Para cada símbolo informa situação/sinal, contagens acumuladas de decisões, compras, vendas, vendas com resultado não negativo, vendas com prejuízo, PnL realizado, taxas fictícias e valor de posições abertas. As métricas são agregadas no SQL Server, sem trazer todo o histórico de fills para a memória e sem modificar dados.

O painel apresenta uma tabela com esses resultados. Vitórias/perdas são calculadas apenas sobre fills de venda com PnL registrado; resultados não realizados são mostrados separadamente como valor de exposição, nunca como lucro. A tabela não constitui promessa de retorno.

## Diagnóstico observado durante desenvolvimento

A carteira PAPER voltou a executar ciclos no SQL após a ativação do MVP 1.2.3. Em um momento foram registrados alertas individuais por candle obsoleto; a política de alertas do MVP 1.2.3 continua ativa e não foi afrouxada. O relatório diferencia estado READY de atualização efetivamente recente.

## Segurança e testes

Nenhum endpoint de ordens é alterado. Sem uso de credenciais ou envio Binance real/Testnet, sem alterações de schema, sem limitação diária de quantidade de operações e sem modificação dos limites financeiros. O teste de recuperação SQL descartável agora verifica também a coleção de desempenho e totais por agente.

Validação local: `dotnet test RAC.slnx -c Debug --nologo --verbosity quiet` e `node --check src/RAC.Web/wwwroot/app.js`.

## Implantação

O código pode ser publicado na `main` sem reiniciar a API em execução. O painel de desempenho novo só aparecerá depois de uma implantação controlada da próxima release; esta entrega não pausa nem reinicia o PAPER.
