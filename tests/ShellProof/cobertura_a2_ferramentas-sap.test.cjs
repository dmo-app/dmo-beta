const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// DMO-418 (beta.104) e DMO-492 (beta.196): o cartão do SAP lê a pasta do SAP
// no browser. «Apagar lidos» apaga só os ficheiros que o DMO já leu, com
// confirmação; a leitura envia para o servidor todos os ficheiros novos da
// pasta (e não só o mais recente), do mais antigo para o mais novo.

function pastaFalsa(ficheiros) {
  const mapa = new Map(Object.entries(ficheiros).map(([nome, lastModified]) => [nome, { lastModified, texto: 'conteudo de ' + nome }]));
  return {
    name: 'SAP',
    mapa,
    queryPermission: async () => 'granted',
    requestPermission: async () => 'granted',
    async *values() {
      for (const [nome, f] of [...mapa]) {
        yield { kind: 'file', name: nome, getFile: async () => ({ name: nome, lastModified: f.lastModified, text: async () => f.texto }) };
      }
    },
    async getFileHandle(nome) {
      return { createWritable: async () => ({ write: async v => { mapa.set(nome, { lastModified: Date.now(), texto: String(v) }); }, close: async () => {} }) };
    },
    async removeEntry(nome) { mapa.delete(nome); }
  };
}

function indexedDBFalso(guardado) {
  return {
    open() {
      const pedido = {};
      setTimeout(() => {
        pedido.result = {
          createObjectStore() {},
          transaction() {
            const tx = {};
            tx.objectStore = () => ({
              get(chave) { const r = { result: guardado[chave] }; setTimeout(() => r.onsuccess && r.onsuccess(), 0); return r; },
              put(valor, chave) { guardado[chave] = valor; setTimeout(() => tx.oncomplete && tx.oncomplete(), 0); },
              delete(chave) { delete guardado[chave]; setTimeout(() => tx.oncomplete && tx.oncomplete(), 0); }
            });
            return tx;
          }
        };
        if (pedido.onupgradeneeded) pedido.onupgradeneeded();
        pedido.onsuccess();
      }, 0);
      return pedido;
    }
  };
}

const espera = ms => new Promise(r => setTimeout(r, ms));
async function ate(condicao) {
  for (let i = 0; i < 200; i++) { if (condicao()) return; await espera(10); }
  throw new Error('a condição nunca se cumpriu');
}

async function pagina({ ficheiros, ficheiroModificadoEm = null, codigos = [], confirma = true, semAtualizar = false }) {
  const pasta = pastaFalsa(ficheiros);
  // SEC-01 (29/10/2026): a página /Ferramentas/Definicoes já não tem
  // «Atualizar do SAP», a janela nem «Instalar» (só o Admin os tem agora,
  // com a senha); o resto do cartão (Copiar códigos, Apagar lidos, Pedir
  // todas) continua lá, e o script não pode rebentar com esses botões ausentes.
  const botoesAtualizar = semAtualizar ? '' : `
    <button data-ferr-sap-atualizar>Atualizar do SAP</button>
    <div data-ferr-sap-janela><button data-ferr-sap-correr></button></div>
    <button data-ferr-sap-instalar></button>`;
  const dom = new JSDOM(`<!doctype html><body><section data-ferr-sap>
    <p data-ferr-sap-estado></p>${botoesAtualizar}
    <button data-ferr-sap-codigos></button>
    <button data-ferr-sap-apagar>Apagar lidos</button>
    <button data-ferr-sap-todas></button>
    <button data-ferr-sap-remover hidden></button>
  </section></body>`, { url: 'http://dmo.test/Ferramentas/Definicoes', runScripts: 'outside-only' });
  const w = dom.window;
  w.showDirectoryPicker = () => {};
  Object.defineProperty(w, 'indexedDB', { value: indexedDBFalso({ pasta }), configurable: true });
  const enviados = [];
  const mensagens = [];
  const confirmacoes = [];
  w.confirm = texto => { confirmacoes.push(texto); return confirma; };
  w.DMOLive = { mostrarMensagens: caixa => mensagens.push(caixa.textContent) };
  w.fetch = async (url, opcoes) => {
    const json = valor => ({ ok: true, status: 200, json: async () => valor });
    if (url.includes('handler=ImportarUsoSap')) {
      enviados.push(opcoes.body.get('ficheiro'));
      return json({ gravado: true, mensagem: 'lido ' + opcoes.body.get('ficheiro') });
    }
    if (url.includes('handler=ScriptsSap')) return json([]);
    return json({ codigos, ficheiroModificadoEm });
  };
  w.eval(fs.readFileSync(path.join(assets, 'js/ferramentas-sap.js'), 'utf8'));
  return { w, doc: w.document, pasta, enviados, mensagens, confirmacoes };
}

test('A primeira leitura da pasta envia todos os ficheiros do SAP, do mais antigo para o mais novo, e nenhum do DMO', async () => {
  const { w, pasta, enviados } = await pagina({
    ficheiros: { 'zppf-b.html': 3000, 'producoes.csv': 1000, 'zppf-a.html': 2000, 'dmo-codigos.txt': 500, 'dmo-sap-estado.txt': 600, 'LEIA-ME.txt': 700, 'foto.png': 800 },
    codigos: ['M544744MPPSX11']
  });
  await ate(() => enviados.length === 3);
  await espera(50);
  assert.deepEqual(enviados, ['producoes.csv', 'zppf-a.html', 'zppf-b.html']);
  // Os códigos a pedir ao SAP ficam escritos na pasta.
  assert.match(pasta.mapa.get('dmo-codigos.txt').texto, /M544744MPPSX11/);
  w.close();
});

test('Com um ficheiro já lido, só os mais novos do que o último lido vão para o servidor', async () => {
  const { w, enviados } = await pagina({
    ficheiros: { 'a.html': 1000, 'b.html': 2000, 'c.html': 3000, 'd.csv': 4000 },
    ficheiroModificadoEm: 2000
  });
  await ate(() => enviados.length === 2);
  await espera(50);
  assert.deepEqual(enviados, ['c.html', 'd.csv']);
  w.close();
});

test('Se a lista mais recente já foi lida, nada vai para o servidor e o cartão diz isso', async () => {
  const { w, doc, enviados } = await pagina({ ficheiros: { 'a.html': 1000, 'b.html': 2000 }, ficheiroModificadoEm: 2000 });
  await ate(() => /já foi lida/.test(doc.querySelector('[data-ferr-sap-estado]').textContent));
  assert.deepEqual(enviados, []);
  w.close();
});

test('Numa pasta com mais de dez listas por ler, vão as dez mais recentes', async () => {
  const ficheiros = {};
  for (let i = 1; i <= 12; i++) ficheiros['lista' + String(i).padStart(2, '0') + '.html'] = i * 1000;
  const { w, enviados } = await pagina({ ficheiros });
  await ate(() => enviados.length === 10);
  await espera(50);
  assert.equal(enviados[0], 'lista03.html');
  assert.equal(enviados[9], 'lista12.html');
  w.close();
});

test('Apagar lidos: pergunta antes e apaga só os ficheiros do SAP já lidos', async () => {
  const { w, doc, pasta, enviados, mensagens, confirmacoes } = await pagina({
    ficheiros: { 'velha.html': 1000, 'lida.html': 2000, 'nova.html': 3000, 'dmo-codigos.txt': 100, 'LEIA-ME.txt': 100 },
    ficheiroModificadoEm: 2000
  });
  await ate(() => enviados.length === 1);
  await espera(50);
  doc.querySelector('[data-ferr-sap-apagar]').click();
  await ate(() => confirmacoes.length === 1);
  assert.equal(confirmacoes[0], 'Apagar 2 ficheiros já lidos da pasta «SAP»?');
  await ate(() => !pasta.mapa.has('velha.html'));
  assert.equal(pasta.mapa.has('lida.html'), false);
  // Ficam o que ainda não foi lido e o que é do DMO.
  assert.deepEqual([...pasta.mapa.keys()].filter(n => n !== 'dmo-codigos.txt').sort(), ['LEIA-ME.txt', 'nova.html']);
  assert.ok(pasta.mapa.has('dmo-codigos.txt'));
  await ate(() => mensagens.some(m => /2 ficheiros apagados da pasta «SAP»/.test(m)));
  w.close();
});

test('Apagar lidos: se a pessoa não confirma, nada é apagado', async () => {
  const { w, doc, pasta, enviados, confirmacoes } = await pagina({
    ficheiros: { 'lida.html': 2000, 'nova.html': 3000 }, ficheiroModificadoEm: 2000, confirma: false
  });
  await ate(() => enviados.length === 1);
  await espera(50);
  doc.querySelector('[data-ferr-sap-apagar]').click();
  await ate(() => confirmacoes.length === 1);
  assert.equal(confirmacoes[0], 'Apagar 1 ficheiro já lido da pasta «SAP»?');
  await espera(100);
  assert.deepEqual([...pasta.mapa.keys()].filter(n => n !== 'dmo-codigos.txt').sort(), ['lida.html', 'nova.html']);
  w.close();
});

test('Apagar lidos: sem nada lido ainda, avisa e não apaga nem pergunta', async () => {
  const { w, doc, pasta, confirmacoes, mensagens } = await pagina({ ficheiros: { 'a.html': 1000 }, ficheiroModificadoEm: null });
  await ate(() => mensagens.length >= 1);
  await espera(50);
  const antes = mensagens.length;
  doc.querySelector('[data-ferr-sap-apagar]').click();
  await ate(() => mensagens.length > antes);
  assert.match(mensagens[mensagens.length - 1], /Não há ficheiros lidos para apagar na pasta «SAP»/);
  assert.equal(confirmacoes.length, 0);
  assert.ok(pasta.mapa.has('a.html'));
  w.close();
});

// SEC-01 (29/10/2026): sem «Atualizar do SAP», a janela e «Instalar» no
// cartão (saíram para o Admin), o script não pode rebentar a ler esses
// botões, e «Apagar lidos» continua a funcionar.
test('Sem «Atualizar do SAP» no cartão (Admin ficou com ele), o script não rebenta e «Apagar lidos» continua a funcionar', async () => {
  const { w, doc, pasta, enviados, confirmacoes } = await pagina({
    ficheiros: { 'lida.html': 2000, 'nova.html': 3000 }, ficheiroModificadoEm: 2000, semAtualizar: true
  });
  assert.equal(doc.querySelector('[data-ferr-sap-atualizar]'), null);
  assert.equal(doc.querySelector('[data-ferr-sap-janela]'), null);
  assert.equal(doc.querySelector('[data-ferr-sap-instalar]'), null);
  await ate(() => enviados.length === 1);
  await espera(50);
  doc.querySelector('[data-ferr-sap-apagar]').click();
  await ate(() => confirmacoes.length === 1);
  assert.equal(confirmacoes[0], 'Apagar 1 ficheiro já lido da pasta «SAP»?');
  await ate(() => !pasta.mapa.has('lida.html'));
  w.close();
});
