# RAC MVP 1.2.3 — Recuperação SQL e observabilidade de 12 agentes

Data: 22/11/2026.

## Recuperação isolada

`PaperSqlRecoveryIntegrationTests` cria um banco LocalDB com nome aleatório `RAC_PAPER_RECOVERY_TEST_*`, sem acessar o banco `RAC`. O teste insere conta em execução, cursor de agente, decisão e fill sintéticos; simula a inicialização de uma nova instância do serviço e confirma restauração em estado pausado, histórico preservado e ausência de ordens Testnet. Repete a observação para verificar que não duplica amostras imediatamente e tenta repetir uma decisão de candle, rejeitada pelo índice único SQL. Ao final, remove somente o banco temporário.

## Monitoramento individual

A política de observabilidade avalia os doze símbolos suportados. Enquanto PAPER estiver em execução, um agente sem cursor, não READY ou com último candle a mais de quatro minutos gera alerta `AGENT_<SYMBOL>`; o alerta geral `AGENTS_NOT_READY` é preservado. Conta pausada intencionalmente não gera alertas individuais de estagnação. Os alertas usam a infraestrutura SQL existente, sem nova migration. O relatório agora inclui os 12 símbolos mesmo que algum cursor esteja ausente, com sinalizadores `fresh` e `healthy`.

O painel exibe quantidade de agentes com candle recente e símbolos que exigem verificação. Histórico de fills e decisões não é apagado. A contagem de operações diárias permanece ilimitada (`MaxEntriesPerDay=0`); limites financeiros e bloqueio de risco permanecem obrigatórios.

## Validação

Executar no repositório: `dotnet test RAC.slnx -c Debug --nologo --verbosity quiet` e `node --check src/RAC.Web/wwwroot/app.js`.

Resultado: 93 testes aprovados. O teste de integração é específico para Windows LocalDB e retorna sem execução nos demais sistemas operacionais.

## Implantação

Código e testes são entregues na branch `main`, mas não é realizado reinício dos processos PAPER/WEB nem alteração do banco persistente nesta etapa. As mudanças de monitoramento no ambiente em execução dependem de implantação posterior controlada. Não houve solicitação nem envio de ordens reais ou Testnet.
