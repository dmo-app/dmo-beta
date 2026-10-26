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

  // A nota curta da lista: só quando explica o que não se vê (a lista é estreita).
  function nota(filtro) {
    const texto = String(filtro || '');
    const mais = /escreva para ver mais (\d+)/.exec(texto);
    if (mais) return 'Escreva para ver mais ' + mais[1] + '.';
    if (/^Catálogo completo \(/.test(texto)) return 'Nenhuma da referência: todas.';
    if (/nenhuma ligada a esta linha/.test(texto)) return 'Nenhuma ligada a esta linha.';
    return '';
  }

  function ferramentas(folha, editar) {
    const reference = document.getElementById('lightReference');
    const machine = document.getElementById('lightMachine');
    folha.querySelectorAll('[data-jof-tool-slot]').forEach(slot => {
      const botao = slot.querySelector('[data-jof-tool]');
      const procura = slot.querySelector('[data-jof-tool-procura]');
      const select = procura && procura.querySelector('select');
      let ref = botao && botao.querySelector('[data-jof-tool-ref]');
      let lt = botao && botao.querySelector('[data-jof-tool-lote]');
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
        // As ferramentas já escolhidas nos outros cartões filtram esta lista (Diogo, 26/10/2026).
        folha.querySelectorAll('[data-jof-tool-procura] select').forEach(outro => {
          if (outro !== select && outro.value && /^(cm|mf|bq)_tool_id$/.test(outro.name)) query.set(outro.name, outro.value);
        });
        const response = await fetch('/JobOn?' + query, { credentials: 'same-origin' });
        if (!response.ok) throw new Error('Sem sugestões');
        const page = await response.json();
        const contagem = page.total > page.items.length ? page.items.length + ' de ' + page.total + '. Escreva para afinar.' : '';
        return {
          // A linha ao lado do lote: o contexto que distingue os lotes.
          // Sem lote, «sem lote» e não «Lote s/lote» (ronda de 26/10/2026, J10).
          items: page.items.map(item => ({ value: item.toolId, label: item.reference + ' · ' + (item.lot && item.lot !== 's/lote' ? 'Lote ' + lote(item.lot) : 'sem lote') + (item.machineCodes ? ' · ' + item.machineCodes : '') })),
          note: nota(page.filtro) || contagem,
          empty: term ? 'Sem resultados para «' + term + '».' : 'Escreva a referência da ' + tipo + '.'
        };
      };
      select.addEventListener('change', () => {
        const option = select.selectedOptions[0];
        if (select.value && option) {
          // «5447 · Lote 3» → a referência e o lote na cabeça do cartão.
          const partes = option.text.trim().split(' · ');
          // O cartão vazio só tem «Sem …»: cria a referência e o lote no lugar dele.
          if (!ref || !lt) {
            const vazio = botao.querySelector('.muted');
            const campo = (rotulo, atributo) => {
              const hv = document.createElement('span');
              hv.className = 'hv';
              const small = document.createElement('small');
              small.textContent = rotulo;
              const b = document.createElement('b');
              b.setAttribute(atributo, '');
              hv.append(small, b);
              return [hv, b];
            };
            const [hvRef, bRef] = campo('Referência', 'data-jof-tool-ref');
            const [hvLote, bLote] = campo('Lote', 'data-jof-tool-lote');
            if (vazio) vazio.replaceWith(hvRef, hvLote);
            else botao.querySelector('.code').after(hvRef, hvLote);
            ref = bRef; lt = bLote;
          }
          if (ref) ref.textContent = (partes[0] || '').replace(new RegExp('^' + tipo + '\\s+'), '');
          if (lt) lt.textContent = partes[1] === 'sem lote' ? 's/lote' : lote((partes[1] || '').replace(/^Lote\s*/, ''));
          botao.classList.remove('tool-empty');
          botao.setAttribute('data-jof-ficha', '/Ferramentas/Ficha?tool_id=' + encodeURIComponent(select.value) + '&voltar=' + encodeURIComponent(location.pathname + location.search));
          // A cor do uso cheio era da ferramenta antiga.
          [ref, lt].forEach(b => b && b.classList.remove('dmo-uso-cheio'));
          dadosDaFerramenta(folha, tipo, select.value);
        }
        procura.hidden = true;
        slot.classList.remove('is-mudar');
        faltam(folha);
      });
      procura.addEventListener('keydown', event => {
        if (event.key === 'Escape') { procura.hidden = true; slot.classList.remove('is-mudar'); botao.focus(); }
      });
    });
  }

  // A ferramenta escolhida traz os dados dela para o cartão (Diogo, 26/10/2026:
  // «quando altero para ferramenta nova não atualiza em tempo real»): a % de uso
  // dela (0 se é nova), e as notas e a informação técnica do último Job On em
  // que foi usada. Sem esse Job On, o resto fica como está.
  async function dadosDaFerramenta(folha, tipo, toolId) {
    const query = new URLSearchParams({ handler: 'DadosDaFerramenta', tipo: tipo, tool_id: toolId });
    const jobOnId = new URLSearchParams(location.search).get('jobon_id');
    if (jobOnId) query.set('jobon_id', jobOnId);
    let dados;
    try {
      const response = await fetch('/JobOn/Folha?' + query, { credentials: 'same-origin' });
      if (!response.ok) return;
      dados = await response.json();
    } catch (e) { return; }
    const por = (id, valor) => {
      const campo = document.getElementById(id);
      if (!campo || campo.value === valor) return;
      campo.value = valor;
      const nota = campo.closest('[data-jof-nota]');
      if (nota) nota.classList.toggle('is-empty', !valor);
      campo.dispatchEvent(new Event('input', { bubbles: true }));
    };
    por(dados.utilizacao.id, dados.utilizacao.valor);
    const uso = document.getElementById(dados.utilizacao.id);
    const barra = uso && uso.closest('.use') && uso.closest('.use').querySelector('.bar > i');
    if (barra) barra.style.width = Math.min(parseFloat(dados.utilizacao.valor) || 0, 100) + '%';
    // A marca «antes» da barra era a da ferramenta antiga.
    const marca = barra && barra.parentElement.querySelector('s');
    if (marca) marca.remove();
    (dados.campos || []).forEach(c => por(c.id, c.valor));
  }

  // O aviso «Falta CM, MF, BQ» acompanha o que já se escolheu, sem esperar pelo Guardar.
  function faltam(folha) {
    const aviso = document.querySelector('[data-jof-falta]');
    if (!aviso) return;
    const falta = [];
    folha.querySelectorAll('[data-jof-tool-slot]').forEach(slot => {
      const botao = slot.querySelector('[data-jof-tool]');
      if (botao && botao.classList.contains('tool-empty')) falta.push(botao.getAttribute('data-jof-tool'));
    });
    if (!falta.length) { aviso.hidden = true; aviso.style.display = 'none'; return; }
    aviso.hidden = false; aviso.style.display = '';
    // Só o texto: a ajuda (o círculo) que vem a seguir fica.
    const texto = [...aviso.childNodes].find(n => n.nodeType === 3 && /Falta /.test(n.nodeValue));
    if (texto) texto.nodeValue = texto.nodeValue.replace(/Falta [^:]*:/, 'Falta ' + falta.join(', ') + ':');
  }

  // A imagem do artigo encaixa sozinha: corta as margens vazias (brancas ou
  // transparentes) e reduz a no máximo 1200 px, sem deformar (Diogo, 23/10/2026).
  function ajustar(src, pronto) {
    const imagem = new Image();
    imagem.onerror = () => pronto(src);
    imagem.onload = () => {
      try {
        const w = imagem.naturalWidth, h = imagem.naturalHeight;
        const c = document.createElement('canvas');
        c.width = w; c.height = h;
        const g = c.getContext('2d');
        g.drawImage(imagem, 0, 0);
        const d = g.getImageData(0, 0, w, h).data;
        const vazio = (i) => d[i + 3] < 16 || (d[i] > 240 && d[i + 1] > 240 && d[i + 2] > 240);
        let x0 = w, y0 = h, x1 = -1, y1 = -1;
        for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
          if (vazio((y * w + x) * 4)) continue;
          if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y;
        }
        if (x1 < 0) { pronto(src); return; }
        const m = 8;
        x0 = Math.max(0, x0 - m); y0 = Math.max(0, y0 - m);
        x1 = Math.min(w - 1, x1 + m); y1 = Math.min(h - 1, y1 + m);
        const cw = x1 - x0 + 1, ch = y1 - y0 + 1;
        const escala = Math.min(1, 1200 / Math.max(cw, ch));
        const out = document.createElement('canvas');
        out.width = Math.round(cw * escala); out.height = Math.round(ch * escala);
        out.getContext('2d').drawImage(c, x0, y0, cw, ch, 0, 0, out.width, out.height);
        pronto(out.toDataURL('image/png'));
      } catch (e) { pronto(src); }
    };
    imagem.src = src;
  }

  // A imagem do artigo vive na pasta geral das imagens, como os PDFs do
  // Controlo (Diogo, 26/10/2026): cada computador associa uma vez a pasta
  // (guardada só neste navegador, File System Access API, Chrome e Edge) e a
  // app cria lá a pasta da referência e grava «artigo.<ext>». Trocar guarda a
  // anterior como «artigo-anterior-<data>»; retirar também (nada se perde).
  // Sem pasta associada (ou sem a API) a imagem fica só neste navegador.
  const ARTIGOS_DB = 'dmo-pasta-pdf';
  const ARTIGOS_STORE = 'pastas';
  const ARTIGOS_KEY = 'artigos';
  const API_PASTA = typeof window.showDirectoryPicker === 'function' && 'indexedDB' in window;

  function pastaDb(modo, acao) {
    return new Promise((resolve, reject) => {
      const abrir = indexedDB.open(ARTIGOS_DB, 1);
      abrir.onupgradeneeded = () => abrir.result.createObjectStore(ARTIGOS_STORE);
      abrir.onerror = () => reject(abrir.error);
      abrir.onsuccess = () => {
        const db = abrir.result;
        const tx = db.transaction(ARTIGOS_STORE, modo);
        const pedido = acao(tx.objectStore(ARTIGOS_STORE));
        tx.oncomplete = () => { db.close(); resolve(pedido && pedido.result); };
        tx.onerror = () => { db.close(); reject(tx.error); };
      };
    });
  }
  function pastaLer() {
    if (!API_PASTA) return Promise.resolve(null);
    return pastaDb('readonly', (s) => s.get(ARTIGOS_KEY)).catch(() => null);
  }
  function pastaPermissao(handle, pedir) {
    const opcoes = { mode: 'readwrite' };
    return handle.queryPermission(opcoes).then((estado) => {
      if (estado === 'granted') return true;
      return pedir ? handle.requestPermission(opcoes).then((r) => r === 'granted') : false;
    }).catch(() => false);
  }
  function nomePasta(referencia) { return referencia.replace(/[\\/:*?"<>|]/g, '_').trim(); }
  async function ficheiroDoArtigo(raiz, referencia) {
    try {
      const dir = await raiz.getDirectoryHandle(nomePasta(referencia));
      for await (const entrada of dir.values()) {
        if (entrada.kind === 'file' && /^artigo\.[a-z0-9]+$/i.test(entrada.name)) return { dir, entrada };
      }
    } catch (e) { /* sem pasta da referência */ }
    return null;
  }
  async function escreverFicheiro(dir, nome, blob) {
    const f = await dir.getFileHandle(nome, { create: true });
    const w = await f.createWritable();
    await w.write(blob);
    await w.close();
  }
  async function arquivarAtual(dir, entrada) {
    const ficheiro = await entrada.getFile();
    const ponto = entrada.name.lastIndexOf('.');
    const carimbo = new Date().toISOString().replace(/[:.]/g, '-');
    await escreverFicheiro(dir, 'artigo-anterior-' + carimbo + entrada.name.slice(ponto), ficheiro);
    await dir.removeEntry(entrada.name);
  }

  function artigo(folha) {
    const figura = folha.querySelector('[data-jof-artigo]');
    if (!figura) return;
    const referencia = figura.getAttribute('data-jof-artigo') || '';
    const pode = figura.getAttribute('data-jof-artigo-pode') === '1';
    const img = figura.querySelector('[data-jof-artigo-img]');
    const vazio = figura.querySelector('[data-jof-artigo-vazio]');
    const ficheiro = figura.querySelector('[data-jof-artigo-ficheiro]');
    const retirar = folha.querySelector('[data-jof-artigo-retirar]');
    const caixaAviso = folha.querySelector('[data-jof-artigo-aviso]');
    const chave = IMAGEM + referencia;
    let raiz = null;
    let permitido = false;
    let url = null;

    function aviso(texto) {
      if (!caixaAviso) return;
      caixaAviso.textContent = texto;
      caixaAviso.hidden = false;
      window.setTimeout(() => { caixaAviso.hidden = true; }, 7000);
    }
    function mostrar(src) {
      if (!img) return;
      if (url && src !== url) { URL.revokeObjectURL(url); url = null; }
      if (src) {
        img.src = src;
        img.hidden = false;
        if (vazio) vazio.style.display = 'none';
      } else {
        img.hidden = true;
        img.removeAttribute('src');
        if (vazio) vazio.style.display = '';
      }
      if (retirar) retirar.hidden = !src;
    }
    async function carregar() {
      if (!referencia) return;
      permitido = !!raiz && await pastaPermissao(raiz, false);
      if (permitido) {
        const achado = await ficheiroDoArtigo(raiz, referencia);
        if (achado) {
          url = URL.createObjectURL(await achado.entrada.getFile());
          mostrar(url);
          return;
        }
      }
      mostrar(ler(chave));
    }

    pastaLer().then((h) => { raiz = h; return carregar(); }).catch(() => {});

    // O primeiro clique do computador associa a pasta geral; em cada sessão o
    // navegador pede licença uma vez (tem de ser logo no clique).
    figura.addEventListener('click', (e) => {
      if (!API_PASTA) {
        if (pode && ficheiro) return;
        e.preventDefault();
        return;
      }
      if (!raiz) {
        // A pasta geral associa-se em Definições → Job On (com o PIN).
        e.preventDefault();
        aviso('Associe primeiro a pasta das imagens em Definições, Job On.');
        return;
      }
      // O estado da licença é lido antes (permitido): o clique decide já.
      if (!permitido) {
        e.preventDefault();
        pastaPermissao(raiz, true).then((dada) => {
          permitido = dada;
          if (dada) { aviso(pode ? 'Pasta autorizada. Clique outra vez para escolher a imagem.' : 'Pasta autorizada.'); return carregar(); }
        });
      }
    });

    if (ficheiro) {
      ficheiro.addEventListener('change', () => {
        const escolhido = ficheiro.files && ficheiro.files[0];
        ficheiro.value = '';
        if (!escolhido || !/^image\//.test(escolhido.type)) return;
        const leitor = new FileReader();
        leitor.onload = () => ajustar(String(leitor.result), async (src) => {
          const blob = await (await fetch(src)).blob();
          try {
            if (raiz && permitido) {
              const dir = await raiz.getDirectoryHandle(nomePasta(referencia), { create: true });
              const atual = await ficheiroDoArtigo(raiz, referencia);
              if (atual) await arquivarAtual(dir, atual.entrada);
              await escreverFicheiro(dir, 'artigo.png', blob);
              url = URL.createObjectURL(blob);
              mostrar(url);
              return;
            }
          } catch (err) {
            aviso('Não foi possível gravar na pasta; a imagem fica só neste navegador.');
          }
          mostrar(src);
          escrever(chave, src);
        });
        leitor.readAsDataURL(escolhido);
      });
    }
    if (retirar) {
      retirar.addEventListener('click', async (e) => {
        e.preventDefault();
        e.stopPropagation();
        try {
          if (raiz && (permitido || (permitido = await pastaPermissao(raiz, true)))) {
            const atual = await ficheiroDoArtigo(raiz, referencia);
            if (atual) await arquivarAtual(atual.dir, atual.entrada);
          }
        } catch (err) { aviso('Não foi possível retirar a imagem da pasta.'); return; }
        try { window.localStorage.removeItem(chave); } catch (err) { /* sem efeito */ }
        mostrar(null);
      });
    }
  }

  // O painel das linhas (o mockup: uma gaveta que o botão «Linhas» abre e o
  // × ou o fundo fecham). A escolha fica neste browser.
  const PAINEL = 'dmo-jobon-folha-painel';
  // IMPRIMIR — PONTO DE TROCA: hoje imprime a folha no browser (window.print()
  // e o @media print do dmo-jobon-folha.css). O gerador das 4 folhas, quando
  // chegar, substitui window.dmoJobOnImprimir (recebe o jobon_id); nunca PDF no servidor.
  // As 4 folhas (Ficha de Artigo, Trabalho de Equipa, Preparador, Pinças) vêm
  // escondidas na página ([data-jof-impressao]); imprimir copia as escolhidas
  // para o fim do body e o @media print só mostra essas.
  window.dmoJobOnImprimir = window.dmoJobOnImprimir || function (_id, qual) {
    var origem = document.querySelector('[data-jof-impressao]');
    if (!origem || !qual || qual === 'ecra') {
      document.querySelectorAll('[data-jof-tecnica]').forEach(function (d) { d.open = true; });
      window.print();
      return;
    }
    var caixa = document.createElement('div');
    caixa.className = 'jof-impressao-print';
    origem.querySelectorAll('[data-jof-folha]').forEach(function (f) {
      if (qual === 'todas' || f.getAttribute('data-jof-folha') === qual) caixa.appendChild(f.cloneNode(true));
    });
    var img = document.querySelector('img[data-jof-artigo-img]');
    if (img && img.getAttribute('src') && !img.hidden) {
      caixa.querySelectorAll('[data-jof-folha-artigo]').forEach(function (a) {
        a.textContent = '';
        var i = document.createElement('img'); i.src = img.src; i.alt = img.alt; a.appendChild(i);
      });
    }
    document.body.appendChild(caixa);
    document.documentElement.setAttribute('data-jof-imprimir-folhas', '');
    var limpar = function () {
      document.documentElement.removeAttribute('data-jof-imprimir-folhas');
      caixa.remove();
      window.removeEventListener('afterprint', limpar);
    };
    window.addEventListener('afterprint', limpar);
    window.print();
  };
  // O botão Imprimir abre a lista das folhas quando a página as tem; sem
  // lista, imprime o ecrã como antes.
  function imprimir() {
    var id = function () { var m = /jobon_id=(\d+)/.exec(location.search); return m ? Number(m[1]) : null; };
    document.querySelectorAll('[data-jof-imprimir]').forEach(function (b) {
      var lista = b.parentElement && b.parentElement.querySelector('[data-jof-imprimir-lista]');
      var fechar = function () { lista.hidden = true; b.setAttribute('aria-expanded', 'false'); };
      b.addEventListener('click', function () {
        if (!lista) { window.dmoJobOnImprimir(id(), 'todas'); return; }
        lista.hidden = !lista.hidden;
        b.setAttribute('aria-expanded', String(!lista.hidden));
      });
      if (!lista) return;
      lista.querySelectorAll('[data-jof-imprimir-folha]').forEach(function (f) {
        f.addEventListener('click', function () { fechar(); window.dmoJobOnImprimir(id(), f.getAttribute('data-jof-imprimir-folha')); });
      });
      document.addEventListener('click', function (e) { if (!b.parentElement.contains(e.target)) fechar(); });
      document.addEventListener('keydown', function (e) { if (e.key === 'Escape') fechar(); });
    });
  }
  // CS · Furos no Editar: dois dígitos (abertos, fechados) vão para o campo cs_furos
  // como «Abertos 4 / Fechados 2» (o CsFurosTexto lê-o).
  function furos() {
    document.querySelectorAll('[data-jof-furos]').forEach(function (z) {
      var valor = z.querySelector('[data-jof-furos-valor]');
      var abertos = z.querySelector('[data-jof-furos-abertos]');
      var fechados = z.querySelector('[data-jof-furos-fechados]');
      function digito(i) { var d = (i.value || '').replace(/[^0-9]/g, '').slice(0, 1); if (i.value !== d) i.value = d; return d; }
      function juntar() {
        var a = digito(abertos), f = digito(fechados);
        if (!a && !f) return; // nada escrito: o valor antigo fica
        valor.value = a && f ? 'Abertos ' + a + ' / Fechados ' + f : a ? 'Abertos ' + a : 'Fechados ' + f;
        valor.dispatchEvent(new Event('input', { bubbles: true }));
      }
      abertos.addEventListener('input', juntar);
      fechados.addEventListener('input', juntar);
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

  // As notas ampliam-se com um clique e voltam ao cartão com Esc ou um clique fora (Diogo, 23/10/2026).
  function notas(folha) {
    let aberta = null;
    // Ampliada, o título diz de que ferramenta é a nota: «CM 5809 · Lote 1 · Notas».
    const titulo = (caixa) => {
      const sec = caixa.closest('[data-jof-familia]');
      if (!sec) return 'Notas';
      const valor = (rotulo) => {
        const hv = [...sec.querySelectorAll(':scope > .sec-head .hv, :scope > .tool-head .hv, :scope > * > .tool-head .hv')].find(h => (h.querySelector('small')?.textContent || '').trim() === rotulo);
        const b = hv?.querySelector('b');
        const v = (b?.querySelector('input')?.value ?? b?.textContent ?? '').trim();
        return v && v !== '—' ? v : '';
      };
      const ref = valor('Referência'), lote = valor('Lote');
      return [sec.getAttribute('data-jof-familia') + (ref ? ' ' + ref : ''), lote ? 'Lote ' + lote : '', 'Notas'].filter(Boolean).join(' · ');
    };
    const fechar = () => {
      if (!aberta) return;
      const rotulo = aberta.querySelector(':scope > span');
      if (rotulo) rotulo.textContent = 'Notas';
      aberta.classList.remove('is-ampliada');
      aberta = null;
    };
    folha.addEventListener('click', (e) => {
      const caixa = e.target.closest('[data-jof-nota]');
      if (caixa === aberta) return;
      fechar();
      if (!caixa) return;
      const rotulo = caixa.querySelector(':scope > span');
      if (rotulo) rotulo.textContent = titulo(caixa);
      caixa.classList.add('is-ampliada');
      aberta = caixa;
    });
    folha.addEventListener('keydown', (e) => {
      if (e.key === 'Escape') fechar();
      else if (e.key === 'Enter' && e.target.matches('.note-view[data-jof-nota]')) e.target.click();
    });
    document.addEventListener('click', (e) => { if (aberta && !folha.contains(e.target)) fechar(); });
  }

  // REMOVER POR ENGANO (Diogo, 23/10/2026): clicar numa linha de «O que
  // mudou» (fora do botão da ação) abre a janela com essa alteração;
  // «Remover por engano» mostra a confirmação.
  function alteracoes() {
    const janela = document.querySelector('[data-jof-remover]');
    if (!janela || typeof janela.showModal !== 'function') return;
    const id = janela.querySelector('[data-jof-remover-id]');
    const texto = janela.querySelector('[data-jof-remover-texto]');
    const quando = janela.querySelector('[data-jof-remover-quando]');
    const pedir = janela.querySelector('[data-jof-remover-pedir]');
    const sim = janela.querySelector('[data-jof-remover-sim]');
    const confirmar = janela.querySelector('[data-jof-remover-confirmar]');
    function abrir(linha) {
      id.value = linha.getAttribute('data-jof-alteracao');
      texto.textContent = linha.getAttribute('data-jof-alteracao-texto') || '';
      quando.textContent = linha.getAttribute('data-jof-alteracao-quando') || '';
      pedir.hidden = false;
      sim.hidden = true;
      confirmar.hidden = true;
      janela.showModal();
      pedir.focus();
    }
    pedir.addEventListener('click', () => {
      pedir.hidden = true;
      sim.hidden = false;
      confirmar.hidden = false;
      sim.focus();
    });
    document.querySelectorAll('li[data-jof-alteracao]').forEach(linha => {
      if (!linha.getAttribute('data-jof-alteracao')) return;
      linha.classList.add('is-abrivel');
      linha.addEventListener('click', event => {
        if (event.target.closest('a, button')) return;
        abrir(linha);
      });
      linha.addEventListener('keydown', event => {
        if (event.key === 'Enter' && event.target === linha) { event.preventDefault(); abrir(linha); }
      });
    });
  }

  function init() {
    painel();
    alteracoes();
    imprimir();
    producoes();
    furos();
    const folha = document.querySelector('[data-jof-folha]');
    if (!folha) return;
    const editar = folha.getAttribute('data-jof-folha') === 'editar';
    ferramentas(folha, editar);
    // A procura da Referência: um clique seleciona-a toda, para o Backspace a apagar logo (Diogo, 26/10/2026).
    const procuraRef = document.getElementById('jofReferencia');
    if (procuraRef) {
      let selecionar = false;
      procuraRef.addEventListener('focus', () => { procuraRef.select(); selecionar = true; });
      procuraRef.addEventListener('mouseup', event => { if (selecionar) { event.preventDefault(); procuraRef.select(); selecionar = false; } });
    }
    artigo(folha);
    notas(folha);
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
  else init();
})();
