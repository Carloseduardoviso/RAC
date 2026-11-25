# RAC — Mapa de agentes com movimentos e diálogos visuais (MVP 1.5.17)

## Objetivo

Adicionar personagens humanos renderizados em 3D e animação 2.5D ao cenário fotorrealista do RAC, mantendo os sinais e os controles PAPER reais.

## Funcionalidades visuais

- 12 avatares de operadores e Ana, Rebeca e Carlos representados com imagens transparentes fotorrealistas. Inclui sequências WebP animadas de digitação, pose de levantar e alternância de passos na caminhada.
- As posições estão ancoradas às mesas do cenário existente. Quando o estado real do agente é **AGUARDANDO / PAUSADO / DESCANSANDO / BLOQUEADO**, o avatar caminha pelo corredor até a área de lazer. Ao receber **OBSERVANDO / ANALISANDO / EM OPERAÇÃO**, retorna à mesa e retoma a animação de digitação.
- A conta PAPER pausada ou com bloqueio de risco nunca é exibida como operando: os avatares ficam na área de descanso visualmente. Isso **não altera** a trava nem a simulação.
- O botão já existente **Solicitar visita** chama o chefe selecionado, que caminha até o posto, exibe um balão com diálogo ilustrativo e retorna ao seu local. Eventos opcionais enviados pelo backend com `supervisorRequest` / `requestedSupervisor` / `needsSupervisor` também podem iniciar visitas, mas nenhum chamado é inventado quando esses dados não existem.
- Botões opcionais **Personagens animados** e **Voz**. Voz é desativada por padrão; quando habilitada por clique do usuário, utiliza síntese local do navegador `speechSynthesis` em português. Não há voz automática sem consentimento.
- Nenhuma rota de trading foi adicionada; o JavaScript de animação não faz requisições de rede e não envia ordens. As falas são **conversas ilustrativas geradas pelo frontend**, não mensagens reais de agentes inteligentes.

## Limitações técnicas

- Este MVP utiliza *sprites* realistas renderizados em 3D sobre um cenário fotorrealista fixo e animação por coordenadas 2D/2.5D. Não é uma cena 3D interativa com esqueleto de personagens, colisões ou sincronização labial fotorrealista. Para isso são necessários modelos 3D rigados com animações e um motor WebGL/Three.js ou Babylon.js.
- A imagem de fundo ainda contém pessoas renderizadas; a camada de avatares é uma composição visual adicional. Uma próxima etapa pode produzir uma versão equivalente da cena sem personagens embutidos para melhorar a integração.
- O início automático de pedidos de ajuda só pode ser plenamente demonstrado quando o backend fornecer eventos de assistência. O clique **Solicitar visita** já funciona com a lógica visual.
- Movimentos e falas representam ações ilustrativas de interface e não decisões autônomas de negociação.

## Validação e proteção

- Testes JavaScript unitários de roteamento dos 12 agentes, destino por estado PAPER, solicitações de liderança e integridade dos três arquivos WebP.
- Verificação `node --check` nos módulos de frontend, testes .NET existentes e build Release sem modificar serviços.
- Preservar banco SQL, posições, histórico, `RiskLocked`, Testnet desabilitada, ordens reais bloqueadas e alterações locais de outros trabalhos.
