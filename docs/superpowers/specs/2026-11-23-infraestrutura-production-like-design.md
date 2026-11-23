# Desenho: preparação de infraestrutura production-like do RAC

## Intenção

Preparar uma base operacional documentada e reproduzível para o RAC, sem publicar o sistema, contratar infraestrutura ou habilitar ordens reais.

## Escopo aprovado

- Criar um runbook de infraestrutura para desenvolvimento controlado, Testnet e futura produção.
- Documentar separação de ambientes, configuração externa, SQL Server, HTTPS, secrets, backups, monitoramento, sincronização de horário e kill switch.
- Registrar um checklist de prontidão e um procedimento de rollback.
- Avaliar o Dockerfile atual e deixar claras as limitações para produção.

## Fora do escopo

- Escolher provedor, domínio, certificado ou serviço de secrets sem decisão do operador.
- Publicar portas na Internet.
- Alterar código de execução de ordens, banco ou autenticação.
- Criar chaves da Binance, instalar serviços externos ou executar migrations em produção.

## Decisões de segurança

- Produção continuará desabilitada por padrão.
- Segredos não serão armazenados no repositório nem em `appsettings.json`.
- SQL Server de produção deverá usar TLS validado, identidade restrita, backup e restauração testada.
- O processo deve permitir parada externa e preservar o bloqueio de risco após reinício.
- A implantação futura deverá separar Web, API, banco e observabilidade.

## Resultado esperado

Um runbook versionado que permita escolher posteriormente o provedor e preencher os valores específicos sem alterar as regras de segurança ou expor o RAC acidentalmente.
