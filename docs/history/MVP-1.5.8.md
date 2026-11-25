# RAC MVP 1.5.8 — Painel de robustez SHADOW

Data: 23/11/2026.

## Implementação

- Nova rota autenticada de somente leitura `GET /api/research/robustness`, com projeção em `RacRobustnessDashboardReport`.
- A API limita tamanho de arquivo, modo, meses, universo de 12 agentes e flags de segurança, recalculando a robustez a partir de 36 resultados de replay e decisões congeladas validadas. Não confia em números de robustez serializados no JSON.
- Proxy Web admite somente GET da nova rota. Não foram adicionadas rotas POST.
- Nova seção `robustnessPanel` exibe 12 agentes, abstenções, alertas, meses positivos, trades hipotéticos, custo 1x/2x, penalidade e drawdown.
- NO_TRADE não é exibido como rentabilidade de estratégia ativa; valores sem evidência aparecem como não avaliados. A interface é limpa ao sair do sistema.
- Dados obtidos de `%LOCALAPPDATA%\RAC\research\monthly\fixed-replay-2026-08-to-2026-10.json`, sem uso de API Binance autenticada ou banco operacional.

## Validação

- `dotnet test tests/RAC.Tests/RAC.Tests.csproj -c Release --no-restore --verbosity quiet`: **221 testes aprovados**, zero falhas.
- `dotnet build RAC.slnx -c Release --no-restore --verbosity quiet`: zero avisos e erros.
- `node --check src/RAC.Web/wwwroot/app.js`: aprovado.
- Publicações Release independentes geradas em `%LOCALAPPDATA%\RAC\staging\mvp-1.5.8\api` e `...\web`; arquivos verificados.

## Implantação e limitações

- A API e a Web em execução nas portas 5080/5081 não foram substituídas nem reiniciadas; a rota nova ainda responde 404 na instância anterior.
- A ativação da versão staged exige uma janela segura com confirmação do estado PAPER/Testnet e troca controlada de processos; **não foi realizada** neste passo.
- Não houve modificação de PAPER, ordens, risco, credenciais, SQL operacional ou quaisquer arquivos do histórico de origem.
- Três meses não comprovam eficácia estatística, e as 12 escolhas congeladas atuais permanecem NO_TRADE.
