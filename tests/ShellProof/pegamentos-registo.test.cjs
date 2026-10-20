const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM, VirtualConsole } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');
const VOLTA = 'http://dmo.test/Controlo/Pegamentos?controlo_id=7';

// Os Pegamentos em miniatura: um cartão por zona (CM e BQ), as mensagens e o
// mapa. Os campos ligam-se aos formulários pelo atributo form, como na página.
function cartao(componente, leituras, { editar = null, campo = null, rascunho = null } = {}) {
  const [rc, rn] = rascunho && rascunho.componente === componente ? rascunho.valores : ['', ''];
  const t = componente === 'CM' ? 'Cm' : 'Bq';
  const linhas = leituras.map((l, i) => (editar === l.id
    ? `<tr id="peg-linha-${l.id}" class="peg-linha-nova peg-linha-editar" data-peg-linha-nova data-peg-cancelar="/Controlo/Pegamentos?controlo_id=7">
        <td>${i + 1}</td>
        <td><input id="editar-costura-${l.id}" name="costura" form="peg-editar-${l.id}" required${campo !== 'contra' ? ' autofocus' : ''} value="${l.c}" data-peg-costura></td>
        <td><input id="editar-contra-${l.id}" name="contra_costura" form="peg-editar-${l.id}"${campo === 'contra' ? ' autofocus' : ''} value="${l.n}" data-peg-contra></td>
        <td data-peg-oval>—</td><td data-peg-media>—</td><td>Enter guarda · Esc cancela</td><td></td></tr>`
    : `<tr id="peg-linha-${l.id}"><td>${i + 1}</td>
        <td>${l.c}<a class="peg-valor" id="valor-${l.id}" href="/Controlo/Pegamentos?controlo_id=7&editar_medicao=${l.id}&campo=costura#peg-linha-${l.id}"></a></td>
        <td>${l.n}</td><td></td><td></td><td></td>
        <td><button type="submit" form="peg-apagar-${componente}" name="measurement_id" value="${l.id}" id="apagar-${l.id}" data-peg-apagar>×</button></td></tr>`)).join('');
  const emEdicao = leituras.some((l) => l.id === editar);
  return `<div id="pegCartao${componente}" class="dmo-zona" data-dmo-regiao>
    <section data-peg-component="${componente}" data-dmo-parcial>
      <table data-peg-registo data-peg-tolerancia="0.20" data-peg-nominal-valor="${componente === 'CM' ? '70.00' : '25.00'}"><tbody>${linhas}
        <tr class="peg-linha-nova" data-peg-linha-nova><td>${leituras.length + 1}</td>
          <td><input id="costura-${t}" name="costura" form="peg-registar-${componente}" required data-peg-costura value="${rc}"></td>
          <td><input id="contra-${t}" name="contra_costura" form="peg-registar-${componente}" data-peg-contra value="${rn}"></td>
          <td data-peg-oval>—</td><td data-peg-media>—</td><td>Enter regista</td><td></td></tr>
      </tbody></table>
      <button type="button" class="peg-mais-linha" data-peg-mais-linha hidden>+ Linha</button>
      <form id="peg-registar-${componente}" method="post" action="/Controlo/Pegamentos?handler=RegisterMeasurement">
        <input type="hidden" name="__RequestVerificationToken" value="token-${leituras.length}">
        <input type="hidden" name="controlo_id" value="7"><input type="hidden" name="component" value="${componente}">
        <button type="submit" tabindex="-1">Registar medição</button></form>
      <form id="peg-apagar-${componente}" method="post" action="/Controlo/Pegamentos?handler=DeleteMeasurement"><input type="hidden" name="controlo_id" value="7"></form>
      ${emEdicao ? `<form id="peg-editar-${editar}" method="post" action="/Controlo/Pegamentos?handler=EditMeasurement"><input type="hidden" name="controlo_id" value="7"><input type="hidden" name="measurement_id" value="${editar}"><button type="submit" tabindex="-1">Guardar</button></form>` : ''}
    </section></div>`;
}

function pagina(estado, opcoes = {}) {
  // O contexto por cima dos cartões: numa folha em branco traz a dica, que sai na primeira medição.
  return `<!doctype html><title>Pegamentos</title><body><main data-dmo-troca><h1 id="titulo">Pegamentos</h1>
    <div id="pegContexto" class="dmo-zona" data-dmo-regiao><section class="dmo-card peg-card"><div class="peg-context-notes">${estado.branco ? '<p class="peg-hint">Ainda sem medições nesta produção.</p>' : ''}</div></section></div>
    <div id="pegAvisos" class="dmo-zona" data-dmo-regiao>${opcoes.aviso || ''}</div>
    ${cartao('CM', estado.CM, opcoes)}${cartao('BQ', estado.BQ, opcoes)}
    <div id="pegMapa" class="dmo-zona" data-dmo-regiao><p id="mapa">${estado.CM.length + estado.BQ.length} medições</p></div></main>
    <dialog id="peg-apagar-janela" class="dmo-dialog"><p>Apagar esta leitura?</p>
      <button type="button" id="apagar-cancelar" data-dmo-dialog-close>Cancelar</button>
      <button type="button" id="apagar-confirmar" data-peg-apagar-confirmar>Apagar</button></dialog></body>`;
}

// O servidor em miniatura: grava, corrige e apaga e volta à folha (PRG).
function servidor({ branco = false } = {}) {
  const estado = branco ? { CM: [], BQ: [], proximo: 3, branco: true } : { CM: [{ id: 1, c: '70.10', n: '69.90' }], BQ: [{ id: 2, c: '25.00', n: '' }], proximo: 3 };
  return {
    estado,
    responder(url, opcoes = {}) {
      const endereco = new URL(url, VOLTA);
      if (opcoes.method === 'POST') {
        const corpo = new URLSearchParams(String(opcoes.body));
        const handler = endereco.searchParams.get('handler');
        const id = Number(corpo.get('measurement_id'));
        const procurar = () => ['CM', 'BQ'].map((c) => estado[c]).find((lista) => lista.some((l) => l.id === id));
        // A correção aberta noutro cartão volta aberta (o servidor real faz o mesmo).
        const aberta = Number(corpo.get('editar_medicao')) || null;
        const volta = aberta ? `${VOLTA}&editar_medicao=${aberta}&campo=${corpo.get('campo')}` : VOLTA;
        if (handler === 'RegisterMeasurement') {
          const contras = corpo.getAll('contra_costura');
          const linhas = corpo.getAll('costura').map((c, i) => [c, contras[i] || '']).filter(([c, n]) => c || n);
          // Como o servidor: uma linha sem costura recusa tudo e só ela volta.
          const ilegivel = linhas.find(([c]) => !c);
          if (ilegivel) {
            const recusa = `${volta}&measurement_refusal=InvalidMeasurementValue&draft_component=${corpo.get('component')}`;
            return { url: recusa, body: pagina(estado, { editar: aberta, campo: corpo.get('campo'), aviso: '<div data-dmo-toast="erro" role="alert">Indique a costura. Os valores introduzidos foram mantidos.</div>', rascunho: { componente: corpo.get('component'), valores: ilegivel } }) };
          }
          // Uma contra costura ilegível não recusa: grava a costura e avisa
          // (o aviso fica no canto, como na página: data-dmo-toast="aviso").
          const legivel = (n) => !n || /^\d+([.,]\d+)?$/.test(n);
          linhas.forEach(([c, n]) => estado[corpo.get('component')].push({ id: estado.proximo++, c, n: legivel(n) ? n : '' }));
          estado.branco = false;
          if (linhas.some(([, n]) => !legivel(n))) {
            const aviso = '<div data-dmo-toast="aviso" class="peg-aviso warn" role="status">Medição registada sem contra costura: o valor escrito não é um número legível.</div>';
            return { url: `${volta}&measurement_notice=ContraCosturaIgnored&notice_component=${corpo.get('component')}`, body: pagina(estado, { editar: aberta, campo: corpo.get('campo'), aviso }) };
          }
          return { url: volta, body: pagina(estado, { editar: aberta, campo: corpo.get('campo') }) };
        } else if (handler === 'EditMeasurement') {
          if (!/^\d+([.,]\d+)?$/.test(corpo.get('costura'))) {
            const recusa = `${VOLTA}&editar_medicao=${id}&correcao=valor-invalido`;
            return { url: recusa, body: pagina(estado, { editar: id, aviso: '<div data-dmo-toast="erro" role="alert">Indique a costura com um valor numérico.</div>' }) };
          }
          Object.assign(procurar().find((l) => l.id === id), { c: corpo.get('costura'), n: corpo.get('contra_costura') || '' });
        } else if (handler === 'DeleteMeasurement') {
          const lista = procurar();
          lista.splice(lista.findIndex((l) => l.id === id), 1);
          return { url: volta, body: pagina(estado, { editar: aberta, campo: corpo.get('campo') }) };
        }
        return { url: VOLTA, body: pagina(estado) };
      }
      const editar = Number(endereco.searchParams.get('editar_medicao')) || null;
      return { url: endereco.href, body: pagina(estado, { editar, campo: endereco.searchParams.get('campo') }) };
    },
  };
}

// A troca acontece quando o fetch responde; o que o script deixa para o
// frame seguinte (setTimeout 0 no jsdom) corre a seguir: espera-se pelos dois.
const esperar = () => new Promise((resolve) => setTimeout(resolve, 20)).then(() => new Promise((resolve) => setTimeout(resolve, 0)));

async function abrirPegamentos({ atrasar = false, branco = false, falhar = null } = {}) {
  const navegou = [];
  const pendentes = [];
  const virtualConsole = new VirtualConsole();
  virtualConsole.on('jsdomError', (error) => { if (/navigation|submit/i.test(error.message)) navegou.push(error.message); });
  const s = servidor({ branco });
  const dom = new JSDOM(pagina(s.estado), { url: VOLTA, runScripts: 'outside-only', virtualConsole });
  const { window } = dom;
  const pedidos = [];
  window.fetch = async (url, opcoes = {}) => {
    pedidos.push({ url: String(url), metodo: opcoes.method || 'GET', corpo: String(opcoes.body || '') });
    // Com atraso, o servidor só responde quando o teste o solta (a latência do DEV).
    if (atrasar) await new Promise((soltar) => pendentes.push(soltar));
    // Uma falha de rede: o fetch rejeita, como sem ligação. 'depois': o
    // servidor trata o pedido e a ligação cai antes de a resposta chegar.
    const falha = falhar && falhar(String(url), opcoes);
    if (falha && falha !== 'depois') throw new TypeError('Failed to fetch');
    const r = s.responder(String(url), opcoes);
    if (falha === 'depois') throw new TypeError('Failed to fetch');
    const pedido = new URL(String(url), window.location.href);
    pedido.hash = '';
    return {
      ok: true,
      url: r.url,
      redirected: r.url !== pedido.href,
      headers: { get: (nome) => (nome.toLowerCase() === 'content-type' ? 'text/html; charset=utf-8' : null) },
      text: async () => r.body,
    };
  };
  // O jsdom não tem showModal: abrir e fechar mexem só no atributo open.
  window.HTMLDialogElement.prototype.showModal = function () { this.setAttribute('open', ''); };
  window.HTMLDialogElement.prototype.close = function () { this.removeAttribute('open'); };
  window.rolagens = [];
  window.Element.prototype.scrollIntoView = function () { window.rolagens.push(this.id || this.tagName); };
  window.scrollBy = (x, y) => window.rolagens.push(`scrollBy ${y}`);
  window.eval(fs.readFileSync(path.join(assets, '0_ASSET_CANONICAL_INTERACTIONS.js'), 'utf8'));
  window.eval(fs.readFileSync(path.join(assets, 'dmo-live.js'), 'utf8'));
  window.eval(fs.readFileSync(path.join(assets, 'js/pegamentos-registo.js'), 'utf8'));
  // Os envios seguem em fila no DmoTroca: o fetch de um envio parte numa
  // tarefa a seguir ao submit. Primeiro espera-se que os pedidos em espera
  // cheguem ao servidor; depois soltam-se e espera-se pelas respostas.
  const soltar = async () => { await esperar(); while (pendentes.length) pendentes.shift()(); await esperar(); };
  return { dom, window, document: window.document, pedidos, navegou, soltar, estado: s.estado };
}

// O × abre a janela da app e «Apagar» confirma.
function apagar(document, id) {
  document.getElementById(`apagar-${id}`).click();
  assert.equal(document.getElementById('peg-apagar-janela').open, true, 'o × abre a janela de confirmação');
  document.getElementById('apagar-confirmar').click();
}

function escrever(window, campo, valor) {
  campo.focus();
  campo.value = valor;
  campo.dispatchEvent(new window.Event('input', { bubbles: true }));
}

function tecla(window, campo, key) {
  campo.focus();
  campo.dispatchEvent(new window.KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }));
}

// Escrever como no browser, tecla a tecla, onde estiver o foco: o keydown
// pode ser cancelado; um campo só de leitura não muda.
function teclar(window, texto) {
  for (const key of texto) {
    const campo = window.document.activeElement;
    const evento = new window.KeyboardEvent('keydown', { key: key === '\t' ? 'Tab' : key === '\n' ? 'Enter' : key, bubbles: true, cancelable: true });
    const seguir = campo.dispatchEvent(evento);
    if (!seguir || key === '\n') continue;
    if (key === '\t') {
      const campos = Array.from(window.document.querySelectorAll('input:not([type=hidden])'));
      campos[campos.indexOf(campo) + 1].focus();
    } else if (!campo.readOnly) {
      campo.value += key;
      campo.dispatchEvent(new window.Event('input', { bubbles: true }));
    }
  }
}

const novas = (document, componente) => Array.from(document.querySelectorAll(`[data-peg-component="${componente}"] [data-peg-linha-nova]:not([data-peg-cancelar])`), (l) => [l.querySelector('[data-peg-costura]').value, l.querySelector('[data-peg-contra]').value]);

const leituras = (document, componente) => document.querySelectorAll(`[data-peg-component="${componente}"] tbody tr:not([data-peg-linha-nova])`).length;

test('Enter numa leitura nova grava e troca só esse cartão, com o foco na costura da próxima leitura', async () => {
  const { dom, window, document, pedidos, navegou } = await abrirPegamentos();
  try {
    const titulo = document.getElementById('titulo');
    const bq = document.querySelector('[data-peg-component="BQ"]');
    escrever(window, document.getElementById('costura-Bq'), '25.02');
    escrever(window, document.getElementById('costura-Cm'), '70.05');
    escrever(window, document.getElementById('contra-Cm'), '69.95');
    assert.equal(document.querySelector('[data-peg-component="CM"] [data-peg-linha-nova] [data-peg-media]').textContent, '70.00', 'a média calcula-se ao escrever');
    tecla(window, document.getElementById('contra-Cm'), 'Enter');
    await esperar();
    assert.deepEqual(navegou, []);
    assert.deepEqual(pedidos.map((p) => p.metodo), ['POST']);
    const corpo = new URLSearchParams(pedidos[0].corpo);
    assert.deepEqual([corpo.get('component'), corpo.get('costura'), corpo.get('contra_costura')], ['CM', '70.05', '69.95']);
    assert.equal(leituras(document, 'CM'), 2, 'a leitura gravada aparece');
    assert.equal(document.getElementById('mapa').textContent, '3 medições', 'o mapa troca');
    assert.equal(document.activeElement.id, 'costura-Cm', 'o foco vai para a costura da próxima leitura');
    assert.equal(document.getElementById('costura-Cm').value, '');
    assert.equal(document.getElementById('titulo'), titulo, 'o resto da página fica');
    assert.equal(document.querySelector('[data-peg-component="BQ"]'), bq, 'o cartão do BQ não é trocado');
    assert.equal(document.getElementById('costura-Bq').value, '25.02', 'o que estava escrito no BQ fica');
    assert.equal(window.location.href, VOLTA);
  } finally {
    dom.window.close();
  }
});

test('no cartão trocado, as contas, o Enter e o «+ Linha» continuam a funcionar', async () => {
  const { dom, window, document, pedidos } = await abrirPegamentos();
  try {
    escrever(window, document.getElementById('costura-Cm'), '70.05');
    tecla(window, document.getElementById('costura-Cm'), 'Enter');
    await esperar();
    const mais = document.querySelector('[data-peg-component="CM"] [data-peg-mais-linha]');
    assert.equal(mais.hidden, false, 'o «+ Linha» aparece no cartão novo');
    escrever(window, document.getElementById('costura-Cm'), '70.40');
    escrever(window, document.getElementById('contra-Cm'), '70.00');
    const nova = document.querySelector('[data-peg-component="CM"] [data-peg-linha-nova]');
    assert.equal(nova.querySelector('[data-peg-oval]').textContent, '+0.40');
    assert.ok(nova.querySelector('[data-peg-oval]').classList.contains('peg-cell-bad'), 'a ovalização fora da tolerância');
    mais.click();
    const linhas = document.querySelectorAll('[data-peg-component="CM"] [data-peg-linha-nova]');
    assert.equal(linhas.length, 2);
    assert.equal(linhas[1].cells[0].textContent, '4');
    assert.equal(document.activeElement, linhas[1].querySelector('[data-peg-costura]'));
    escrever(window, linhas[1].querySelector('[data-peg-costura]'), '70.10');
    tecla(window, linhas[1].querySelector('[data-peg-costura]'), 'Enter');
    tecla(window, linhas[1].querySelector('[data-peg-costura]'), 'Enter');
    await esperar();
    assert.equal(pedidos.filter((p) => p.metodo === 'POST').length, 2, 'um POST por gravação, mesmo com Enter duas vezes');
    assert.equal(leituras(document, 'CM'), 4, 'as duas linhas gravam-se de uma vez');
    assert.equal(document.querySelectorAll('[data-peg-component="CM"] [data-peg-linha-nova]').length, 1);
    assert.equal(document.activeElement.id, 'costura-Cm');
  } finally {
    dom.window.close();
  }
});

test('corrigir e apagar trocam o cartão sem perder o que estava escrito na linha nova', async () => {
  const { dom, window, document, pedidos, navegou } = await abrirPegamentos();
  try {
    escrever(window, document.getElementById('costura-Cm'), '70.11');
    escrever(window, document.getElementById('costura-Bq'), '25.02');
    // Abrir a correção: um link dentro do cartão (GET), com o foco no valor.
    document.getElementById('valor-1').click();
    await esperar();
    assert.deepEqual(navegou, []);
    assert.equal(document.activeElement.id, 'editar-costura-1');
    assert.equal(document.getElementById('costura-Cm').value, '70.11', 'o rascunho volta à linha nova');
    escrever(window, document.getElementById('editar-costura-1'), '70.20');
    tecla(window, document.getElementById('editar-costura-1'), 'Enter');
    await esperar();
    assert.equal(document.getElementById('editar-costura-1'), null, 'a correção fecha');
    assert.match(document.getElementById('peg-linha-1').textContent, /70\.20/);
    assert.equal(document.activeElement.id, 'costura-Cm');
    assert.equal(document.getElementById('costura-Cm').value, '70.11');
    assert.equal(window.location.href, VOLTA);
    // Esc cancela a correção sem recarregar.
    document.getElementById('valor-1').click();
    await esperar();
    tecla(window, document.getElementById('editar-costura-1'), 'Escape');
    await esperar();
    assert.deepEqual(navegou, []);
    assert.equal(document.getElementById('editar-costura-1'), null);
    assert.equal(document.activeElement.id, 'costura-Cm');
    // Apagar com o ×.
    apagar(document, 1);
    await esperar();
    assert.equal(leituras(document, 'CM'), 0);
    assert.equal(new URLSearchParams(pedidos.at(-1).corpo).get('measurement_id'), '1');
    assert.equal(document.getElementById('costura-Cm').value, '70.11');
    assert.equal(document.getElementById('costura-Bq').value, '25.02');
    assert.equal(document.activeElement.id, 'costura-Cm');
  } finally {
    dom.window.close();
  }
});

test('o × pede confirmação: cancelar não envia nada', async () => {
  const { dom, window, document, pedidos } = await abrirPegamentos();
  try {
    document.getElementById('apagar-1').click();
    assert.equal(document.getElementById('peg-apagar-janela').open, true);
    document.getElementById('apagar-cancelar').click();
    await esperar();
    assert.equal(document.getElementById('peg-apagar-janela').open, false);
    assert.equal(pedidos.length, 0);
    assert.equal(leituras(document, 'CM'), 1);
  } finally {
    dom.window.close();
  }
});

test('uma recusa da correção mostra a mensagem e deixa a correção aberta, com o foco no valor', async () => {
  const { dom, window, document, navegou } = await abrirPegamentos();
  try {
    document.getElementById('valor-1').click();
    await esperar();
    escrever(window, document.getElementById('editar-costura-1'), 'abc');
    tecla(window, document.getElementById('editar-costura-1'), 'Enter');
    await esperar();
    assert.deepEqual(navegou, []);
    assert.match(document.querySelector('.dmo-toast-pilha [data-dmo-toast="erro"]').textContent, /valor numérico/);
    assert.equal(document.activeElement.id, 'editar-costura-1');
  } finally {
    dom.window.close();
  }
});

test('a leitura seguinte, escrita enquanto a anterior grava, chega inteira à linha nova', async () => {
  // O operador passa as leituras da folha de papel: escreve a seguinte logo
  // a seguir ao Enter, antes de o servidor responder. Nunca pode ficar meio
  // número num campo (gravava-se 6 em vez de 70.06).
  const { dom, window, document, pedidos, soltar, estado } = await abrirPegamentos({ atrasar: true });
  try {
    document.getElementById('costura-Cm').focus();
    teclar(window, '70.05\n');
    await esperar();
    assert.equal(pedidos.length, 1, 'o envio partiu e espera pelo servidor');
    teclar(window, '70.0');
    assert.equal(document.getElementById('costura-Cm').value, '70.05', 'o campo enviado não muda durante o envio');
    await soltar();
    teclar(window, '6');
    assert.equal(document.getElementById('costura-Cm').value, '70.06', 'o número chega inteiro à linha nova');
    assert.equal(document.activeElement.id, 'costura-Cm');
    assert.deepEqual(estado.CM.map((l) => l.c), ['70.10', '70.05']);
    assert.deepEqual(pedidos.map((p) => p.metodo), ['POST']);
  } finally {
    dom.window.close();
  }
});

test('Tab e Enter escritos durante o envio passam à contra costura e registam a leitura seguinte', async () => {
  const { dom, window, document, pedidos, soltar, estado } = await abrirPegamentos({ atrasar: true });
  try {
    document.getElementById('costura-Cm').focus();
    teclar(window, '70.05\n70.06\t69.96\n');
    await soltar();
    assert.deepEqual(pedidos.map((p) => p.metodo), ['POST', 'POST'], 'o Enter escrito durante o envio regista a seguinte, uma vez');
    const segundo = new URLSearchParams(pedidos[1].corpo);
    assert.deepEqual([segundo.get('costura'), segundo.get('contra_costura')], ['70.06', '69.96']);
    await soltar();
    assert.deepEqual(estado.CM.map((l) => [l.c, l.n]), [['70.10', '69.90'], ['70.05', ''], ['70.06', '69.96']]);
    assert.deepEqual(novas(document, 'CM'), [['', '']]);
    assert.equal(document.activeElement.id, 'costura-Cm');
    assert.equal(pedidos.length, 2);
  } finally {
    dom.window.close();
  }
});

test('com o servidor lento, várias leituras escritas durante o mesmo envio registam-se uma a uma, sem se juntarem', async () => {
  const { dom, window, document, pedidos, soltar, estado } = await abrirPegamentos({ atrasar: true });
  try {
    document.getElementById('costura-Cm').focus();
    teclar(window, '70.05\n70.06\n\n70.07');
    await soltar();
    assert.equal(document.getElementById('costura-Cm').value, '70.06', 'a segunda leitura está a ser enviada');
    assert.equal(document.getElementById('costura-Cm').readOnly, true);
    teclar(window, '1');
    await soltar();
    assert.deepEqual(estado.CM.map((l) => l.c), ['70.10', '70.05', '70.06'], 'cada leitura uma vez, sem o duplo Enter');
    assert.deepEqual(novas(document, 'CM'), [['70.071', '']], 'a terceira fica inteira na linha nova, por registar');
    assert.equal(document.activeElement.id, 'costura-Cm');
    assert.equal(pedidos.length, 2);
  } finally {
    dom.window.close();
  }
});

test('uma recusa com «+ Linha» devolve todas as linhas enviadas, e uma gravação noutro cartão não as apaga', async () => {
  const { dom, window, document, estado } = await abrirPegamentos();
  try {
    escrever(window, document.getElementById('costura-Cm'), '70.07');
    escrever(window, document.getElementById('contra-Cm'), '69.97');
    document.querySelector('[data-peg-component="CM"] [data-peg-mais-linha]').click();
    const segunda = document.querySelectorAll('[data-peg-component="CM"] [data-peg-linha-nova]')[1];
    escrever(window, segunda.querySelector('[data-peg-contra]'), '69.80');
    tecla(window, segunda.querySelector('[data-peg-contra]'), 'Enter');
    await esperar();
    assert.match(document.querySelector('.dmo-toast-pilha [data-dmo-toast="erro"]').textContent, /mantidos/);
    assert.equal(estado.CM.length, 1, 'nada foi gravado');
    assert.deepEqual(novas(document, 'CM'), [['70.07', '69.97'], ['', '69.80']], 'as linhas voltam como foram enviadas');
    escrever(window, document.getElementById('costura-Bq'), '25.01');
    tecla(window, document.getElementById('costura-Bq'), 'Enter');
    await esperar();
    assert.equal(estado.BQ.length, 2);
    assert.deepEqual(novas(document, 'CM'), [['70.07', '69.97'], ['', '69.80']], 'gravar no BQ não apaga o que estava no CM');
  } finally {
    dom.window.close();
  }
});

test('numa recusa, o que se escreveu durante o envio fica numa linha a seguir às enviadas, sem registar', async () => {
  const { dom, window, document, pedidos, soltar, estado } = await abrirPegamentos({ atrasar: true });
  try {
    escrever(window, document.getElementById('costura-Cm'), '70.07');
    document.querySelector('[data-peg-component="CM"] [data-peg-mais-linha]').click();
    const segunda = document.querySelectorAll('[data-peg-component="CM"] [data-peg-linha-nova]')[1];
    escrever(window, segunda.querySelector('[data-peg-contra]'), '69.80');
    teclar(window, '\n70.08\n');
    await soltar();
    assert.equal(estado.CM.length, 1, 'nada foi gravado');
    assert.deepEqual(novas(document, 'CM'), [['70.07', ''], ['', '69.80'], ['70.08', '']]);
    assert.equal(pedidos.length, 1, 'o Enter escrito durante uma recusa não envia outra vez');
  } finally {
    dom.window.close();
  }
});

test('corrigir, cancelar e apagar não mexem no ecrã; registar acompanha a linha nova', async () => {
  const { dom, window, document } = await abrirPegamentos();
  try {
    // As linhas novas ficam abaixo do ecrã (uma tabela longa).
    window.HTMLInputElement.prototype.getBoundingClientRect = () => ({ top: 2000, bottom: 2030, left: 0, right: 100, width: 100, height: 30 });
    document.getElementById('valor-1').click();
    await esperar();
    tecla(window, document.getElementById('editar-costura-1'), 'Enter');
    await esperar();
    document.getElementById('valor-1').click();
    await esperar();
    tecla(window, document.getElementById('editar-costura-1'), 'Escape');
    await esperar();
    apagar(document, 1);
    await esperar();
    assert.deepEqual(window.rolagens, [], 'o ecrã fica onde estava');
    assert.equal(document.activeElement.id, 'costura-Cm', 'o cursor fica na costura da próxima leitura');
    escrever(window, document.getElementById('costura-Cm'), '70.05');
    tecla(window, document.getElementById('costura-Cm'), 'Enter');
    await esperar();
    assert.deepEqual(window.rolagens, ['TR'], 'depois de registar, o ecrã acompanha a linha nova');
  } finally {
    dom.window.close();
  }
});

// O layout em miniatura (o jsdom não desenha): o contexto tem 200 px mais as
// notas (34 px com a dica da folha em branco), cada um com o min-height que o
// script lhe der; os cartões começam por baixo do contexto. Assim o teste mede
// o efeito (onde fica o cartão), não só o que o script chamou.
function desenhar(window) {
  const { document } = window;
  const altura = (el, conteudo) => Math.max(conteudo, parseFloat(el.style.minHeight) || 0);
  const notas = () => { const n = document.querySelector('#pegContexto .peg-context-notes'); return altura(n, n.querySelector('.peg-hint') ? 34 : 0); };
  const contexto = () => altura(document.querySelector('#pegContexto .peg-card'), 200 + notas());
  window.HTMLElement.prototype.getBoundingClientRect = function () {
    let top = 0;
    let height = 0;
    if (this.matches('#pegContexto .peg-context-notes')) { top = 280; height = notas(); }
    else if (this.matches('#pegContexto .peg-card')) { top = 100; height = contexto(); }
    else if (this.matches('[data-peg-component]')) { top = 100 + contexto() + 16; height = 300; }
    return { top, bottom: top + height, height, left: 0, right: 100, width: 100 };
  };
  return () => Math.round(document.querySelector('[data-peg-component="BQ"]').getBoundingClientRect().top);
}

test('na primeira medição de uma folha em branco, a dica sai mas os cartões não sobem, com a página no topo', async () => {
  // A dica da folha em branco (34 px por cima dos cartões) sai na primeira
  // medição. Com a página no topo não há scroll para compensar: o espaço
  // do contexto tem de ficar.
  const { dom, window, document, estado } = await abrirPegamentos({ branco: true });
  try {
    const topoDoCartao = desenhar(window);
    assert.ok(document.querySelector('.peg-hint'), 'a folha em branco traz a dica');
    const antes = topoDoCartao();
    escrever(window, document.getElementById('costura-Bq'), '25.02');
    tecla(window, document.getElementById('costura-Bq'), 'Enter');
    await esperar();
    assert.equal(estado.BQ.length, 1);
    assert.equal(document.querySelector('.peg-hint'), null, 'a dica saiu (o contexto foi trocado)');
    assert.equal(topoDoCartao(), antes, 'o cartão fica no mesmo sítio');
    assert.equal(window.pageYOffset, 0);
    assert.deepEqual(window.rolagens.filter((r) => String(r).startsWith('scrollBy')), [], 'sem mexer no scroll');
  } finally {
    dom.window.close();
  }
});

test('dois envios em fila: a resposta do primeiro não gasta o registo que espera; a linha nova dele fica com o foco e à vista', async () => {
  // Uma correção do CM e logo a seguir uma leitura do BQ, os dois à espera
  // do servidor. A linha nova do BQ está no fundo do ecrã.
  const { dom, window, document, pedidos, soltar, estado } = await abrirPegamentos({ atrasar: true });
  try {
    document.getElementById('valor-1').click();
    await soltar();
    window.HTMLInputElement.prototype.getBoundingClientRect = function () {
      return this.id === 'costura-Bq' ? { top: 880, bottom: 910, left: 0, right: 100, width: 100, height: 30 } : { top: 300, bottom: 330, left: 0, right: 100, width: 100, height: 30 };
    };
    window.innerHeight = 900;
    escrever(window, document.getElementById('editar-costura-1'), '70.20');
    tecla(window, document.getElementById('editar-costura-1'), 'Enter');
    escrever(window, document.getElementById('costura-Bq'), '25.02');
    tecla(window, document.getElementById('costura-Bq'), 'Enter');
    await soltar();
    assert.match(document.getElementById('peg-linha-1').textContent, /70\.20/, 'a correção do CM gravou');
    await soltar();
    assert.deepEqual(pedidos.filter((p) => p.metodo === 'POST').length, 2);
    assert.deepEqual(estado.BQ.map((l) => l.c), ['25.00', '25.02']);
    assert.equal(document.activeElement.id, 'costura-Bq', 'o foco fica na linha nova do BQ');
    assert.deepEqual(window.rolagens, ['TR'], 'e o ecrã acompanha-a');
  } finally {
    dom.window.close();
  }
});

for (const [caso, acao] of [
  ['corrigir uma leitura (Enter guarda)', (window, document) => {
    escrever(window, document.getElementById('editar-costura-1'), '70.20');
    tecla(window, document.getElementById('editar-costura-1'), 'Enter');
  }],
  ['cancelar a correção com Esc', (window, document) => tecla(window, document.getElementById('editar-costura-1'), 'Escape')],
  ['apagar com o ×', (window, document) => {
    // No browser, o clique no × dá-lhe o foco.
    document.getElementById('apagar-1').focus();
    apagar(document, 1);
  }],
]) {
  test(`depois de ${caso}, a leitura seguinte escrita durante o pedido chega inteira à linha nova e regista-se uma vez`, async () => {
    const { dom, window, document, pedidos, soltar, estado } = await abrirPegamentos({ atrasar: true });
    try {
      if (!caso.startsWith('apagar')) {
        document.getElementById('valor-1').click();
        await soltar();
        assert.equal(document.activeElement.id, 'editar-costura-1');
      }
      acao(window, document);
      await esperar();
      const enviados = pedidos.length;
      teclar(window, '70.06\n');
      await soltar();
      assert.equal(pedidos.length, enviados + 1, 'o Enter escrito durante o pedido regista a leitura, uma vez');
      await soltar();
      assert.deepEqual(estado.CM.slice(-1).map((l) => l.c), ['70.06'], 'gravado inteiro');
      assert.equal(estado.CM.filter((l) => l.c === '70.06').length, 1);
      assert.deepEqual(novas(document, 'CM'), [['', '']]);
      assert.equal(document.activeElement.id, 'costura-Cm');
    } finally {
      dom.window.close();
    }
  });
}

test('um clique na contra costura durante o envio: o que se escreve a seguir vai para a contra costura da leitura seguinte', async () => {
  const { dom, window, document, soltar, estado } = await abrirPegamentos({ atrasar: true });
  try {
    document.getElementById('costura-Cm').focus();
    teclar(window, '70.51\n');
    await esperar();
    // O operador clica na contra costura (que faltou) e escreve.
    document.getElementById('contra-Cm').focus();
    teclar(window, '69.95');
    await soltar();
    assert.deepEqual(estado.CM.map((l) => [l.c, l.n]), [['70.10', '69.90'], ['70.51', '']]);
    assert.deepEqual(novas(document, 'CM'), [['', '69.95']], 'na contra costura, não na costura');
    assert.equal(document.activeElement.id, 'contra-Cm');
  } finally {
    dom.window.close();
  }
});

// Sem ligação, o registo espera que ela volte (deep sweep, 18/10/2026,
// B-pegamentos-3): a confirmação (um GET) repete-se quando a ligação regressa.
// Enquanto espera, a linha enviada fica só de leitura e as teclas vão para a
// fila do cartão, como com o servidor lento.
const ligacaoVolta = (window, rede) => { rede.cortada = false; window.dispatchEvent(new window.Event('online')); };

test('sem ligação, o registo espera que ela volte; sem nada gravado, os campos voltam a escrever-se e o que se escreveu entretanto fica numa linha a seguir', async () => {
  const rede = { cortada: true };
  const { dom, window, document, pedidos, soltar } = await abrirPegamentos({ atrasar: true, falhar: () => rede.cortada });
  try {
    window.DmoTroca.tempos.confirmar = 60000;
    document.getElementById('costura-Cm').focus();
    teclar(window, '70.05\n');
    await esperar();
    teclar(window, '70.06');
    await soltar();
    await soltar();
    assert.deepEqual(pedidos.map((p) => p.metodo), ['POST', 'GET'], 'o POST não se repete; a confirmação também falha');
    assert.match(document.querySelector('.dmo-toast-pilha [data-dmo-toast="erro"]').textContent, /não foi possível confirmar/);
    assert.equal(document.getElementById('costura-Cm').readOnly, true, 'sem confirmar, o registo continua à espera');
    assert.deepEqual(novas(document, 'CM'), [['70.05', '']]);
    ligacaoVolta(window, rede);
    await soltar();
    assert.deepEqual(pedidos.map((p) => p.metodo), ['POST', 'GET', 'GET'], 'a ligação voltou: confirma-se com um GET, sem repetir o POST');
    assert.equal(leituras(document, 'CM'), 1, 'nada ficou gravado');
    assert.match(document.querySelector('.dmo-toast-pilha [data-dmo-toast="erro"]').textContent, /mostra agora o que ficou gravado/);
    assert.equal(document.getElementById('costura-Cm').readOnly, false, 'os campos voltam a escrever-se');
    assert.deepEqual(novas(document, 'CM'), [['70.05', ''], ['70.06', '']], 'o enviado fica e o que se escreveu vai para a linha a seguir');
    teclar(window, '1');
    assert.deepEqual(novas(document, 'CM'), [['70.05', ''], ['70.061', '']], 'as teclas já não vão para a fila: continuam na linha a seguir, onde o operador escrevia');
  } finally {
    dom.window.close();
  }
});

test('a ligação cai depois de o servidor gravar: quando volta, a leitura aparece gravada, a seguinte (escrita durante a espera) regista-se e nada se repete (B-pegamentos-3)', async () => {
  // O primeiro POST grava e a ligação cai antes de a resposta chegar.
  const rede = { cortada: false, posts: 0 };
  const { dom, window, document, pedidos, soltar, estado } = await abrirPegamentos({ atrasar: true,
    falhar: (url, opcoes) => {
      if (rede.cortada) return true;
      if (opcoes.method !== 'POST' || ++rede.posts > 1) return false;
      rede.cortada = true;
      return 'depois';
    } });
  try {
    window.DmoTroca.tempos.confirmar = 60000;
    document.getElementById('costura-Cm').focus();
    teclar(window, '70.05\n');
    await esperar();
    teclar(window, '70.0');
    await soltar();
    await soltar();
    assert.deepEqual(estado.CM.map((l) => l.c), ['70.10', '70.05'], 'o servidor gravou');
    assert.equal(leituras(document, 'CM'), 1, 'sem ligação, o ecrã ainda não o mostra');
    assert.equal(document.getElementById('costura-Cm').readOnly, true, 'a linha enviada continua à espera, não pronta a repetir');
    teclar(window, '6\n');
    ligacaoVolta(window, rede);
    await soltar();
    await soltar();
    assert.deepEqual(pedidos.map((p) => p.metodo), ['POST', 'GET', 'GET', 'POST'], 'um POST de cada leitura: o que falhou não se repete');
    assert.deepEqual(estado.CM.map((l) => l.c), ['70.10', '70.05', '70.06'], '70.05 gravado uma vez; 70.06 a seguir');
    assert.equal(leituras(document, 'CM'), 3);
    assert.deepEqual(novas(document, 'CM'), [['', '']]);
  } finally {
    dom.window.close();
  }
});

test('uma correção aberta e escrita num cartão fica quando se regista noutro', async () => {
  const { dom, window, document, pedidos } = await abrirPegamentos();
  try {
    document.getElementById('valor-1').click();
    await esperar();
    const correcao = document.getElementById('editar-costura-1');
    escrever(window, correcao, '70.30');
    escrever(window, document.getElementById('costura-Bq'), '25.02');
    tecla(window, document.getElementById('costura-Bq'), 'Enter');
    await esperar();
    const corpo = new URLSearchParams(pedidos.at(-1).corpo);
    assert.deepEqual([corpo.get('editar_medicao'), corpo.get('campo')], ['1', 'costura'], 'o registo leva a correção aberta');
    assert.equal(document.getElementById('editar-costura-1'), correcao, 'o cartão do CM não é trocado');
    assert.equal(correcao.value, '70.30', 'o que estava escrito na correção fica');
    assert.equal(window.location.href, `${VOLTA}&editar_medicao=1&campo=costura`);
    assert.equal(document.activeElement.id, 'costura-Bq');
    // Fechada a correção, o registo seguinte já não a leva.
    tecla(window, correcao, 'Escape');
    await esperar();
    escrever(window, document.getElementById('costura-Bq'), '25.03');
    tecla(window, document.getElementById('costura-Bq'), 'Enter');
    await esperar();
    assert.equal(new URLSearchParams(pedidos.at(-1).corpo).get('editar_medicao'), null);
  } finally {
    dom.window.close();
  }
});

test('uma correção recusada enquanto um registo noutro cartão espera na fila fica aberta quando o registo parte', async () => {
  // O registo do BQ é enviado com a correção do CM ainda à espera: só quando
  // parte se sabe se ela ficou aberta (recusa) ou foi guardada.
  const { dom, window, document, pedidos, soltar } = await abrirPegamentos({ atrasar: true });
  try {
    document.getElementById('valor-1').click();
    await soltar();
    escrever(window, document.getElementById('editar-costura-1'), 'abc');
    tecla(window, document.getElementById('editar-costura-1'), 'Enter');
    escrever(window, document.getElementById('costura-Bq'), '25.02');
    tecla(window, document.getElementById('costura-Bq'), 'Enter');
    await soltar();
    assert.match(document.querySelector('.dmo-toast-pilha [data-dmo-toast="erro"]').textContent, /valor numérico/);
    await soltar();
    const corpo = new URLSearchParams(pedidos.at(-1).corpo);
    assert.equal(pedidos.at(-1).metodo, 'POST');
    assert.deepEqual([corpo.get('editar_medicao'), corpo.get('campo')], ['1', 'costura'], 'o registo leva a correção recusada');
    assert.ok(document.getElementById('editar-costura-1'), 'a correção recusada continua aberta');
  } finally {
    dom.window.close();
  }
});

// Várias ações no mesmo cartão (ronda 3 da revisão): cada uma segue na fila
// do DmoTroca e a resposta de cada uma troca o cartão.
test('registar e logo apagar outra leitura do mesmo cartão: a resposta do registo mostra a leitura com a linha nova vazia, e ela nunca volta como rascunho', async () => {
  const { dom, window, document, pedidos, soltar, estado } = await abrirPegamentos({ atrasar: true });
  try {
    document.getElementById('costura-Cm').focus();
    teclar(window, '70.41\n');
    // No browser, o clique no × dá-lhe o foco.
    document.getElementById('apagar-1').focus();
    apagar(document, 1);
    await soltar();
    assert.deepEqual(estado.CM.map((l) => l.c), ['70.10', '70.41'], 'o registo gravou');
    assert.equal(leituras(document, 'CM'), 2, 'a leitura aparece na tabela com a resposta do registo');
    assert.deepEqual(novas(document, 'CM'), [['', '']], 'e a linha nova fica vazia');
    assert.equal(document.getElementById('costura-Cm').readOnly, false);
    assert.equal(document.activeElement.id, 'costura-Cm', 'o foco vai para a costura');
    await soltar();
    assert.deepEqual(estado.CM.map((l) => l.c), ['70.41'], 'o × apagou a leitura 1');
    assert.equal(leituras(document, 'CM'), 1);
    assert.deepEqual(novas(document, 'CM'), [['', '']], 'a resposta do × não traz a leitura gravada de volta');
    assert.equal(document.activeElement.id, 'costura-Cm');
    assert.deepEqual(pedidos.map((p) => p.metodo), ['POST', 'POST']);
    // Uma ação a seguir no cartão (abrir uma correção) também não a traz.
    document.querySelector('[data-peg-component="CM"] a.peg-valor').click();
    await soltar();
    assert.deepEqual(novas(document, 'CM'), [['', '']]);
  } finally {
    dom.window.close();
  }
});

test('apagar e logo registar a leitura já escrita no mesmo cartão: o × mostra-a à espera, o registo grava-a uma vez e a linha nova fica vazia', async () => {
  const { dom, window, document, pedidos, soltar, estado } = await abrirPegamentos({ atrasar: true });
  try {
    escrever(window, document.getElementById('costura-Cm'), '70.41');
    document.getElementById('apagar-1').focus();
    apagar(document, 1);
    await esperar();
    tecla(window, document.getElementById('costura-Cm'), 'Enter');
    await soltar();
    assert.deepEqual(estado.CM.map((l) => l.c), [], 'o × apagou a leitura 1');
    assert.equal(leituras(document, 'CM'), 0);
    assert.deepEqual(novas(document, 'CM'), [['70.41', '']], 'a leitura enviada fica à vista enquanto o registo espera');
    assert.equal(document.getElementById('costura-Cm').readOnly, true, 'só de leitura');
    assert.equal(document.getElementById('peg-registar-CM').getAttribute('aria-busy'), 'true', 'e marcada à espera');
    await soltar();
    assert.deepEqual(estado.CM.map((l) => l.c), ['70.41'], 'gravada uma vez');
    assert.equal(leituras(document, 'CM'), 1, 'a leitura aparece na tabela');
    assert.deepEqual(novas(document, 'CM'), [['', '']], 'e a linha nova fica vazia');
    assert.equal(document.getElementById('costura-Cm').readOnly, false);
    assert.equal(document.activeElement.id, 'costura-Cm');
    assert.deepEqual(pedidos.map((p) => p.metodo), ['POST', 'POST']);
    document.querySelector('[data-peg-component="CM"] a.peg-valor').click();
    await soltar();
    assert.deepEqual(novas(document, 'CM'), [['', '']], 'nunca volta como rascunho');
  } finally {
    dom.window.close();
  }
});

for (const [caso, valor, depois] of [['guardada', '70.20', '70.20'], ['recusada', 'abc', '70.10']]) {
  test(`Enter numa linha nova já escrita enquanto uma correção do mesmo cartão grava (${caso}): vai para a fila e grava`, async () => {
    const { dom, window, document, pedidos, soltar, estado } = await abrirPegamentos({ atrasar: true });
    try {
      escrever(window, document.getElementById('costura-Cm'), '70.41');
      document.getElementById('valor-1').click();
      await soltar();
      assert.equal(document.getElementById('costura-Cm').value, '70.41', 'o que estava escrito na linha nova volta');
      escrever(window, document.getElementById('editar-costura-1'), valor);
      tecla(window, document.getElementById('editar-costura-1'), 'Enter');
      await esperar();
      // O operador volta à linha nova (já escrita) e carrega Enter.
      tecla(window, document.getElementById('costura-Cm'), 'Enter');
      await soltar();
      assert.equal(document.activeElement.id, 'costura-Cm', 'a resposta da correção não tira o foco da linha nova');
      await soltar();
      const posts = pedidos.filter((p) => p.metodo === 'POST').map((p) => new URL(p.url, VOLTA).searchParams.get('handler'));
      assert.deepEqual(posts, ['EditMeasurement', 'RegisterMeasurement'], 'o registo partiu depois da correção');
      assert.deepEqual(estado.CM.map((l) => l.c), [depois, '70.41'], 'a leitura gravou uma vez');
      assert.equal(leituras(document, 'CM'), 2);
      assert.deepEqual(novas(document, 'CM'), [['', '']], 'a linha nova fica vazia');
      assert.equal(document.getElementById('editar-costura-1'), null, 'registar no cartão da correção fecha-a');
      assert.equal(document.activeElement.id, 'costura-Cm');
    } finally {
      dom.window.close();
    }
  });
}

test('uma recusa do registo com um × pelo meio: a contagem das leituras não engana, as linhas enviadas voltam', async () => {
  // O × parte primeiro (uma leitura a menos); o registo de uma linha sem
  // costura é recusado a seguir: o cartão tem o mesmo número de leituras que
  // depois do ×, por isso é uma recusa.
  const { dom, window, document, pedidos, soltar, estado } = await abrirPegamentos({ atrasar: true });
  try {
    document.getElementById('apagar-1').focus();
    apagar(document, 1);
    await esperar();
    escrever(window, document.getElementById('costura-Cm'), '70.07');
    document.querySelector('[data-peg-component="CM"] [data-peg-mais-linha]').click();
    const segunda = document.querySelectorAll('[data-peg-component="CM"] [data-peg-linha-nova]')[1];
    escrever(window, segunda.querySelector('[data-peg-contra]'), '69.80');
    tecla(window, segunda.querySelector('[data-peg-contra]'), 'Enter');
    await soltar();
    assert.equal(leituras(document, 'CM'), 0, 'o × apagou');
    assert.deepEqual(novas(document, 'CM'), [['70.07', ''], ['', '69.80']], 'o registo à espera mostra as linhas enviadas');
    await soltar();
    assert.match(document.querySelector('.dmo-toast-pilha [data-dmo-toast="erro"]').textContent, /mantidos/);
    assert.equal(estado.CM.length, 0, 'nada foi gravado');
    assert.deepEqual(novas(document, 'CM'), [['70.07', ''], ['', '69.80']], 'a recusa devolve as linhas como foram enviadas');
    assert.equal(document.getElementById('costura-Cm').readOnly, false);
    assert.deepEqual(pedidos.map((p) => p.metodo), ['POST', 'POST']);
  } finally {
    dom.window.close();
  }
});

test('o aviso de uma medição gravada fica no canto até à ação seguinte no cartão; a leitura escrita durante a espera não o tira', async () => {
  const { dom, window, document, pedidos, soltar, estado } = await abrirPegamentos({ atrasar: true });
  const prazos = [];
  const relogio = window.setTimeout;
  window.setTimeout = (fn, ms, ...resto) => { prazos.push(ms); return relogio(fn, ms, ...resto); };
  try {
    const aviso = () => document.querySelector('.dmo-toast-pilha [data-dmo-toast="aviso"]');
    document.getElementById('costura-Cm').focus();
    // Uma contra costura ilegível (grava a costura e avisa) e, durante a
    // espera, a leitura seguinte com Enter.
    teclar(window, '70.02\t6x9\n');
    await esperar();
    teclar(window, '70.03\n');
    await soltar();
    assert.ok(aviso(), 'o aviso vai para a pilha do canto (não empurra os cartões)');
    assert.match(aviso().textContent, /sem contra costura/);
    assert.equal(pedidos.filter((p) => p.metodo === 'POST').length, 2, 'a leitura escrita durante a espera parte');
    await soltar();
    assert.deepEqual(estado.CM.map((l) => [l.c, l.n]), [['70.10', '69.90'], ['70.02', ''], ['70.03', '']]);
    assert.ok(aviso(), 'a leitura escrita antes de o aviso aparecer não o tira');
    assert.equal(prazos.includes(6000), false, 'o aviso não fecha sozinho');
    // A ação seguinte no cartão tira-o.
    teclar(window, '70.04\n');
    await soltar();
    assert.equal(estado.CM.length, 4);
    assert.equal(aviso(), null, 'a ação seguinte no cartão tira o aviso');
  } finally {
    window.setTimeout = relogio;
    dom.window.close();
  }
});

test('o aviso de uma medição gravada fecha-se no × e fica quando outro cartão grava', async () => {
  const { dom, window, document, soltar } = await abrirPegamentos({ atrasar: true });
  try {
    const aviso = () => document.querySelector('.dmo-toast-pilha [data-dmo-toast="aviso"]:not(.a-sair)');
    document.getElementById('costura-Cm').focus();
    teclar(window, '70.02\t6x9\n');
    await soltar();
    assert.ok(aviso());
    escrever(window, document.getElementById('costura-Bq'), '25.01');
    tecla(window, document.getElementById('costura-Bq'), 'Enter');
    await soltar();
    assert.ok(aviso(), 'gravar no BQ não tira o aviso do CM');
    aviso().querySelector('.dmo-toast-fechar').click();
    assert.equal(aviso(), null, 'o × fecha-o');
  } finally {
    dom.window.close();
  }
});

test('a escrever numa correção que o registo do mesmo cartão fecha: o resto do número não vai para a linha nova', async () => {
  // A correção da leitura 1 está aberta; o operador regista uma leitura nova
  // no mesmo cartão (o que fecha a correção) e volta logo à correção. A
  // resposta do registo chega a meio do número: o que escreveu na correção
  // perde-se (uma correção de cada vez), e o resto não pode ir parar à linha
  // nova (o Enter registava «0.39»).
  const { dom, window, document, pedidos, soltar, estado } = await abrirPegamentos({ atrasar: true });
  try {
    document.getElementById('valor-1').click();
    await soltar();
    document.getElementById('costura-Cm').focus();
    teclar(window, '70.41\n');
    await esperar();
    const correcao = document.getElementById('editar-costura-1');
    correcao.focus();
    correcao.value = '';
    teclar(window, '7');
    await soltar();
    assert.equal(document.getElementById('editar-costura-1'), null, 'registar no cartão da correção fecha-a');
    assert.notEqual(document.activeElement.id, 'costura-Cm', 'o foco não salta para a linha nova');
    teclar(window, '0.39\n');
    await soltar();
    assert.deepEqual(estado.CM.map((l) => l.c), ['70.10', '70.41'], 'nenhuma leitura com o resto do número');
    assert.deepEqual(novas(document, 'CM'), [['', '']]);
    assert.equal(pedidos.filter((p) => p.metodo === 'POST').length, 1);
  } finally {
    dom.window.close();
  }
});

test('a correção guardada (Enter) enquanto o registo do mesmo cartão espera grava, e o foco vai para a linha nova', async () => {
  const { dom, window, document, pedidos, soltar, estado } = await abrirPegamentos({ atrasar: true });
  try {
    document.getElementById('valor-1').click();
    await soltar();
    document.getElementById('costura-Cm').focus();
    teclar(window, '70.41\n');
    await esperar();
    const correcao = document.getElementById('editar-costura-1');
    correcao.focus();
    correcao.value = '';
    teclar(window, '70.39\n');
    await soltar();
    assert.equal(document.getElementById('editar-costura-1'), null, 'a resposta do registo fecha a correção');
    assert.equal(document.activeElement.id, 'costura-Cm', 'a correção já tinha partido: o foco vai para a linha nova');
    await soltar();
    const posts = pedidos.filter((p) => p.metodo === 'POST').map((p) => new URL(p.url, VOLTA).searchParams.get('handler'));
    assert.deepEqual(posts, ['RegisterMeasurement', 'EditMeasurement']);
    assert.deepEqual(estado.CM.map((l) => l.c), ['70.39', '70.41'], 'a correção gravou');
    assert.deepEqual(novas(document, 'CM'), [['', '']]);
    assert.equal(document.activeElement.id, 'costura-Cm');
  } finally {
    dom.window.close();
  }
});

test('sem ligação a meio do número seguinte: o resto do número continua na linha a seguir, não no valor enviado', async () => {
  // A ligação cai quando o operador já escreveu «70.0» da leitura seguinte:
  // o «6» que escreve a seguir junta-se a «70.0» (na linha a seguir) e o
  // valor enviado (70.05) fica como foi enviado. Enquanto a ligação não
  // volta, as teclas esperam na fila do cartão.
  const rede = { cortada: true };
  const { dom, window, document, soltar } = await abrirPegamentos({ atrasar: true, falhar: () => rede.cortada });
  try {
    window.DmoTroca.tempos.confirmar = 60000;
    document.getElementById('costura-Cm').focus();
    teclar(window, '70.05\n');
    await esperar();
    teclar(window, '70.0');
    await soltar();
    await soltar();
    teclar(window, '6');
    ligacaoVolta(window, rede);
    await soltar();
    assert.deepEqual(novas(document, 'CM'), [['70.05', ''], ['70.06', '']]);
    assert.equal(document.activeElement, document.querySelectorAll('[data-peg-component="CM"] [data-peg-linha-nova] [data-peg-costura]')[1]);
  } finally {
    dom.window.close();
  }
});
