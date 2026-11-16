# RAC — Rebeca, Ana e Carlos | MVP 0.3 — Plano patrimonial

Projeto ASP.NET Core (.NET 10) com painel responsivo, **simulação local sintética** e leitura **pública** de endpoints Binance Spot Testnet. **Não envia nenhuma ordem para Binance, nem Testnet nem produção.** Não há chaves de API, conta conectada, depósito ou dinheiro movimentado.

## Executar

```bash
cd src/RAC.Api
dotnet run --urls http://localhost:5080
```

Abra `http://localhost:5080`. Alternativa: `docker build -t rac-mvp . && docker run --rm -p 5080:8080 rac-mvp`.

## Política implementada

Arquivo: `src/RAC.Api/appsettings.json` -> `Rac:Risk`.

| Parâmetro | Valor | Significado |
|---|---:|---|
| `ReferenceCapitalBrl` | 100 | Saldo inicial fictício em BRL |
| `MonthlyContributionBrl` | 200 | Aporte fictício a cada 30 dias simulados |
| `ReinvestmentPct` | 100 | Sem retiradas; ganhos e perdas permanecem no saldo |
| `MaxCumulativeDrawdownPct` | 10 | Trava e pausa para revisão quando a queda desde o pico ajustado por aportes alcança 10% |
| `MaxDailyLossPct` | 1 | Trava e pausa na perda diária de 1% |
| `MaxRiskPerTradePct` | 0.5 | Orçamento de risco planejado por entrada |
| `MaxAllocationPerPositionPct` | 20 | No máximo 20% do patrimônio por posição |
| `MaxTotalAllocationPct` | 30 | Exposição Spot total máxima de 30% |
| `MaxOpenPositions` | 1 | Uma posição simultânea |
| `MaxEntriesPerDay` | 3 | Três novas entradas por dia simulado |
| `SimulatedTicksPerDay` | 288 | Calendário **fictício**, sem relação com tempo de relógio |
| `SimulatedDaysPerMonth` | 30 | Aporte a cada 8.640 ticks fictícios |
| `IllustrativeFeePctPerSide` | 0.1 | Taxa apenas ilustrativa em cada lado |
| `AssumedBrlPerUsdt` | 5 | Câmbio inventado para contabilidade; **não é cotação** |

A queda acumulada usa um **pico do patrimônio ajustado por depósitos externos**: quando entram R$ 200 simulados, o pico de referência sobe em R$ 200, impedindo que o aporte oculte o prejuízo de trading. O painel mostra separadamente `TotalContributionsBrl` e `ProfitAfterContributionsBrl` (patrimônio menos aportes). Uma trava de 1% diária pode ocorrer antes da de 10% acumulada. O botão **Reiniciar** apaga o cenário simulado; não é uma autorização de retorno ao mercado real nem substitui revisão humana. Não há desbloqueio automático após aporte.

## Agentes

- **Ana** coordena os 12 traders e respeita o limite de uma posição.
- **Rebeca** é um *placeholder*: ainda não há feed de notícias.
- **Carlos** bloqueia entradas ao atingir perda diária ou queda acumulada, fecha posições no simulador e pausa o cenário.
- **12 traders**: BTC, ETH, SOL, BNB, XRP, ADA, DOGE, LINK, AVAX, LTC, DOT, TRX contra USDT. Preços gerados por números pseudoaleatórios; estratégia apenas ilustrativa, **não IA treinada**.

O simulador só abre compras com dinheiro fictício disponível e só vende posições compradas anteriormente. A trava de risco e o stop-loss **não garantem** que perdas reais possam ficar dentro desses valores: gaps, volatilidade, indisponibilidade de rede, slippage e custos podem ampliá-las. O simulador ainda não modela integralmente tais condições.

## API

- `GET /api/health`, `GET /api/state`
- `POST /api/control/start`, `pause`, `step`, `reset`: apenas estado fictício local
- `GET /api/binance/testnet/ping`: consulta conectividade pública
- `GET /api/binance/testnet/symbol/BTCUSDT`: filtros públicos do ativo

**Sem endpoints de envio de ordens**, sem credenciais armazenadas. API local não possui autenticação e **não deve ser exposta à internet**.

## Etapas para considerar ordens reais (NÃO implementadas)

1. Alimentação com cotações reais da Binance; manter ordens desativadas e armazenar preços com timestamps e rastreabilidade.
2. Backtest com dados históricos, custos, spread e slippage, separação de treino/teste e testes fora da amostra. Sem alterar estratégia após ver os resultados do teste final.
3. Paper trading em tempo real, relatórios transparentes de perdas, drawdown, custos e comparação com comprar-e-manter.
4. Validar ordens **Spot Testnet** via API autenticada (chaves **somente da Testnet**, armazenadas em secret manager, sem chaves reais), com filtros `LOT_SIZE`, `MARKET_LOT_SIZE`, `NOTIONAL`, `MIN_NOTIONAL`, `PRICE_FILTER` e precisão aplicáveis ao símbolo.
5. Testes de resiliência: tempo limite, retorno incerto de ordem, retry idempotente, reconciliação de posições/saldo, falha de notícia/preço, kill switch, interrupção e reinício, desconexão, auditoria imutável.
6. Revisão de segurança e autorização humana **explícita** para futura mudança de modo. Se não houver resultado consistente após taxas ou os mínimos inviabilizarem o orçamento de R$ 100, permanecer em simulação. Futura implantação real inicialmente com pequeno capital, Spot sem margem/alavancagem e API com saques desabilitados e restrição por IP.

As métricas mínimas de validação devem incluir lucro líquido **após custos**, maior drawdown, número de operações, taxa de acerto, payoff médio, duração de testes, qualidade de dados e meses negativos. Não há taxa de acerto nem lucro garantidos.

Documentação oficial: https://github.com/binance/binance-spot-api-docs/blob/master/testnet/general-info.md e https://github.com/binance/binance-spot-api-docs/blob/master/filters.md

## Estado de validação

Inspeções estáticas e formato JSON feitos. **Build e execução .NET 10 ainda não verificados neste ambiente.**
