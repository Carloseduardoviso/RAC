# MVP 1.6.3.2 - Revisao de incidente PAPER
Data: 2026-11-24. Evidencias de leitura, sem autorizacao de reativacao.
Incidente: PAPER_RISK_LOCK em 2026-11-23 16:14:13 UTC. Causa especifica indeterminada.
Conta pausada e bloqueada. 17 fills, uma posicao DOTUSDT de 4.717290039479 DOT, zero ordens Testnet.
Saldo registrado 79.687463510084 BRL, patrimonio registrado 99.556026848848 BRL.
Quantidades liquidas reconciliadas; patrimonio registrado consistente com caixa e marcacao persistida.
API e Web saudaveis; nove testes de recuperacao aprovados na etapa anterior.
Em 2026-11-24 15:42:12 UTC, 12 cotacoes persistidas atualizadas; DOT 0.650 USDT. Consulta publica independente DOT 0.649 USDT.
Cotacao USDT nao equivale a marcacao BRL; patrimonio PAPER permanece desatualizado.
## Checklist antes de qualquer reativacao
- [ ] Revisao humana do incidente e aceitacao documentada da causa indeterminada.
- [ ] Backup verificavel e reconciliacao consistente em snapshot transacional.
- [ ] Validar candles fechados, conversao USDT/BRL, marcacao e stop da posicao sem alterar o ledger.
- [ ] Validar limites de risco com patrimonio atualizado, sem redefinir perdas ou pico.
- [ ] Revisar procedimento de recuperacao autenticado e auditado, com controle de concorrencia.
- [ ] Autorizacao humana separada para PAPER; Testnet e producao continuam desabilitadas.
- [ ] Confirmar rotacao das credenciais Testnet antes de homologacao de ordens.
Nenhum item deste documento libera a trava. Nao executar UPDATE manual para contornar a protecao.
