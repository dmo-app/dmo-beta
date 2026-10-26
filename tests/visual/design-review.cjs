// Inspect server-rendered fixtures exported by the HTTP tests. No live database.
const { chromium } = require('playwright');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../..');
const fixtures = path.resolve(process.argv[2] || path.join(root, 'artifacts/design'));
const assets = path.join(root, 'src/DMO.Alpha.Web/wwwroot');
const types = {'.css':'text/css','.js':'application/javascript','.png':'image/png','.woff2':'font/woff2','.svg':'image/svg+xml'};
(async () => {
  const browser = await chromium.launch({headless:true, executablePath:process.env.DMO_CHROMIUM || undefined});
  const results = [];
  for (const filename of fs.readdirSync(fixtures).filter(f => f.endsWith('.html') && /<!DOCTYPE html>/i.test(fs.readFileSync(path.join(fixtures,f),'utf8')))) {
    for (const width of [1366]) {
      const context = await browser.newContext({viewport:{width,height:768},locale:'pt-PT'});
      await context.route('**/*', route => {
        const url = new URL(route.request().url());
        if (url.pathname === '/review') return route.fulfill({contentType:'text/html',body:fs.readFileSync(path.join(fixtures,filename),'utf8')});
        const target = path.resolve(assets, '.'+decodeURIComponent(url.pathname));
        if (target.startsWith(assets + path.sep) && fs.existsSync(target) && fs.statSync(target).isFile()) return route.fulfill({contentType:types[path.extname(target)] || 'application/octet-stream',body:fs.readFileSync(target)});
        return route.fulfill({status:404,body:''});
      });
      const page = await context.newPage();
      const errors=[];
      page.on('pageerror',e=>errors.push(e.message));
      await page.goto('http://dmo.test/review');
      await page.evaluate(()=>document.fonts.ready);
      // Test fixtures open the changes panel explicitly; close it for the initial overview.
      await page.locator('#fichaMudancas').evaluateAll(es=>es.forEach(e=>e.open=false));
      const geometry = await page.evaluate(()=>({width:innerWidth,scroll:document.documentElement.scrollWidth,height:document.documentElement.scrollHeight,logo:!!document.querySelector('.dmo-shell-logo'),user:document.querySelector('[data-user-profile-name]')?.textContent.trim()}));
      if(process.env.DMO_DESIGN_DEBUG && width===1366 && filename==='jobon-folha.html') console.log(await page.locator('[data-jof-familia="CM"] > *').evaluateAll(es=>es.map(e=>({tag:e.className,h:e.getBoundingClientRect().height,css:getComputedStyle(e).cssText,margin:getComputedStyle(e).margin,flex:getComputedStyle(e).flex}))));
      await page.screenshot({path:path.join(fixtures,filename.replace('.html',`-${width}.png`)),fullPage:false});
      results.push({filename,...geometry,errors});
      await context.close();
    }
  }
  await browser.close();
  fs.writeFileSync(path.join(fixtures,'browser-review.json'),JSON.stringify(results,null,2));
  console.log(JSON.stringify(results,null,2));
  if(results.some(r=>r.scroll>r.width+1 || r.errors.length)) process.exitCode=1;
})();
