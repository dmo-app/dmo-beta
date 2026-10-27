// O RECORTE DA IMAGEM DO ARTIGO (Diogo, 27/10/2026, …QZ5YJs7W «sim!»).
// Depois de escolher ou colar (Ctrl+V) uma imagem no cartão Artigo do Job On,
// abre esta janela: arrasta-se a área a guardar e «Guardar recorte» grava logo
// na pasta da referência (o jobon-folha.js grava, como antes). «Imagem
// inteira» guarda como até aqui; Cancelar (ou Esc) não grava nada.
// window.dmoRecortarArtigo(src, referencia) → Promise com a imagem (data URL)
// ou null quando se cancela. Sem este ficheiro, a imagem grava-se inteira.
(() => {
  const MINIMO = 12; // px no ecrã: um clique solto não faz um recorte vazio

  let janela = null;

  function criar() {
    const d = document.createElement('dialog');
    d.className = 'dmo-dialog dmo-recorte';
    d.setAttribute('aria-labelledby', 'dmoRecorteTitulo');
    d.innerHTML =
      '<h3 id="dmoRecorteTitulo">Recortar a imagem do artigo</h3>' +
      '<p class="dmo-recorte-sub" data-recorte-sub></p>' +
      '<div class="dmo-recorte-area" data-recorte-area>' +
        '<div class="dmo-recorte-palco" data-recorte-palco>' +
          '<img alt="" data-recorte-img draggable="false" />' +
          '<div class="dmo-recorte-sel" data-recorte-sel>' +
            '<i data-canto="nw"></i><i data-canto="ne"></i><i data-canto="sw"></i><i data-canto="se"></i>' +
          '</div>' +
        '</div>' +
        '<span class="dmo-recorte-medida" data-recorte-medida></span>' +
      '</div>' +
      '<div class="dmo-recorte-rodape">' +
        '<span class="dmo-recorte-info" data-recorte-info></span>' +
        '<button type="button" class="dmo-button dmo-dialog-cancel" data-recorte="cancelar">Cancelar</button>' +
        '<button type="button" class="dmo-button dmo-dialog-cancel" data-recorte="inteira">Imagem inteira</button>' +
        '<button type="button" class="dmo-button" data-recorte="guardar">Guardar recorte</button>' +
      '</div>';
    document.body.appendChild(d);
    return d;
  }

  window.dmoRecortarArtigo = function (src, referencia) {
    if (typeof HTMLDialogElement !== 'function') return Promise.resolve(src);
    janela = janela || criar();
    const d = janela;
    const img = d.querySelector('[data-recorte-img]');
    const palco = d.querySelector('[data-recorte-palco]');
    const sel = d.querySelector('[data-recorte-sel]');
    const medida = d.querySelector('[data-recorte-medida]');
    d.querySelector('[data-recorte-sub]').textContent = 'Arraste para escolher a área. Referência ' + referencia + '.';
    d.querySelector('[data-recorte-info]').textContent = 'Grava na pasta das imagens: ' + referencia + '\\artigo.png (a anterior fica guardada).';

    return new Promise((resolve) => {
      // r: o recorte em px do ecrã, dentro da imagem mostrada.
      let r = null, w = 0, h = 0, arrasto = null, fim = false;

      function desenhar() {
        sel.style.left = r.x + 'px'; sel.style.top = r.y + 'px';
        sel.style.width = r.w + 'px'; sel.style.height = r.h + 'px';
        const k = img.naturalWidth / w;
        medida.textContent = Math.round(r.w * k) + ' × ' + Math.round(r.h * k) + ' px';
      }
      function ponto(e) {
        const caixa = palco.getBoundingClientRect();
        return { x: Math.min(w, Math.max(0, e.clientX - caixa.left)), y: Math.min(h, Math.max(0, e.clientY - caixa.top)) };
      }
      function premir(e) {
        if (e.button !== 0) return;
        e.preventDefault();
        const p = ponto(e);
        const canto = e.target.getAttribute && e.target.getAttribute('data-canto');
        if (canto) {
          // O canto oposto fica fixo.
          const fx = canto.includes('w') ? r.x + r.w : r.x;
          const fy = canto.includes('n') ? r.y + r.h : r.y;
          arrasto = { modo: 'novo', ox: fx, oy: fy };
        } else if (e.target === sel && (r.w < w || r.h < h)) {
          // Dentro do recorte move-o; com a imagem toda escolhida, arrasta-se um novo.
          arrasto = { modo: 'mover', dx: p.x - r.x, dy: p.y - r.y };
        } else {
          arrasto = { modo: 'novo', ox: p.x, oy: p.y };
          r = { x: p.x, y: p.y, w: 0, h: 0 };
        }
        palco.setPointerCapture(e.pointerId);
      }
      function mover(e) {
        if (!arrasto) return;
        const p = ponto(e);
        if (arrasto.modo === 'mover') {
          r.x = Math.min(w - r.w, Math.max(0, p.x - arrasto.dx));
          r.y = Math.min(h - r.h, Math.max(0, p.y - arrasto.dy));
        } else {
          r = { x: Math.min(arrasto.ox, p.x), y: Math.min(arrasto.oy, p.y), w: Math.abs(p.x - arrasto.ox), h: Math.abs(p.y - arrasto.oy) };
        }
        desenhar();
      }
      function largar() {
        if (!arrasto) return;
        arrasto = null;
        if (r.w < MINIMO || r.h < MINIMO) r = { x: 0, y: 0, w, h };
        desenhar();
      }
      function recorte() {
        const k = img.naturalWidth / w;
        const sx = Math.round(r.x * k), sy = Math.round(r.y * k);
        const sw = Math.max(1, Math.round(r.w * k)), sh = Math.max(1, Math.round(r.h * k));
        const c = document.createElement('canvas');
        c.width = sw; c.height = sh;
        c.getContext('2d').drawImage(img, sx, sy, sw, sh, 0, 0, sw, sh);
        return c.toDataURL('image/png');
      }
      function acabar(valor) {
        if (fim) return;
        fim = true;
        palco.removeEventListener('pointerdown', premir);
        palco.removeEventListener('pointermove', mover);
        palco.removeEventListener('pointerup', largar);
        palco.removeEventListener('pointercancel', largar);
        d.removeEventListener('click', clique);
        d.removeEventListener('cancel', cancelar);
        if (d.open) d.close();
        img.removeAttribute('src');
        resolve(valor);
      }
      function clique(e) {
        const b = e.target.closest && e.target.closest('[data-recorte]');
        if (!b) return;
        const acao = b.getAttribute('data-recorte');
        if (acao === 'cancelar') acabar(null);
        else if (acao === 'inteira') acabar(src);
        else {
          let valor = src;
          try { valor = recorte(); } catch (err) { /* fica a inteira */ }
          acabar(valor);
        }
      }
      function cancelar(e) { e.preventDefault(); acabar(null); }

      img.onerror = () => acabar(src);
      img.onload = () => {
        d.showModal();
        // A imagem ajusta-se à área sem deformar; o recorte começa na imagem toda.
        const area = d.querySelector('[data-recorte-area]').getBoundingClientRect();
        const escala = Math.min(1, (area.width - 24) / img.naturalWidth, (area.height - 24) / img.naturalHeight);
        w = Math.max(1, Math.round(img.naturalWidth * escala));
        h = Math.max(1, Math.round(img.naturalHeight * escala));
        palco.style.width = w + 'px'; palco.style.height = h + 'px';
        r = { x: 0, y: 0, w, h };
        desenhar();
        d.querySelector('[data-recorte="guardar"]').focus();
      };
      palco.addEventListener('pointerdown', premir);
      palco.addEventListener('pointermove', mover);
      palco.addEventListener('pointerup', largar);
      palco.addEventListener('pointercancel', largar);
      d.addEventListener('click', clique);
      d.addEventListener('cancel', cancelar);
      img.src = src;
    });
  };
})();
