# RAC MVP 1.5.10 — Persistência e auditoria das propostas de risco

Data: 23/11/2026. Host: `080001325ADPLAN`.

## Escopo concluído

- Entidades `ProductionRiskPolicyRecord` e `ProductionRiskPolicyAudit`, isoladas de ordens, executores Binance e contas PAPER.
- `ProductionRiskPolicyStore` fornece proposta versionada de política, serialização estável dos limites, SHA-256 para detectar alteração e revisão separada (`ADMIN` diferente do proponente), com justificativa de 20 a 500 caracteres.
- Proposta e auditoria inicial são gravadas em transação serializável; revisão e auditoria da revisão também são gravadas na mesma transação. Revisões duplicadas e alteração do conteúdo registrado são rejeitadas.
- Revisão positiva grava somente `REVIEWED_NO_TRADING_PERMISSION`. O bloqueio estrutural `ProductionRiskGate.RealTradingImplementedAndAuthorized == false` continua obrigatório: revisão documental **não autoriza ordens**.
- Configuração financeira real inicial não foi aprovada, não foram inseridas propostas nem aprovações no banco.
- Migração EF Core `20261123150103_ProductionRiskPolicyLedgerMvp1510` cria exclusivamente `rac.ProductionRiskPolicies` e `rac.ProductionRiskPolicyAudits`, com índices de versão e histórico de auditoria.
- Nenhuma rota de API POST ou executor de trading foi implementado nesta etapa. A vinculação de papel `ADMIN` deverá ser feita em uma futura API autenticada; o parâmetro interno `role` não deve ser exposto diretamente ao usuário.

## Validação e aplicação no SQL Server

- Testes .NET Release: 240/240 aprovados, zero falhas.
- Build completo Release: 0 erros, 0 avisos.
- Backup SQL `RAC-pre-MVP1510-20261123.bak`, gerado com CHECKSUM e verificado via `RESTORE VERIFYONLY` antes da alteração do esquema.
- Migração aplicada com `dotnet ef database update 20261123150103_ProductionRiskPolicyLedgerMvp1510` ao SQL LocalDB `RAC`; tabela de migrations confirmou a nova migração.
- Verificado: 0 registros em ambas as tabelas novas; nenhuma aprovação automática.
- Após a migração: PAPER `Running=1`, `RiskLocked=0`; uma posição e histórico PAPER preservados; Testnet `Enabled=0`, 0 ordens Testnet. `/api/health` confirma `realOrdersEnabled=false`, `testnetOrderEnabled=false`.

## Limitações

- O armazenamento de propostas e a revisão estão implementados no código e esquema do SQL, **mas não estão registrados como serviço da API ativa**. Nenhum fluxo de aprovação operacional foi ativado.
- Não há mecanismo de recuperação automática após perda, reconciliação em produção, kill switch real, nem garantia de append-only no banco para operadores com privilégios administrativos. Todos serão requisitos para uma futura integração, que demandará autorização separada.
- A API e a Web em execução não foram reiniciadas. Nenhuma ordem foi enviada à Binance; o PAPER não foi pausado.
- Pasta local `docs/` não rastreada pelo Git foi preservada.
