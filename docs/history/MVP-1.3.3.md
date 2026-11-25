# RAC MVP 1.3.3 — Operações completas, horizontes e risco (SHADOW)

Data: 22/11/2026.

## Implementação

- Novo `PaperTradeHorizonResearch` no projeto RAC.Application, executado somente na consulta pública autenticada de `GET /api/strategy/walk-forward`. Usa os mesmos candles 1m fechados do MVP 1.3.0, sem chamadas adicionais à Binance e sem persistência.
- Quatro horizontes predefinidos: **1, 5, 15 e 30 minutos**. Cada candidato simula operações completas com sinal no fechamento, compra na abertura do candle seguinte, stop fixado no preço de entrada menos **2x ATR14**, e saída por stop, conclusão do horizonte ou liquidação final da janela.
- Compra emprega 100 USDT inteiramente fictícios, taxa fixa por lado de 0,1%, slippage por lado de 0,05%, meio spread por lado de 0,01%; saída desconta custos. Gaps de abertura abaixo do stop recebem preenchimento no pior preço entre abertura e stop. Stops e alvos de tempo não dependem de preços futuros antes de uma ordem hipotética ser posicionada.
- Simulação reinicia com 100 USDT a cada janela; registra operações encerradas, vitórias/derrotas, saídas por stop, saídas por horizonte, liquidação de fim da janela, duração média, retorno em porcentagem e drawdown máximo por janela.
- Pesquisa walk-forward: **180 candles de treino** e **120 candles de teste posteriores**, com testes cronológicos não sobrepostos. Horizonte é escolhido apenas pelo maior retorno do treino; empate conserva horizonte de 1 minuto, candidato sem trades no treino não é escolhido. A seleção de 1 minuto é baseline.
- Um módulo distinto `tradeHorizonResearch` é incorporado ao JSON de saída, com flags imutáveis `paperModified=false`, `orderPlacementEnabled=false`, `promotionAllowed=false`, `riskLimitsModified=false`, `historyPersisted=false`. O painel ganha tabela por agente em modo SHADOW.

## Limitações e proteção

- Simulação é completamente independente do PAPER: dimensionamento, custos e stops podem ser diferentes do ambiente real, e reiniciar capital a cada janela não replica uma conta operando continuamente.
- Treinos walk-forward **se sobrepõem**, mas os testes dentro de cada símbolo não. Quatro horizontes são avaliados no treino, então há risco de sobreajuste apesar da separação cronológica.
- Histórico curto (até ~17 horas de 1m), taxas fixas, ausência de profundidade, mínimo de lote, latency, slippage real, spreads dinâmicos e eventos de mercado; os resultados não demonstram retorno financeiro futuro.
- Na entrada e na saída pela abertura/candle usam-se apenas informações disponíveis no momento apropriado. O stop de entrada considera o próximo candle depois da ordem de compra hipotética. A ordem dos eventos assume preço de stop conservador, mas não é um livro de ofertas.
- Folds sem trades não são tratados como sinal de lucro; a rentabilidade por fold pode ficar em zero se nenhuma operação ocorrer, caso que precisa ser interpretado junto à contagem.
- **Nenhum desbloqueio PAPER ou Testnet, nenhuma promoção automática, nenhuma ordem privada Binance.** Risco de perda financeira do PAPER permanece preservado.

## Validação

- `dotnet test RAC.slnx -c Release`: 145/145 aprovações.
- Testes dedicados: horizontes 1/5/15/30, contabilidade de fechamentos, igualdade wins+losses, consistência de motivos, limite de duração, determinismo, dados antigos/insuficientes e falha conservadora.
- `node --check src/RAC.Web/wwwroot/app.js`, `git diff --check`.
- Verificação isolada com 12 pares públicos reais: `MVP-1.3.3-VERIFICATION.md`.
- Release preparada em `%LOCALAPPDATA%\RAC\staging\mvp-1.3.3`; implantação separada exige backup SQL validado e rollback para MVP 1.3.2.
