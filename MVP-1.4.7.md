# RAC MVP 1.4.7 — Gráficos comparativos de risco SHADOW

Data: 23/11/2026. Computador `080001325ADPLAN`; repositório local `C:\Users\05035144208\source\repos\RAC`.

## Implementado

- Na seção autenticada **Auditoria SHADOW dos 12 agentes**, um gráfico de barras responsivo compara os pares de outubro de 2026. O usuário pode selecionar: saídas hipotéticas sem cobertura, tentativas de entrada bloqueadas, entradas parcialmente preenchidas, trades hipotéticos e dias sem operação.
- As barras exibem contagens por agente e proporção relativa ao máximo do indicador escolhido, **não lucros ou previsão de retorno**. A marcação de risco destaca contagens de saídas não resolvidas.
- A fonte continua exclusivamente o endpoint de leitura `/api/research/shadow-dashboard` implementado na 1.4.6; não há endpoints de controle, nova consulta privada da Binance ou operações.
- O cliente só desenha comparativos depois de receber 12 agentes em um relatório SHADOW validado. Valores inválidos, ausência do relatório e logout limpam a visualização; no logout, nenhum dado da pesquisa permanece visível.
- A UI explicita que existem dados **apenas de outubro de 2026**. Não foram inventadas curvas de patrimônio, histórico entre meses ou resultados não observados. A ampliação histórica dependerá de novos relatórios genuinamente validados.
- Entrega local: atualização de `index.html`, `app.js` e `styles.css` no `wwwroot` publicado do MVP 1.4.6, preservando o executável operacional, a API e a sessão sempre que possível.

## Segurança e limites

- Funcionalidade apenas de visualização SHADOW; nenhuma autorização de ordens, promoção de estratégias, ajuste de circuit breaker ou reinício do PAPER.
- A origem OHLCV e o estudo de capacidade de saída continuam hipotéticos; contagens altas não provam melhor performance financeira.

Veja `MVP-1.4.7-VERIFICATION.md` para evidências e testes.
