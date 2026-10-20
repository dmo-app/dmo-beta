const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// AS MENSAGENS DO DMOLive.toast VÃO PARA A PILHA DA CASA (regra 109 e
// B-peso-7 do deep sweep de 18/10/2026): havia uma segunda pilha no canto de
// baixo, com cores fora da paleta, que não esmorecia ao sair; e sem rede, no
// Peso, cada tecla juntava mais um aviso igual. Agora a mensagem entra na
// pilha das data-dmo-toast (a do canto, com «×») e a mesma mensagem só
// aparece uma vez enquanto estiver à vista.

const esperar = (ms = 20) => new Promise((resolve) => setTimeout(resolve, ms));

function abrir(html = '<main><h2>Peso</h2></main>') {
  const dom = new JSDOM(`<!doctype html><title>DMO</title><body>${html}</body>`,
    { url: 'http://dmo.test/Controlo/Peso', runScripts: 'outside-only' });
  dom.window.eval(fs.readFileSync(path.join(assets, 'dmo-live.js'), 'utf8'));
  return { dom, window: dom.window, document: dom.window.document };
}

const visiveis = (document, tipo) => Array.from(
  document.querySelectorAll(`.dmo-toast-pilha [data-dmo-toast${tipo ? `="${tipo}"` : ''}]:not(.a-sair)`));
const texto = (m) => Array.from(m.childNodes).filter((n) => !(n.classList && n.classList.contains('dmo-toast-fechar'))).map((n) => n.textContent).join('');

test('Sem rede, seis falhas da pré-visualização mostram um só aviso, na pilha da casa', () => {
  const { dom, window, document } = abrir();
  const aviso = 'Pré-visualização indisponível — os valores podem estar desatualizados; o registo continua no botão «Registar leitura»';
  for (let i = 0; i < 6; i += 1) window.DMOLive.toast(aviso, { kind: 'warn' });
  const avisos = visiveis(document, 'aviso');
  assert.equal(avisos.length, 1, 'um só aviso');
  assert.equal(texto(avisos[0]), aviso);
  assert.ok(avisos[0].querySelector('.dmo-toast-fechar'), 'fecha-se no «×», como as outras');
  assert.equal(document.querySelector('.dmo-live-region'), null, 'já não há a segunda pilha no canto de baixo');
  assert.equal(document.getElementById('dmo-live-styles'), null, 'nem as cores soltas em JS');
  dom.window.close();
});

test('Fechado o aviso, a mesma mensagem volta a aparecer na falha seguinte', async () => {
  const { dom, window, document } = abrir();
  window.DMOLive.toast('Sem ligação.', { kind: 'warn' });
  visiveis(document, 'aviso')[0].querySelector('.dmo-toast-fechar').click();
  assert.equal(visiveis(document, 'aviso').length, 0);
  window.DMOLive.toast('Sem ligação.', { kind: 'warn' });
  assert.equal(visiveis(document, 'aviso').length, 1);
  await esperar(260);
  assert.equal(document.querySelectorAll('.dmo-toast-pilha [data-dmo-toast]').length, 1, 'a fechada saiu da página');
  dom.window.close();
});

test('Os tipos seguem os da casa: erro, aviso e ok; mensagens diferentes empilham-se', async () => {
  const { dom, window, document } = abrir('<div data-dmo-toast="ok" role="status">Leitura registada.</div>');
  await esperar();
  window.DMOLive.toast('Peso indicado não existe — pré-visualização indisponível', { kind: 'error' });
  window.DMOLive.toast('PDF guardado em Controlo 2026.', { kind: 'success' });
  window.DMOLive.toast('Os PDFs voltam a ir para as Transferências do navegador.', { kind: 'info' });
  assert.equal(document.querySelectorAll('.dmo-toast-pilha').length, 1, 'uma pilha só');
  assert.deepEqual(visiveis(document).map((m) => m.getAttribute('data-dmo-toast')), ['ok', 'erro', 'ok', 'ok']);
  const erro = visiveis(document, 'erro')[0];
  assert.equal(erro.getAttribute('role'), 'alert');
  assert.equal(visiveis(document, 'ok')[1].getAttribute('role'), 'status');
  dom.window.close();
});

test('Ao fim do tempo a mensagem esmorece (a-sair) antes de sair', async () => {
  const { dom, window, document } = abrir();
  const node = window.DMOLive.toast('Pasta associada.', { kind: 'success', timeout: 30 });
  await esperar(60);
  assert.ok(node.classList.contains('a-sair'), 'esmorece com a transição da pilha');
  assert.ok(node.parentNode, 'ainda está na página enquanto esmorece');
  await esperar(260);
  assert.equal(node.parentNode, null);
  dom.window.close();
});
