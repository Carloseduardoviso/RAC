# RAC MVP 1.0 — sinais com dados reais e backtest ilustrativo

## Escopo implementado

- Motor observacional paralelo ao simulador sintético. Analisa os 12 pares Binance Spot públicos existentes em `RacMarketSymbols.Supported`.
- Fonte exclusiva: `rac.MarketCandles` (candles fechados de 1 minuto coletados pela aplicação). **Não consulta a chave privada, não movimenta a Binance.**
- Estratégia determinística `SMA9-21_RSI14_ATR14_V1`: médias móveis simples de 9 e 21 candles, RSI 14 e ATR 14.
- Lógica observacional: `COMPRA_OBSERVACIONAL` se SMA9 > SMA21 e RSI entre 50 e 68; `SAIDA_OBSERVACIONAL` se SMA9 < SMA21 ou RSI > 74; demais casos `AGUARDAR`.
- Bloqueia sinais se há menos de 35 candles, se há lacunas na série de 1 minuto, candles ainda abertos, inconsistência de dados ou preço com mais de 3 minutos de atraso.
- Dashboard dos 12 agentes observacionais; atualização automática dos **sinais públicos** a cada 60 s, sem qualquer ordem.
- Backtest manual de um par por vez, leitura dos últimos 1000 candles disponíveis. Ordem simulada em **abertura do próximo candle**, nunca no mesmo candle do sinal.
- Backtest educativo: capital inicial de **100 USDT fictícios**, posição long-only (sem alavancagem), custo presumido de 0,1% por lado, slippage de 0,05% por lado, valor final e drawdown. Não representa lucro real, rentabilidade futura nem o resultado de uma estratégia validada fora da amostra.
- API `GET /api/strategy/overview` e `GET /api/strategy/backtest/{symbol}`, ambas autenticadas; proxy Web com allowlist do caminho. Sem rotas de ordens.

## Atenção: dois mundos separados

1. **Agentes observacionais:** calculam sinais com dados Binance reais e mostram backtest histórico ilustrativo.
2. **Motor anterior de simulação:** continua com capital de R$ 100 **fictícios**, preços gerados aleatoriamente e ordens sintéticas persistidas. **Ele ainda NÃO obedece aos sinais observacionais.**

Nunca trate a curva do simulador como lucro da Binance. Os novos sinais não são recomendação de investimento.

## Validação e execução

```powershell
cd "C:\Users\Carlos Eduardo\source\repos\RAC"
dotnet build RAC.slnx -c Release
dotnet test RAC.slnx -c Release
```

Inicie RAC.Api (5080) e RAC.Web (5081) e entre com usuário autorizado. A seção **MVP 1.0 — 12 traders observacionais com candles reais** mostra status, SMA, RSI e ATR, além do backtest por par.

## Próximas condições para automação

1. Desenvolver uma **carteira de papel** separada, alimentada por sinais reais, com reconciliação e checkpoints próprios, sem dinheiro.
2. Validar estratégias fora da amostra, com custos, filtros de spread, latência, liquidez, restrições mínimas, limites de posição, risco de mercado e falhas de rede.
3. Integrar Ana (coordenação), Rebeca (notícias verificáveis) e Carlos (risco) ao fluxo verificável de aprovação automática.
4. Binance Spot Testnet com credenciais **de teste separadas**, idempotência de ordens, reconciliação, circuit breakers e monitoramento.
5. Somente após homologação e autorização explícita considerar um modo de produção distinto, com limite de capital e desligamento de emergência.

**Não habilitar permissão de trading na chave Binance Spot de leitura. Não existem ordens reais nesta versão.**
