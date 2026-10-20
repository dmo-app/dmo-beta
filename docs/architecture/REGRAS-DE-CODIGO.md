# Regra do código: eficiente e robusto, sem perder segurança

Pedido do proprietário em 19/10/2026: uma regra que mantenha o código mais eficiente sem remover a segurança e que mantenha a robustez (evitar bugs, evitar código frágil). Aprovada por ele no mesmo dia, com o texto do plano do sweep do Codex de 19/10/2026.

Vale para todas as alterações ao DMO-BETA-DEV, de qualquer agente (Claude ou Codex) ou pessoa, e junta-se às regras do `AGENTS.md`; não as substitui.

## A regra

**Cada alteração muda pouco, resolve a causa num só sítio, prova-se com um teste que falhava antes, e não tira nenhuma proteção.** Simplificar é ter menos código para a mesma proteção, nunca menos proteção.

## Como se cumpre

Cada ponto vem de erros reais encontrados nos sweeps de 18 e 19/10/2026.

### Robustez (evitar bugs)

1. **Só a resposta mais recente conta.** Um pedido que nasce de escrever ou de escolher só mostra a resposta se ainda for o último (contador de pedidos ou `AbortController`). Primeiro usar o que já o faz: `data-dmo-procura`, `data-dmo-ao-escrever`, `DmoTroca`, `dmo-suggest`. Erro real: a calote do Peso e os dados das ferramentas do Job On mostravam a resposta de uma escolha anterior.
2. **Nunca falhar em silêncio.** Quando um pedido falha ou o servidor recusa, o ecrã deixa de mostrar dados antigos como se fossem atuais e diz o que aconteceu. O que o operador escreveu nunca se perde sem aviso. Um `catch` vazio só com um comentário que diga porque o que fica no ecrã continua certo.
3. **O servidor valida tudo o que recebe,** também o que a página «nunca envia»: o formato (um JSON objeto ou lista), os tipos e os limites. Uma entrada estranha dá recusa ou aviso, nunca erro 500. A lista de candidatos na página ajuda o operador, mas a decisão final (existe, é compatível, alguém mudou entretanto) é do servidor ao gravar.
4. **Uma gravação nunca se repete sozinha nem dá uma mensagem falsa.** Os formulários gravam pelo `DmoTroca`. Quando o mesmo conteúdo duas vezes é legítimo (duas leituras iguais), o handler leva `[EnvioUnico]`. Quando não é, o servidor reconhece o envio repetido (o que chega é igual ao que ficou gravado) e abre o que ficou, sem mensagem. Ver `docs/ui-reference/atualizar-so-o-que-muda.md`.
5. **Cada correção traz um teste que falha sem ela** e passa com ela, e o PR diz como se confirmou que falhava. Numa regra que já existe, estraga-se a regra de propósito uma vez e um teste tem de falhar. Nos dois casos o teste falha numa verificação (o PR copia a mensagem), com o código estragado a compilar; um erro de compilação não prova nada. Como escrever os testes: [REGRAS-DOS-TESTES.md](REGRAS-DOS-TESTES.md).

### Código menos frágil

6. **Uma regra do negócio vive num sítio** (Core ou Infrastructure), e as páginas, os PDFs e as listas chamam-na. Exemplos do que já se desviou por estar repetido: o estado da aprovação, o «hoje» das Boquilhas (hora de viragem), a «produção anterior» e a leitura do nome técnico. A procura e a gravação usam a mesma regra; não se copiam condições entre páginas, nem CSS entre módulos.
7. **Dependências pelo construtor.** Nada de `new AlgumaQuery(db)`, `HttpContext.RequestServices` ou uma página a criar outra página para lhe usar um método: o que duas páginas partilham passa para um serviço.
8. **Páginas pequenas.** Um separador ou caso de uso novo vai para a sua página, handler ou serviço. Uma página com mais de cerca de 1500 linhas ou 15 dependências (hoje o Peso, o Job On e as Boquilhas) só recebe correções: o que é novo nasce fora dela.
9. **Testar o comportamento, não o texto.** Um teste diz o que o operador vê ou o que fica gravado. Testes novos não comparam HTML nem CSS letra a letra; os que existem ficam até ao estudo dos testes do CI.
10. **Ficheiros do browser.** Scripts novos em ficheiros, com a versão no endereço (`asp-append-version`), e nunca em `<script>` nem `onclick` dentro das páginas, porque impedem a política de scripts do browser. CSS por componente: cores só da paleta (`--dmo-palette-*` e tons derivados), uma variante no componente partilhado em vez de uma regra nova por página, e nenhum `!important` novo, salvo para vencer estilo de terceiros, com comentário.

### Eficiência sem perder segurança

11. **Medir antes de otimizar** (o tempo, o número de consultas) e manter o mesmo resultado, com teste. As consultas trazem só o que a página mostra, paginadas, sem uma consulta por linha. Nada específico do Supabase: PostgreSQL e .NET portáveis, para a Azure.
12. **Proteções que nunca saem para simplificar:** a entrada no módulo e as permissões, a antifalsificação nos POST, a validação no servidor, a verificação da versão nas edições, o `[EnvioUnico]`, as transações e os trincos das gravações ao mesmo tempo, o escape do HTML (`textContent` ou `escapeHtml`, nunca `innerHTML` com dados por escapar), os cabeçalhos de segurança, os segredos fora do código e dos registos, e os PDFs só em memória.
13. **Base de dados:** só migrações novas depois da `Baseline`, de preferência só a acrescentar. Uma regra de identidade (cm_id, mf_id, bq_id, uma folha por combinação) também fica protegida na base, com um índice único ou um CHECK, e não só no código. Apagar ou mudar dados só com o OK do proprietário. O SQL do DEV fica em `docs/dev/`, com README.

### Textos que o utilizador vê

14. **Português de Portugal** (pedido do proprietário em 19/10/2026) em tudo o que se vê: ecrãs, botões, avisos, mensagens de validação, PDFs, emails e notas da versão. Usar as palavras da fábrica: utilizador, ecrã, ficheiro, pasta, palavra-passe, gravar ou guardar, apagar ou eliminar, registo e registar, procurar, descarregar, meter a zero (nunca usuário, tela, arquivo, diretório, senha, salvar, excluir, registro, buscar, baixar, zerar ou pôr a zero). Nada de gerúndio («A carregar…», não «Carregando…»; «com o email», não «usando o email»), «Tem a certeza», não «Tem certeza», e «à» em «até à». Os nomes no código e na base de dados não mudam.

## O que esta regra não é

- Não é uma ronda de revisão a mais em cada entrega (decisão do proprietário de 18/10/2026): cumpre-se com o teste da correção e o CI local (`scripts/ci/ci-local.sh`).
- Não manda reescrever o que funciona. O código antigo só se arruma quando se mexe nele, uma parte de cada vez.
- Não troca avisos por bloqueios: a regra das datas livres e dos avisos (19/10/2026) continua igual.
