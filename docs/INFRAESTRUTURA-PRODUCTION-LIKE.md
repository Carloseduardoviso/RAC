# Infraestrutura production-like do RAC

Este runbook orienta o desenvolvimento local e a validação em Testnet. Produção continua desabilitada e ordens reais permanecem bloqueadas em todos os ambientes desta fase. O operador deverá registrar responsáveis, evidências e decisões de infraestrutura antes de qualquer proposta de implantação futura.

## Matriz de ambientes

| Controle | Local | Testnet | Produção futura |
| --- | --- | --- | --- |
| Finalidade | Desenvolver e simular com dados sintéticos | Ensaiar simulação, leitura e recuperação em ambiente de testes | Ambiente ainda desabilitado, sujeito a aprovação formal |
| Acesso | Web e API limitados ao loopback. Veja os perfis em [COMO-EXECUTAR.md](../COMO-EXECUTAR.md) | Web e API em rede restrita, com acesso autenticado e HTTPS validado | HTTPS validado no ponto de entrada da Web. API, SQL Server e observabilidade em rede restrita |
| Banco | SQL Server LocalDB dedicado ao desenvolvimento | SQL Server dedicado à Testnet, com identidade própria, TLS validado e restauração ensaiada | SQL Server dedicado, com identidade restrita, TLS validado, backup e restauração testada |
| Integração de ordens | Simulação por padrão. Ordens de Testnet desativadas | Envio de ordens bloqueado no checkout atual (`SubmissionExplicitlyAllowed=false`) | Ordens reais bloqueadas por padrão, inclusive após reinício ou rollback |

Mantenha os serviços locais e de Testnet fora da Internet. Separe bancos, identidades, credenciais e dados entre os três ambientes. Hoje os ensaios disponíveis são de simulação e leitura. O envio de ordens na Testnet depende de uma implementação futura revisada e de autorização separada. Essa implementação deverá restringir o cliente de ordens ao destino de testes.

## Configuração externa e segredos

- Use `appsettings.json` e suas variantes para padrões seguros e valores não sensíveis. Injete segredos no processo em tempo de execução por um mecanismo externo com acesso restrito à identidade do serviço. Guarde senhas, tokens, chaves e strings de conexão com credenciais fora do Git, das imagens, dos argumentos de linha de comando e dos logs.
- A API aceita `RAC_SQLSERVER_CONNECTION` como substituição da conexão configurada nos arquivos do aplicativo. A Web lê `RacApi:BaseUrl`, que pode ser fornecida externamente como `RacApi__BaseUrl`. Valide o ambiente, o destino e os controles efetivos antes da inicialização, sem registrar os valores sensíveis.
- Mantenha `TestnetOrdersEnabled` desativado. Valide o estado persistido antes dos ensaios de simulação e leitura. Trate configuração obrigatória ausente, destino incorreto ou estado de risco indisponível como condição de bloqueio.
- Restrinja acesso aos segredos por serviço e ambiente. Defina responsáveis pela rotação e revogação e confira que o serviço inicia com a configuração esperada após uma troca. Nesta fase, use somente as credenciais de Testnet já autorizadas para os ensaios e mantenha o ambiente sem credenciais de negociação real.

## SQL Server, TLS e recuperação

Use uma identidade de aplicação com as permissões estritamente necessárias. Reserve as operações administrativas, de backup e de recuperação para identidades separadas. Mantenha o SQL Server acessível somente pelos serviços e operadores autorizados.

Para SQL Server fora do desenvolvimento local, exija conexão criptografada com cadeia de confiança e nome do servidor validados, usando `TrustServerCertificate=False`. Restrinja a exceção de confiança do LocalDB ao desenvolvimento local. Valide também HTTPS e a renovação dos certificados nas conexões entre componentes. Consulte [SQL-SERVER.md](../SQL-SERVER.md) e [SEGURANCA.md](../SEGURANCA.md).

1. Defina com o operador a perda máxima de dados aceitável (RPO) e o tempo máximo de recuperação (RTO). Alinhe frequência, retenção e política de backup a esses objetivos. Proteja as cópias com criptografia e acesso restrito e mantenha uma cópia separada da instância.
2. Restaure periodicamente uma cópia em um SQL Server isolado. Verifique a integridade do banco, a presença dos checkpoints, das trilhas de auditoria e dos bloqueios de risco. Suba o RAC em modo pausado, com execução externa bloqueada, e valide a leitura dos dados restaurados.
3. Registre data, responsável, ponto recuperado, duração, resultado e desvios em relação ao RPO e ao RTO. A prontidão exige evidência de restauração bem-sucedida.
4. Se o ponto restaurado for anterior a um bloqueio ou houver dúvida sobre o estado de risco, mantenha a parada externa ativa até reconciliar a auditoria e recuperar o bloqueio. Mantenha uma decisão humana registrada antes de retomar qualquer ensaio.

## Horário, logs e alertas

- Sincronize os relógios dos hosts da Web, da API, do SQL Server e da observabilidade com fontes confiáveis. Use UTC em eventos, métricas e auditoria. Monitore a saúde da sincronização e o desvio de relógio. Suspenda os ensaios de Testnet quando a sincronização falhar ou o desvio exceder o limite operacional aprovado.
- Centralize logs estruturados com ambiente, serviço, versão, horário UTC e identificador de correlação. Registre falhas de autenticação, eventos de risco, parada, reinício, recuperação e decisões operacionais. Omita credenciais, cookies, assinaturas e dados sensíveis. Defina retenção e controle de acesso à auditoria.
- Configure alertas para indisponibilidade e reinícios repetidos, falhas de TLS ou SQL Server, falhas de backup ou restauração, desvio de horário, interrupção da coleta de logs e ativação de bloqueios de risco ou da parada externa. Defina responsável, canal, prazo e regra de escalonamento. Teste a entrega e o reconhecimento dos alertas em Testnet.

## Parada externa e retomada

1. Configure o kill switch fora da Web e da API, com a `RAC.Api` como alvo. O operador deve conseguir parar o processo da API pelo gerenciador de processos mesmo com o painel indisponível. Parar a `RAC.Api` interrompe também o serviço hospedado `RacTestnetWorker` e indisponibiliza a API.
2. Ao acionar a parada, impeça o reinício automático da `RAC.Api` e confirme que o processo encerrou e a API ficou indisponível. Registre motivo, horário e responsável.
3. Mantenha o bloqueio externo durante reinícios manuais ou automáticos. Confirme em Testnet que o RAC inicia pausado, preserva bloqueios persistidos no SQL Server e recusa a retomada enquanto houver bloqueio. Se a persistência ou a confirmação falhar, mantenha a execução bloqueada.
4. Para retomar um ensaio em Testnet, reconcilie o estado e obtenha aprovação humana. Use o fluxo autorizado e auditado para revisar bloqueios de risco. Retome somente os ensaios de simulação e leitura após validar os controles. O bloqueio de ordens reais continua ativo.

## Rollback

1. Diante de falha de inicialização, degradação grave, perda de integridade ou falha em um controle de risco, acione a parada externa. Preserve logs, estado do banco, versões e horário da falha.
2. Volte à última versão validada da Web e da API e à configuração externa compatível, mantendo a execução bloqueada. Antes de iniciar, verifique a compatibilidade com o esquema atual. Se ela faltar, siga o procedimento aprovado de recuperação em banco isolado e avalie a perda de dados e a reconciliação antes de decidir sobre a troca de banco.
3. Com a execução bloqueada, verifique saúde, HTTPS, autenticação, leitura do banco, checkpoints, auditoria e bloqueios persistidos. Registre o resultado e solicite aprovação humana antes de retomar um ensaio. A troca de versão e a restauração mantêm ordens reais desabilitadas.

## Dockerfile atual e topologia futura

O [Dockerfile](../Dockerfile) descreve uma imagem Linux apenas para `RAC.Api`, com build em etapas e escuta HTTP no contêiner. A declaração `EXPOSE` informa a porta interna da imagem. A configuração de rede do ambiente é que controla a exposição externa. O build copia somente os arquivos da API para a etapa de compilação. O projeto referencia `RAC.Infrastructure`, ausente desse contexto, de modo que o arquivo atual não empacota todas as dependências necessárias para restaurar e publicar a API. O armazenamento de credenciais da Testnet usa DPAPI com `DataProtectionScope.CurrentUser` e depende do contexto do usuário Windows que protegeu as credenciais. A imagem Linux exige um desenho adicional para o armazenamento e acesso a essas credenciais antes de servir a ensaios autenticados na Testnet.

Uma implantação futura precisa validar o build completo da API e empacotar a Web separadamente. Também precisa provisionar SQL Server com persistência e recuperação, fornecer HTTPS e configuração externa e estabelecer observabilidade e controle de parada. Defina as fronteiras entre os componentes:

| Componente | Responsabilidade e acesso |
| --- | --- |
| Web | Interface do operador com HTTPS e autenticação. Encaminha somente rotas autorizadas à API |
| API | Processamento e controles de risco em rede privada. Mantenha uma instância executora por banco enquanto a execução concorrente não tiver sido validada |
| SQL Server | Persistência, checkpoints, bloqueios e auditoria. Acesso restrito à API e à operação autorizada |
| Observabilidade | Coleta e retenção de logs, métricas e alertas. Acesso administrativo restrito e coleta independente da disponibilidade da Web |

## Checklist de prontidão e critérios de bloqueio

- [ ] Local e Testnet isolados. Destinos e configuração efetiva conferidos, com ordens reais desabilitadas e ausência de credenciais de negociação real confirmada.
- [ ] HTTPS e TLS do SQL Server validados. Identidades, permissões, rotação de segredos e acesso operacional revisados. Para produção futura, inclua MFA, recuperação de contas e revisão de segurança.
- [ ] Backup restaurado em ambiente isolado, com integridade, checkpoint, auditoria e bloqueios conferidos e RPO e RTO registrados.
- [ ] Sincronização de horário conferida e alertas recebidos pelo responsável.
- [ ] Ensaios de simulação e leitura validados, com evidências da parada externa da API, do reinício e do rollback. Bloqueios persistiram e a retomada exigiu autorização humana. O envio de ordens na Testnet permanece bloqueado nesta fase.
- [ ] Para eventual fase futura, permissões de uma credencial dedicada revisadas pelo operador, com saques e transferências desabilitados e acesso limitado ao mínimo necessário.
- [ ] Operador responsável e aprovador humano identificados. Evidências, pendências e decisão registradas.

Se qualquer critério estiver pendente, falhar ou perder validade, mantenha a execução bloqueada. O preenchimento do checklist permite uma avaliação futura. Qualquer proposta de habilitação de ordens reais exige uma mudança separada, revisão de segurança, aprovação humana explícita e registro auditável. Esta fase termina com produção e ordens reais desabilitadas.
