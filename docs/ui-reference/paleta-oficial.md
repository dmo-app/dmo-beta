# Paleta oficial do DMO

Decisão do Diogo (proprietário), 26/10/2026, a partir do handoff `dmo-mockup-handoff` (`referencias/prompts-paleta.md`). Substitui a paleta de 17/10/2026 (`#44647c`…). Esta paleta é o template de cores de toda a aplicação — interface, logo (login e cabeçalho) e PDFs.

| Nome | Cor | Uso |
| --- | --- | --- |
| `--dmo-palette-slate` | `#4f6f9c` | Navegação, separador ativo, ligações, linha selecionada, selos C3, logo, títulos dos PDFs. |
| `--dmo-palette-ocean` | `#4f6f9c` | Contornos de foco e destaques grandes (o mockup não separa um segundo tom). |
| `--dmo-palette-steel` | `#779ecb` | Tom intermédio; botão carregado (`.btn:active`). |
| `--dmo-palette-mist` | `#779ecb` | Contornos tracejados e a dica de ajuda. |
| `--dmo-palette-sky` | `#b7c9e2` | Hover das linhas, fim do gradiente do login, fundos suaves. |

Ação principal: os botões sólidos passam a `--dmo-action` `#1b263b` (o navy do mockup) com letra branca. Texto principal `#0b1320`; texto secundário `#1b263b`; etiquetas `#586a80`. Fundo da página `#e5eaf1`, barras `#eff3f8`, campos `#f8fafc`, cartões brancos.

Tons derivados (mesma família): `--dmo-brand-950` `#0b1320`, `--dmo-brand-900` `#1b263b`, `--dmo-brand-800` `#2f3f5b`, `--dmo-brand-700` `#41567a`; contorno `#ccd7e4`; fundos `#dbe6f3`, `#eaf1f9`, `#eef2f7`.

## Onde vive

- **App:** `src/DMO.Alpha.Web/wwwroot/0_ASSET_CANONICAL_DESIGN_SYSTEM.css`, primeiro `:root`. A escala `--dmo-brand-*`, o `--dmo-action` e os tokens `--dmo-row-hover`, `--dmo-focus-ring` saem daí.
- **PDFs:** `src/DMO.Alpha.Infrastructure/Pdf/DmoPdfPalette.cs`, com os mesmos valores.
- **Logo:** `wwwroot/0_ASSET_LOGO.png` e `Infrastructure/Controlo/Assets/ba-logo.png`, a `#4f6f9c`.
- O teste `UiStyleRuleTests` falha se a paleta mudar só num sítio ou se voltarem azuis soltos ao CSS ou aos geradores de PDF. O `CoresDeEstadoNosPdfsTests` falha se um gerador de PDF tiver uma cor solta (todas saem da `DmoPdfPalette`, as de estado incluídas).

## Fora da paleta (de propósito)

- Cores de estado: OK verde, NOK vermelho, por aprovar âmbar.
- Identidade das máquinas (`--dmo-machine-*`) e das componentes no mapa dos Pegamentos (CM, MF, BQ). O mockup de Planeamento mostra as linhas num azul-neutro com o C3 a `#b7c9e2` e barra `#4f6f9c`; a app mantém a cor própria de cada linha (regra de 23/10/2026), porque é o que distingue as máquinas no painel lateral e no mapa.
- Cinzento de «não avaliável» e de botão indisponível.

Nos PDFs, as cores de estado vivem na `DmoPdfPalette` (sweep de 19/10/2026, 3.16), com os valores do ecrã:

| Estado | Cor | Onde |
| --- | --- | --- |
| OK, «Visto» | `#15803d` (fundo `#effaf3`) | Áreas, Pegamentos e PU NNPB, como no ecrã do Resumo |
| NOK, fora do corredor, «Não aprovado» | `#b91c1c` (fundo `#fdf2f2`) | Áreas, Pegamentos, selos |
| Por verificar | `#a16207` (fundo `#fdf8ec`) | PU NNPB |
| Não avaliável | `#94a3b8` (fundo `#f4f6f8`) | Pegamentos, como no ecrã dos Pegamentos; nunca âmbar |
| Aprovado | `#3d7a5a` | Selos e cartão «Aprovação», como no cartão de aprovação do ecrã |
| Por aprovar | `#a97943` | Selos e cartão «Aprovação»; também os avisos (`--dmo-warning`) |

## Nota para o blueprint (beta)

Texto para `dmo-app/blueprint` (shared/frontend/FRONTEND.md):

> **Paleta oficial (26/10/2026).** `#b7c9e2` (light), `#779ecb` (secondary), `#4f6f9c` (primary), `#1b263b` (navy, ação principal), `#0b1320` (ink, texto). Navegação, separadores ativos e linha selecionada a `#4f6f9c`; botões sólidos a `#1b263b` com letra branca; hover das linhas a `#b7c9e2`. Aplica-se à interface, ao logo e aos PDFs. Fonte: `dmo-app/dmo-beta`, `docs/ui-reference/paleta-oficial.md`.
