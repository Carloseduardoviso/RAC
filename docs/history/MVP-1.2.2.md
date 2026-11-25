# RAC MVP 1.2.2 — Endurecimento da recuperação PAPER

Data: 22/11/2026.

## Entrega

A restauração dos checkpoints da simulação PAPER agora rejeita, antes de alterar o estado em memória, agentes duplicados, histórico de preços inválido, posições com identificadores ou preços inconsistentes, símbolos duplicados em posições, quantidades de posições acima da política e valores financeiros negativos ou incoerentes.

A migração compatível com checkpoints antigos que usavam limite diário de três entradas permanece permitida apenas para `MaxEntriesPerDay`. A configuração de produção segue `MaxEntriesPerDay=0` (quantidade ilimitada), preservando os controles de risco financeiro já existentes.

## Segurança operacional

- Nenhuma ordem é enviada à Binance Spot ou Spot Testnet.
- O executor Testnet continua desabilitado; PAPER permanece rodando sem reinício ou migração de banco.
- Esta entrega não altera os dados já persistidos no SQL Server.
- Checkpoints inválidos impedem uma restauração silenciosa com estado parcial.
- A mudança de código requer implantação posterior para valer na API em execução; não é feita troca de binários em uso nesta etapa.

## Validação

`dotnet test RAC.slnx -c Debug --nologo --verbosity quiet`

Foram aprovados 91 testes, incluindo três novos testes para integridade de checkpoint e preservação do estado diante de entradas inválidas.

## Próximos itens não incluídos

Ensaios de recuperação de SQL com banco descartável, observabilidade de 12 agentes e homologação externa de ordens Testnet seguem como entregas futuras. Qualquer envio de ordem Testnet requer autorização separada.
