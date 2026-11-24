# MVP 1.6.3.5 - Restore isolado
Backup MVP 1.6.3.3: SHA256 conferido, RESTORE DATABASE com MOVE para arquivos exclusivos, CHECKSUM e DBCC CHECKDB aprovados.
Estado restaurado: PAPER pausado/bloqueado, 17 fills, uma posicao, zero ordens Testnet.
Banco temporario removido; banco RAC operacional nao alterado.
Recuperacao transacional de escrita ainda pendente. Exigir autorizacao autenticada, snapshot consistente, rowversion, identidade da posicao, chave idempotente persistida e auditoria atomica.
