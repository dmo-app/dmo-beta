# Paleta oficial do DMO

Decisão do Diogo (proprietário), 17/10/2026: esta paleta é o template de cores de toda a aplicação — interface, logo (login e cabeçalho) e PDFs.

| Nome | Cor | Uso |
|---|---|---|
| `--dmo-palette-slate` | `#44647c` | Botões, cabeçalhos, linha selecionada, separador ativo, logo, títulos dos PDFs. Letra branca a 6,3:1. |
| `--dmo-palette-ocean` | `#4c7c9c` | Contornos de foco, destaques grandes. Não usar em texto pequeno (4,5:1 à justa). |
| `--dmo-palette-steel` | `#6494b4` | Tom intermédio. |
| `--dmo-palette-mist` | `#92b4cc` | Texto claro sobre fundo escuro, contornos tracejados. |
| `--dmo-palette-sky` | `#afd1f2` | Hover das linhas, fim do gradiente do login, contornos azul-claros. |

Tons derivados (mesma família): texto `#1b2832`/`#253744`, `--dmo-brand-700` `#3c586d`, texto secundário `#577389`, contorno `#d9e5ed`, fundos `#d7e8f8`, `#ebf4fc`, `#f3f8fd`, `#f7fafe`.

## Onde vive

- **App:** `src/DMO.Alpha.Web/wwwroot/0_ASSET_CANONICAL_DESIGN_SYSTEM.css`, primeiro `:root`. A escala `--dmo-brand-*` e os tokens `--dmo-row-hover`, `--dmo-focus-ring` saem daí.
- **PDFs:** `src/DMO.Alpha.Infrastructure/Pdf/DmoPdfPalette.cs`, com os mesmos valores.
- **Logo:** `wwwroot/0_ASSET_LOGO.png` e `Infrastructure/Controlo/Assets/ba-logo.png`, a `#44647c`.
- O teste `UiStyleRuleTests` falha se a paleta mudar só num sítio ou se voltarem azuis soltos ao CSS ou aos geradores de PDF.

## Fora da paleta (de propósito)

- Cores de estado: OK verde, NOK vermelho, por aprovar âmbar.
- Identidade das máquinas (`--dmo-machine-*`) e das componentes no mapa dos Pegamentos (CM, MF, BQ).
- Cinzento de «não avaliável» e de botão indisponível.

## Nota para o blueprint (beta)

Texto para `dmo-app/blueprint` (shared/frontend/FRONTEND.md):

> **Paleta oficial (17/10/2026).** `#afd1f2` (sky), `#92b4cc` (mist), `#6494b4` (steel), `#4c7c9c` (ocean), `#44647c` (slate). Botões, cabeçalhos e linha selecionada a `#44647c` com letra branca; hover das linhas a `#afd1f2`. Aplica-se à interface, ao logo e aos PDFs. Fonte: `dmo-app/DMO`, `docs/ui-reference/paleta-oficial.md`.
