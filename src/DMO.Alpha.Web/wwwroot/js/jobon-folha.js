// A FOLHA NOVA DO JOB ON (/JobOn/Folha, Diogo 22/10/2026).
// - O duplo clique numa ferramenta abre a ficha dela (sempre).
// - No Editar, um clique na ferramenta abre a procura para a mudar; as
//   sugestões vêm do mesmo servidor do Planeamento (?handler=ToolCandidates),
//   filtradas pela linha e pela referência da produção.
// - No Editar, clicar no artigo abre a escolha da imagem. A imagem fica neste
//   browser (por referência); ainda não é gravada no servidor.
// Sem este ficheiro, a folha lê-se e grava-se na mesma.
(() => {
  const ESPERA_CLIQUE = 260;
  const IMAGEM = 'dmo-jobon-artigo:';
  // O lote sem zeros à esquerda, como o servidor mostra (LoteTexto).
  const lote = valor => /^\d+$/.test(String(valor || '').trim()) ? (String(valor).trim().replace(/^0+/, '') || '0') : String(valor || '').trim();

  function ler(chave) { try { return window.localStorage.getItem(chave); } catch (e) { return null; } }
  function escrever(chave, valor) { try { window.localStorage.setItem(chave, valor); return true; } catch (e) { return false; } }

  function ferramentas(folha, editar) {
    const reference = document.getElementById('lightReference');
    const machine = document.getElementById('lightMachine');
    folha.querySelectorAll('[data-jof-tool-slot]').forEach(slot => {
      const botao = slot.querySelector('[data-jof-tool]');
      const procura = slot.querySelector('[data-jof-tool-procura]');
      const select = procura && procura.querySelector('select');
      const ref = botao && botao.querySelector('[data-jof-tool-ref]');
      const lt = botao && botao.querySelector('[data-jof-tool-lote]');
      if (!botao) return;
      const tipo = botao.getAttribute('data-jof-tool');
      let espera = null;

      function abrirFicha() {
        const ficha = botao.getAttribute('data-jof-ficha');
        if (ficha) (window.dmoIr || (url => window.location.assign(url)))(ficha);
      }
      function mudar() {
        if (!procura) return;
        procura.hidden = false;
        slot.classList.add('is-mudar');
        const campo = procura.querySelector('input.dmo-suggest-combo') || select;
        if (campo) campo.focus();
      }

      botao.addEventListener('click', () => {
        if (!editar) return;
        // Um clique muda; o segundo clique logo a seguir é o duplo clique (abre a ficha).
        if (espera) return;
        espera = setTimeout(() => { espera = null; mudar(); }, ESPERA_CLIQUE);
      });
      botao.addEventListener('dblclick', () => {
        if (espera) { clearTimeout(espera); espera = null; }
        abrirFicha();
      });

      if (!select) return;
      select.dmoSource = async term => {
        const query = new URLSearchParams({ handler: 'ToolCandidates', type: tipo, q: term, reference: reference ? reference.value.trim() : '', all: 'false' });
        if (machine && machine.value) query.set('machine_id', machine.value);
        const response = await fetch('/JobOn?' + query, { credentials: 'same-origin' });
        if (!response.ok) throw new Error('Sem sugestões');
        const page = await response.json();
        return {
          items: page.items.map(item => ({ value: item.toolId, label: item.reference + ' · Lote ' + lote(item.lot) })),
          note: page.total > page.items.length ? page.items.length + ' de ' + page.total + '. Escreva para afinar.' : '',
          empty: term ? 'Sem resultados para «' + term + '».' : 'Escreva a referência da ' + tipo + '.'
        };
      };
      select.addEventListener('change', () => {
        const option = select.selectedOptions[0];
        if (select.value && option) {
          // «5447 · Lote 3» → a referência e o lote na cabeça do cartão.
          const partes = option.text.trim().split(' · ');
          if (ref) ref.textContent = partes[0] || '';
          if (lt) lt.textContent = lote((partes[1] || '').replace(/^Lote\s*/, ''));
          botao.classList.remove('tool-empty');
          botao.setAttribute('data-jof-ficha', '/Ferramentas/Ficha?tool_id=' + encodeURIComponent(select.value));
        }
        procura.hidden = true;
        slot.classList.remove('is-mudar');
      });
      procura.addEventListener('keydown', event => {
        if (event.key === 'Escape') { procura.hidden = true; slot.classList.remove('is-mudar'); botao.focus(); }
      });
    });
  }

  function artigo(folha) {
    const figura = folha.querySelector('[data-jof-artigo]');
    if (!figura) return;
    const img = figura.querySelector('[data-jof-artigo-img]');
    const vazio = figura.querySelector('[data-jof-artigo-vazio]');
    const ficheiro = figura.querySelector('[data-jof-artigo-ficheiro]');
    const chave = IMAGEM + (figura.getAttribute('data-jof-artigo') || '');
    function mostrar(src) {
      if (!img || !src) return;
      img.src = src;
      img.hidden = false;
      if (vazio) vazio.style.display = 'none';
    }
    mostrar(ler(chave));
    if (!ficheiro) return;
    ficheiro.addEventListener('change', () => {
      const escolhido = ficheiro.files && ficheiro.files[0];
      if (!escolhido || !/^image\//.test(escolhido.type)) return;
      const leitor = new FileReader();
      leitor.onload = () => {
        mostrar(String(leitor.result));
        escrever(chave, String(leitor.result));
      };
      leitor.readAsDataURL(escolhido);
    });
  }

  // O painel das linhas (o mockup: uma gaveta que o botão «Linhas» abre e o
  // × ou o fundo fecham). A escolha fica neste browser.
  const PAINEL = 'dmo-jobon-folha-painel';
  // IMPRIMIR — PONTO DE TROCA: hoje imprime a folha no browser (window.print()
  // e o @media print do dmo-jobon-folha.css). O gerador das 4 folhas, quando
  // chegar, substitui window.dmoJobOnImprimir (recebe o jobon_id); nunca PDF no servidor.
  window.dmoJobOnImprimir = window.dmoJobOnImprimir || function () {
    document.querySelectorAll('[data-jof-tecnica]').forEach(function (d) { d.open = true; });
    window.print();
  };
  function imprimir() {
    document.querySelectorAll('[data-jof-imprimir]').forEach(function (b) {
      b.addEventListener('click', function () {
        var m = /jobon_id=(\d+)/.exec(location.search);
        window.dmoJobOnImprimir(m ? Number(m[1]) : null);
      });
    });
  }
  // CS · Furos no Editar: junta Aberto/Fechado e o número em «Fechado · 4» no campo cs_furos.
  function furos() {
    document.querySelectorAll('[data-jof-furos]').forEach(function (z) {
      var valor = z.querySelector('[data-jof-furos-valor]');
      var estado = z.querySelector('[data-jof-furos-estado]');
      var numero = z.querySelector('[data-jof-furos-numero]');
      function juntar() {
        var e = estado.value, n = (numero.value || '').trim();
        if (!e && !n) return; // nada escolhido: o valor antigo fica
        valor.value = e ? (n ? e + ' · ' + n : e) : n;
        valor.dispatchEvent(new Event('input', { bubbles: true }));
      }
      estado.addEventListener('change', juntar);
      numero.addEventListener('input', juntar);
    });
  }
  // Produções: a <select> fica (sem JS funciona e é ela que o data-dmo-ir segue);
  // por cima desenha-se uma lista fechada que, aberta, cai POR CIMA do resto.
  function producoes() {
    var sel = document.getElementById('jofProducoes');
    if (!sel || sel.dataset.jofDrop) return;
    sel.dataset.jofDrop = '1';
    var caixa = document.createElement('div');
    caixa.className = 'prod-drop';
    var botao = document.createElement('button');
    botao.type = 'button';
    botao.className = 'prod-trigger';
    botao.setAttribute('aria-haspopup', 'listbox');
    botao.setAttribute('aria-expanded', 'false');
    botao.setAttribute('aria-label', sel.getAttribute('aria-label') || 'Produção');
    botao.setAttribute('data-jof-producoes-abrir', '');
    var lista = document.createElement('ul');
    lista.className = 'prod-list';
    lista.setAttribute('role', 'listbox');
    lista.hidden = true;
    function texto() { var o = sel.options[sel.selectedIndex]; botao.textContent = o ? o.textContent : ''; }
    Array.prototype.forEach.call(sel.options, function (o, i) {
      var li = document.createElement('li');
      li.setAttribute('role', 'option');
      li.textContent = o.textContent;
      li.setAttribute('aria-selected', i === sel.selectedIndex ? 'true' : 'false');
      li.addEventListener('click', function () {
        fechar();
        if (i === sel.selectedIndex) return;
        sel.selectedIndex = i;
        texto();
        sel.dispatchEvent(new Event('change', { bubbles: true }));
      });
      lista.appendChild(li);
    });
    function abrir() { lista.hidden = false; botao.setAttribute('aria-expanded', 'true'); }
    function fechar() { lista.hidden = true; botao.setAttribute('aria-expanded', 'false'); }
    botao.addEventListener('click', function () { lista.hidden ? abrir() : fechar(); });
    document.addEventListener('click', function (e) { if (!caixa.contains(e.target)) fechar(); });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') fechar(); });
    sel.parentNode.insertBefore(caixa, sel);
    caixa.appendChild(sel);
    caixa.appendChild(botao);
    caixa.appendChild(lista);
    sel.hidden = true;
    texto();
  }
  function painel() {
    const rail = document.querySelector('[data-jof-rail]');
    const abrir = document.querySelector('[data-jof-rail-abrir]');
    if (!rail || !abrir) return;
    const fundo = document.querySelector('[data-jof-rail-fundo]');
    const fechar = rail.querySelector('[data-jof-rail-fechar]');
    function mostrar(aberto, guardar) {
      rail.classList.toggle('open', aberto);
      rail.inert = !aberto;
      rail.setAttribute('aria-hidden', aberto ? 'false' : 'true');
      if (fundo) fundo.hidden = !aberto;
      abrir.setAttribute('aria-expanded', aberto ? 'true' : 'false');
      if (guardar) escrever(PAINEL, aberto ? '1' : '0');
    }
    mostrar(ler(PAINEL) === '1', false);
    abrir.addEventListener('click', () => {
      mostrar(true, true);
      const alvo = rail.querySelector('.rail-item.selected') || rail.querySelector('.rail-item') || fechar;
      if (alvo) alvo.focus();
    });
    if (fechar) fechar.addEventListener('click', () => { mostrar(false, true); abrir.focus(); });
    if (fundo) fundo.addEventListener('click', () => mostrar(false, true));
    document.addEventListener('keydown', event => {
      if (event.key === 'Escape' && rail.classList.contains('open')) { mostrar(false, true); abrir.focus(); }
    });
  }

  function init() {
    painel();
    imprimir();
    producoes();
    furos();
    const folha = document.querySelector('[data-jof-folha]');
    if (!folha) return;
    const editar = folha.getAttribute('data-jof-folha') === 'editar';
    ferramentas(folha, editar);
    artigo(folha);
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
  else init();
})();
