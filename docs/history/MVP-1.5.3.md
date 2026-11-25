# RAC MVP 1.5.3 — Auditoria de proveniência local SHADOW

Data: 23/11/2026.

## Implementação

- Comando manual e somente leitura: `dotnet run --project src/RAC.HistoryResearch -c Release -- --archive-provenance`.
- Valida seis manifestos de maio a outubro de 2026, 12 símbolos por mês, origem declarada, modo de pesquisa e flags de segurança.
- Verifica nome, tamanho e SHA-256 de todos os 72 arquivos ZIP de candles históricos contra os hashes nos respectivos manifestos locais.
- Falha fechada em arquivos ausentes, alterados, referências inválidas ou manifestos incompletos. Não consulta credenciais nem banco operacional, não executa ordens nem reescreve arquivos.

## Verificação local

- Auditoria executada neste computador: 72/72 arquivos com hashes iguais aos manifestos, seis meses completos, código de saída 0.
- Compilação Release de `RAC.HistoryResearch`: zero avisos e zero erros.

## Limitações

- Coincidência com SHA-256 registrado no manifesto local não comprova assinatura independente ou autenticidade externa dos arquivos da Binance. A origem é declarada, não autenticada criptograficamente.
- O comando não prova resultados futuros de negociação nem habilita estratégias, PAPER, Testnet ou operações reais.
