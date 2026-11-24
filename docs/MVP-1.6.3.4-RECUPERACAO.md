# MVP 1.6.3.4 - Previa de recuperacao virtual
Calculadora pura para uma unica posicao, com cotacao recente, stop atingido e conta pausada/bloqueada.
Aplica taxas e slippage PAPER existentes, inclui taxa de entrada no resultado realizado.
EquityAfter pressupoe que esta seja a unica posicao. Nenhuma gravacao ou ordem.
Etapa seguinte: restaurar backup em banco isolado; implementar transacao com identidade da posicao, rowversion, idempotencia persistida, autorizacao humana e auditoria.
Nao conectar a calculadora a endpoint de escrita antes dos testes de concorrencia e recuperacao. Nao remover trava nem alterar referencias de risco.
