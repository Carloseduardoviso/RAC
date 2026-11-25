# RAC MVP 1.4.6 — Painel auditável de pesquisa SHADOW dos 12 agentes

Data: 23/11/2026. Implantação local no computador `080001325ADPLAN`, repositório `C:\Users\05035144208\source\repos\RAC`.

## Implementação

- Endpoint autenticado e **somente leitura** `/api/research/shadow-dashboard` na API local. Consulta exclusivamente o relatório de pesquisa previamente validado em `%LOCALAPPDATA%\RAC\research\monthly\2026-10\exit-capacity-gated-backtest.json`, **sem executar backtests automaticamente nem chamar Binance**.
- O novo `RacShadowDashboardReport.Load` projeta dados mínimos em vez de expor todo o JSON, recusando relatórios ausentes/incompletos, modos diferentes, símbolos faltantes/repetidos, contagens inconsistentes, flags de operação habilitadas ou status de pesquisa não reconhecidos. Na ausência de arquivo, retorna `available=false` e nenhuma promoção.
- A API implementa falha fechada, retornando relatório indisponível quando detecta inconsistência, nunca inventando resultados ou desativando proteções.
- A Web acrescenta a rota à allowlist fixa do proxy (GET apenas). Não há POST adicional, credenciais, mudanças no banco ou alteração de travas.
- Nova seção na interface autenticada **Auditoria SHADOW dos 12 agentes**, com resumo de dias de estudo, dias sem negociar, saídas hipotéticas sem cobertura e agentes rejeitados, além de tabela por par com motivo do gate de pesquisa, operações hipotéticas, entradas bloqueadas e parcialmente preenchidas, e média líquida fictícia por janela. Botão manual de atualizar.
- Exibe avisos explícitos: valores de pesquisa não são lucros reais; OHLCV não é liquidez executável e revisão científica não constitui autorização para negociar.
- A pesquisa SHADOW 1.4.5 é um arquivo offline **separado da carteira PAPER**. As médias de janelas de 100 USDT não são patrimônio composto ou PnL registrado no SQL.

## Segurança

- Não habilita PAPER, Binance Testnet nem ordens reais, e não reduz limites financeiros. O endpoint exige uma sessão RAC autenticada e a Web mantém proxy com allowlist fixa.
- A API/Web operacional anterior é preservada em relação às rotas de execução; apenas adiciona leitura de resultados locais. Não atualiza banco de dados nem credenciais.
- O relatório da versão inicial do painel é **fixado em outubro de 2026** para evitar seleção arbitrária de arquivos. Outros meses serão um desenvolvimento separado.

Veja `MVP-1.4.6-VERIFICATION.md` para testes, deploy e validações.
