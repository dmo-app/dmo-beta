// O RASCUNHO DA FOLHA DO JOB ON (teste no browser real, 22/10/2026: recarregar
// a página apagava o que estava escrito sem aviso). O que se escreve na folha
// fica guardado neste browser (localStorage), um rascunho por folha: o Novo
// Job On, o Duplicar de cada produção e o Editar de cada Job On. Ao voltar à
// mesma folha, o rascunho volta aos campos com a nota «Rascunho recuperado» e
// «Descartar rascunho» repõe o que o servidor mostrou. Sair com alterações por
// gravar pede confirmação ao browser. Gravar apaga o rascunho; se o servidor
// recusar a gravação, o rascunho volta. Os campos que chegam no endereço (o
// regresso de «Criar CM») ganham ao rascunho. Sem armazenamento (janela
// privada, dados bloqueados) a folha funciona como antes.
(() => {
  const PREFIXO = 'dmo-jobon-rascunho:';
  const ENVIADO = 'dmo-jobon-enviado:';
  const CAMPOS = 'input[name]:not([type=hidden]):not([type=submit]):not([type=button]):not([type=reset]):not([type=image]):not([type=file]), textarea[name], select[name]';

  function ler(area, chave) {
    try { const texto = window[area].getItem(chave); return texto ? JSON.parse(texto) : null; } catch (e) { return null; }
  }
  function escrever(area, chave, valor) {
    try { window[area].setItem(chave, JSON.stringify(valor)); return true; } catch (e) { return false; }
  }
  function apagar(area, chave) {
    try { window[area].removeItem(chave); } catch (e) { /* sem armazenamento */ }
  }
  function chavesDe(area, prefixo) {
    const chaves = [];
    try {
      const store = window[area];
      for (let i = 0; i < store.length; i++) {
        const chave = store.key(i);
        if (chave && chave.indexOf(prefixo) === 0) chaves.push(chave);
      }
    } catch (e) { /* sem armazenamento */ }
    return chaves;
  }

  function init() {
    const form = document.getElementById('lightForm');
    const folha = form && form.getAttribute('data-jobon-rascunho');
    // Sem folha aberta: um envio que esperava a resposta gravou (a folha fechou).
    chavesDe('sessionStorage', ENVIADO).forEach(chave => {
      if (!folha || chave !== ENVIADO + folha) apagar('sessionStorage', chave);
    });
    if (!form || !folha) return;
    const chave = PREFIXO + folha;

    function campos() { return Array.from(form.querySelectorAll(CAMPOS)); }
    function valor(campo) {
      if (campo.type === 'checkbox' || campo.type === 'radio') return campo.checked ? '1' : '';
      return campo.value;
    }
    function fotografia() {
      return campos().map(campo => {
        const linha = { n: campo.name, v: valor(campo) };
        if (campo.tagName === 'SELECT' && campo.selectedOptions[0]) linha.t = campo.selectedOptions[0].text;
        return linha;
      });
    }
    function igual(a, b) {
      if (a.length !== b.length) return false;
      return a.every((linha, i) => linha.n === b[i].n && linha.v === b[i].v);
    }
    function aplicar(linhas, ignorar) {
      const porNome = {};
      linhas.forEach(linha => { (porNome[linha.n] = porNome[linha.n] || []).push(linha); });
      const vez = {};
      let mudou = false;
      campos().forEach(campo => {
        const i = vez[campo.name] = (vez[campo.name] || 0);
        vez[campo.name]++;
        const linha = (porNome[campo.name] || [])[i];
        if (!linha || (ignorar && ignorar.has(campo.name)) || campo.readOnly || campo.disabled) return;
        if (valor(campo) === linha.v) return;
        if (campo.type === 'checkbox' || campo.type === 'radio') {
          campo.checked = linha.v === '1';
        } else if (campo.tagName === 'SELECT') {
          // A ferramenta escolhida pode não vir nas opções da página (a procura
          // só traz sugestões): a opção volta com o nome que tinha.
          if (linha.v !== '' && !Array.from(campo.options).some(o => o.value === linha.v)) {
            campo.add(new Option(linha.t || linha.v, linha.v));
          }
          campo.value = linha.v;
          if (campo.value !== linha.v) return;
        } else {
          campo.value = linha.v;
        }
        mudou = true;
        campo.dispatchEvent(new Event('input', { bubbles: true }));
        campo.dispatchEvent(new Event('change', { bubbles: true }));
      });
      return mudou;
    }

    const inicial = fotografia();
    let enviado = false;

    // Uma gravação que o servidor recusou: o que se enviou volta a ser rascunho.
    const pendente = ler('sessionStorage', ENVIADO + folha);
    apagar('sessionStorage', ENVIADO + folha);
    if (pendente && document.querySelector('#lightSheet [data-dmo-toast="erro"]')) {
      escrever('localStorage', chave, pendente);
    }

    let nota = null;
    function tirarNota() { if (nota) { nota.remove(); nota = null; } }
    const rascunho = ler('localStorage', chave);
    if (rascunho && Array.isArray(rascunho.campos)) {
      const doEndereco = new Set(Array.from(new URLSearchParams(location.search).keys()));
      if (aplicar(rascunho.campos, doEndereco)) {
        nota = document.createElement('div');
        nota.className = 'jobon-aviso info';
        nota.id = 'jobonRascunhoNota';
        nota.setAttribute('role', 'status');
        let quando = '';
        try {
          if (rascunho.guardado) quando = ' (' + new Date(rascunho.guardado).toLocaleString('pt-PT', { dateStyle: 'short', timeStyle: 'short' }) + ')';
        } catch (e) { quando = ''; }
        nota.append('Rascunho recuperado' + quando + ': esta folha tem alterações por gravar que estavam neste browser. ');
        const descartar = document.createElement('button');
        descartar.type = 'button';
        descartar.className = 'dmo-button';
        descartar.id = 'jobonRascunhoDescartar';
        descartar.textContent = 'Descartar rascunho';
        descartar.addEventListener('click', () => {
          apagar('localStorage', chave);
          aplicar(inicial, null);
          tirarNota();
        });
        nota.append(descartar);
        form.parentNode.insertBefore(nota, form);
      } else {
        // O rascunho já é o que a folha mostra.
        apagar('localStorage', chave);
      }
    }

    function guardar() {
      if (enviado) return;
      const agora = fotografia();
      if (igual(agora, inicial)) { apagar('localStorage', chave); return; }
      escrever('localStorage', chave, { guardado: new Date().toISOString(), campos: agora });
    }
    form.addEventListener('input', guardar);
    form.addEventListener('change', guardar);

    form.addEventListener('submit', () => {
      // O envio leva os dados; se o servidor recusar, voltam (ver pendente).
      const agora = fotografia();
      enviado = true;
      escrever('sessionStorage', ENVIADO + folha, { guardado: new Date().toISOString(), campos: agora });
      apagar('localStorage', chave);
      tirarNota();
    });

    window.addEventListener('beforeunload', event => {
      if (enviado || igual(fotografia(), inicial) && !ler('localStorage', chave)) return;
      event.preventDefault();
      event.returnValue = '';
    });
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
  else init();
})();
