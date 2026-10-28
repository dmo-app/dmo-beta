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
//
// Relatório do dmo-Improvements de 28/10/2026 (S01, S02):
// - O rascunho guarda a versão da produção com que foi escrito. Se outra
//   pessoa gravou depois, o rascunho não entra sozinho: a folha mostra o que
//   está gravado e pergunta «Usar o rascunho» ou «Descartar rascunho».
// - O que se enviou fica neste browser (localStorage) até haver a certeza de
//   que gravou: a folha gravada (data-jobon-gravado). Uma falha de rede ou um
//   envio que não chegou deixa-o recuperável na próxima vez que a folha abre.
(() => {
  const PREFIXO = 'dmo-jobon-rascunho:';
  const ENVIADO = 'dmo-jobon-enviado:';
  // A folha nova (/JobOn/Folha) diz quando gravou; o formulário antigo não.
  const CONFIRMA = 'input[name=voltar][value=folha]';
  // O rascunho é de quem o escreveu e dura um dia (vistoria ao frontend F3,
  // 27/10/2026): num computador partilhado, outro operador que abre a mesma
  // folha não recebe o que ficou por gravar de outra pessoa, nem um rascunho
  // de há dias por cima do que está gravado. O dono vem do servidor em
  // data-jobon-rascunho-dono (o id da conta).
  const VALIDADE_MS = 24 * 60 * 60 * 1000;
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
    const gravou = !!document.querySelector('[data-jobon-gravado]');
    // As cópias antigas, de antes de ficarem no localStorage, saem.
    chavesDe('sessionStorage', ENVIADO).forEach(chave => apagar('sessionStorage', chave));
    // Sem folha aberta: o envio gravou se a página o diz (a folha nova) ou,
    // no formulário antigo, por a folha ter fechado. Sem essa certeza (uma
    // falha de rede e depois outra página), a cópia fica para a próxima vez.
    chavesDe('localStorage', ENVIADO).forEach(chave => {
      if (folha && chave === ENVIADO + folha) return;
      const copia = ler('localStorage', chave);
      if (gravou || !copia || !copia.confirma) apagar('localStorage', chave);
    });
    if (!form || !folha) return;
    const chave = PREFIXO + folha;
    const dono = form.getAttribute('data-jobon-rascunho-dono') || null;
    // Os rascunhos de outra pessoa ou com mais de um dia saem (todas as
    // folhas, não só esta), para não ficarem neste browser.
    function caducado(valor) {
      if (!valor) return false;
      if (dono && valor.dono && valor.dono !== dono) return true;
      const quando = Date.parse(valor.guardado || '');
      return Number.isFinite(quando) && Date.now() - quando > VALIDADE_MS;
    }
    chavesDe('localStorage', PREFIXO).concat(chavesDe('localStorage', ENVIADO)).forEach(outra => {
      if (caducado(ler('localStorage', outra))) apagar('localStorage', outra);
    });
    // A versão da produção com que a folha abriu (só no Editar).
    const campoVersao = form.querySelector('input[name=versao]');
    const versao = campoVersao ? campoVersao.value : null;
    function copia(agora) {
      return { guardado: new Date().toISOString(), dono, versao, campos: agora };
    }

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
    function aplicar(linhas, ignorar, simular) {
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
        if (simular) { mudou = true; return; }
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

    // Um envio sem confirmação de que gravou (o servidor recusou, a rede
    // falhou, voltou-se atrás): o que se enviou volta a ser rascunho.
    const pendente = ler('localStorage', ENVIADO + folha);
    apagar('localStorage', ENVIADO + folha);
    if (pendente && !gravou && Array.isArray(pendente.campos)) {
      escrever('localStorage', chave, pendente);
    }

    let nota = null;
    function tirarNota() { if (nota) { nota.remove(); nota = null; } }
    function botao(id, texto, acao) {
      const b = document.createElement('button');
      b.type = 'button';
      b.className = 'dmo-button';
      b.id = id;
      b.textContent = texto;
      b.addEventListener('click', acao);
      return b;
    }
    const rascunho = ler('localStorage', chave);
    if (rascunho && Array.isArray(rascunho.campos)) {
      const doEndereco = new Set(Array.from(new URLSearchParams(location.search).keys()));
      // Outra pessoa gravou depois do rascunho: não entra sem se escolher.
      const outraVersao = versao !== null && String(rascunho.versao ?? '') !== versao;
      if (outraVersao && aplicar(rascunho.campos, doEndereco, true)) {
        nota = document.createElement('div');
        nota.className = 'jobon-aviso warn';
        nota.id = 'jobonRascunhoNota';
        nota.setAttribute('role', 'status');
        nota.append('Há um rascunho desta folha neste browser, mas outra pessoa gravou esta produção depois. A folha mostra o que está gravado. ');
        nota.append(botao('jobonRascunhoUsar', 'Usar o rascunho', () => {
          tirarNota();
          aplicar(rascunho.campos, doEndereco);
          guardar();
        }));
        nota.append(botao('jobonRascunhoDescartar', 'Descartar rascunho', () => {
          apagar('localStorage', chave);
          tirarNota();
        }));
        form.parentNode.insertBefore(nota, form);
      } else if (outraVersao) {
        apagar('localStorage', chave);
      } else if (aplicar(rascunho.campos, doEndereco)) {
        nota = document.createElement('div');
        nota.className = 'jobon-aviso info';
        nota.id = 'jobonRascunhoNota';
        nota.setAttribute('role', 'status');
        let quando = '';
        try {
          if (rascunho.guardado) quando = ' (' + new Date(rascunho.guardado).toLocaleString('pt-PT', { dateStyle: 'short', timeStyle: 'short' }) + ')';
        } catch (e) { quando = ''; }
        nota.append('Rascunho recuperado' + quando + ': esta folha tem alterações por gravar que estavam neste browser. ');
        nota.append(botao('jobonRascunhoDescartar', 'Descartar rascunho', () => {
          apagar('localStorage', chave);
          aplicar(inicial, null);
          tirarNota();
        }));
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
      escrever('localStorage', chave, copia(agora));
    }
    form.addEventListener('input', guardar);
    form.addEventListener('change', guardar);

    form.addEventListener('submit', () => {
      // O envio leva os dados; até se saber que gravou, ficam neste browser
      // (ver pendente).
      const agora = fotografia();
      enviado = true;
      escrever('localStorage', ENVIADO + folha, Object.assign(copia(agora), { confirma: !!form.querySelector(CONFIRMA) }));
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
