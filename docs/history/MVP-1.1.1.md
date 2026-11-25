# RAC MVP 1.1.1 — Monitoramento e auditoria da carteira PAPER ONLY

## Escopo implementado

- Observador independente `RacPaperMonitoringWorker`, com consultas ao SQL Server a cada 60 segundos. Ele continua executando mesmo se o worker de trading fictício for interrompido por falha.
- Registro periódico de patrimônio virtual, caixa, exposição, drawdown, quantidade de agentes prontos/bloqueados, posições e estado do motor.
- Registro de incidentes: bloqueio de risco de Carlos, ciclo do motor sem atualização, candles públicos desatualizados, agentes sem sinal, saldo inválido, perda diária >= 1%, drawdown >= 10%. Incidentes são deduplicados enquanto ativos, e são marcados como resolvidos quando a condição deixa de existir.
- Auditoria da avaliação de sinais por par e candle fechado: ator `Ana/Carlos`, sinal, resultado, justificativa e `PlacedRealOrder=false`. Para compras e vendas fictícias, o histórico `PaperFills` mantém os motivos, quantidades, preços, taxas e P&L realizado.
- **Rebeca ainda não possui integração de notícias**: a auditoria informa explicitamente essa limitação e não inventa recomendações.
- `GET /api/paper/monitoring` autenticado e proxied pela allowlist do painel; relatório com amostras recentes, decisões recentes, alertas, taxas, resultado realizado e patrimônio virtual líquido de aportes.
- Nova seção **MVP 1.1.1 — Monitoramento e auditoria PAPER ONLY** no painel Web, com atualização automática a cada 30 s.
- Retenção automática de até 14 dias de *amostras periódicas*. Eventos de auditoria, alertas e fills não são excluídos automaticamente.
- Sem envio de e-mail/push externo. Alertas ficam no painel e no SQL, não são garantia de entrega ou reação humana.

## Recuperação e travas

O watchdog não desbloqueia o motor. Um ciclo atrasado gera alerta somente enquanto o motor está em execução; estado pausado após reinício não é considerado falha. Alertas antigos são resolvidos automaticamente ao recuperar a saúde, mas o bloqueio de risco da conta permanece intacto. O motor de papel continua **iniciando pausado**, sem recuperação automática de ordens após falhas. Ele segue sujeito aos limites de R$ 100 iniciais, R$ 200 fictícios por 30 dias, risco 0,5% por operação, perda diária 1%, drawdown 10% e reinvestimento virtual 100%.

## Banco SQL Server

As novas tabelas em schema `rac` são:

- `PaperMonitorSamples`: amostras de saúde e desempenho.
- `PaperMonitorAlerts`: incidentes ativos e resolvidos.
- `PaperDecisionAudits`: uma decisão por par/candle, chave única, preservada após reinício.

A migration adiciona apenas essas três tabelas; não apaga nem altera tabelas de carteira real ou a estrutura das operações fictícias anteriores. Faça backup antes de aplicar.

## Testes e limites

```powershell
cd "C:\Users\Carlos Eduardo\source\repos\RAC"
dotnet build RAC.slnx -c Release
dotnet test RAC.slnx -c Release
dotnet ef migrations has-pending-model-changes --project src/RAC.Infrastructure --startup-project src/RAC.Infrastructure
dotnet ef database update --project src/RAC.Infrastructure --startup-project src/RAC.Infrastructure
```

A suíte automatizada verifica regras de monitoramento e recuperação lógica. Testes de caos com múltiplos processos, falha real de SQL e restauração de backup em ambiente separado ainda são tarefas para homologação, e não devem ser simulados destruindo o banco atual.

**Não há integração de trading real ou Testnet para ordens neste MVP. Chaves Binance continuam somente leitura.**
