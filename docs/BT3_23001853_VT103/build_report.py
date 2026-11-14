from pathlib import Path
import re, json, hashlib, csv, math, statistics, unicodedata
import fitz, markdown
from PIL import Image, ImageOps, ImageDraw

ROOT=Path(__file__).resolve().parent
EVIDENCE=Path((ROOT/'evidence-root.txt').read_text().strip())
source_files=sorted(ROOT.glob('page-*.md'))
assert len(source_files)==12
pages=[p.read_text(encoding='utf-8') for p in source_files]
reflection=pages[7].split('<!-- REFLECTION_START -->')[1].split('<!-- REFLECTION_END -->')[0]
word_count=len(re.findall(r'\S+',reflection)); assert 250<=word_count<=350,word_count
combined='\n\n'.join('<!-- PAGE: '+('MAIN '+str(i+1) if i<8 else 'APPENDIX '+str(i-7))+' -->\n\n'+p for i,p in enumerate(pages))
(ROOT/'BT3_23001853_VT-103.md').write_text(combined,encoding='utf-8')
css='\n@font-face {font-family: Report; src:url(arial.ttf);}\n@font-face {font-family: Report; src:url(arialbd.ttf);font-weight:bold;}\n@font-face {font-family: Report; src:url(ariali.ttf);font-style:italic;}\nbody {font-family:Report; font-size:10.2pt;line-height:1.3;color:#233347;margin:0;}\nh1 {font-size:21pt;line-height:1.15;color:#102e4a;margin:0 0 12pt 0;}\nh2 {font-size:12pt;line-height:1.2;color:#007c89;margin:10pt 0 5pt 0;}\nh3 {font-size:11pt;color:#007c89;margin:0 0 9pt 0;}\np {margin:4pt 0 6pt 0;} strong {font-weight:bold;}\na {color:#007c89;text-decoration:none;}\ntable {width:100%;border-collapse:collapse;margin:6pt 0 8pt 0;table-layout:fixed;font-size:8.4pt;line-height:1.22;}\nth {background:#e5eef5;color:#102e4a;font-weight:bold;text-align:left;}\ntd,th {border:0.5pt solid #cbd7df;padding:4pt;vertical-align:top;}\nblockquote {margin:10pt 0;padding:8pt 10pt;border-left:3pt solid #009ca4;background:#eef6f7;}\nblockquote p {margin:0;}\ncode {font-family:Report;font-size:9pt;color:#234a64;}\npre {padding:7pt;background:#f1f5f8;font-size:8.6pt;line-height:1.25;white-space:pre-wrap;}\nul,ol {padding-left:15pt;margin:4pt 0 8pt 0;} li {margin-bottom:4pt;}\n'
widths={3:[8,14,13,25,34,6],5:[15,11,11,11,12,11,9,20],6:[18,17,22,29,14],8:[11,8,20,20,28,13],9:[11,8,20,20,28,13]}
def table_widths(html, widths):
    def row(m):
        n=[0]
        def cell(c):
            i=n[0];n[0]+=1
            return '<'+c.group(1)+' style="width:'+str(widths[i])+'%">' if i<len(widths) else c.group(0)
        return '<tr>'+re.sub(r'<(th|td)>',cell,m.group(1))+'</tr>'
    return re.sub(r'<tr>(.*?)</tr>',row,html,flags=re.S)
archive=fitz.Archive('C:/Windows/Fonts')
doc=fitz.open();geometry=[];htmls=[]
for i,text in enumerate(pages):
    html=markdown.markdown(text,extensions=['tables','fenced_code'])
    if i in widths:html=table_widths(html,widths[i])
    extra=''
    if i==3:extra='body {font-size:9.5pt;} table {font-size:8.1pt;line-height:1.16;} td,th {padding:3pt;} h1 {font-size:20pt;}'
    if i in (6,10):extra='body {font-size:9.7pt;} table {font-size:8.3pt;} td,th {padding:3.5pt;}'
    if i==7:extra='body {font-size:10pt;} table {font-size:8.4pt;} td,th {padding:3pt;}'
    htmls.append('<html><head><meta charset="utf-8"><style>'+css+extra+'</style></head><body>'+html+'</body></html>')
    page=doc.new_page(width=595.276,height=841.89)
    page.draw_rect(fitz.Rect(0,0,595.276,7),color=None,fill=(0.03,.46,.5))
    page.insert_text((42,30),'SOFTWARE ENGINEERING 2026  /  VT-103',fontname='helv',fontsize=8,color=(.29,.38,.46))
    page.insert_text((467,30),'DRAFT 14.11.2026',fontname='helv',fontsize=8,color=(.29,.38,.46))
    rect=fitz.Rect(42,49,553.276,794)
    spare,scale=page.insert_htmlbox(rect,html,css=css+extra,archive=archive,scale_low=1)
    print('page',i+1,'spare',round(spare,2),'scale',scale,flush=True)
    if spare<0:raise RuntimeError('Page does not fit without scaling: '+str(i+1))
    page.draw_line((42,808),(553.276,808),color=(.78,.84,.88),width=.5)
    label=('MAIN '+str(i+1)+'/8') if i<8 else ('APPENDIX '+str(i-7)+'/4')
    page.insert_text((42,823),'23001853  |  Campus Escape  |  '+label,fontname='helv',fontsize=8,color=(.29,.38,.46))
    page.insert_text((515,823),str(i+1)+' / 12',fontname='helv',fontsize=8,color=(.29,.38,.46))
    geometry.append({'page':i+1,'main':i<8,'fontBodyPt':10.2,'spareHeightPt':spare,'scale':scale})
# Arial shares glyphs for ASCII punctuation and Unicode aliases. MuPDF may select
# the alias in ToUnicode. Correct only destination mappings, preserving CID/glyphs.
seen=set();aliases={'00a0':'0020','00ad':'002d','037e':'003b','a78f':'00b7'}
for page in doc:
    for font in page.get_fonts():
        kind,value=doc.xref_get_key(font[0],'ToUnicode')
        if kind!='xref':continue
        xref=int(value.split()[0])
        if xref in seen:continue
        seen.add(xref);cmap=doc.xref_stream(xref).decode('ascii')
        cmap=re.sub(r'(?m)^(<[0-9a-fA-F]+> )<(00a0|00ad|037e|a78f)>$',lambda m:m.group(1)+'<'+aliases[m.group(2).lower()]+'>',cmap)
        doc.update_stream(xref,cmap.encode('ascii'))
doc.set_metadata({'title':'BT3 - 23001853 - VT-103','author':'Nguyen Huu Duy (AI assistance disclosed)','subject':'Autosave draft: evidence-based software engineering report','keywords':'VT-103; decision table; mutation; autosave; draft'})
pdf=ROOT/'BT3_23001853_VT-103.pdf';doc.save(pdf,garbage=4,deflate=True)
(ROOT/'BT3_23001853_VT-103.html').write_text('\n<hr class="pagebreak">\n'.join(htmls),encoding='utf-8')
# Verify the saved PDF rather than trusting successful generation alone.
check=fitz.open(pdf);full='\n'.join(p.get_text() for p in check);norm=unicodedata.normalize('NFC',' '.join(full.split()))
assert len(check)==12
assert '\ufffd' not in full
required=['Nguy\u1ec5n H\u1eefu Duy','23001853','SE2026-T26','Campus Escape','NOT AVAILABLE','48,951','70,817','105,375','PENDING']
assert all(unicodedata.normalize('NFC',s) in norm for s in required),[s for s in required if s not in norm]
# Character bounds detect any clipping or geometry outside the safe page body.
violations=[]
for i,page in enumerate(check):
    for block in page.get_text('dict')['blocks']:
        if 'lines' not in block:continue
        for line in block['lines']:
            for span in line['spans']:
                x0,y0,x1,y1=span['bbox']
                if x0<40 or x1>557 or y0<18 or y1>830:violations.append({'page':i+1,'text':span['text'],'bbox':span['bbox']})
assert not violations,violations[:3]
# Independently verify raw performance p95 again, without editing measurements.
w=ROOT.parent.parent; perf=w/'Evidence/VT103/performance-20261113-182949'
summary=json.loads((perf/'summary.json').read_text(encoding='utf-8-sig'));samples=[]
for s in summary:
    p=perf/Path(s['SamplesCsv'].replace('\\','/'))
    with p.open(encoding='utf-8-sig',newline='') as f:rows=list(csv.DictReader(f))
    values=sorted(float(row['latencyMs']) for row in rows);assert len(values)==120
    p95=values[math.ceil(.95*len(values))-1];assert abs(p95-s['Latency']['P95Ms'])<.001
    samples.append({'scenario':s['Scenario'],'round':s['Round'],'N':len(values),'p95Ms':p95,'peak':s['PeakInFlight']})
# A3-sized contact sheet and larger selected pages for visual review. Not gameplay screenshots.
preview=ROOT/'pdf-previews';preview.mkdir(exist_ok=True);thumbs=[]
for i,page in enumerate(check):
    pix=page.get_pixmap(matrix=fitz.Matrix(.65,.65),alpha=False);im=Image.frombytes('RGB',[pix.width,pix.height],pix.samples)
    canvas=Image.new('RGB',(im.width+16,im.height+30),'#dce5ed');canvas.paste(im,(8,8));ImageDraw.Draw(canvas).text((8,im.height+12),'PDF page '+str(i+1),fill='black');thumbs.append(canvas)
    if i in (0,3,5,6,7,8,10,11):page.get_pixmap(matrix=fitz.Matrix(1.4,1.4),alpha=False).save(str(preview/('page-'+str(i+1)+'.png')))
cw=max(t.width for t in thumbs);ch=max(t.height for t in thumbs);sheet=Image.new('RGB',(cw*3,ch*4),'#c8d4de')
for i,t in enumerate(thumbs):sheet.paste(t,((i%3)*cw,(i//3)*ch))
sheet.save(preview/'contact-sheet.png')
quality={'pdf':str(pdf),'mainPages':8,'appendixPages':4,'totalPages':12,'reflectionWhitespaceWordCount':word_count,'decisionTableRules':15,'testCaseRows':17,'individuallyListedTestCases':13,'testCaseRequiredColumns':6,'unicodeExtractionChecksPassed':True,'noReplacementCharacters':True,'noGeometryClipping':not violations,'pages':geometry,'recomputedSamples':samples,'lineCoverage':'NOT AVAILABLE','branchCoverage':'NOT AVAILABLE','manualG01':'PARTIAL','manualG02':'PARTIAL','manualG03':'PASS within observed scope','manualG04':'PASS user reported; pair artifacts missing','manualG05':'NOT EXECUTED','manualG06':'NOT EXECUTED','manualG07':'NOT EXECUTED','githubBranch':'PENDING','pr':'PENDING','originalLecturerRubricAvailable':False,'newTestsRunThisStep':0,'pdfSHA256':hashlib.sha256(pdf.read_bytes()).hexdigest()}
(ROOT/'report-quality.json').write_text(json.dumps(quality,indent=2),encoding='utf-8');(EVIDENCE/'report-quality.json').write_text(json.dumps(quality,indent=2),encoding='utf-8')
print('PDF created, reflection words:',word_count,'pages:',len(check),'size:',pdf.stat().st_size,flush=True)
