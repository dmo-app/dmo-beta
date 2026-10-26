# Handoff para Astra — DMO Design
Data: 26/10/2026. Estado: trabalho interrompido a pedido do utilizador para passagem a outro Astra.

## Começar aqui
O utilizador quer uma versão **Razor Pages da app real** em [dmo-app/dmo-app-design](https://github.com/dmo-app/dmo-app-design), baseada em [dmo-app/dmo-beta](https://github.com/dmo-app/dmo-beta). Não quer uma recoloração da app antiga. A primeira tentativa desta sessão manteve demasiado a composição antiga e foi **explicitamente rejeitada**.

Últimas correções do utilizador, por ordem:
- «todas as paginas devem ser 1366 768»
- «o desigfn nao é assim»
- «estas a copiar muito do anterior»
- Reafirmou tabs no topo e conteúdo melhor organizado dentro dessa estrutura.
- «tu criast uam versao muito boa que abria paginas suporte ao lado, adorei esse asepcto»
- «cresciam do lado como as linhas o fazem no jobon, mas aplicaste em varios sitios da app»
- «faz ne um handoff par dar a outro astar»

A próxima tarefa é recuperar esse aspeto e o comportamento de **painéis de apoio deslizantes**, não continuar a polir a primeira tentativa rejeitada. Não afirmar que a versão atual está aprovada.

## Decisões que não devem voltar a perder-se
1. **1366 × 768 para todas as páginas.** Medir no browser a esta resolução, com dados representativos, sem zoom artificial nem screenshots que escondam overflow. A informação principal e as ações devem caber. Para listas extensas usar paginação; detalhes em apoio lateral. Não resolver com letras minúsculas, clipping ou overflow:hidden.
2. **Navegação principal por tabs no topo**, consistente entre módulos. Não criar uma barra lateral de navegação permanente.
3. **Páginas/fichas de apoio deslizam pela lateral**, como Linhas no Job On: a página de trabalho continua no contexto anterior. Reutilizar um componente entre módulos. Não confundir isto com a sidebar rejeitada.
4. Logo verdadeiro à esquerda; nome do utilizador autenticado à direita. Não inventar logo nem utilizador.
5. Interface adulta, contemporânea, densa e legível: superfícies discretas, poucas bordas, divisórias finas e tipografia Inter. Sem ícones decorativos, cartões enormes vazios, informação repetida ou dashboards inventados.
6. AN pertence à BQ; ARR pertence à PU. Sem cartões grandes independentes. Lote AN partilhado com BQ, sem duplicação desnecessária.
7. Preservar dados, permissões, formulários, validações, handlers, antiforgery, campos e ligações da app real. Apenas a apresentação e a interação contextual são redesenhadas.
8. Paleta fornecida pelo utilizador: **#4f6f9c #779ecb #b7c9e2 #1b263b #0b1320**. Tema claro como referência. Estados operacionais e identidade das linhas não são decoração.

## Referências visuais
- [Job On contemporâneo aprovado](referencias/jobon-aprovado-original.png): foi seguido da resposta «wow be mmelhor». É a referência de composição, densidade e linguagem visual.
- [Job On com a paleta fornecida](referencias/01-jobon.png): atualização de cor gerada posteriormente; não recebeu nova aprovação explícita. Não interpretar imperfeições da imagem como requisitos.
- [Paleta do utilizador](referencias/paleta.png).
- [Planeamento](referencias/02-planeamento.png), [Ferramentas](referencias/03-ferramentas.png), [Controlo](referencias/04-controlo.png), [Boquilhas](referencias/05-boquilhas.png), [Armazém](referencias/06-armazem.png), [Reparação](referencias/07-reparacao.png): estudos de extensão, não páginas funcionais nem aprovação individual. Verdes/plum anteriores à paleta devem ser substituídos conforme as decisões.
- O utilizador lembra uma versão com vários painéis laterais de apoio. **Não foi localizado nesta sessão o código dessa versão interativa.** As imagens disponíveis não demonstram o comportamento. O pedido verbal atual é inequívoco, mas não afirmar que o original interativo foi recuperado.

Erros conhecidos das imagens: Boquilhas tem rodapé de alterações copiado indevidamente; Armazém omite Entrada/Saída e contém paginação/exemplos incoerentes. Não copiar esses erros nem valores ilustrativos para a aplicação.

## Base e fontes verificadas
- Base clonada: dmo-beta main, **3e3b175d272e394b8d63c1d8561e536da370c6d6**, versão **0.3.0-beta.100**. O repositório de destino estava vazio quando inspecionado.
- Blueprint consultado: **568286671d3eab88f9f1a349c546f67ab7977432**. Lidos README, shared/frontend/FRONTEND.md e modules/job-on/FOLHA-COMPLETA.md, além do índice de caminhos.
- O blueprint tem conteúdo desatualizado: por exemplo FOLHA-COMPLETA ainda começa dizendo que não existe na beta, mas a folha já existe no código clonado. A paleta antiga e algumas regras de botões também precedem esta conversa.
- Usar as instruções recentes do utilizador para a direção visual; código atual para o comportamento implementado; blueprint para requisitos e decisões, verificando data e proveniência.

Consultar [CONTINUAR.md](CONTINUAR.md) para o mapa técnico e fontes por módulo.

## Estado exato do trabalho
A base é ASP.NET Core .NET 10 Razor Pages, com Core, Infrastructure e Web; PostgreSQL e fornecedores de autenticação já existentes. Não foi alterada a base de dados nem feito deploy.

A implementação experimental está em **rascunho-rejeitado.patch**, para inspeção/recuperação seletiva. Não é o design final e **não se deve aplicar cegamente**. No checkout original, essas alterações continuam no working tree, por gravar. O patch inclui:
- dmo-design.css: tokens, cabeçalho e tentativas de composição; a primeira versão foi rejeitada, a revisão contínua posterior ainda não foi validada visualmente pelo utilizador.
- _DmoShellLayout: tabs integrados no cabeçalho, logo e sessão reais.
- Folha.cshtml: stock visível, notas vazias omitidas, dimensões CM/MF à vista e alterações nos agrupamentos.
- dmo-session.js: extração do contador de sessão do inline para ficheiro partilhado.
- Início de _DmoSupportPanel.cshtml e dmo-support.js, ligações no dmo-live.js e jobon-folha.js. **INCOMPLETO: falta CSS do painel, verificação de renderização, navegação, formulários e segurança de dados por guardar. Não usar como componente terminado.**
- Testes atualizados para as alterações intencionais de título, stock e notas.
- tests/visual/design-review.cjs para capturas do HTML exportado por testes HTTP.

O primeiro pedido incluía ZIP/handoff; este pacote entrega os ficheiros necessários à continuação. Não confundir o ZIP com uma publicação funcional validada.

## Verificações já realizadas e limites
- Build da solução passou antes das últimas alterações do painel, com 3 avisos preexistentes em testes.
- Suite Web: 1177 testes; 1170 passaram, 3 PostgreSQL ignorados, 4 falharam (inline da sessão após mudança do cabeçalho, caret de summary e dois títulos antigos).
- Corrigidos esses quatro pontos; repetição direcionada passou **79/79**, depois da revisão da composição, **antes de introduzir o painel de apoio**.
- Chromium abriu 24 páginas completas exportadas por testes HTTP em 1366 × 768, sem overflow horizontal nem erros JS nesse conjunto. Várias **ainda excedem 768 em altura**; logo o objetivo vertical NÃO está cumprido.
- Esses HTML são fixtures renderizadas pela app com dados de teste. Não são testes end-to-end contra PostgreSQL, nem provam todos os POSTs via browser.
- Sem testes finais após os últimos ficheiros de painel. Sem aprovação visual. Sem deploy em Railway, Sites ou produção.

## Próximos passos para outro Astra
1. Ler este handoff, abrir as imagens aprovadas e o guia. Não adotar capturas da tentativa rejeitada como referência.
2. Conferir HEAD atual do dmo-beta e diferenciar alterações posteriores da base registada. Não importar alterações funcionais às cegas.
3. Construir uma apresentação própria fiel ao mockup, mantendo o contrato de dados. Evitar acumular centenas de overrides em cima do CSS antigo.
4. Terminar primeiro uma fatia concreta: Job On em 1366 × 768 + abrir ficha de ferramenta num painel deslizante, fechar e regressar sem perder contexto/dados.
5. Validar a direção visual com o utilizador antes de a propagar às restantes páginas: ele já rejeitou a aproximação anterior.
6. Propagar o componente de apoio a Ferramentas, Controlo e aos restantes contextos em que é útil. Tabs de módulo continuam navegação normal. Não abrir todos os links indiscriminadamente em painéis.
7. Testar teclado, foco, Escape, histórico/regresso, confirmação de alterações não guardadas, gravar/recusa, atualização da página de origem e links externos. Não presumir que um iframe por si resolve estes fluxos.
8. Adaptar Planeamento, Ferramentas, Controlo, Boquilhas, Armazém e Reparação à grelha 1366 × 768. Preservar seleção de linhas e capacidades.
9. Atualizar o guia e registar as provas reais. Publicar no dmo-app-design, nunca sobrescrever dmo-beta.

## Local de trabalho original
Checkout: C:/Users/vk_do/Documents/Codex/2026-10-26/crie-um-novo-site-com-sites-2/dmo-app-design
Imagens: pasta referencias deste handoff (portátil).
Artefactos locais não versionados: artifacts/design (HTML de teste, capturas, browser-review.json).
Remote origin: dmo-app/dmo-app-design. Remote upstream: dmo-app/dmo-beta.

