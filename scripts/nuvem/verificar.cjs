// Verificação da cópia na nuvem (DMO-736 / A-0509): entra com a conta de teste,
// abre as páginas principais a 1366×768 e a meio ecrã (683×768) e confirma:
// a página abre sem erro e, se o conteúdo não cabe, o scroll funciona (chega ao fundo).
// Uso: node scripts/nuvem/verificar.cjs [endereço]   (por omissão http://127.0.0.1:5080)
const { chromium } = require('playwright');
const base = process.argv[2] || 'http://127.0.0.1:5080';
const contas = [
  { email: 'operador@teste.local', paginas: ['/JobOn', '/Boquilhas', '/Boquilhas?historico=1&vista=movimentos',
    '/Ferramentas', '/Armazem/Ultimos', '/Armazem/Movimentos', '/Armazem/Pesquisar', '/Controlo/Approve', '/Controlo/Peso',
    '/Controlo/Pegamentos', '/Controlo/Puncoes', '/Controlo/Resumo', '/ReparacaoInterna', '/Definicoes'] },
  { email: 'admin@teste.local', paginas: ['/Admin', '/Admin/Users', '/Admin/Templates', '/Admin/Registos', '/Definicoes'] },
];
const ecras = [{ nome: '1366', width: 1366, height: 768 }, { nome: 'meio', width: 683, height: 768 }];
(async () => {
  const browser = await chromium.launch();
  let falhas = 0;
  for (const conta of contas) for (const ecra of ecras) {
    const page = await browser.newPage({ viewport: { width: ecra.width, height: ecra.height } });
    const erros = []; page.on('pageerror', e => erros.push(e.message));
    await page.goto(base + '/login');
    await page.fill('[name=identifier]', conta.email);
    await page.fill('[name=password]', process.env.DMO_TESTE_PW || 'teste-nuvem-123');
    await Promise.all([page.waitForNavigation(), page.click('[data-login-submit]')]);
    if (page.url().includes('/login')) { console.log(`FALHA login ${conta.email}`); falhas++; continue; }
    for (const p of conta.paginas) {
      erros.length = 0;
      const r = await page.goto(base + p);
      const estado = r ? r.status() : 0;
      const s = await page.evaluate(async () => {
        const el = document.scrollingElement;
        const precisa = el.scrollHeight > innerHeight + 2;
        el.scrollTop = 0; window.scrollTo(0, el.scrollHeight);
        await new Promise(r => setTimeout(r, 150));
        const chegou = Math.ceil(el.scrollTop + innerHeight) >= el.scrollHeight - 2;
        const largura = el.scrollWidth > innerWidth + 2;
        // Caixas grandes com conteúdo cortado e sem scroll (overflow hidden/clip) = presas.
        const presas = [];
        for (const e of document.querySelectorAll('body *')) {
          if (e.clientHeight < 150 || e.scrollHeight <= e.clientHeight + 20) continue;
          const oy = getComputedStyle(e).overflowY;
          const r = e.getBoundingClientRect(); if (r.width === 0) continue;
          if (oy === 'hidden' || oy === 'clip') presas.push((e.id ? '#' + e.id : e.tagName.toLowerCase() + '.' + [...e.classList].join('.')));
        }
        return { precisa, chegou, largura, alt: el.scrollHeight, presas };
      });
      const ok = estado < 400 && !page.url().includes('/login') && (!s.precisa || s.chegou) && s.presas.length === 0 && erros.length === 0;
      if (!ok) falhas++;
      console.log(`${ok ? 'OK   ' : 'FALHA'} ${ecra.nome} ${conta.email.split('@')[0]} ${p} http=${estado}` +
        ` altura=${s.alt}${s.precisa ? (s.chegou ? ' scroll=ok' : ' scroll=PRESO') : ''}${s.largura ? ' (desliza para o lado)' : ''}${s.presas.length ? ' PRESO=' + s.presas.slice(0,3).join(',') : ''}` +
        `${erros.length ? ' js=' + erros[0] : ''}${page.url().includes('/login') ? ' (mandou para o login)' : ''}`);
    }
    await page.close();
  }
  await browser.close();
  console.log(falhas ? `\n${falhas} FALHA(S)` : '\nTudo OK');
  process.exit(falhas ? 1 : 0);
})();
