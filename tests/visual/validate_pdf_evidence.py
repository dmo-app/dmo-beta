from pathlib import Path
import fitz
import subprocess
from PIL import Image, ImageOps, ImageDraw
import argparse
import sys
parser=argparse.ArgumentParser(description="Validate PDF recovery evidence and render every page for owner review.")
parser.add_argument("root",type=Path,help="Evidence root with a current/ directory containing generated samples")
parser.add_argument("--reference",type=Path,help="Owner's reference Pegamentos PDF (required for visual comparison)")
parser.add_argument("--content-only",action="store_true",help="Check generated PDF content and links without rendering or visual comparison")
args=parser.parse_args()
if not args.content_only and args.reference is None:
    parser.error("--reference is required unless --content-only is selected")
root=args.root
current=root/'current'

def text(name):
    doc=fitz.open(current/name)
    return doc,'\n'.join(p.get_text() for p in doc)

peg, t=text('pegamentos.pdf')
assert len(peg)==1
for value in ['34.55','34.71','34.94','90°','Ovalização','72','27','44','59','19','63','80','33','37','29','atingem ou ultrapassam']:
    assert value in t, (value,t)
assert t.count('AVG')==3
weight,w=text('peso.pdf')
for value in ['143.10','143.63','145.00','138.00','+5.10','+3.70','−0.53','−0.37','−1.90','61','95','63','36','Volume atual','Volume anterior','Variação','4 dia(s)','ST100','NNPB']:
    assert value.casefold() in ' '.join(w.split()).casefold(),(value,w)
assert len(weight)==2
first=weight[0].get_text()
labels=['Peso atual', 'Controlo anterior', 'Produção anterior', 'Nominal do desenho', 'Comparação por contra-molde', 'Contexto técnico']
positions=[first.index(label) for label in labels]
assert positions==sorted(positions)
assert 'Comparação com o controlo anterior' not in first
assert 'RESULTADO PRINCIPAL' not in first
assert 'Peso da produção' in first and 'Peso médio' not in first
assert 'Delta' not in w and 'selecionado' not in w and 'PESO ATUAL · MÉDIA DO VIDRO' not in w
for inline in ['−0.53 g · −0.37 %', '−1.90 g · −1.31 %', '+5.10 g · +3.70 %']:
    assert inline in first, inline
assert w.count('05/03/2026')==1
assert w.count('Média dos CM')==3
for name,expected in [('pegamentos-inside.pdf','Todas as medições dentro'),('pegamentos-seam-outside.pdf','atingem ou ultrapassam'),('pegamentos-across-outside.pdf','atingem ou ultrapassam'),('pegamentos-without-mf.pdf','Sem medições'),('pegamentos-unknown.pdf','não avaliáveis')]:
    doc,t=text(name); assert expected in t, (name,t)
a,t=text('pegamentos-historical.pdf')
assert '40.00' not in t and '34.50' in t
combined,c=text('resumo-completo.pdf')
assert len(combined)==4
assert '143.10' in combined[0].get_text()
assert 'leitura(s)' not in combined[0].get_text() and 'medição(ões)' not in combined[0].get_text()
assert [link['page'] for link in combined[0].get_links()]==[1,3]
assert all(combined[i].get_links()[0]['page']==0 for i in [1,2,3])
assert combined[1].get_text().count('Média dos CM')==1
assert combined[2].get_text().count('Média dos CM')==2 and combined[3].get_text().count('AVG')==3
# Confirm the application returned the complete sheets, not reduced views.
for name, required in [('app-pegamentos.pdf',['34.55','34.71','34.94','Mapa de limites','AVG']),('app-peso.pdf',['Volume anterior','Peso anterior','Média dos CM']),('app-resumo-completo.pdf',['Produção selecionada','Mapa de limites','Volume anterior','Média dos CM','AVG'])]:
    doc,t=text(name)
    assert all(v.casefold() in ' '.join(t.split()).casefold() for v in required),(name,t)
    if name=='app-resumo-completo.pdf': assert len(doc)==4 and len(doc[0].get_links())==2
if args.content_only:
    print('PASS: PDF content, comparisons, alerts, historical values, composition, internal links and HTTP downloads. Visual comparison not run.')
    sys.exit(0)

# Export every page; reference side-by-side images support manual owner review.
for path in current.glob('*.pdf'):
    subprocess.run(["pdftoppm", "-scale-to", "1600", "-png", str(path), str(path.with_suffix(''))+"-page"], check=True)
reference=args.reference
subprocess.run(["pdftoppm", "-f", "1", "-l", "1", "-scale-to", "1600", "-png", "-singlefile", str(reference), str(root/'reference-pegamentos')], check=True)
for old,label,target in [(root/'reference-pegamentos.png','Referência do proprietário','comparison-reference.png'),(root/'baseline/pegamentos-page-1.png','Gerador do branch de referência','comparison-baseline.png')]:
    if not old.exists(): continue
    pictures=[Image.open(old).convert('RGB'),Image.open(current/'pegamentos-page-1.png').convert('RGB')]
    width,height=1131,1600
    canvas=Image.new('RGB',(width*2,height+45),'#eef2f6')
    draw=ImageDraw.Draw(canvas)
    for i,p in enumerate(pictures):
        p=ImageOps.contain(p,(width,height)); canvas.paste(p,(i*width+(width-p.width)//2,45))
    draw.text((20,15),label,fill='black');draw.text((width+20,15),'Recuperação: alerta correto no CM 27',fill='black')
    canvas.save(current/target)
print('PASS: content, independent weight comparisons, all components, boundary scenarios, historical nominal, complete module composition, internal links to both full modules and back from every module page and HTTP downloads.')
