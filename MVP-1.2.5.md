# RAC MVP 1.2.5 — Diagnostico detalhado por agente PAPER

Data: 2026-11-22.

## Escopo entregue

- Novo `PaperAgentDiagnostics` (puro, somente leitura): classifica cada um dos doze agentes com `READY`, `STALE`, `MISSING`, `BLOCKED` e `CLOCK_SKEW`; calcula a idade do candle publico de 1 minuto e fornece um motivo diagnostico.
- O relatorio autenticado existente (`GET /api/paper/monitoring`) inclui `diagnosticStatus`, `candleAgeSeconds` e `diagnosticReason` em `agentStates` e `agentPerformance`; preserva as propriedades anteriores.
- O painel agrega colunas de diagnostico/idade do candle e instante da ultima decisao por agente. Um agente sem compra ou sem decisao recente nao e automaticamente classificado como falha, pois `AGUARDAR` e bloqueios de risco sao resultados normais.
- Nenhuma mudanca em schemas, migracoes SQL, motor de execucao, estrategias financeiras, credenciais ou envio de ordens.

## Testes e limites

- Os 12 agentes sempre permanecem representados no relatorio, mesmo sem posicoes.
- Testes de fronteira: candle com 240 segundos e aceito; com 241 segundos fica `STALE`; timestamp futuro e `CLOCK_SKEW`.
- A integracao SQL descartavel verifica os campos adicionados sem alterar o banco persistente.
- `dotnet test RAC.slnx -c Release`: 102 testes aprovados; `node --check src/RAC.Web/wwwroot/app.js`: aprovado.
- A quantidade de entradas diarias continua sem limite numerico. Limites financeiros, locks e parada de emergencia Binance sao preservados.

## Implantacao

O commit na branch `main` nao atualiza automaticamente a API/painel atuais. Para ativar o novo diagnostico visual e necessario deploy com backup, restart controlado e retomada autenticada de PAPER. A simulacao atualmente ativa nao deve ser interrompida pela etapa de desenvolvimento.
