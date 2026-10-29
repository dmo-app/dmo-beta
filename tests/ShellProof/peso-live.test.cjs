const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM, VirtualConsole } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

for (const previous of ['202601', 'Por criar']) {
  test(`preview preserves comparison ${previous} while updating draft totals`, async () => {
    const dom = new JSDOM(`
      <section data-peso-live data-peso-id="7" data-preview-url="/Controlo/Peso?handler=Preview">
        <div data-peso-strip></div>
        <div data-peso-summary>
          <div class="peso-fact" data-peso-comparison><span>Comparação anterior</span><strong>${previous}</strong></div>
        </div>
        <form class="peso-leitura-form" data-peso-rows-form>
          <div data-peso-rows>
            <div data-peso-row><input id="cmSubject" name="cm_subject"><input id="waterWeight" name="water_weight"><div data-peso-row-result></div></div>
          </div>
          <button type="button" data-peso-add-row>+</button><button type="button" data-peso-remove-row>−</button>
        </form>
      </section>`, { runScripts: 'outside-only' });
    try {
      const { window } = dom;
      const document = window.document;
      let requests = 0;
      window.fetch = async (_url, options) => {
        requests++;
        const body = new URLSearchParams(options.body);
        const lines = JSON.parse(body.get('lines'));
        assert.equal(body.get('peso_id'), '7');
        return { ok: true, json: async () => ({
          context: { waterDensityGramsPerCm3: 0.9982, nominalWeightGrams: 200 },
          totals: { sampleCount: 3, averageWaterWeightGrams: 150.25,
            averageCapacityCm3: 150.52, averageGlassWeightGrams: 325.39, desvioNominalG: 125.39 },
          lines: lines.map(line => ({ cmSubject: line.cm_subject, status: 'ok',
            capacityCm3: 200, glassWeightGrams: 450.52 }))
        }) };
      };
      window.eval(fs.readFileSync(path.join(assets, 'dmo-live.js'), 'utf8'));
      window.eval(fs.readFileSync(path.join(assets, 'peso-live.js'), 'utf8'));
      // Let the normal DOMContentLoaded initialization finish.
      await new Promise(resolve => window.addEventListener('load', resolve, { once: true }));
      const fact = document.querySelector('[data-peso-comparison]');
      const weight = document.querySelector('#waterWeight');
      document.querySelector('#cmSubject').value = '12';

      for (const value of ['199.64', '99.82', '']) {
        weight.value = value;
        weight.dispatchEvent(new window.Event('blur'));
        await new Promise(resolve => setImmediate(resolve));
        assert.equal(document.querySelector('[data-peso-comparison]'), fact,
          'the comparison provided by the server survives every preview');
        assert.equal(fact.querySelector('strong').textContent, previous);
        // A faixa tem os quatro factos do rascunho; o resumo, o nominal,
        // a diferença nominal e a comparação guardada.
        assert.equal(document.querySelectorAll('[data-peso-strip] .peso-fact').length, 4);
        assert.match(document.querySelector('[data-peso-strip]').textContent, /325\.39 g/);
        assert.equal(document.querySelectorAll('[data-peso-summary] .peso-fact').length, 3);
        // A linha mostra logo o seu volume e peso; vazia, não mostra nada.
        assert.equal(document.querySelector('[data-peso-row-result]').textContent,
          value === '' ? '' : '200.000 cm³ · 450.52 g');
      }
      assert.equal(requests, 3);

      // «+» acrescenta uma linha vazia; «−» tira a última e fica sempre uma.
      document.querySelector('[data-peso-add-row]').click();
      assert.equal(document.querySelectorAll('[data-peso-row]').length, 2);
      assert.equal(document.querySelectorAll('[data-peso-row] input[name="water_weight"]')[1].value, '');
      document.querySelector('[data-peso-remove-row]').click();
      document.querySelector('[data-peso-remove-row]').click();
      assert.equal(document.querySelectorAll('[data-peso-row]').length, 1);
    } finally {
      dom.window.close();
    }
  });
}

// GRAVAR SEM RECARREGAR (Diogo, 17/10/2026), com a troca real: o
// 0_ASSET_CANONICAL_INTERACTIONS.js (DmoTroca), o dmo-live.js, o
// dmo-suggest.js, o peso-live.js e os scripts da própria página, lidos do
// Peso.cshtml, pela ordem do _DmoShellLayout. A folha de teste tem todas as
// zonas da folha da produção, com as marcas lidas do Peso.cshtml (o primeiro
// teste diz quais têm de ser), e um servidor de teste responde como o
// PesoModel: a página desenhada a partir do endereço e da base, e o redirect
// de cada POST (PRG), com a comparação e o «Supomos» que o formulário leva.
const PAGINA = path.resolve(__dirname, '../../src/DMO.Alpha.Web/Pages/Controlo/Peso.cshtml');
const cshtml = fs.readFileSync(PAGINA, 'utf8');

const M = {
  raiz: '<main class="peso-page dmo-workspace" aria-labelledby="peso-title" data-dmo-troca>',
  apoio: '<div class="dmo-zona" data-dmo-fica>',
  capContexto: '<div id="pesoCapContexto" class="dmo-zona" data-dmo-regiao>',
  producaoAvisos: '<div id="pesoProducaoAvisos" class="dmo-zona" data-dmo-regiao>',
  leituras: '<div id="pesoLeituras" class="dmo-zona" data-dmo-regiao>',
  recusado: '<span hidden data-peso-registo-recusado></span>',
  resumo: '<div id="pesoLeituraResumo" class="dmo-zona" data-dmo-regiao>',
  leituraContexto: '<div id="pesoLeituraContexto" class="dmo-zona" data-dmo-regiao>',
  gravadas: '<div id="pesoLeiturasGravadas" class="dmo-zona" data-dmo-regiao>',
  leituraNova: '<div class="peso-leitura-nova" data-dmo-fica>',
  comparacao: '<div id="pesoComparacao" class="dmo-zona" data-dmo-regiao>',
  anteriorAvisos: '<div id="pesoAnteriorAvisos" class="dmo-zona" data-dmo-regiao>',
  anterior: '<div id="pesoAnterior" class="dmo-zona" data-dmo-fica>',
  anteriorContexto: '<div id="pesoAnteriorContexto" class="dmo-zona" data-dmo-regiao>',
  delta: '<div id="pesoAnteriorDelta" class="dmo-zona" data-dmo-regiao>',
  supomos: '<div id="pesoSupomos" class="peso-supomos" data-peso-supomos data-dmo-regiao style="margin-top:16px">',
  suporRecusa: '<div data-dmo-toast="erro" class="peso-aviso" role="alert">A calote suposta «',
  pdf: '<div id="pesoPdf" class="dmo-zona" data-dmo-regiao>'
};

// Os scripts da página (os dois blocos de @section Scripts), sem os
// comentários Razor, que também falam de <script>.
function scriptsDaPagina() {
  const seccao = cshtml.slice(cshtml.indexOf('@section Scripts {')).replace(/@\*[\s\S]*?\*@/g, '');
  return [...seccao.matchAll(/<script>([\s\S]*?)<\/script>/g)].map((m) => m[1]);
}

const BASE = 'http://dmo.test/Controlo/Peso';
const OPCOES = [{ id: 21, t: '202601' }, { id: 22, t: '202604' }];
const ANTERIORES = [
  { id: 11, t: '202601 · B1', inicio: '02/03/2026', fim: '20/03/2026' },
  { id: 12, t: '202604 · B2', inicio: '06/04/2026', fim: '24/04/2026' }
];
const NUMERO = /^\d+([.,]\d+)?$/;

function novaBase() {
  return { leituras: [{ cm: '45', agua: '150.2' }], sap: '300', anterior: 11, tokens: 0 };
}

// A folha de teste desenha-se com as marcas que o Peso.cshtml tem (lidas do
// ficheiro): se a página mudar, a folha de teste muda com ela.
function etiqueta(re, nome) {
  const m = cshtml.match(re);
  if (!m) throw new Error(`${nome} não está no Peso.cshtml`);
  return m[0];
}
const zona = (zonaId) => etiqueta(new RegExp(`<div id="${zonaId}"[^>]*>`), zonaId);
const temZona = (zonaId) => cshtml.includes(`<div id="${zonaId}"`);
function trecho(inicio, fim) {
  const i = cshtml.indexOf(inicio);
  return i < 0 ? '' : cshtml.slice(i, cshtml.indexOf(fim, i));
}
const F = {
  raiz: etiqueta(/<main [^>]*data-dmo-troca[^>]*>/, 'a raiz'),
  apoio: /<div class="dmo-zona" data-dmo-fica>\s*<details class="tp-support"/.test(cshtml) ? M.apoio : '<div>',
  capContexto: temZona('pesoCapContexto') ? zona('pesoCapContexto') : '<div>',
  producaoAvisos: zona('pesoProducaoAvisos'),
  leituras: zona('pesoLeituras'),
  recusado: cshtml.includes(M.recusado) ? M.recusado : '',
  resumo: zona('pesoLeituraResumo'),
  leituraContexto: zona('pesoLeituraContexto'),
  gravadas: zona('pesoLeiturasGravadas'),
  leituraNova: etiqueta(/<div class="peso-leitura-nova"[^>]*>/, 'a leitura nova'),
  comparacao: zona('pesoComparacao'),
  anteriorAvisos: zona('pesoAnteriorAvisos'),
  anterior: zona('pesoAnterior'),
  anteriorContexto: temZona('pesoAnteriorContexto') ? zona('pesoAnteriorContexto') : '<div>',
  delta: zona('pesoAnteriorDelta'),
  supomos: zona('pesoSupomos'),
  suporRecusa: etiqueta(/<div[^>]*>(?=A calote suposta «)/, 'a recusa do «Supomos»'),
  pdf: zona('pesoPdf')
};
// O que os formulários levam de volta, como no Peso.cshtml.
const LEVA = {
  leituraSupor: trecho('id="pesoLeituraContexto"', '</div>').includes('name="supor_calote"'),
  anteriorSupor: trecho('asp-page-handler="GuardarProducaoAnterior"', '</form>').includes('name="supor_calote"'),
  calcularComparacao: trecho('class="tp-sim-form"', '</form>').includes('name="comparar"')
};

// A folha como o Peso.cshtml a desenha para este endereço e esta base.
function desenhar(db, endereco) {
  const q = new URL(endereco, BASE).searchParams;
  const controlo = q.get('controlo_id');
  const comparar = q.get('comparar') ? Number(q.get('comparar')) : null;
  const supor = (q.get('supor_calote') || '').trim() || null;
  const suporValido = supor !== null && NUMERO.test(supor);
  const cap = q.get('cap_h');
  const anteriorRecusa = q.get('anterior_recusa') === 'peso';
  const anterior = Number(q.get('anterior') || db.anterior);
  const leituraRecusa = q.get('leitura_refusal');
  const n = db.leituras.length;
  const media = (db.leituras.reduce((a, l) => a + Number(l.agua), 0) / n).toFixed(2);
  const escolhido = OPCOES.find((o) => o.id === comparar);
  const periodo = ANTERIORES.find((a) => a.id === anterior);
  const token = () => `<input name="__RequestVerificationToken" type="hidden" value="tok-${++db.tokens}" />`;
  const voltar = controlo ? `<input type="hidden" name="controlo_id" value="${controlo}" />` : '<input type="hidden" name="peso_id" value="7" />';
  // Os campos escondidos que levam a folha de volta, como no Peso.cshtml
  // (SuporCaloteParaLevar: a calote suposta ilegível não segue).
  const campo = (nome, valor) => `<input type="hidden" name="${nome}" value="${valor}" />`;
  const levaControlo = controlo ? campo('controlo_id', controlo) : '';
  const levaComparar = comparar ? campo('comparar', comparar) : '';
  const levaSupor = suporValido ? campo('supor_calote', supor) : '';
  const producao = !controlo ? '' : `
  <section class="dmo-card peso-card" aria-labelledby="peso-production-title">
    <h3 id="peso-production-title">Produção 202610</h3>
    <div class="peso-facts">
      ${F.apoio}
      <details class="tp-support" data-cap-url="/Controlo/Peso?handler=CapSupport" data-cap-params="controlo_id=${controlo}"${cap ? ' open="open"' : ''}>
        <summary class="tp-button"><span>TP / calote · Apoio separado</span><small>h ${cap || '4.1'}</small></summary>
        <div class="tp-support-body">
          <div class="peso-facts" data-cap-result><div class="peso-fact"><span>Volume</span><strong>${cap || '4.1'} cm³</strong></div></div>
          <form method="get" action="/Controlo/Peso" class="tp-sim-form">
            ${F.capContexto}${levaControlo}${LEVA.calcularComparacao ? levaComparar + levaSupor : ''}</div>
            <input id="capSimulada" name="cap_h" value="${cap || '4.1'}" />
            <button type="submit" class="dmo-button">Calcular</button>
          </form>
        </div>
      </details>
      </div>
    </div>
    ${F.producaoAvisos}</div>
  </section>`;
  return `${F.raiz}
  <h1 id="peso-title">Peso</h1>${producao}
  <section class="dmo-card peso-card" aria-labelledby="peso-open-title" data-peso-live data-peso-id="7" data-preview-url="/Controlo/Peso?handler=Preview" data-dmo-parcial>
    <h3 id="peso-open-title">Peso — CM ST100</h3>
    ${F.leituras}
      ${leituraRecusa ? `<div data-dmo-toast="erro" class="peso-aviso" role="alert">A massa de água escrita é ilegível.</div>${F.recusado}` : ''}
      <table class="dmo-table" data-peso-leituras><tbody>${db.leituras.map((l) => `<tr><td>${l.cm}</td><td>${l.agua}</td></tr>`).join('')}</tbody></table>
    </div>
    <div class="peso-live-region">
      <div class="peso-live-area" aria-live="polite">
        ${F.resumo}
          <div class="peso-live-strip" data-peso-strip><div class="peso-fact"><span>Leituras válidas</span><strong>${n}</strong></div><div class="peso-fact"><span>Média em água</span><strong>${media} g</strong></div></div>
          <div class="peso-resumo" data-peso-summary><div class="peso-fact"><span>Peso nominal</span><strong>200 g</strong></div><div class="peso-fact"><span>Diferença nominal</span><strong>+1 g</strong></div><div class="peso-fact" data-peso-comparison><span>Comparação anterior</span><strong>${escolhido ? escolhido.t : 'Por criar'}</strong></div></div>
        </div>
      </div>
      <form method="post" action="/Controlo/Peso?handler=RegisterMeasurement" class="peso-leitura-form" data-peso-rows-form>
        <input type="hidden" name="peso_id" value="7" />
        ${F.leituraContexto}${levaControlo}${levaComparar}${LEVA.leituraSupor ? levaSupor : ''}</div>
        <div class="peso-leitura-grade" data-peso-leitura-cartoes>
          ${F.gravadas}${db.leituras.map((l, i) => `<div class="peso-leitura-row is-saved" data-peso-leitura-gravada><input id="pesoGravadaCm-${i}" value="${l.cm}" readonly tabindex="-1" /></div>`).join('')}</div>
          ${F.leituraNova}
            <div class="peso-leitura-rows" data-peso-rows>
              <div class="peso-leitura-row" data-peso-row>
                <input id="cmSubject" name="cm_subject" placeholder="Ex.: 12" value="${leituraRecusa ? q.get('leitura_cm') || '' : ''}" />
                <input id="waterWeight" name="water_weight" type="text" required value="${leituraRecusa ? q.get('leitura_water') || '' : ''}" />
                <output class="peso-leitura-chip" data-peso-row-result></output>
              </div>
            </div>
            <div class="peso-leitura-actions">
              <button type="button" data-peso-add-row>+</button><button type="button" data-peso-remove-row>−</button>
              <button type="submit" class="dmo-button peso-registar">Registar leitura</button>
            </div>
          </div>
        </div>
        ${token()}
      </form>
    </div>
  </section>
  <section class="dmo-card peso-card peso-compare" aria-labelledby="peso-comparacao-title" data-dmo-parcial>
    <h3 id="peso-comparacao-title">Comparação com outro controlo</h3>
    ${F.comparacao}
      <form method="get" action="/Controlo/Peso" class="peso-inline-form peso-procura-larga">
        ${voltar}${suporValido ? `<input type="hidden" name="supor_calote" value="${supor}" />` : ''}
        <div class="dmo-field">
          <label for="compararDropdown">Produção a comparar (1075C142)</label>
          <select id="compararDropdown" name="comparar" data-auto-submit data-dmo-filter>
            <option value=""${comparar ? '' : ' selected="selected"'}>Procurar produção…</option>
            ${OPCOES.map((o) => `<option value="${o.id}"${comparar === o.id ? ' selected="selected"' : ''}>${o.t} · Peso 320.0${n} g</option>`).join('')}
          </select>
        </div>
      </form>
      ${escolhido ? `<table class="dmo-table" data-peso-comparacao-tabela><tbody><tr><td>${n} CM · ${escolhido.t}</td></tr></tbody></table>` : ''}
    </div>
    ${F.anteriorAvisos}${anteriorRecusa ? '<div data-dmo-toast="erro" class="peso-aviso" role="alert">O peso da produção anterior tem de ser um número maior que 0. Nada foi guardado.</div>' : ''}${q.get('anterior_ok') ? '<div data-dmo-toast="ok" class="peso-aviso-simples" role="status">Produção anterior guardada.</div>' : ''}</div>
    ${F.anterior}
      <form method="post" action="/Controlo/Peso?handler=GuardarProducaoAnterior" class="peso-inline-form" data-producao-anterior>
        ${F.anteriorContexto}<input type="hidden" name="peso_id" value="7" /><input type="hidden" name="controlo_id" value="${controlo || ''}" />${levaComparar}${LEVA.anteriorSupor ? levaSupor : ''}</div>
        <div class="dmo-field"><label for="producaoAnterior">Produção anterior</label>
          <select id="producaoAnterior" name="anterior" data-producao-anterior-select>${ANTERIORES.map((a) => `<option value="${a.id}" data-inicio="${a.inicio}" data-fim="${a.fim}"${a.id === anterior ? ' selected="selected"' : ''}>${a.t}</option>`).join('')}</select>
        </div>
        <div class="dmo-field"><input id="producaoAnteriorPeso" name="anterior_peso" aria-label="Peso usado na produção (g)" value="${anteriorRecusa ? q.get('anterior_peso') : anterior === db.anterior ? db.sap : ''}" /></div>
        <button type="submit" class="dmo-button" id="producaoAnteriorGuardar">Guardar</button>
        ${token()}
      </form>
      <p class="peso-hint" data-producao-anterior-periodo>Início <span data-inicio>${periodo.inicio}</span> · Fim <span data-fim>${periodo.fim}</span></p>
    </div>
    ${F.delta}${db.sap ? `<div class="peso-resumo" data-producao-anterior-delta><strong>${db.sap} g · média ${media}</strong></div>` : ''}</div>
    ${F.supomos}
      <h4>Supomos</h4>
      <form method="get" action="/Controlo/Peso" class="dmo-field" style="max-width:420px">
        ${voltar}${comparar ? `<input type="hidden" name="comparar" value="${comparar}" />` : ''}
        <label for="suporCalote">Calote do tampão suposta (mm)</label>
        <input id="suporCalote" name="supor_calote" value="${supor ?? ''}" placeholder="ex.: 5,2" />
        <button type="submit" class="dmo-button" id="suporCaloteVer">Ver a diferença</button>
      </form>
      ${supor !== null && !suporValido ? `${F.suporRecusa}A calote suposta «${supor}» não é um número válido.</div>` : ''}
      ${suporValido ? `<div class="peso-resumo" data-supor-resultado>Calote 4.1 → ${supor} mm · média ${media}</div>` : ''}
    </div>
  </section>
  ${F.pdf}
    <form method="post" action="/Controlo/Peso?handler=GeneratePdf" class="peso-actions">${voltar}${comparar ? `<input type="hidden" name="comparar" value="${comparar}" />` : ''}<button type="submit" class="dmo-button">Exportar PDF</button>${token()}</form>
  </div>
</main>`;
}

const documento = (db, endereco) => `<!doctype html><title>Peso</title><body>${desenhar(db, endereco)}</body>`;

// O POST como o PesoModel o trata: grava (ou recusa) e devolve o endereço do
// redirect, com o Back() do RegisterMeasurement e o do GuardarProducaoAnterior.
function gravarNoServidor(db, endereco, corpo) {
  const handler = new URL(endereco, BASE).searchParams.get('handler');
  const f = new URLSearchParams(corpo);
  const volta = new URLSearchParams();
  const juntar = (nome, valor) => { if (valor) volta.set(nome, valor); };
  if (handler === 'RegisterMeasurement') {
    const aguas = f.getAll('water_weight');
    const cms = f.getAll('cm_subject');
    const recusada = aguas.findIndex((a) => a.trim() && !NUMERO.test(a.trim()));
    if (recusada >= 0) {
      juntar('leitura_refusal', 'ilegivel');
      juntar('leitura_cm', cms[recusada]);
      juntar('leitura_water', aguas[recusada]);
    } else {
      aguas.forEach((a, i) => { if (a.trim()) db.leituras.push({ cm: cms[i] || '', agua: a.trim().replace(',', '.') }); });
    }
    if (f.get('controlo_id')) juntar('controlo_id', f.get('controlo_id')); else juntar('peso_id', f.get('peso_id'));
  } else if (handler === 'GuardarProducaoAnterior') {
    if (f.get('controlo_id')) juntar('controlo_id', f.get('controlo_id')); else juntar('peso_id', f.get('peso_id'));
    juntar('anterior', f.get('anterior'));
    const peso = (f.get('anterior_peso') || '').trim();
    if (peso && !NUMERO.test(peso)) {
      juntar('anterior_recusa', 'peso');
      juntar('anterior_peso', peso);
    } else {
      db.sap = peso;
      db.anterior = Number(f.get('anterior'));
      juntar('anterior_ok', '1');
    }
  } else {
    throw new Error('handler ' + handler);
  }
  juntar('comparar', f.get('comparar'));
  juntar('supor_calote', (f.get('supor_calote') || '').trim());
  return BASE + '?' + volta;
}

// segurarPosts: cada POST espera por soltar() antes de o servidor o tratar
// (a rede lenta). rede.cortada: os pedidos falham como sem ligação.
// rede.cairDepoisDoPost: o POST seguinte grava e a ligação cai antes de a
// resposta chegar (fica cortada).
async function abrirFolhaReal({ endereco = BASE + '?controlo_id=5', antesDoPost = null, segurarPosts = false } = {}) {
  const db = novaBase();
  const presos = [];
  const rede = { cortada: false };
  const navegou = [];
  const virtualConsole = new VirtualConsole();
  virtualConsole.on('jsdomError', (e) => { if (/navigation|submit/i.test(e.message)) navegou.push(e.message); });
  const dom = new JSDOM(documento(db, endereco), { url: endereco, runScripts: 'outside-only', virtualConsole, pretendToBeVisual: true });
  const { window } = dom;
  const pedidos = [];
  window.fetch = async (url, options = {}) => {
    const u = new URL(String(url), window.location.href);
    const metodo = options.method || 'GET';
    pedidos.push({ metodo, url: u.search, corpo: options.body ? String(options.body) : '' });
    if (rede.cortada) throw new window.TypeError('Failed to fetch');
    if (u.searchParams.get('handler') === 'Preview') {
      const lines = JSON.parse(new URLSearchParams(String(options.body)).get('lines'));
      return { ok: true, json: async () => ({ context: { nominalWeightGrams: 200 },
        totals: { sampleCount: db.leituras.length + lines.length, averageWaterWeightGrams: 150.25,
          averageCapacityCm3: 150.52, averageGlassWeightGrams: 325.39, desvioNominalG: 125.39 },
        lines: lines.map(() => ({ status: 'ok', capacityCm3: 200, glassWeightGrams: 450.52 })) }) };
    }
    let final = u.href;
    if (metodo === 'POST') {
      if (segurarPosts) await new Promise((resolve) => presos.push(resolve));
      if (antesDoPost) antesDoPost(db);
      final = gravarNoServidor(db, u.href, String(options.body));
      if (rede.cairDepoisDoPost) {
        rede.cairDepoisDoPost = false;
        rede.cortada = true;
        throw new window.TypeError('Failed to fetch');
      }
    }
    return { ok: true, redirected: metodo === 'POST', url: final,
      headers: { get: (nome) => (nome.toLowerCase() === 'content-type' ? 'text/html; charset=utf-8' : null) },
      text: async () => documento(db, final) };
  };
  window.Element.prototype.scrollIntoView = function () {};
  for (const ficheiro of ['0_ASSET_CANONICAL_INTERACTIONS.js', 'dmo-live.js', 'js/dmo-suggest.js', 'peso-live.js']) {
    window.eval(fs.readFileSync(path.join(assets, ficheiro), 'utf8'));
  }
  for (const bloco of scriptsDaPagina()) window.eval(bloco);
  await new Promise((resolve) => window.addEventListener('load', resolve, { once: true }));
  window.__marca = 'a mesma página';
  // O servidor trata o POST mais antigo que espera e responde.
  const soltar = async () => {
    for (let i = 0; i < 50 && !presos.length; i++) await esperar(5);
    assert.ok(presos.length, 'um POST à espera');
    presos.shift()();
    await esperar();
  };
  return { dom, window, document: window.document, db, pedidos, navegou, soltar, rede };
}

const esperar = (ms = 30) => new Promise((resolve) => setTimeout(resolve, ms));
const posts = (pedidos) => pedidos.filter((p) => p.metodo === 'POST' && !p.url.includes('Preview'));
const gets = (pedidos) => pedidos.filter((p) => p.metodo === 'GET');
const pilha = (document) => [...document.querySelectorAll('.dmo-toast-pilha [data-dmo-toast]')].map((t) => t.textContent);
const id = (document, valor) => document.getElementById(valor);

// Escreve num campo como o operador: o valor e o evento input.
function escrever(window, campo, valor) {
  campo.value = valor;
  campo.dispatchEvent(new window.Event('input', { bubbles: true }));
}

// O envio de um formulário pelo botão, como o browser o faz: no clique, o
// foco está no botão; no Enter, fica no campo onde se escreveu.
function enviar(window, botao, foco = botao) {
  foco.focus();
  botao.form.dispatchEvent(new window.SubmitEvent('submit', { bubbles: true, cancelable: true, submitter: botao }));
}

// Escolher no combo da comparação com o teclado: escrever no campo de
// filtrar e Enter (o dmo-suggest escolhe a primeira opção filtrada).
function escolherComparacao(window, document, texto) {
  const filtro = id(document, 'compararDropdown-filtro');
  filtro.focus();
  escrever(window, filtro, texto);
  filtro.dispatchEvent(new window.KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true }));
}

test('DMO-689: previous production dates and named differences survive selection and live updates', () => {
  const dom = new JSDOM(`
    <select data-producao-anterior-select>
      <option value="1" data-inicio="01/10/2026" data-fim="10/10/2026">1</option>
      <option value="2" data-inicio="11/10/2026" data-fim="20/10/2026">2</option>
      <option value="3" data-inicio="21/10/2026">3</option>
    </select>
    <div id="pesoAnteriorDelta"><span data-peso-atual="132.89"></span></div>
    <div data-producao-anterior-sap>
      <table><tbody>
        <tr><th scope="row">Datas</th><td colspan="2"><span data-producao-anterior-periodo><span data-inicio></span><span data-fim></span></span><span data-sap-com><span data-sap-valor="dias"></span></span></td></tr>
        <tr data-sap-com><td data-sap-valor="media">130</td><td data-sap-valor="moda">130</td></tr>
        <tr data-sap-com><td data-sap-delta="media"></td><td data-sap-delta="moda"></td></tr>
        <tr data-sap-sem hidden><td>No production weight</td></tr>
      </tbody></table>
      <span hidden data-sap-opcao="1" data-sap-media="130" data-sap-moda="130" data-sap-dias="10"></span>
      <span hidden data-sap-opcao="2" data-sap-media="140" data-sap-moda="135" data-sap-dias="9"></span>
    </div>`, { runScripts: 'outside-only', url: 'http://localhost/Controlo/Peso' });
  try {
    const { window } = dom;
    const document = window.document;
    for (const bloco of scriptsDaPagina()) window.eval(bloco);
    const select = document.querySelector('select');
    const choose = value => {
      select.value = value;
      select.dispatchEvent(new window.Event('change', { bubbles: true }));
    };
    const difference = key => document.querySelector(`[data-sap-delta="${key}"]`).textContent;
    choose('1');
    assert.equal(difference('media'), 'Peso médio atual − Média: +2.89 g (+2.22 %)');
    assert.equal(difference('moda'), 'Peso médio atual − Moda: +2.89 g (+2.22 %)');
    choose('2');
    assert.equal(document.querySelector('table [data-inicio]').textContent, '11/10/2026');
    assert.equal(document.querySelector('table [data-fim]').textContent, '20/10/2026');
    assert.equal(document.querySelector('table [data-sap-valor="dias"]').textContent, '9');
    assert.equal(difference('media'), 'Peso médio atual − Média: −7.11 g (−5.08 %)');
    assert.equal(difference('moda'), 'Peso médio atual − Moda: −2.11 g (−1.56 %)');
    document.querySelector('[data-peso-atual]').setAttribute('data-peso-atual', '140');
    document.dispatchEvent(new window.Event('dmo:troca'));
    assert.equal(difference('media'), 'Peso médio atual − Média: +0.00 g (+0.00 %)');
    assert.equal(difference('moda'), 'Peso médio atual − Moda: +5.00 g (+3.70 %)');
    choose('3');
    assert.equal(document.querySelector('table [data-fim]').textContent, '—');
    assert.equal(document.querySelector('[data-sap-sem]').hidden, false);
    assert.ok([...document.querySelectorAll('[data-sap-com]')].every(el => el.hidden));
    choose('1');
    assert.equal(document.querySelector('[data-sap-sem]').hidden, true);
    assert.equal(difference('media'), 'Peso médio atual − Média: +10.00 g (+7.69 %)');
  } finally {
    dom.window.close();
  }
});

test('a folha de teste usa as marcas das zonas e os scripts do Peso.cshtml', () => {
  for (const [nome, marca] of Object.entries(M)) assert.ok(cshtml.includes(marca), `${nome} não está no Peso.cshtml: ${marca}`);
  // O apoio TP fica (data-dmo-fica) e só os campos escondidos do «Calcular» trocam.
  assert.match(cshtml, /<div class="dmo-zona" data-dmo-fica>\s*<details class="tp-support"/);
  const apoio = cshtml.indexOf(M.apoio);
  assert.ok(apoio < cshtml.indexOf(M.capContexto) && cshtml.indexOf(M.capContexto) < cshtml.indexOf(M.producaoAvisos));
  // A produção anterior fica; os campos escondidos dela são uma zona lá dentro.
  assert.ok(cshtml.indexOf(M.anterior) < cshtml.indexOf(M.anteriorContexto) && cshtml.indexOf(M.anteriorContexto) < cshtml.indexOf(M.delta));
  const blocos = scriptsDaPagina();
  assert.equal(blocos.length, 2, 'os dois blocos de script da página');
  for (const bloco of blocos) assert.ok(!bloco.includes('@'), 'um bloco de script com Razor não pode ser lido daqui');
});

test('registar: a gravação troca as zonas sem recarregar; a leitura nova fica (o mesmo nó), limpa, com o foco no N.º CM', async () => {
  const { dom, window, document, pedidos, navegou } = await abrirFolhaReal();
  try {
    const nova = document.querySelector('[data-dmo-fica] [data-peso-rows]');
    const tabela = id(document, 'pesoLeituras');
    id(document, 'cmSubject').value = '12';
    escrever(window, id(document, 'waterWeight'), '199,64');
    id(document, 'waterWeight').dispatchEvent(new window.Event('blur'));
    document.querySelector('[data-peso-add-row]').click();
    const segunda = document.querySelectorAll('[data-peso-row]')[1];
    segunda.querySelector('input[name="cm_subject"]').value = '13';
    escrever(window, segunda.querySelector('input[name="water_weight"]'), '99.82');
    await esperar();
    enviar(window, document.querySelector('.peso-registar'));
    await esperar();

    assert.deepEqual(navegou, []);
    assert.equal(window.__marca, 'a mesma página');
    assert.equal(posts(pedidos).length, 1, 'um POST');
    assert.notEqual(id(document, 'pesoLeituras'), tabela, 'a tabela das leituras trocou');
    assert.equal(document.querySelectorAll('[data-peso-leitura-gravada]').length, 3, 'os cartões gravados são os do servidor');
    assert.equal(document.querySelector('[data-peso-strip] strong').textContent, '3');
    assert.equal(document.querySelector('[data-dmo-fica] [data-peso-rows]'), nova, 'a leitura nova é o mesmo nó');
    assert.equal(document.querySelectorAll('[data-peso-row]').length, 1, 'fica uma linha só');
    assert.equal(id(document, 'cmSubject').value, '');
    assert.equal(id(document, 'waterWeight').value, '');
    assert.equal(document.activeElement, id(document, 'cmSubject'), 'o foco vai para o N.º CM da próxima leitura');
    assert.equal(window.location.search, '?controlo_id=5');

    // Os ouvintes delegados servem o resumo novo: a próxima leitura pré-visualiza.
    id(document, 'cmSubject').value = '14';
    escrever(window, id(document, 'waterWeight'), '70.5');
    id(document, 'waterWeight').dispatchEvent(new window.Event('blur'));
    await esperar();
    assert.match(document.querySelector('[data-peso-strip]').textContent, /325\.39 g/);
    assert.equal(document.querySelector('[data-peso-row-result]').textContent, '200.000 cm³ · 450.52 g');
  } finally {
    dom.window.close();
  }
});

test('uma recusa da leitura mantém o que o operador escreveu e o foco; a mensagem vai para o canto', async () => {
  const { dom, window, document, navegou } = await abrirFolhaReal();
  try {
    id(document, 'cmSubject').value = '12';
    id(document, 'waterWeight').value = '70,2';
    document.querySelector('[data-peso-add-row]').click();
    document.querySelectorAll('[data-peso-row]')[1].querySelector('input[name="water_weight"]').value = '7o';
    const registar = document.querySelector('.peso-registar');
    enviar(window, registar);
    await esperar();
    assert.deepEqual(navegou, []);
    assert.equal(document.querySelectorAll('[data-peso-row]').length, 2);
    assert.equal(id(document, 'waterWeight').value, '70,2');
    assert.equal(document.querySelectorAll('[data-peso-row]')[1].querySelector('input[name="water_weight"]').value, '7o');
    assert.equal(document.activeElement, registar, 'o foco fica onde estava');
    assert.ok(pilha(document).some((t) => /ilegível/.test(t)), 'a recusa aparece no canto');

    // Corrigida, a gravação seguinte limpa: a marca da recusa não fica pendurada.
    document.querySelectorAll('[data-peso-row]')[1].querySelector('input[name="water_weight"]').value = '70.5';
    enviar(window, registar);
    await esperar();
    assert.equal(document.querySelectorAll('[data-peso-leitura-gravada]').length, 3);
    assert.equal(document.querySelectorAll('[data-peso-row]').length, 1);
    assert.equal(id(document, 'waterWeight').value, '');
  } finally {
    dom.window.close();
  }
});

test('com uma leitura gravada entretanto noutro separador, uma recusa não limpa a leitura a corrigir (E4-07)', async () => {
  const { dom, window, document, navegou } = await abrirFolhaReal({
    antesDoPost: (db) => { if (db.leituras.length === 1) db.leituras.push({ cm: '88', agua: '150.1' }); }
  });
  try {
    id(document, 'cmSubject').value = '12';
    id(document, 'waterWeight').value = '7o';
    enviar(window, document.querySelector('.peso-registar'), id(document, 'waterWeight'));
    await esperar();
    assert.deepEqual(navegou, []);
    assert.equal(document.querySelectorAll('[data-peso-leitura-gravada]').length, 2, 'o cartão da outra pessoa aparece');
    assert.equal(id(document, 'cmSubject').value, '12', 'a leitura recusada fica para corrigir');
    assert.equal(id(document, 'waterWeight').value, '7o');
    assert.equal(document.activeElement, id(document, 'waterWeight'), 'o foco não salta para o N.º CM');
  } finally {
    dom.window.close();
  }
});

// O teclado do operador: as teclas vão para o campo com o foco; o Tab passa
// ao seguinte (sem os campos só de leitura, tabindex -1); o Enter num campo
// envia o formulário (submissão implícita) e num botão carrega nele.
function teclado(window, document) {
  const focaveis = () => [...document.querySelectorAll('input, button, select, textarea')]
    .filter((el) => el.type !== 'hidden' && el.tabIndex !== -1 && !el.disabled);
  return {
    escrever(texto) {
      const campo = document.activeElement;
      if (campo.tagName !== 'INPUT') return;
      campo.value += texto;
      campo.dispatchEvent(new window.Event('input', { bubbles: true }));
    },
    tab() {
      const todos = focaveis();
      todos[todos.indexOf(document.activeElement) + 1].focus();
    },
    enter() {
      const campo = document.activeElement;
      if (campo.tagName === 'BUTTON') { campo.click(); return; }
      campo.form.requestSubmit(campo.form.querySelector('button[type="submit"]'));
    }
  };
}
const leiturasNaBase = (db) => db.leituras.map((l) => `${l.cm}=${l.agua}`);
const rascunho = (document) => [...document.querySelectorAll('[data-peso-row]')]
  .map((row) => `${row.querySelector('input[name="cm_subject"]').value}/${row.querySelector('input[name="water_weight"]').value}`);

test('a leitura seguinte escrita enquanto a anterior grava fica no ecrã, com o foco onde o operador escreve (B-peso-1)', async () => {
  const { dom, window, document, db, pedidos, navegou, soltar } = await abrirFolhaReal({ segurarPosts: true });
  try {
    id(document, 'cmSubject').value = '62';
    escrever(window, id(document, 'waterWeight'), '128,1');
    enviar(window, document.querySelector('.peso-registar'));
    await esperar();
    assert.equal(posts(pedidos).length, 1, 'a gravação partiu');

    // Enquanto grava, o operador clica no N.º CM e escreve a leitura seguinte.
    id(document, 'cmSubject').focus();
    escrever(window, id(document, 'cmSubject'), '63');
    id(document, 'waterWeight').focus();
    escrever(window, id(document, 'waterWeight'), '129,1');
    await soltar();

    assert.deepEqual(navegou, []);
    assert.deepEqual(leiturasNaBase(db), ['45=150.2', '62=128.1']);
    assert.equal(document.querySelectorAll('[data-peso-leitura-gravada]').length, 2, 'a 62 está nos cartões gravados');
    assert.deepEqual(rascunho(document), ['63/129,1'], 'a leitura seguinte continua no ecrã');
    assert.equal(document.activeElement, id(document, 'waterWeight'), 'o foco fica no campo onde o operador escreve');
  } finally {
    dom.window.close();
  }
});

test('o operador rápido (41 Tab 130,1 Enter e logo 42 Tab 130,2 Enter) grava as duas leituras, sem perder nem misturar (B-peso-1)', async () => {
  const { dom, window, document, db, pedidos, navegou, soltar } = await abrirFolhaReal({ segurarPosts: true });
  try {
    const t = teclado(window, document);
    id(document, 'cmSubject').focus();
    t.escrever('41'); t.tab(); t.escrever('130,1'); t.enter();
    // A leitura enviada aparece logo nos cartões, «A gravar…», e a leitura
    // nova fica livre para a seguinte.
    const aGravar = [...document.querySelectorAll('[data-peso-leitura-pendente]')];
    assert.deepEqual(aGravar.map((card) => [...card.querySelectorAll('input')].map((i) => i.value).join('/')), ['41/130.1']);
    assert.match(aGravar[0].textContent, /A gravar…/);
    assert.deepEqual(rascunho(document), ['/']);
    assert.equal(document.activeElement, id(document, 'cmSubject'));
    t.escrever('42'); t.tab(); t.escrever('130,2'); t.enter();
    assert.equal(document.querySelectorAll('[data-peso-leitura-pendente]').length, 2);
    await soltar();
    assert.equal(document.querySelectorAll('[data-peso-leitura-pendente]').length, 1, 'a 42 continua «A gravar…» depois da resposta da 41');
    await soltar();

    assert.deepEqual(navegou, []);
    assert.equal(posts(pedidos).length, 2);
    assert.deepEqual(leiturasNaBase(db), ['45=150.2', '41=130.1', '42=130.2']);
    assert.equal(document.querySelectorAll('[data-peso-leitura-gravada]').length, 3);
    assert.equal(document.querySelectorAll('[data-peso-leitura-pendente]').length, 0);
    assert.deepEqual(rascunho(document), ['/']);
    assert.equal(document.activeElement, id(document, 'cmSubject'));
  } finally {
    dom.window.close();
  }
});

test('cliques repetidos em «Registar leitura» enquanto grava: um POST, uma leitura e sem a bolha «Preencha este campo»', async () => {
  const { dom, window, document, db, pedidos, soltar } = await abrirFolhaReal({ segurarPosts: true });
  try {
    const bolhas = [];
    id(document, 'waterWeight').addEventListener('invalid', (event) => bolhas.push(event.defaultPrevented ? 'calada' : 'bolha'));
    id(document, 'cmSubject').value = '12';
    escrever(window, id(document, 'waterWeight'), '131,5');
    const registar = document.querySelector('.peso-registar');
    registar.click();
    registar.click();
    registar.click();
    await soltar();
    assert.equal(posts(pedidos).length, 1);
    assert.deepEqual(leiturasNaBase(db), ['45=150.2', '12=131.5']);
    assert.ok(!bolhas.includes('bolha'), 'nenhuma bolha do browser');
    assert.equal(document.activeElement, id(document, 'cmSubject'), 'o foco volta ao N.º CM da próxima leitura');
  } finally {
    dom.window.close();
  }
});

// Sem ligação, a leitura enviada espera que ela volte, «A gravar…» (deep
// sweep, 18/10/2026, B-peso-3): só a confirmação (um GET, quando a ligação
// volta) diz se ficou gravada. Antes voltava logo à leitura nova e, se o
// servidor a tinha gravado, um novo «Registar leitura» gravava-a outra vez.
const ligacaoVolta = (window, rede) => { rede.cortada = false; window.dispatchEvent(new window.Event('online')); };

test('sem ligação, a leitura enviada espera que ela volte; sem nada gravado, volta à leitura nova para repetir, com o foco onde estava', async () => {
  const { dom, window, document, db, rede } = await abrirFolhaReal();
  try {
    window.DmoTroca.tempos.confirmar = 60000;
    id(document, 'cmSubject').value = '12';
    escrever(window, id(document, 'waterWeight'), '131,5');
    rede.cortada = true;
    enviar(window, document.querySelector('.peso-registar'), id(document, 'waterWeight'));
    await esperar();
    assert.deepEqual(leiturasNaBase(db), ['45=150.2']);
    assert.ok(pilha(document).some((t) => /O que escreveu continua no ecrã/.test(t)));
    assert.equal(document.querySelectorAll('[data-peso-leitura-pendente]').length, 1, 'a leitura enviada espera, «A gravar…»');
    assert.deepEqual(rascunho(document), ['/']);
    ligacaoVolta(window, rede);
    await esperar();
    assert.deepEqual(leiturasNaBase(db), ['45=150.2']);
    assert.deepEqual(rascunho(document), ['12/131,5'], 'não ficou gravada: volta à leitura nova');
    assert.equal(document.querySelectorAll('[data-peso-leitura-pendente]').length, 0);
    assert.equal(document.activeElement, id(document, 'waterWeight'));
    assert.ok(pilha(document).some((t) => /mostra agora o que ficou gravado/.test(t)));
  } finally {
    dom.window.close();
  }
});

test('a ligação cai depois de o servidor gravar a leitura: quando volta, a leitura aparece gravada, não volta ao rascunho e não se grava duas vezes (B-peso-3)', async () => {
  const { dom, window, document, db, pedidos, rede } = await abrirFolhaReal();
  try {
    window.DmoTroca.tempos.confirmar = 60000;
    id(document, 'cmSubject').value = '52';
    escrever(window, id(document, 'waterWeight'), '131,2');
    rede.cairDepoisDoPost = true;
    enviar(window, document.querySelector('.peso-registar'), id(document, 'waterWeight'));
    await esperar();
    assert.deepEqual(leiturasNaBase(db), ['45=150.2', '52=131.2'], 'o servidor gravou');
    assert.equal(document.querySelectorAll('[data-peso-leitura-pendente]').length, 1, 'sem ligação, a leitura espera, «A gravar…»');
    assert.deepEqual(rascunho(document), ['/'], 'e não volta ao rascunho, pronta a repetir');
    ligacaoVolta(window, rede);
    await esperar();
    assert.equal(posts(pedidos).length, 1, 'o POST não se repete');
    assert.deepEqual(leiturasNaBase(db), ['45=150.2', '52=131.2'], 'gravada uma vez');
    assert.equal(document.querySelectorAll('[data-peso-leitura-pendente]').length, 0);
    assert.equal(document.querySelectorAll('#pesoLeiturasGravadas [data-peso-leitura-gravada]').length, 2, 'a leitura aparece gravada');
    assert.deepEqual(rascunho(document), ['/'], 'a leitura nova fica vazia');
    assert.ok(pilha(document).some((t) => /mostra agora o que ficou gravado/.test(t)));
  } finally {
    dom.window.close();
  }
});

test('escolher a comparação troca a tabela e o resumo; a leitura, o peso do SAP e a produção anterior por guardar ficam (E4-01)', async () => {
  const { dom, window, document, pedidos, navegou } = await abrirFolhaReal();
  try {
    escrever(window, id(document, 'waterWeight'), '199.64');
    id(document, 'waterWeight').dispatchEvent(new window.Event('blur'));
    await esperar();
    id(document, 'producaoAnteriorPeso').value = '305,5';
    const anterior = id(document, 'producaoAnterior');
    anterior.value = '12';
    anterior.dispatchEvent(new window.Event('change', { bubbles: true }));
    const previews = pedidos.filter((p) => p.url.includes('Preview')).length;

    escolherComparacao(window, document, '202604');
    await esperar();
    assert.deepEqual(navegou, []);
    assert.equal(gets(pedidos).length, 1, 'um GET');
    assert.equal(window.location.search, '?controlo_id=5&comparar=22');
    assert.ok(document.querySelector('[data-peso-comparacao-tabela]'), 'a tabela da comparação aparece');
    assert.equal(document.querySelector('[data-peso-comparison] strong').textContent, '202604');
    assert.equal(id(document, 'waterWeight').value, '199.64', 'a leitura em curso fica');
    assert.equal(pedidos.filter((p) => p.url.includes('Preview')).length, previews + 1, 'e volta a pré-visualizar');
    assert.match(document.querySelector('[data-peso-strip]').textContent, /325\.39 g/);
    assert.equal(id(document, 'producaoAnteriorPeso').value, '305,5', 'o peso do SAP por guardar fica');
    assert.equal(id(document, 'producaoAnterior').value, '12', 'a produção anterior escolhida fica');
    assert.equal(document.querySelector('[data-producao-anterior-periodo] [data-inicio]').textContent, '06/04/2026');
    // A comparação nova vai em todos os formulários que levam a folha de volta.
    for (const zona of ['pesoLeituraContexto', 'pesoAnteriorContexto', 'pesoCapContexto', 'pesoPdf', 'pesoSupomos']) {
      assert.equal(id(document, zona).querySelector('input[name="comparar"]').value, '22', zona);
    }

    // Guardar o SAP a seguir grava o que está escrito e não volta à comparação de antes.
    enviar(window, id(document, 'producaoAnteriorGuardar'));
    await esperar();
    assert.deepEqual(navegou, []);
    const corpo = new URLSearchParams(posts(pedidos)[0].corpo);
    assert.deepEqual([corpo.get('anterior'), corpo.get('anterior_peso'), corpo.get('comparar')], ['12', '305,5', '22']);
    assert.equal(document.querySelector('[data-peso-comparison] strong').textContent, '202604');
    assert.equal(document.activeElement, id(document, 'producaoAnteriorGuardar'), 'o foco fica no «Guardar»');
    assert.ok(pilha(document).some((t) => /guardada/.test(t)));
    assert.match(document.querySelector('[data-producao-anterior-delta]').textContent, /305,5 g/);
    assert.equal(id(document, 'waterWeight').value, '199.64');
  } finally {
    dom.window.close();
  }
});

test('depois de escolher a comparação no combo, com o teclado ou com o rato, o foco volta ao campo de filtrar, com a lista fechada e o texto selecionado (E4-02)', async () => {
  const { dom, window, document, navegou } = await abrirFolhaReal();
  try {
    escolherComparacao(window, document, '202604');
    await esperar();
    assert.deepEqual(navegou, []);
    assert.equal(document.querySelector('[data-peso-comparison] strong').textContent, '202604');
    let filtro = id(document, 'compararDropdown-filtro');
    assert.equal(document.activeElement, filtro, 'o foco continua no combo da comparação');
    assert.equal(filtro.getAttribute('aria-expanded'), 'false', 'a lista não volta a abrir');
    assert.equal(document.querySelectorAll('#pesoComparacao .dmo-suggest-combo').length, 1, 'um combo só');
    assert.equal(filtro.value.split(' · ')[0], '202604');
    assert.deepEqual([filtro.selectionStart, filtro.selectionEnd], [0, filtro.value.length], 'o texto da escolha fica selecionado');

    // Com o rato: abrir a lista e escolher a outra produção. O clique num campo
    // que já tem o foco põe o cursor onde se clicou e não passa pelo focus.
    filtro.setSelectionRange(filtro.value.length, filtro.value.length);
    filtro.focus();
    filtro.dispatchEvent(new window.MouseEvent('click', { bubbles: true }));
    assert.deepEqual([filtro.selectionStart, filtro.selectionEnd], [0, filtro.value.length], 'escrever substitui a escolha');
    const opcao = [...filtro.closest('.dmo-suggest').querySelectorAll('li[role=option]')].find((li) => li.textContent.startsWith('202601'));
    opcao.dispatchEvent(new window.MouseEvent('mousedown', { bubbles: true, cancelable: true }));
    await esperar();
    assert.equal(document.querySelector('[data-peso-comparison] strong').textContent, '202601');
    filtro = id(document, 'compararDropdown-filtro');
    assert.equal(document.activeElement, filtro);
    assert.equal(filtro.getAttribute('aria-expanded'), 'false');

    // Se o operador já foi para outro campo enquanto a resposta chegava, o foco fica lá.
    escolherComparacao(window, document, '202604');
    id(document, 'waterWeight').focus();
    await esperar();
    assert.equal(document.querySelector('[data-peso-comparison] strong').textContent, '202604');
    assert.equal(document.activeElement, id(document, 'waterWeight'));
  } finally {
    dom.window.close();
  }
});

test('depois de uma recusa do SAP, a correção ainda por guardar fica ao registar uma leitura e no «Supomos» (E4-01)', async () => {
  const { dom, window, document, navegou } = await abrirFolhaReal();
  try {
    id(document, 'producaoAnteriorPeso').value = '3o5';
    enviar(window, id(document, 'producaoAnteriorGuardar'));
    await esperar();
    assert.ok(pilha(document).some((t) => /maior que 0/.test(t)), 'a recusa aparece no canto');
    assert.match(window.location.search, /anterior_recusa=peso/);
    assert.equal(id(document, 'producaoAnteriorPeso').value, '3o5');

    id(document, 'producaoAnteriorPeso').value = '305';
    id(document, 'cmSubject').value = '12';
    id(document, 'waterWeight').value = '199.64';
    enviar(window, document.querySelector('.peso-registar'));
    await esperar();
    assert.equal(document.querySelectorAll('[data-peso-leitura-gravada]').length, 2);
    assert.equal(id(document, 'producaoAnteriorPeso').value, '305', 'a correção por guardar fica depois de registar');

    id(document, 'suporCalote').value = '5,2';
    enviar(window, id(document, 'suporCaloteVer'));
    await esperar();
    assert.ok(document.querySelector('[data-supor-resultado]'));
    assert.equal(id(document, 'producaoAnteriorPeso').value, '305', 'e depois do «Supomos»');
    assert.deepEqual(navegou, []);
  } finally {
    dom.window.close();
  }
});

test('depois do «Calcular» (cap_h no endereço), a comparação, o «Supomos» e o SAP trocam só as zonas; o apoio fica aberto (E4-03)', async () => {
  const { dom, window, document, pedidos, navegou } = await abrirFolhaReal({ endereco: BASE + '?controlo_id=5&cap_h=4.5' });
  try {
    const apoio = document.querySelector('.tp-support');
    assert.ok(apoio.open);
    id(document, 'waterWeight').value = '199.64';

    escolherComparacao(window, document, '202604');
    await esperar();
    assert.deepEqual(navegou, [], 'escolher a comparação não abre a página inteira');
    assert.equal(document.querySelector('.tp-support'), apoio);
    assert.ok(apoio.open, 'o apoio continua aberto');
    assert.equal(id(document, 'capSimulada').value, '4.5');
    assert.equal(id(document, 'waterWeight').value, '199.64');
    // O «Calcular» seguinte (um GET da página inteira) leva a comparação escolhida.
    const calcular = new URLSearchParams(new window.FormData(document.querySelector('.tp-sim-form')));
    assert.deepEqual([calcular.get('controlo_id'), calcular.get('comparar'), calcular.get('cap_h')], ['5', '22', '4.5']);

    id(document, 'suporCalote').value = '5,2';
    enviar(window, id(document, 'suporCaloteVer'));
    await esperar();
    assert.deepEqual(navegou, [], 'o «Supomos» não abre a página inteira');
    assert.ok(document.querySelector('[data-supor-resultado]'));

    id(document, 'producaoAnteriorPeso').value = '305';
    enviar(window, id(document, 'producaoAnteriorGuardar'));
    await esperar();
    assert.deepEqual(navegou, [], 'guardar o SAP não abre a página inteira');
    assert.equal(gets(pedidos).length, 2, 'um GET por escolha, sem pedidos repetidos');
    assert.equal(posts(pedidos).length, 1);
    assert.equal(id(document, 'waterWeight').value, '199.64', 'a leitura em curso fica');
    assert.ok(apoio.open);
  } finally {
    dom.window.close();
  }
});

test('o «Supomos» aberto fica ao registar uma leitura e ao guardar o SAP, recalculado com a média nova (E4-04)', async () => {
  const { dom, window, document, pedidos, navegou } = await abrirFolhaReal();
  try {
    escolherComparacao(window, document, '202604');
    await esperar();
    id(document, 'suporCalote').value = '5,2';
    enviar(window, id(document, 'suporCaloteVer'));
    await esperar();
    assert.match(document.querySelector('[data-supor-resultado]').textContent, /média 150\.20/);

    id(document, 'cmSubject').value = '12';
    id(document, 'waterWeight').value = '160.2';
    enviar(window, document.querySelector('.peso-registar'));
    await esperar();
    assert.equal(new URLSearchParams(posts(pedidos)[0].corpo).get('supor_calote'), '5,2', 'o registo leva o «Supomos»');
    assert.ok(document.querySelector('[data-supor-resultado]'), 'o resultado do «Supomos» fica');
    assert.match(document.querySelector('[data-supor-resultado]').textContent, /média 155\.20/, 'recalculado com a leitura nova');
    assert.equal(id(document, 'suporCalote').value, '5,2');
    assert.equal(document.querySelector('[data-peso-comparison] strong').textContent, '202604');
    assert.equal(document.activeElement, id(document, 'cmSubject'));

    enviar(window, id(document, 'producaoAnteriorGuardar'));
    await esperar();
    assert.ok(document.querySelector('[data-supor-resultado]'), 'e ao guardar o SAP');
    assert.equal(id(document, 'suporCalote').value, '5,2');
    assert.deepEqual(navegou, []);
  } finally {
    dom.window.close();
  }
});

test('um «Supomos» com valor inválido mostra a mensagem no canto, sem a pôr no meio da página (T10)', async () => {
  const { dom, window, document, pedidos, navegou } = await abrirFolhaReal();
  try {
    id(document, 'suporCalote').value = 'abc';
    enviar(window, id(document, 'suporCaloteVer'));
    await esperar();
    assert.deepEqual(navegou, []);
    assert.ok(pilha(document).some((t) => /«abc» não é um número válido/.test(t)), 'a mensagem está na pilha do canto');
    assert.equal(id(document, 'pesoSupomos').querySelector('[data-dmo-toast]'), null, 'nada fica no fluxo da página');
    assert.equal(document.activeElement, id(document, 'suporCaloteVer'));

    // O valor ilegível não segue com o registo: a recusa não volta a cada leitura.
    const recusas = pilha(document).filter((t) => /«abc»/.test(t)).length;
    id(document, 'waterWeight').value = '160.2';
    enviar(window, document.querySelector('.peso-registar'));
    await esperar();
    assert.equal(new URLSearchParams(posts(pedidos)[0].corpo).get('supor_calote'), null);
    assert.equal(window.location.search, '?controlo_id=5');
    assert.ok(pilha(document).filter((t) => /«abc»/.test(t)).length <= recusas, 'sem a mesma recusa outra vez');
  } finally {
    dom.window.close();
  }
});

// O aviso «Pré-visualização indisponível» fica até se fechar, como as outras
// mensagens da casa (regra 109): sai sozinho quando a pré-visualização volta
// a responder, para não ficar no canto com a ligação já de volta.
test('sem rede, um só aviso «Pré-visualização indisponível», que sai quando a pré-visualização volta', async () => {
  const { dom, window, document, rede } = await abrirFolhaReal();
  const indisponivel = (doc) => [...doc.querySelectorAll('.dmo-toast-pilha [data-dmo-toast="aviso"]:not(.a-sair)')]
    .filter((t) => /Pré-visualização indisponível/.test(t.textContent));
  try {
    rede.cortada = true;
    escrever(window, id(document, 'waterWeight'), '199');
    await esperar(400);
    escrever(window, id(document, 'waterWeight'), '199.6');
    await esperar(400);
    assert.equal(indisponivel(document).length, 1, 'um só aviso, sem rede');

    rede.cortada = false;
    escrever(window, id(document, 'waterWeight'), '199.64');
    await esperar(400);
    assert.equal(indisponivel(document).length, 0, 'com a pré-visualização de volta, o aviso sai');
    assert.match(document.querySelector('[data-peso-strip]').textContent, /325\.39 g/);
  } finally {
    dom.window.close();
  }
});

// Sweep de 19/10/2026, FE-8: a resposta da pré-visualização só conta se
// ainda for a do que está escrito. O pedido antigo só se cancela quando sai
// o seguinte (300 ms depois da última tecla), e uma resposta que chegava
// nesse intervalo punha os valores por posição nas linhas de agora: ao
// apagar a massa da linha 2, a linha 3 mostrava o volume da linha 2.
test('FE-8: uma resposta antiga da pré-visualização não põe os valores nas linhas erradas', async () => {
  const linhas = [1, 2, 3].map((n) => `<div data-peso-row><input name="cm_subject" value="${n}">`
    + `<input id="agua${n}" name="water_weight"><div id="resultado${n}" data-peso-row-result></div></div>`).join('');
  const dom = new JSDOM(`
    <section data-peso-live data-peso-id="7" data-preview-url="/Controlo/Peso?handler=Preview">
      <div data-peso-strip></div>
      <div data-peso-summary></div>
      <form class="peso-leitura-form" data-peso-rows-form><div data-peso-rows>${linhas}</div></form>
    </section>`, { runScripts: 'outside-only' });
  try {
    const { window } = dom;
    const document = window.document;
    const presos = [];
    // Cada linha responde com o volume igual à massa de água, para se ver de que linha veio.
    window.fetch = (_url, options) => new Promise((resolve) => {
      const lines = JSON.parse(new URLSearchParams(options.body).get('lines'));
      presos.push(() => resolve({ ok: true, json: async () => ({
        context: { nominalWeightGrams: 200 },
        totals: { sampleCount: lines.length },
        lines: lines.map((line) => ({ status: 'ok', capacityCm3: Number(line.water_weight), glassWeightGrams: 2 * Number(line.water_weight) })),
      }) }));
    });
    window.eval(fs.readFileSync(path.join(assets, 'dmo-live.js'), 'utf8'));
    window.eval(fs.readFileSync(path.join(assets, 'peso-live.js'), 'utf8'));
    await new Promise((resolve) => window.addEventListener('load', resolve, { once: true }));
    const resultado = (n) => document.getElementById(`resultado${n}`).textContent;
    const pausa = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

    document.getElementById('agua1').value = '100';
    document.getElementById('agua2').value = '110';
    document.getElementById('agua3').value = '120';
    document.getElementById('agua3').dispatchEvent(new window.Event('blur'));
    assert.equal(presos.length, 1, 'o pedido das três linhas partiu');

    // O operador apaga a massa da linha 2, e a resposta antiga chega antes do pedido seguinte.
    const agua2 = document.getElementById('agua2');
    agua2.value = '';
    agua2.dispatchEvent(new window.Event('input', { bubbles: true }));
    presos.shift()();
    await pausa(20);
    assert.doesNotMatch(resultado(3), /110/, 'a linha 3 nunca mostra o volume da linha 2');
    assert.equal(resultado(2), '');

    // O pedido seguinte, com as linhas 1 e 3, põe cada valor na sua linha.
    await pausa(350);
    assert.equal(presos.length, 1, 'o pedido das linhas de agora partiu');
    presos.shift()();
    await pausa(20);
    assert.match(resultado(1), /^100\.000 cm³/);
    assert.equal(resultado(2), '');
    assert.match(resultado(3), /^120\.000 cm³/);
  } finally {
    dom.window.close();
  }
});
