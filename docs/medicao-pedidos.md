# Medição dos pedidos (PERF-01)

Cada pedido à aplicação (páginas, partes de página, POSTs e o `/Sugestoes`) é medido do princípio ao fim. Os ficheiros estáticos (`/css`, `/js`, imagens) não são medidos. A medição não muda nada no que a página faz: o `no-store` e os outros cabeçalhos ficam iguais.

## O cabeçalho `Server-Timing`

Cada resposta medida leva, por exemplo:

```
Server-Timing: db;dur=9.8;desc="1 consultas", conn;dur=0.3;desc="abrir ligacao", app;dur=701.0, total;dur=711.2
```

- `db` — tempo das consultas à base (ms) e, na descrição, quantas foram.
- `conn` — tempo a abrir a ligação à base (inclui esperar por uma ligação livre do pool).
- `app` — o resto: o código da página, o Razor e a autenticação (`total − db − conn`).
- `total` — do início do pedido até a resposta começar a ser enviada.

O tempo de cada consulta é até a base começar a responder; a leitura das linhas conta em `app`.

### Ver no browser

1. Abrir as ferramentas de programador (F12) e o separador **Network / Rede**.
2. Recarregar a página e clicar no pedido do documento (o primeiro, tipo `document`).
3. No separador **Timing**, a secção **Server Timing** mostra `db`, `conn`, `app` e `total` em barras, com a descrição «N consultas». (No Firefox: separador **Timings**, em baixo.)
4. Em alternativa, em **Headers / Cabeçalhos** da resposta, a linha `Server-Timing`.

Para partes de página (troca parcial, paginação), filtrar por **Fetch/XHR** e ver o pedido da mesma forma.

## A linha no log

Uma linha por pedido, nível Information, categoria `DMO.Medicao`:

```
Pedido POST /Login handler=- -> 200 em 712.9 ms; 1 consultas, bd 9.8 ms, ligação 0.3 ms [762053a5e1f33cb033a7cf5a8a0954c4]
```

Campos estruturados: `Metodo`, `Rota` (o modelo da rota, ex. `/Ferramentas/Ficha/{id?}`, nunca o caminho com valores), `Handler` (só um nome simples; outro valor fica `(outro)`), `Estado`, `TotalMs`, `Consultas`, `BdMs`, `LigacaoMs` e `CorrelationId` (o trace id do pedido: a parte do meio da «Referência do erro», `00-<trace id>-…`, para encontrar a linha de um erro). Um pedido que falha tem duas linhas: a dele, com 500, e a da página de erro.

A linha **nunca** leva o SQL, os parâmetros das consultas, a query string, cookies, emails, palavras-passe, referências ou outros dados.

## Ligar e desligar

Por omissão está ligada. Para desligar (sem o cabeçalho e sem a linha): `Dmo:MedicaoPedidos:Ligada=false` (variável de ambiente `Dmo__MedicaoPedidos__Ligada=false`). Mais nada muda.

## Tirar a linha de base

Para cada rota — RI (Reparação Interna), Histórico de Ferramentas, Peso, Job On, Boquilhas e Armazém:

1. Abrir a página 5 vezes (a primeira aquece a aplicação; não contar).
2. Anotar de cada vez `total`, `db` e o número de consultas (pelo browser ou pela linha do log, filtrando por `Rota`).
3. Guardar a mediana e o número de consultas, com a data e a versão (`/versao`).

O número de consultas é o mais estável: uma mudança que o faça subir numa rota merece ser vista antes de seguir.
