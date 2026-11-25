# RAC MVP 1.2.8 — Diagnostico de risco somente leitura

Data de desenvolvimento: 2026-11-22.

## Achados operacionais no banco RAC

No momento da investigacao: `PaperAccounts.Running=0`, `RiskLocked=1`, patrimonio R$98,997516258031 vs. referencia diaria R$100,00 (perda virtual de 1,002483741969%). O bloqueio diario de 1% foi acionado corretamente. Nao desbloquear automaticamente.

- Historico: 33 fills virtuais, dos quais 16 vendas fechadas e uma posicao BNBUSDT ainda aberta.
- Saidas por ATR_STOP: 11 vendas, resultado realizado R$ -0,834505022062.
- Saidas por SIGNAL_EXIT: 5 vendas, resultado realizado R$ -0,138275502952.
- Resultado total realizado: R$ -0,972780525014; taxas totais de todos os fills: R$ 0,656279005465.
- **Nao subtrair as taxas novamente do resultado realizado**: o calculo de PnL das vendas ja desconta taxas de entrada e saida. Patrimonio liquido tambem inclui a posicao aberta, cotada na ultima avaliacao.
- Os dados nao sao evidencia suficiente para concluir que os sinais ou stops estejam errados; custos, regime de mercado, liquidez e reentradas devem ser investigados com mais amostras e validacao fora da amostra.

## Evolucao implementada

- `GET /api/strategy/validation` agora inclui `riskReview` com estado e motivo do bloqueio, patrimonio virtual, referencia diaria, percentual de perda e limite configurado, sem modificar protecoes.
- Resume numero de vendas encerradas e perdas, motivos de saida agrupados por SQL, exposicao de posicoes abertas e resultado nao realizado **antes da taxa de saida**.
- O painel mostra o diagnostico em tabelas ao executar "Validar 12 agentes", alem do backtest ja existente.
- `PaperRiskDiagnostics` calcula percentuais de forma pura; casos sem base valida retornam `null` em vez de inventar 0%.
- 125 testes automatizados Release aprovados, incluindo verificacoes de estado e ausencias no SQL descartavel; sintaxe JS aprovada.

## Seguranca e proximo passo

- Nao alterou PAPER, quantidades diarias de negociacoes, stops, limites financeiros, credenciais, rede privada ou executor Binance.
- Testnet e ordens reais continuam bloqueadas.
- Nova API/Web deve ser implantada com backup SQL e rollback; manter `Running=0`, `RiskLocked=1` ate revisao e decisao humana autorizada. Revisar posicao aberta e perdas antes de qualquer retomada.
