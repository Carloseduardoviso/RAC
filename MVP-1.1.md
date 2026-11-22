# RAC MVP 1.1 — operação automática exclusivamente simulada

## Controles fixos do motor PAPER

- Capital fictício inicial: **R$ 100**, registrado em `rac.PaperAccounts`.
- Aporte fictício de **R$ 200 a cada 30 dias corridos**; o sistema acrescenta ao saldo e às referências de risco, registra auditoria, sem tocar na Binance.
- Reinvestimento virtual: **100%** dos saldos e resultados permanecem na carteira.
- Risco máximo previsto por entrada: **0,5% do patrimônio fictício**, considerado stop de 2 ATR e provisões para taxas/slippage.
- Limite de perda diária: **1%**, calculado sobre patrimônio virtual do início do dia UTC.
- Queda acumulada máxima: **10%**, em relação ao pico de patrimônio virtual ajustado por aportes.
- Alocação por posição: no máximo **20%**; exposição total: no máximo **30%**; **1 posição simultânea** e até **3 entradas virtuais por dia**.
- Taxa fictícia: 0,1% por lado; slippage simulado: 0,05% por lado.
- Posições long-only Spot fictícias, sem alavancagem. Cotações são públicas Binance USDT e USDTBRL, nunca provenientes de APIs de negociação.
- A perda real poderia exceder o orçamento por lacunas, slippage ou volatilidade; limites de papel são controles estimados e não garantias.

## Arquitetura e segurança

1. `RacPaperTradingService` usa um singleton e trava de serialização para gerar no máximo um processamento de cada candle por par.
2. Dados de entrada são `rac.MarketCandles` fechados e `BinancePortfolioPricesClient` público, com conversão BRL via par público USDTBRL.
3. Entradas de papel são sinalizadas pela estratégia SMA/RSI/ATR do MVP 1.0 e limitadas pelo `PaperRisk`.
4. Saídas de papel seguem stop de 2 ATR na entrada ou sinal de saída (sem ordens reais).
5. A execução é no preço público observado na requisição, com ajustes **hipotéticos** de slippage; não é backtest real nem cotação de ordem garantida.
6. `rac.PaperAccounts` mantém saldo, pico, dia, travas, capital total e estado. `rac.PaperAgentCursors` evita reprocessar candles. `rac.PaperPositions` e `rac.PaperFills` persistem posições e operações fictícias.
7. A persistência de ciclo/fills/cursors ocorre no mesmo `SaveChangesAsync` e o checkpoint usa `rowversion` SQL.
8. Erros de mercado, câmbio, ausência de dados de posição, volatilidade atípica ou falhas de persistência interrompem o worker e tentam registrar **bloqueio de risco**. O motor não tenta retomar automaticamente.
9. Ao iniciar o servidor, a conta é forçada a **PAUSADA**. O operador precisa clicar em **Iniciar simulação automática** após login.
10. Após bloqueio de risco, não existe desbloqueio remoto genérico ou reset silencioso. Exige uma futura rotina de revisão auditável.
11. Riscos de execução dupla por **múltiplas instâncias** não foram homologados. Execute somente **uma instância** do `RAC.Api`; validação de liderança distribuída e teste de falhas concorrentes são requisitos futuros.

## APIs e painel

- `GET /api/paper/status`: snapshot autenticado da conta fictícia.
- `POST /api/paper/start` e `POST /api/paper/pause`: somente administradores ou operadores, sessão autenticada e cabeçalho CSRF.
- `http://localhost:5081`: seção **MVP 1.1 — Carteira automática fictícia com dados reais**.
- As chaves Binance anteriores seguem **somente leitura**, e o motor de papel nem sequer usa essas credenciais.
- `GET /api/health` mantém `realOrdersEnabled=false` e `testnetOrderEnabled=false`.
- A simulação sintética antiga é independente e não representa o lucro da carteira de papel, tampouco o patrimônio Binance Spot real.

## Inicialização segura

```powershell
cd "C:\Users\Carlos Eduardo\source\repos\RAC"
dotnet build RAC.slnx -c Release
dotnet test RAC.slnx -c Release
dotnet ef database update --project src/RAC.Infrastructure --startup-project src/RAC.Infrastructure --context RacDbContext
```

Inicie RAC.Api e RAC.Web com uma instância de cada. A conta começa em PAUSADO; o operador escolhe se liga ou pausa o motor virtual.

## Lacunas antes da Testnet e da produção

A engine usa preços de mercado em momentos diferentes (candles fechados e ticker atual) e uma carteira virtual interna; não representa execução real. Ainda é preciso testar concorrência, quedas de rede, volatilidade extrema, filtros de quantidade mínima, reconciliação de taxas e ordens da Testnet, alertas e monitoramento. Nenhuma chave com trading foi criada ou habilitada.

**Nenhum código desta versão executa ordens reais, saques ou transferências.**
