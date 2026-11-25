# RAC MVP 1.5.11 — API e dashboard para propostas de risco

Data: 23/11/2026. Host: `080001325ADPLAN`.

## Entregas

- Rotas autenticadas: `GET /api/risk/policies`, `POST /api/risk/policies` e `POST /api/risk/policies/{id}/review`.
- Consulta de propostas versionadas e histórico de auditoria em `rac.ProductionRiskPolicies` e `rac.ProductionRiskPolicyAudits`. Resposta não habilita ordens.
- Proposta permitida aos papéis `Administrator` e `RiskReviewer`, por política `RacRiskReviewer`. A revisão exige `Administrator` e administrador diferente do proponente; o papel provém da sessão autenticada, não do corpo da requisição.
- Revisão exige justificativa de 20 a 500 caracteres; tentativa de revisão duplicada, revisão da própria proposta e falha de integridade são recusadas. O estado favorável permanece `REVIEWED_NO_TRADING_PERMISSION`.
- Painel `RAC MVP 1.5.11` exibe formulário de proposta, formulário restrito de revisão, histórico versionado e eventos de auditoria.
- Proxy Web com allowlist explícita para as novas rotas, `POST` protegido por cabeçalho CSRF e cookies com sessão.
- Reforço de `ProductionRiskLimits`: pares do universo suportado, capital máximo configurável de 1 milhão de USDT, teto de 100 posições, 10 mil ordens/hora e 100 mil ordens/dia. São limites máximos de configuração de políticas documentais, **não** autorização operacional ou parâmetros recomendados de trading.
- Comparação de nomes do proponente/revisor ignora espaços periféricos e diferenças de maiúsculas/minúsculas.

## Verificações

- Suíte .NET Release: **247 testes aprovados**, zero falhas.
- Integração SQL com banco LocalDB descartável: criação de proposta, auditoria, revisão por outra pessoa, bloqueio de duplicidade e nenhuma permissão de trading.
- Build completo Release: zero erros e avisos; verificação JavaScript `node --check` aprovada.
- Builds publicados em `%LOCALAPPDATA%\RAC\staging\mvp-1.5.11\api` e `...\web`.
- Banco operacional verificado: **0 propostas, 0 eventos de auditoria**, PAPER `Running=1`, Testnet `Enabled=0`, zero ordens Testnet.
- API em produção local nas portas 5080/5081 **não foi reiniciada**: rota da versão nova ainda responde 404 na instância anterior. Necessária implantação controlada separada.

## Segurança e limitações

O fluxo documental **não ativa Binance real nem Testnet**. O bloqueio `ProductionRiskGate.RealTradingImplementedAndAuthorized == false` permanece. Nenhuma ordem foi enviada, nenhum limite real foi aprovado automaticamente e nenhum dado PAPER foi alterado. A versão staged não deve ser executada como segunda instância da API contra o mesmo banco. Falta teste autenticado da interface em navegador após o deploy.
