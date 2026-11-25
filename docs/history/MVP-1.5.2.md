# RAC MVP 1.5.2 — Consistência dos relatórios SHADOW

Data: 23/11/2026.

## Implementação

- O auditor de decisões congeladas calcula o SHA-256 dos seis relatórios SHADOW antes da validação mensal.
- Confere novamente os hashes após a validação e ao concluir a leitura das seleções de treinamento, rejeitando alterações durante a análise.
- Arquivos inexistentes ou fora do limite de 20 a 2.000.000 bytes são recusados pela rotina de integridade; meses ausentes continuam sem inferência de retorno.
- Testes adicionados verificam a detecção de mudança de conteúdo e rejeição de entradas ausentes/pequenas.
- Todas as alterações são restritas à pesquisa SHADOW e aos testes. PAPER, chaves Binance, permissões de negociação e dados SQL operacionais não são alterados.

## Verificação

- `dotnet test tests/RAC.Tests/RAC.Tests.csproj --no-restore --verbosity quiet`: 206 testes aprovados, zero falhas.

## Limitações

- SHA-256 aqui garante consistência dos arquivos durante a auditoria, não prova proveniência externa ou autenticidade dos arquivos históricos.
- A regra congelada NO_TRADE permanece uma linha de base sem negociação e sem prova de rentabilidade. Não habilita Testnet ou ordens reais.
