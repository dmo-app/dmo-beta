// O registo dos Pegamentos na própria tabela (Diogo, 17/10/2026): a
// ovalização e a média calculam-se ao escrever, Enter regista ou guarda,
// Esc cancela a correção e o × pede confirmação antes de apagar. O servidor
// volta a calcular tudo; isto é só a pré-visualização.
//
// Gravar sem recarregar (Diogo, 17/10/2026: «atualizar só o que muda»): cada
// cartão é uma zona que o DmoTroca troca depois de gravar. Por isso tudo aqui
// é delegado no document, e o que precisa de arranque (as contas da linha
// nova, o «+ Linha») volta a correr nas zonas novas (evento dmo:troca). Depois
// de registar, o foco vai para a costura da próxima leitura desse componente;
// depois de corrigir, cancelar ou apagar também, a não ser que o operador já
// esteja a escrever na linha nova (aí fica). O que estava escrito na linha
// nova de um cartão trocado por outra ação volta para lá; o que um registo
// enviou nunca volta como rascunho. O ecrã só acompanha a linha nova depois
// de registar; corrigir, cancelar e apagar não o mexem. O que está por cima
// dos cartões não encolhe enquanto a página está aberta (ver «O contexto não
// encolhe»), por isso os cartões não sobem.
//
// Várias ações no mesmo cartão (registar e logo apagar outra leitura, ou
// guardar uma correção e registar a leitura nova já escrita) seguem na fila
// do DmoTroca, cada uma com a sua resposta: a resposta de cada uma troca o
// cartão e a seguinte parte depois.
//
// Uma correção de cada vez (como antes da E2): abrir a correção de outra
// leitura, ou registar e apagar no cartão da correção, fecha a que estava
// aberta, e o que lá estava escrito perde-se (se o operador ainda lá
// escrevia, o foco não salta para a linha nova). Registar ou apagar noutro
// cartão mantém-na aberta, com o que lá está escrito.
(function () {
  var NOVA = '[data-peg-linha-nova]:not([data-peg-cancelar])';


  function cartaoDe(elemento) { return elemento && elemento.closest ? elemento.closest('[data-peg-component]') : null; }

  function componenteDe(cartao) { return cartao.getAttribute('data-peg-component'); }

  function cartaoDoComponente(componente) { return document.querySelector('[data-peg-component="' + componente + '"]'); }

  // A costura, a contra costura e o número da ferramenta (só o número não
  // conta como escrito: as contas olham para [0] e [1]).
  function valores(linha) {
    var numero = linha.querySelector('[data-peg-numero]');
    return [linha.querySelector('[data-peg-costura]').value, linha.querySelector('[data-peg-contra]').value, numero ? numero.value : ''];
  }

  // As leituras gravadas de um cartão (a que está em correção também conta).
  function leituras(cartao) { return cartao.querySelectorAll('tbody tr[id^="peg-linha-"]').length; }

  // A PRÉ-VISUALIZAÇÃO SEGUE O SERVIDOR (PegamentosCalculation e o cartão;
  // sweep de 19/10/2026, 3.12): tocar o limite já conta como fora, a
  // costura e a contra costura também se marcam, e a ovalização arredonda
  // antes do sinal («+0.00», nunca «-0.00»). As contas são em decimal exato
  // (inteiros BigInt com as casas decimais): em vírgula flutuante 70.10 +
  // 70.30 dá 140.39999… e a média no limite escapava. O corredor (nominal ±
  // tolerância) vem calculado do servidor. Os casos de
  // tests/ShellProof/pegamentos-previsao.casos.json verificam-se aqui e no
  // servidor, para as duas regras não se separarem.
  var DEZ = BigInt(10);

  // Um número escrito (vírgula ou ponto, como o servidor aceita), ou null.
  function exato(texto) {
    if (texto == null) return null;
    var partes = /^([+-]?)(\d*)(?:\.(\d*))?$/.exec(String(texto).trim().replace(/,/g, '.'));
    if (!partes || partes[2] + (partes[3] || '') === '') return null;
    var inteiro = BigInt((partes[2] || '0') + (partes[3] || ''));
    return { valor: partes[1] === '-' ? -inteiro : inteiro, casas: (partes[3] || '').length };
  }

  // numerador / denominador com duas casas, arredondado como no servidor
  // (metade para longe do zero).
  function duasCasas(numerador, denominador) {
    var negativo = numerador < BigInt(0);
    var absoluto = negativo ? -numerador : numerador;
    var centesimas = (absoluto * BigInt(200) + denominador) / (denominador * BigInt(2));
    var texto = centesimas.toString().padStart(3, '0');
    return { negativo: negativo && centesimas !== BigInt(0), texto: texto.slice(0, -2) + '.' + texto.slice(-2) };
  }

  function calcular(linha) {
    var tabela = linha.closest('[data-peg-registo]');
    if (!tabela) return;
    var campoCostura = linha.querySelector('[data-peg-costura]');
    var campoContra = linha.querySelector('[data-peg-contra]');
    var numeros = [campoCostura.value, campoContra.value, tabela.getAttribute('data-peg-tolerancia'),
      tabela.getAttribute('data-peg-limite-inferior'), tabela.getAttribute('data-peg-limite-superior')].map(exato);
    // Todos em inteiros na mesma escala (as casas do que tiver mais).
    var casas = Math.max.apply(null, numeros.map(function (n) { return n ? n.casas : 0; }));
    var unidade = DEZ ** BigInt(casas);
    var inteiros = numeros.map(function (n) { return n ? n.valor * DEZ ** BigInt(casas - n.casas) : null; });
    var c = inteiros[0], n = inteiros[1], tolerancia = inteiros[2], inferior = inteiros[3], superior = inteiros[4];
    // Fora quando toca ou passa um limite (PegamentosCalculation.IsAlert).
    // A média vai a dobrar (a soma dos dois eixos), para ficar inteira.
    function fora(valor, vezes) {
      return inferior != null && superior != null && valor != null
        && (valor <= inferior * vezes || valor >= superior * vezes);
    }
    var oval = c != null && n != null ? c - n : null;
    var dobroDaMedia = c != null ? (n != null ? c + n : c * BigInt(2)) : null;
    var celOval = linha.querySelector('[data-peg-oval]');
    var celMedia = linha.querySelector('[data-peg-media]');
    var o = oval == null ? null : duasCasas(oval, unidade);
    celOval.textContent = o == null ? '—' : (o.negativo ? '\u2212' : '+') + o.texto;
    celOval.classList.toggle('peg-cell-bad', oval != null && tolerancia != null && (oval < BigInt(0) ? -oval : oval) >= tolerancia);
    var m = dobroDaMedia == null ? null : duasCasas(dobroDaMedia, unidade * BigInt(2));
    celMedia.textContent = m == null ? '—' : (m.negativo ? '-' : '') + m.texto;
    celMedia.classList.toggle('peg-cell-bad', fora(dobroDaMedia, BigInt(2)));
    campoCostura.parentNode.classList.toggle('peg-cell-bad', fora(c, BigInt(1)));
    campoContra.parentNode.classList.toggle('peg-cell-bad', fora(n, BigInt(1)));
  }

  // «+ Linha»: copia a última linha nova, vazia e com o número seguinte.
  function maisLinha(cartao) {
    var linhas = cartao.querySelectorAll(NOVA);
    var ultima = linhas[linhas.length - 1];
    if (!ultima) return null;
    var nova = ultima.cloneNode(true);
    nova.querySelectorAll('input').forEach(function (campo) {
      campo.removeAttribute('id');
      campo.removeAttribute('required');
      campo.removeAttribute('autofocus');
      campo.readOnly = false;
      campo.value = '';
    });
    nova.cells[0].textContent = String(Number(ultima.cells[0].textContent) + 1);
    ultima.parentNode.insertBefore(nova, ultima.nextSibling);
    calcular(nova);
    return nova;
  }

  // O arranque de uma zona: as contas das linhas novas e o «+ Linha».
  function preparar(raiz) {
    raiz.querySelectorAll('[data-peg-registo] [data-peg-linha-nova]').forEach(calcular);
    raiz.querySelectorAll('[data-peg-mais-linha]').forEach(function (mais) {
      var cartao = cartaoDe(mais);
      if (cartao && cartao.querySelector(NOVA)) mais.hidden = false;
    });
  }

  // O que está escrito nas linhas novas de cada cartão, para voltar a elas
  // quando o cartão é trocado por outra ação. Registar envia-as: aí apagam-se.
  var rascunhos = {};
  // As ações de cada cartão à espera de resposta (registar, corrigir, apagar,
  // cancelar com Esc), por componente e pela ordem em que partiram: o tipo, o
  // número do envio no DmoTroca (ou, no Esc, a linha da correção) e, no
  // registo, as linhas enviadas. Cada uma acaba quando a resposta dela troca
  // o cartão (dmo:troca) ou, se não o trocou, quando o DmoTroca acaba o
  // pedido (dmo:fim).
  var acoes = {};
  // A fila de teclas de cada cartão enquanto ele espera (ver «A leitura
  // seguinte sem esperar»): as leituras escritas, o campo delas (0 costura,
  // 1 contra costura), o elemento onde o operador estava quando a primeira
  // ação partiu e o último onde escreveu.
  var teclas = {};
  // Quantas leituras cada cartão mostra (para saber se um registo gravou).
  var contagem = {};

  // O registo do cartão que ainda espera (há no máximo um: enquanto espera,
  // as teclas da linha nova vão para a fila).
  function registoDe(componente) {
    return (acoes[componente] || []).filter(function (acao) { return acao.tipo === 'registar'; })[0] || null;
  }

  // A ação a que um evento do DmoTroca responde: pelo número do envio ou,
  // num clique (o Esc), pelo elemento que o pediu.
  function indiceDe(lista, detalhe) {
    for (var i = 0; i < lista.length; i++) {
      var acao = lista[i];
      if (acao.envio != null ? acao.envio === detalhe.envio : detalhe.envio == null && acao.origem === detalhe.origem) return i;
    }
    return -1;
  }

  // Os campos de um registo ficam só de leitura (não desativados: o envio já
  // leva os valores e, se a troca falhar, o envio do browser leva os mesmos).
  function travar(cartao, sim) {
    cartao.querySelectorAll(NOVA + ' input').forEach(function (campo) { campo.readOnly = sim; });
  }

  // O CONTEXTO NÃO ENCOLHE: na primeira medição sai a dica da folha em branco
  // de cima dos cartões (34 px). O espaço que o contexto ocupava fica até se
  // recarregar a página, por isso os cartões não sobem, nem com a página no
  // topo. As medidas tiram-se antes de cada ação de um cartão.
  var contexto = null;

  function medirContexto() {
    var zona = document.getElementById('pegContexto');
    var cartao = zona && zona.querySelector('.peg-card');
    var notas = cartao && cartao.querySelector('.peg-context-notes');
    if (!cartao) return;
    contexto = { cartao: cartao.getBoundingClientRect().height, notas: notas ? notas.getBoundingClientRect().height : 0 };
  }

  function reservarContexto() {
    var zona = document.getElementById('pegContexto');
    var cartao = zona && zona.querySelector('.peg-card');
    if (!cartao || !contexto) return;
    var notas = cartao.querySelector('.peg-context-notes');
    if (notas && contexto.notas > 0) notas.style.minHeight = contexto.notas + 'px';
    if (contexto.cartao > 0) cartao.style.minHeight = contexto.cartao + 'px';
  }

  // A linha nova desce a cada leitura: se sair do ecrã, o ecrã acompanha-a.
  function acompanhar(campo) {
    var caixa = campo.getBoundingClientRect();
    if (caixa.bottom > window.innerHeight || caixa.top < 0) campo.closest('tr').scrollIntoView({ block: 'nearest' });
  }

  function guardarRascunho(cartao) {
    var componente = componenteDe(cartao);
    // O que um registo enviou não é um rascunho.
    if (registoDe(componente)) return;
    var linhas = Array.prototype.map.call(cartao.querySelectorAll(NOVA), valores);
    while (linhas.length && !linhas[linhas.length - 1][0] && !linhas[linhas.length - 1][1]) linhas.pop();
    if (linhas.length) rascunhos[componente] = linhas;
    else delete rascunhos[componente];
  }

  function reporRascunho(cartao) {
    var guardado = rascunhos[componenteDe(cartao)];
    if (!guardado) return;
    var linhas = Array.prototype.slice.call(cartao.querySelectorAll(NOVA));
    if (!linhas.length) return;
    guardado.forEach(function (valores, i) {
      var linha = linhas[i] || (linhas[i] = maisLinha(cartao));
      if (!linha) return;
      // Um valor que o servidor devolveu (uma recusa) ganha ao rascunho.
      var campos = [linha.querySelector('[data-peg-costura]'), linha.querySelector('[data-peg-contra]'), linha.querySelector('[data-peg-numero]')];
      campos.forEach(function (campo, j) { if (campo && !campo.value && valores[j]) campo.value = valores[j]; });
      calcular(linha);
    });
  }

  // A LEITURA SEGUINTE SEM ESPERAR: o operador passa as leituras da folha de
  // papel e escreve a seguinte logo a seguir ao Enter, também depois de
  // corrigir uma leitura, de Esc ou do ×. Enquanto o cartão espera pela
  // resposta, as teclas escritas no campo onde o operador estava quando a
  // ação partiu (ou numa linha nova do cartão, enquanto um registo dele
  // espera: os campos dele ficam só de leitura) guardam-se numa fila de
  // leituras (Tab passa à contra costura, Enter fecha a leitura e começa a
  // seguinte). Quando o cartão volta sem registo à espera, a primeira leitura
  // escreve-se na linha nova como se tivesse sido escrita lá e, se tinha
  // Enter, regista-se, com as outras à espera da gravação seguinte. Se o
  // operador clicou noutro campo da linha nova durante a espera (a contra
  // costura), o que escreve a seguir vai para esse campo. Numa recusa, o que
  // se escreveu fica em linhas a seguir, sem registar. Sem registo à espera,
  // a linha nova escreve-se como sempre: Enter numa linha nova já escrita,
  // enquanto uma correção do mesmo cartão grava, regista (vai para a fila do
  // DmoTroca). As teclas escritas noutro sítio (outro cartão, outro campo)
  // não se tocam.
  function comecar(cartao, tipo, origem, envio) {
    var componente = componenteDe(cartao);
    var acao = { tipo: tipo, origem: origem, envio: envio == null ? null : envio };
    if (tipo === 'registar') {
      delete rascunhos[componente];
      acao.enviado = Array.prototype.map.call(cartao.querySelectorAll(NOVA), valores);
      travar(cartao, true);
    }
    (acoes[componente] = acoes[componente] || []).push(acao);
    if (!teclas[componente]) {
      var ativo = document.activeElement;
      teclas[componente] = { fila: [['', '']], campo: 0, inicio: ativo, alvo: ativo };
    }
  }

  // A tecla vai para a fila do cartão: escrita no campo onde o operador
  // estava quando a ação partiu, ou numa linha nova enquanto um registo do
  // cartão espera.
  function eDaFila(componente, alvo) {
    var fila = teclas[componente];
    if (!fila) return false;
    return alvo === fila.inicio || (registoDe(componente) !== null && !!alvo.matches && alvo.matches(NOVA + ' input'));
  }

  function guardarTecla(pendente, evento) {
    var tecla = evento.key;
    if (evento.ctrlKey || evento.metaKey || evento.altKey || evento.isComposing || !tecla) return;
    var alvo = evento.target;
    // O operador clicou noutro campo da linha nova: o clique ganha.
    if (alvo !== pendente.alvo) {
      pendente.alvo = alvo;
      if (alvo.matches(NOVA + ' [data-peg-contra]')) pendente.campo = 1;
      else if (alvo.matches(NOVA + ' [data-peg-costura]')) pendente.campo = 0;
    }
    var atual = pendente.fila[pendente.fila.length - 1];
    if (tecla === 'Enter') {
      // Sem nada escrito (um duplo Enter), não regista nada.
      if (atual[0] || atual[1]) {
        pendente.fila.push(['', '']);
        pendente.campo = 0;
      }
    } else if (tecla === 'Tab') pendente.campo = evento.shiftKey ? 0 : 1;
    else if (tecla === 'Backspace') atual[pendente.campo] = atual[pendente.campo].slice(0, -1);
    else if (tecla.length === 1) atual[pendente.campo] += tecla;
    else return;
    evento.preventDefault();
  }

  function escrever(campo, texto, focar) {
    if (!campo || !texto) return;
    if (focar) {
      campo.focus({ preventScroll: true });
      // Como ao escrever: um campo numérico filtra as teclas da mesma forma.
      try { if (document.execCommand('insertText', false, texto)) return; } catch (erro) { /* sem execCommand */ }
    }
    campo.value += texto;
    campo.dispatchEvent(new Event('input', { bubbles: true }));
  }

  // Escreve uma leitura da fila numa linha nova e devolve o campo onde o
  // operador estava a escrever (fica para o fim, com o cursor no fim).
  function continuar(linha, leitura, campo, focar) {
    var campos = [linha.querySelector('[data-peg-costura]'), linha.querySelector('[data-peg-contra]')];
    [1 - campo, campo].forEach(function (i) { escrever(campos[i], leitura[i], focar); });
    return campos[campo];
  }

  // As leituras da fila que não se registam agora ficam em linhas a seguir
  // (com «+ Linha»), à vista; devolve o campo da última.
  function emLinhas(cartao, fila, campo, focar) {
    var escritas = fila.filter(function (leitura) { return leitura[0] || leitura[1]; });
    var ultimo = null;
    escritas.forEach(function (leitura, i) {
      var linha = maisLinha(cartao);
      if (linha) ultimo = continuar(linha, leitura, i === escritas.length - 1 ? campo : 0, focar);
    });
    return ultimo;
  }

  // As linhas de um registo como foram enviadas (numa recusa, ou enquanto
  // ele espera e o cartão foi trocado por outra ação), em vez da só que o
  // servidor devolve.
  function reporEnviado(cartao, enviado) {
    var linhas = Array.prototype.slice.call(cartao.querySelectorAll(NOVA));
    if (!linhas.length) return;
    enviado.forEach(function (par, i) {
      var linha = linhas[i] || maisLinha(cartao);
      if (!linha) return;
      linha.querySelector('[data-peg-costura]').value = par[0];
      linha.querySelector('[data-peg-contra]').value = par[1];
      var numero = linha.querySelector('[data-peg-numero]');
      if (numero) numero.value = par[2] || '';
      calcular(linha);
    });
  }

  function escrito(fila) { return fila.length > 1 || fila[0][0] !== '' || fila[0][1] !== ''; }

  // O foco pode ir para um cartão trocado se o operador não foi para outro sítio.
  function podeFocar(cartao) {
    var ativo = document.activeElement;
    return !ativo || ativo === document.body || cartao.contains(ativo);
  }

  // O AVISO FICA À VISTA até o operador o fechar ou fazer a ação seguinte
  // nesse cartão (o DmoTroca tira do canto as mensagens do cartão quando a
  // resposta seguinte dele chega). Uma leitura escrita durante a espera e
  // registada agora, logo a seguir à resposta que trouxe o aviso, não é essa
  // ação seguinte: foi escrita antes de o aviso aparecer. O registo dela
  // parte sem levar os avisos do cartão.
  function semTirarAvisos(cartao, enviar) {
    var zona = cartao.closest('[data-dmo-regiao]');
    var avisos = zona ? Array.prototype.slice.call(document.querySelectorAll('.dmo-toast-pilha [data-dmo-toast="aviso"]'))
      .filter(function (aviso) { return aviso.getAttribute('data-dmo-origem') === zona.id; }) : [];
    avisos.forEach(function (aviso) { aviso.setAttribute('data-dmo-origem', zona.id + ':antes'); });
    try { enviar(); } finally { avisos.forEach(function (aviso) { aviso.setAttribute('data-dmo-origem', zona.id); }); }
  }

  document.addEventListener('input', function (evento) {
    var linha = evento.target.closest && evento.target.closest('[data-peg-linha-nova]');
    if (!linha || !linha.closest('[data-peg-registo]')) return;
    calcular(linha);
    if (linha.matches(NOVA)) guardarRascunho(cartaoDe(linha));
  });

  document.addEventListener('keydown', function (evento) {
    var campo = evento.target;
    var cartao = cartaoDe(campo);
    if (cartao && eDaFila(componenteDe(cartao), campo)) {
      guardarTecla(teclas[componenteDe(cartao)], evento);
      return;
    }
    var linha = campo.closest && campo.matches('input') ? campo.closest('[data-peg-linha-nova]') : null;
    if (!linha) return;
    if (evento.key === 'Enter') {
      evento.preventDefault();
      if (campo.form && campo.form.reportValidity()) campo.form.requestSubmit();
    } else if (evento.key === 'Escape' && linha.hasAttribute('data-peg-cancelar') && cartao) {
      medirContexto();
      comecar(cartao, 'cancelar', linha, null);
      if (window.DmoTroca) window.DmoTroca.ir(linha.getAttribute('data-peg-cancelar'), linha);
      else window.location.href = linha.getAttribute('data-peg-cancelar');
    }
  });

  // O × pede confirmação na janela da app (#peg-apagar-janela, fora das
  // zonas): «Apagar» fecha-a e carrega outra vez no mesmo ×, que envia como
  // sempre (o foco fica no ×, como sem a janela).
  var aApagar = null;
  var confirmado = null;

  function pedirConfirmacao(botao, evento) {
    evento.preventDefault();
    aApagar = botao;
    document.getElementById('peg-apagar-janela').showModal();
  }

  function confirmarApagar(janela) {
    janela.close();
    // Se o cartão trocou entretanto, o × é o da mesma leitura no cartão novo.
    var botao = aApagar && (aApagar.isConnected ? aApagar
      : document.querySelector('[data-peg-apagar][value="' + aApagar.value + '"]'));
    aApagar = null;
    if (!botao) return;
    confirmado = botao;
    botao.focus();
    botao.click();
  }

  document.addEventListener('click', function (evento) {
    var alvo = evento.target.closest ? evento.target : null;
    if (!alvo) return;
    var mais = alvo.closest('[data-peg-mais-linha]');
    if (mais && cartaoDe(mais)) {
      var nova = maisLinha(cartaoDe(mais));
      if (nova) nova.querySelector('[data-peg-costura]').focus();
      return;
    }
    var apagar = alvo.closest('[data-peg-apagar]');
    if (apagar) {
      if (apagar !== confirmado) pedirConfirmacao(apagar, evento);
      confirmado = null;
      return;
    }
    if (alvo.closest('[data-peg-apagar-confirmar]')) {
      confirmarApagar(alvo.closest('dialog'));
      return;
    }
    if (alvo.closest('a[href]') && cartaoDe(alvo)) medirContexto();
  });

  // Uma correção aberta noutro cartão fica aberta, com o que lá está escrito
  // (Diogo, 17/10/2026: «o que está escrito nos outros cartões fica»):
  // registar e apagar levam-na e o servidor volta com ela, por isso esse
  // cartão não muda. Também a que está a ser guardada ou cancelada: a fila
  // do DmoTroca é em série, por isso a resposta dela chega depois e fecha-a.
  function correcaoAberta(cartao) {
    var aberta = document.querySelector('tr[data-peg-cancelar][id^="peg-linha-"]');
    if (!aberta || cartao.contains(aberta)) return [];
    return [['editar_medicao', aberta.id.slice('peg-linha-'.length)],
      ['campo', aberta.querySelector('[data-peg-contra][autofocus]') ? 'contra'
        : aberta.querySelector('[data-peg-numero][autofocus]') ? 'numero' : 'costura']];
  }

  function levaCorrecao(form) { return cartaoDe(form) && /^peg-(registar|apagar)-/.test(form.id || ''); }

  // No envio (em captura, para ir no pedido, também no do browser).
  document.addEventListener('submit', function (evento) {
    var form = evento.target;
    if (!levaCorrecao(form)) return;
    form.querySelectorAll('[data-peg-correcao-aberta]').forEach(function (campo) { campo.remove(); });
    correcaoAberta(cartaoDe(form)).forEach(function (par) {
      var campo = document.createElement('input');
      campo.type = 'hidden';
      campo.name = par[0];
      campo.value = par[1];
      campo.setAttribute('data-peg-correcao-aberta', '');
      form.appendChild(campo);
    });
  }, true);

  // Um envio que esperou na fila do DmoTroca decide quando parte, já com as
  // respostas anteriores no ecrã: uma correção guardada entretanto não vai;
  // uma recusada (continua aberta) vai.
  document.addEventListener('dmo:enviar', function (evento) {
    var form = evento.detail && evento.detail.form;
    var corpo = evento.detail && evento.detail.corpo;
    if (!form || !corpo || !levaCorrecao(form) || !form.isConnected) return;
    corpo.delete('editar_medicao');
    corpo.delete('campo');
    correcaoAberta(cartaoDe(form)).forEach(function (par) { corpo.append(par[0], par[1]); });
  });

  // Registar, corrigir e apagar são formulários do cartão (os campos ligam-se
  // pelo atributo form). O envio em si é do DmoTroca, ou do browser.
  document.addEventListener('submit', function (evento) {
    if (cartaoDe(evento.target)) medirContexto();
  });

  // A ação do cartão começa quando o envio entra na fila do DmoTroca (um
  // envio igual a um que espera não entra: um segundo clique no mesmo × não
  // conta duas vezes).
  document.addEventListener('dmo:fila', function (evento) {
    var form = evento.detail && evento.detail.form;
    var cartao = cartaoDe(form);
    if (!cartao) return;
    var tipo = form.id === 'peg-registar-' + componenteDe(cartao) ? 'registar'
      : /^peg-editar-/.test(form.id || '') ? 'corrigir'
        : /^peg-apagar-/.test(form.id || '') ? 'apagar' : null;
    if (tipo) comecar(cartao, tipo, form, evento.detail.envio);
  });

  // A resposta não trocou o cartão (a ligação falhou e nada mudou, ou a
  // página vai abrir inteira): sem registo à espera, os campos voltam a
  // escrever-se e o que se escreveu entretanto fica em linhas a seguir, sem
  // registar, com o foco onde o operador escrevia (o resto do número que
  // ainda está a escrever continua lá, não no valor enviado).
  document.addEventListener('dmo:fim', function (evento) {
    var detalhe = evento.detail || {};
    Object.keys(acoes).forEach(function (componente) {
      var lista = acoes[componente];
      var i = indiceDe(lista, detalhe);
      if (i < 0) return;
      lista.splice(i, 1);
      if (!lista.length) delete acoes[componente];
      if (registoDe(componente)) return;
      var fila = teclas[componente];
      delete teclas[componente];
      var cartao = cartaoDoComponente(componente);
      if (!cartao) return;
      travar(cartao, false);
      if (fila) emLinhas(cartao, fila.fila, fila.campo, escrito(fila.fila) && podeFocar(cartao));
      guardarRascunho(cartao);
    });
  });

  document.addEventListener('dmo:troca', function (evento) {
    var detalhe = evento.detail || {};
    var zonas = (detalhe.zonas || [])
      .map(function (id) { return document.getElementById(id); })
      .filter(Boolean);
    if (zonas.some(function (zona) { return zona.id === 'pegContexto'; })) reservarContexto();
    // O operador escrevia numa correção que esta resposta fechou (registar ou
    // apagar no cartão da correção fecha-a) e ainda não a tinha guardado nem
    // cancelado: o que lá escreveu perde-se, como sempre, e o foco não salta
    // para a linha nova desse cartão (o resto do número ia para lá e o Enter
    // registava-o).
    var largado = detalhe.ativo && !detalhe.ativo.isConnected && detalhe.ativo.closest
      ? detalhe.ativo.closest('tr[data-peg-cancelar]') && cartaoDe(detalhe.ativo) : null;
    var largou = largado ? componenteDe(largado) : null;
    var largar = null;
    var foco = null;
    var gravou = null;
    var registar = null;
    zonas.forEach(function (zona) {
      preparar(zona);
      zona.querySelectorAll('[data-peg-component]').forEach(function (cartao) {
        var componente = componenteDe(cartao);
        // Só a ação a que esta resposta responde acaba aqui: as outras do
        // cartão continuam à espera da resposta delas.
        var lista = acoes[componente] || [];
        var i = indiceDe(lista, detalhe);
        var acao = i >= 0 ? lista.splice(i, 1)[0] : null;
        if (!lista.length) delete acoes[componente];
        var antes = contagem[componente];
        contagem[componente] = leituras(cartao);
        var linha = cartao.querySelector(NOVA);
        var registo = registoDe(componente);
        if (registo) {
          // Um registo do cartão ainda espera: a linha nova mostra o que ele
          // enviou (o DmoTroca repôs a primeira linha e marcou-a à espera),
          // só de leitura, e as teclas continuam a ir para a fila.
          if (linha) {
            reporEnviado(cartao, registo.enviado);
            travar(cartao, true);
          }
          return;
        }
        if (!acao || !linha) {
          reporRascunho(cartao);
          return;
        }
        var fila = teclas[componente] || { fila: [['', '']], campo: 0 };
        delete teclas[componente];
        var aMeio = largou === componente && acao.tipo !== 'corrigir' && acao.tipo !== 'cancelar'
          && !(acoes[componente] || []).some(function (outra) { return outra.tipo === 'corrigir' || outra.tipo === 'cancelar'; });
        if (aMeio) largar = cartao;
        var focar = podeFocar(cartao) && !aMeio;
        // Uma correção aberta pelo servidor (autofocus) fica com o foco que o
        // DmoTroca lhe deu.
        var aberta = cartao.querySelector('[autofocus]') !== null;
        var seguir = false;
        var focoCartao = null;
        if (acao.tipo === 'registar' && antes != null && contagem[componente] <= antes) {
          // Uma recusa: o que se escreveu entretanto fica em linhas a seguir.
          reporEnviado(cartao, acao.enviado);
          focoCartao = emLinhas(cartao, fila.fila, fila.campo, focar);
        } else if (acao.tipo === 'registar') {
          seguir = true;
        } else {
          reporRascunho(cartao);
          // Com a correção ainda aberta (uma recusa), o que se escreveu
          // entretanto fica na linha nova, sem registar.
          if (aberta) emLinhas(cartao, fila.fila, fila.campo, false);
          else seguir = escrito(fila.fila);
        }
        if (seguir) {
          focoCartao = continuar(linha, fila.fila[0], fila.fila.length > 1 ? 0 : fila.campo, focar);
          if (focar && acao.tipo === 'registar') gravou = focoCartao;
          // A primeira leitura da fila teve Enter: regista-se a seguir.
          if (fila.fila.length > 1) registar = { cartao: cartao, fila: fila.fila.slice(1), campo: fila.campo };
        }
        // O operador já escrevia na linha nova (o DmoTroca deu o foco ao
        // campo com o mesmo id): fica lá.
        var ativo = document.activeElement;
        var naLinha = ativo && cartao.contains(ativo) && ativo.matches(NOVA + ' input') ? ativo : null;
        if (!focoCartao && !aberta) focoCartao = naLinha || linha.querySelector('[data-peg-costura]');
        if (focar && focoCartao) foco = focoCartao;
        guardarRascunho(cartao);
      });
    });
    if (foco && document.activeElement !== foco) foco.focus({ preventScroll: true });
    else if (largar && !foco && largar.contains(document.activeElement) && !document.activeElement.closest('tr[data-peg-cancelar]')) {
      document.activeElement.blur();
    }
    // Enter escrito durante a espera: regista a leitura seguinte, e o resto
    // da fila espera pela gravação dela.
    if (registar) {
      var componente = componenteDe(registar.cartao);
      var form = registar.cartao.querySelector('form[id^="peg-registar-"]');
      if (form && form.reportValidity()) semTirarAvisos(registar.cartao, function () { form.requestSubmit(); });
      if (registoDe(componente) && teclas[componente]) {
        teclas[componente].fila = registar.fila;
        teclas[componente].campo = registar.campo;
      } else {
        emLinhas(registar.cartao, registar.fila, registar.campo, false);
        guardarRascunho(registar.cartao);
      }
    }
    // Cada medida obriga o browser a desenhar a tabela outra vez (centenas de
    // leituras numa folha): só no frame seguinte, quando ele já a ia desenhar.
    if (!gravou) return;
    var depois = function () { acompanhar(gravou); };
    if (window.requestAnimationFrame) window.requestAnimationFrame(depois);
    else window.setTimeout(depois, 0);
  });

  preparar(document);
  document.querySelectorAll('[data-peg-component]').forEach(function (cartao) {
    contagem[componenteDe(cartao)] = leituras(cartao);
    // Um rascunho que o servidor devolveu (uma recusa com a página inteira)
    // também volta se o cartão for trocado por outra ação.
    guardarRascunho(cartao);
  });
})();
