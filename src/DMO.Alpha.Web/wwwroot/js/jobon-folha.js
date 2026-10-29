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
        if (ficha && window.DmoSupport?.tryOpen(ficha, botao)) return;
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
        // Fora do Editar, um clique abre a ficha (Diogo, 28/10/2026: «abrir ferramenta com 1 clique em vez de 2»).
        if (!editar) { abrirFicha(); return; }
        // Um clique muda; o segundo clique logo a seguir é o duplo clique (abre a ficha).
        if (espera) return;
        espera = setTimeout(() => { espera = null; mudar(); }, ESPERA_CLIQUE);
      });
      botao.addEventListener('dblclick', () => {
        if (!editar) return;
        if (espera) { clearTimeout(espera); espera = null; }
        abrirFicha();
      });

      if (!select) return;
      // O triângulo à frente de cada ferramenta da lista abre a ficha dela no painel (Diogo, 28/10/2026).
      select.dmoFicha = id => '/Ferramentas/Ficha?tool_id=' + encodeURIComponent(id);
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
          items: page.items.map(item => ({ value: item.toolId, label: item.reference + ' · ' + (item.lot && item.lot !== 's/lote' ? 'Lote ' + lote(item.lot) : 'sem lote') + (item.machineCodes ? ' · ' + item.machineCodes : '') + (item.uso ? ' · ' + item.uso : '') })),
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
          dadosDaFerramenta(folha, tipo, select, select.value);
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
  async function dadosDaFerramenta(folha, tipo, select, toolId) {
    const query = new URLSearchParams({ handler: 'DadosDaFerramenta', tipo: tipo, tool_id: toolId });
    const jobOnId = new URLSearchParams(location.search).get('jobon_id');
    if (jobOnId) query.set('jobon_id', jobOnId);
    let dados;
    try {
      const response = await fetch('/JobOn/Folha?' + query, { credentials: 'same-origin' });
      // Entretanto escolheram outra ferramenta: a resposta é de quem já não
      // está escolhido, não se aplica (auditoria do Codex de 29/10/2026,
      // bug alto: a resposta atrasada de A sobrepunha o cartão de B).
      if (select.value !== toolId) return;
      if (!response.ok) return;
      dados = await response.json();
      if (select.value !== toolId) return;
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

    // Gravar: a imagem escolhida ou colada passa pela janela do recorte
    // (dmo-artigo-recorte.js) e o «Guardar recorte» grava logo na pasta da
    // referência. O clique no Guardar ainda vale para o navegador pedir a
    // licença da pasta, quando falta (Diogo, 27/10/2026).
    function gravar(escolhido) {
      if (!escolhido || !/^image\//.test(escolhido.type)) return;
      const leitor = new FileReader();
      leitor.onload = async () => {
        let original = String(leitor.result);
        if (typeof window.dmoRecortarArtigo === 'function') {
          original = await window.dmoRecortarArtigo(original, referencia);
          if (!original) return;
          if (raiz && !permitido) permitido = await pastaPermissao(raiz, true);
        }
        ajustar(original, async (src) => {
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
      };
      leitor.readAsDataURL(escolhido);
    }
    if (ficheiro) {
      ficheiro.addEventListener('change', () => {
        const escolhido = ficheiro.files && ficheiro.files[0];
        ficheiro.value = '';
        gravar(escolhido);
      });
      // Colar (Ctrl+V) uma imagem na folha também a associa, fora dos campos
      // de escrita (aí o colar é do campo).
      document.addEventListener('paste', (e) => {
        const alvo = e.target;
        if (alvo && alvo.closest && alvo.closest('input, textarea, select, [contenteditable=""], [contenteditable="true"]')) return;
        if (document.querySelector('dialog[open]')) return;
        const itens = (e.clipboardData && e.clipboardData.items) || [];
        const item = [...itens].find((i) => i.kind === 'file' && /^image\//.test(i.type));
        if (!item) return;
        e.preventDefault();
        if (API_PASTA && !raiz) { aviso('Associe primeiro a pasta das imagens em Definições, Job On.'); return; }
        gravar(item.getAsFile());
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
    // A letra é grande (Diogo, 27/10/2026); se uma folha tiver texto a mais para
    // o A4, a letra dessa folha encolhe só o preciso, para nada ficar cortado. A caixa
    // só aparece na impressão; mede-se fora do ecrã.
    caixa.style.cssText = 'display:block;position:absolute;left:-10000px;top:0';
    caixa.querySelectorAll('.jfi-folha').forEach(function (f) {
      var a4 = f.getBoundingClientRect().height;
      var todos = [f].concat(Array.prototype.slice.call(f.querySelectorAll('*')));
      for (var vez = 0; vez < 6; vez++) {
        f.style.height = 'auto';
        var precisa = f.getBoundingClientRect().height;
        f.style.height = '';
        if (!(precisa > a4 + 1)) break;
        // Só a letra encolhe (as margens ficam): por isso pode precisar de mais de uma volta.
        var k = a4 / precisa;
        var tamanhos = todos.map(function (e) { return parseFloat(getComputedStyle(e).fontSize); });
        todos.forEach(function (e, i) { e.style.fontSize = (tamanhos[i] * k) + 'px'; });
      }
    });
    caixa.style.cssText = '';
    document.documentElement.setAttribute('data-jof-imprimir-folhas', '');
    var limpar = function () {
      document.documentElement.removeAttribute('data-jof-imprimir-folhas');
      caixa.remove();
      window.removeEventListener('afterprint', limpar);
    };
    window.addEventListener('afterprint', limpar);
    window.print();
  };
  // O botão Imprimir imprime as folhas todas.
  function imprimir() {
    var id = function () { var m = /jobon_id=(\d+)/.exec(location.search); return m ? Number(m[1]) : null; };
    document.querySelectorAll('[data-jof-imprimir]').forEach(function (b) {
      b.addEventListener('click', function () { window.dmoJobOnImprimir(id(), 'todas'); });
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
        // Os dois vazios (DMO-637): o campo escondido também fica vazio, em
        // vez de guardar o último valor combinado que já não aparece no ecrã.
        valor.value = a && f ? 'Abertos ' + a + ' / Fechados ' + f : a ? 'Abertos ' + a : f ? 'Fechados ' + f : '';
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
    // A nota ampliada abre por cima e o lugar dela fica guardado no cartão, para o resto não
    // subir nem ser empurrado (Diogo, 27/10/2026: «maximiza por cima em vez de empurrar»).
    let lugar = null;
    const fechar = () => {
      if (!aberta) return;
      const rotulo = aberta.querySelector(':scope > span');
      if (rotulo) rotulo.textContent = 'Notas';
      aberta.classList.remove('is-ampliada');
      lugar?.remove();
      lugar = null;
      aberta = null;
    };
    // Com uma nota ampliada, um clique fora só a fecha: não abre outra nem carrega no que está por
    // baixo (Diogo, 28/10/2026: «carregar nas notas abre e outro clique fora fecha, não deixa
    // substituir»). No Editar, o que se escreveu fica na caixa e vai com o Guardar.
    // O mousedown fora também não passa, para o clique que fecha não pôr o cursor noutra caixa.
    document.addEventListener('mousedown', (e) => { if (aberta && !aberta.contains(e.target)) e.preventDefault(); }, true);
    document.addEventListener('click', (e) => {
      if (!aberta || aberta.contains(e.target)) return;
      fechar();
      e.preventDefault();
      e.stopImmediatePropagation();
    }, true);
    folha.addEventListener('click', (e) => {
      const caixa = e.target.closest('[data-jof-nota]');
      if (!caixa || caixa === aberta) return;
      const rotulo = caixa.querySelector(':scope > span');
      if (rotulo) rotulo.textContent = titulo(caixa);
      lugar = document.createElement('div');
      lugar.className = 'jof-nota-lugar';
      lugar.setAttribute('aria-hidden', 'true');
      lugar.style.height = caixa.getBoundingClientRect().height + 'px';
      caixa.before(lugar);
      caixa.classList.add('is-ampliada');
      aberta = caixa;
      // No Editar abre já pronta a escrever, no fim do texto (a não ser que se tenha clicado no texto).
      const texto = caixa.querySelector('textarea');
      if (texto && e.target !== texto) { texto.focus(); texto.setSelectionRange(texto.value.length, texto.value.length); }
    });
    folha.addEventListener('keydown', (e) => {
      if (e.key === 'Escape') fechar();
      else if (e.key === 'Enter' && e.target.matches('.note-view[data-jof-nota]')) e.target.click();
    });
    // A «Informação técnica» abre por cima das Notas (Diogo, 27/10/2026), em vista e no Editar
    // (28/10/2026: a folha não desliza): um clique fora dela, ou o Escape, fecha-a; abre uma de cada vez.
    const tecnicas = () => folha.querySelectorAll('.sec > details.more[open]');
    folha.addEventListener('toggle', (e) => {
      if (!e.target.matches?.('.sec > details.more') || !e.target.open) return;
      tecnicas().forEach(d => { if (d !== e.target) d.open = false; });
    }, true);
    document.addEventListener('click', (e) => {
      tecnicas().forEach(d => { if (!d.contains(e.target)) d.open = false; });
    });
    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape') tecnicas().forEach(d => { d.open = false; });
    });
  }

  // REMOVER POR ENGANO (Diogo, 23/10/2026): clicar numa linha de «O que
  // mudou» (fora do botão da ação) abre a janela com essa alteração;
  // «Remover por engano» mostra a confirmação.
  // SEM BOTÕES NAS LINHAS (Diogo, 27/10/2026): um clique (ou Espaço/Enter)
  // numa alteração escolhe-a e o botão do canto passa a ser a ação dela;
  // sem ação, o botão fica desativado.
  function escolherAlteracoes() {
    const acao = document.querySelector('[data-jof-resumo-acao] > a');
    const linhas = document.querySelectorAll('.jobon-resumo > li');
    function escolher(linha) {
      linhas.forEach(l => {
        l.classList.toggle('selected', l === linha);
        if (l === linha) l.setAttribute('aria-current', 'true'); else l.removeAttribute('aria-current');
      });
      if (!acao) return;
      const href = linha.getAttribute('data-jof-acao-href');
      const rotulo = linha.getAttribute('data-jof-acao-rotulo');
      if (href) {
        acao.setAttribute('href', href);
        acao.textContent = rotulo || acao.textContent;
        acao.removeAttribute('aria-disabled');
      } else {
        acao.removeAttribute('href');
        acao.setAttribute('aria-disabled', 'true');
      }
    }
    linhas.forEach(linha => {
      linha.addEventListener('click', event => {
        if (event.target.closest('a, button')) return;
        escolher(linha);
      });
      linha.addEventListener('keydown', event => {
        if (event.target !== linha) return;
        if (event.key === ' ') { event.preventDefault(); escolher(linha); }
        else if (event.key === 'Enter') escolher(linha);
      });
    });
  }

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

  // As Notas gerais (ver) deslizam por dentro quando não cabem; a altura da lista acaba sempre no fim de uma linha inteira,
  // para nunca ficar uma nota cortada a meio (Diogo, 28/10/2026).
  function notasGerais() {
    const lista = document.querySelector('.jof2 .side > .notes > ul');
    if (!lista) return;
    const ajustar = () => {
      lista.style.flex = '';
      lista.style.height = '';
      if (lista.scrollHeight <= lista.clientHeight + 1) return;
      const limite = lista.clientHeight;
      let fim = 0;
      for (const item of lista.children) {
        const base = item.offsetTop + item.offsetHeight;
        if (base <= limite) fim = base; else break;
      }
      if (fim > 0) { lista.style.flex = '0 0 auto'; lista.style.height = fim + 'px'; }
    };
    ajustar();
    window.addEventListener('resize', ajustar);
    if (document.fonts && document.fonts.ready) document.fonts.ready.then(ajustar);
  }

  // As caixas de notas crescem até ocupar o espaço livre do cartão; quando o texto não cabe, deslizam por dentro e a altura
  // visível acaba sempre numa linha inteira (18px), nunca a meio de uma linha (Diogo, 28/10/2026).
  const NOTA_LINHA = 18;
  function notasLinhas() {
    const caixas = document.querySelectorAll('.jof2 .sec .note-box > p, .jof2 .sec .note-box > textarea, .jof2 .side > .notes > textarea');
    caixas.forEach(el => { el.style.flex = ''; el.style.height = ''; });
    caixas.forEach(el => {
      if (el.closest('.is-ampliada') || el.offsetParent === null) return;
      if (el.scrollHeight <= el.clientHeight + 1) return;
      const cs = window.getComputedStyle(el);
      const enchimento = parseFloat(cs.paddingTop) + parseFloat(cs.paddingBottom);
      const bordas = parseFloat(cs.borderTopWidth) + parseFloat(cs.borderBottomWidth);
      const linhas = Math.max(1, Math.floor((el.clientHeight - enchimento) / NOTA_LINHA));
      // «important»: as folhas de estilo da folha também fixam o flex da nota com !important.
      el.style.setProperty('flex', '0 0 auto', 'important');
      el.style.setProperty('height', (linhas * NOTA_LINHA + enchimento + bordas) + 'px', 'important');
    });
  }
  function notasLinhasInicio() {
    notasLinhas();
    window.addEventListener('resize', notasLinhas);
    if (document.fonts && document.fonts.ready) document.fonts.ready.then(notasLinhas);
    document.addEventListener('input', e => { if (e.target.matches && e.target.matches('.note-box > textarea, .side > .notes > textarea')) notasLinhas(); });
    document.addEventListener('click', () => setTimeout(notasLinhas, 0));
    document.addEventListener('keydown', e => { if (e.key === 'Escape') setTimeout(notasLinhas, 0); });
  }

  // Calibres (ver): cada valor «P 59.20 / M 58.60» quebra, se for preciso, só na « / » e nunca a meio de um número.
  function calibresValores() {
    // O nome do calibre colado («Cab.Sopro») pode quebrar a seguir ao ponto, para os oito caberem a 1290 px.
    document.querySelectorAll('.jof2:not(.editing) table[data-jof-cal] tbody th').forEach(th => {
      if (th.children.length || !/\.\S/.test(th.textContent)) return;
      th.textContent = th.textContent.replace(/\.(?=\S)/g, '.\u200B');
    });
    document.querySelectorAll('.jof2:not(.editing) table[data-jof-cal] tbody td').forEach(td => {
      if (td.children.length) return;
      const partes = td.textContent.trim().split(' / ');
      if (partes.length < 2) return;
      const envolucro = document.createElement('span');
      envolucro.className = 'calv-pilha';
      td.title = td.textContent.trim();
      partes.forEach(parte => {
        const seg = document.createElement('span');
        seg.className = 'calv-seg';
        // Sem a «/»: um valor por cima do outro (Diogo, 29/10/2026: «remover / do extrator, ficam separados
        // alinhar eles um em cima do outro»).
        seg.textContent = parte;
        envolucro.append(seg);
      });
      td.textContent = '';
      td.append(envolucro);
    });
  }

  // Dimensões compridas («54.02/52.02/54.02/44.10/45.20»): quebram só depois de cada «/», nunca a meio
  // de um número, para não saírem do cartão (Diogo, 27/10/2026: o Corpo do MF saía para a BQ).
  function dimensoesLongas() {
    document.querySelectorAll('.jof2:not(.editing) .jof-dim.longo .jof-dim-valor').forEach(valor => {
      const textos = [];
      const andar = document.createTreeWalker(valor, NodeFilter.SHOW_TEXT);
      while (andar.nextNode()) textos.push(andar.currentNode);
      textos.forEach(texto => {
        const partes = texto.nodeValue.split('/');
        if (partes.length < 2) return;
        const pedaco = document.createDocumentFragment();
        partes.forEach((parte, i) => {
          if (i) { pedaco.append('/'); pedaco.append(document.createElement('wbr')); }
          pedaco.append(parte);
        });
        texto.replaceWith(pedaco);
      });
    });
  }

  // No painel deslizante (?painel=1) o «Cancelar» do Editar volta ao resumo da
  // folha dentro do painel, não à folha inteira (Diogo, 28/10/2026).
  function cancelarNoPainel() {
    if (document.documentElement.dataset.dmoEmbedded !== 'true') return;
    document.querySelectorAll('.head-tools a.btn[href^="/JobOn/Folha"]').forEach(a => {
      const u = new URL(a.getAttribute('href'), location.href);
      u.searchParams.set('painel', '1');
      a.setAttribute('href', u.pathname + u.search);
    });
  }

  // Os avisos dos cartões CM, MF e BQ (B2 sem Dupla/Tripla, «não registada no Armazém») ficavam cortados
  // dentro do cartão (Diogo, 29/10/2026: «meter estes alertas aparecer no canto superior direito, com x para
  // remover»): no ver, saem do cartão para uma pilha no canto superior direito, cada um com o seu botão e um ×.
  function avisosNoCanto() {
    const avisos = document.querySelectorAll('.jof2:not(.editing) .sec :is([data-dmo-aviso-b2],[data-dmo-aviso-armazem])');
    if (!avisos.length) return;
    // Como os outros alertas da app: na pilha do canto do ecrã (DMOLive), com o seu × (Diogo, 29/10/2026:
    // «deve aparecer como aparecem os alertas»).
    // No telemóvel os alertas ficam em baixo a toda a largura e tapavam a folha: lá ficam no cartão.
    if (window.matchMedia && window.matchMedia('(max-width: 640px)').matches) return;
    if (window.DMOLive && window.DMOLive.mostrarMensagens) {
      const caixa = document.createElement('div');
      avisos.forEach(aviso => {
        aviso.classList.remove('jobon-aviso', 'warn');
        aviso.setAttribute('data-dmo-toast', 'aviso');
        aviso.setAttribute('role', 'status');
        caixa.append(aviso);
      });
      window.DMOLive.mostrarMensagens(caixa);
      return;
    }
    const pilha = document.createElement('div');
    pilha.className = 'jof-avisos-canto';
    pilha.setAttribute('data-jof-avisos-canto', '');
    avisos.forEach(aviso => {
      const fechar = document.createElement('button');
      fechar.type = 'button';
      fechar.className = 'jof-aviso-fechar';
      fechar.setAttribute('aria-label', 'Fechar este aviso');
      fechar.textContent = '×';
      fechar.addEventListener('click', () => { aviso.remove(); if (!pilha.children.length) pilha.remove(); });
      aviso.append(fechar);
      pilha.append(aviso);
    });
    document.body.append(pilha);
  }

  // A «Informação técnica» aberta fica por cima do que está por baixo e acaba no fundo do ecrã, para a página
  // não crescer nem mexer (Diogo, 29/10/2026: «o minimizar/maximizar voltou a empurrar elementos»).
  function tecnicaPorCima() {
    document.querySelectorAll('.jof2:not(.editing) .sec>details.more').forEach(det => {
      det.addEventListener('toggle', () => {
        const corpo = det.querySelector(':scope>.more-body');
        if (!corpo || !det.open) return;
        const topo = det.getBoundingClientRect().bottom + 2;
        corpo.style.maxHeight = Math.max(120, window.innerHeight - topo - 8) + 'px';
      });
    });
  }

  function init() {
    tecnicaPorCima();
    avisosNoCanto();
    cancelarNoPainel();
    calibresValores();
    dimensoesLongas();
    painel();
    escolherAlteracoes();
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
    notasGerais();
    notasLinhasInicio();
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
  else init();
})();
