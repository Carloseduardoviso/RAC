# RAC — Aprimoramento visual dos movimentos dos agentes

## Intenção

Deixar os personagens do escritório visualmente vivos enquanto permanecem sobre a cena fotorrealista existente: digitação contínua, pequenos movimentos corporais, levantar antes de caminhar, caminhada com passos alternados e conversa com a liderança.

## Objetivos

- Aproveitar `trading-floor-motion.js`, `trading-floor-motion-core.js`, `trading-floor-motion.css` e os WebP atuais.
- Melhorar a percepção de movimento sem exigir câmera livre ou motor 3D.
- Manter os movimentos sincronizados aos estados visuais dos agentes.
- Preservar o PAPER pausado e bloqueado (`Running=0`, `RiskLocked=1`).
- Manter a camada de animação sem chamadas de rede, comandos de trading ou alterações no banco.

## Fora do escopo

- Modelos 3D rigados, WebGL, câmera livre ou colisões físicas.
- Alteração de posições do cenário fotorrealista.
- Alteração de risco, PAPER, Binance, histórico ou serviços backend.
- Geração automática de falas reais de agentes.

## Abordagens consideradas

1. **Aprimorar a camada 2.5D atual — escolhida**: menor risco, aproveita o código e assets existentes e permite validação imediata no navegador.
2. **Criar spritesheets maiores**: melhor riqueza visual, mas depende de novos assets e será uma etapa posterior.
3. **Migrar para personagens 3D reais**: maior fidelidade e liberdade, porém incompatível com o objetivo imediato e com a cena fixa atual.

## Desenho escolhido

- Estender os estados visuais dos sprites para incluir variações de digitação, olhar, respiração, conversa e caminhada.
- Usar animações CSS leves e alternância de frames/transformações, sem aumentar a carga da camada de dados.
- Garantir que a atualização de estado não reinicie continuamente as animações quando o agente permanece na mesma zona.
- Preservar `prefers-reduced-motion`, o botão de desligar animações e a demonstração somente visual.
- Manter a regra de segurança existente: estado PAPER pausado ou `riskLocked` envia o avatar para a área de lazer apenas como representação visual.

## Validação

- `node --check` nos módulos JavaScript.
- Testes existentes de motion core.
- Build/testes .NET existentes quando disponíveis.
- Conferência visual após atualização forçada do navegador com Ctrl+F5.

## Decisões pendentes do construtor

- Ajustar duração e intensidade dos movimentos para não competir com os dados da interface.
- Reutilizar os WebP atuais como fallback para navegadores sem suporte às animações avançadas.
