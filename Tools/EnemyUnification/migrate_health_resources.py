from pathlib import Path
import re,json
root=Path.cwd()
old='3e656d7bea194d128df1cce3a6d2689c'; new='6d482e64f4c70ee4fae5f379165d46a4'
paths=[p for p in Path('Assets').rglob('*') if p.suffix in ('.prefab','.unity','.asset')]
changes={}; refs={}; report=[]
for p in paths:
 t=p.read_text(encoding='utf-8-sig',errors='replace')
 if old not in t: continue
 chunks=re.split(r'(?=^--- !u!)',t,flags=re.M); removed=set(); bygo={}
 for i,c in enumerate(chunks):
  if 'm_Script:' not in c or new not in c: continue
  go=re.search(r'm_GameObject: \{fileID: (\d+)\}',c); fid=re.search(r'^--- !u!\d+ &(-?\d+)',c)
  if go and fid: bygo[go[1]]=(i,fid[1])
 for i,c in enumerate(chunks):
  if 'm_Script:' not in c or old not in c: continue
  fid=re.search(r'^--- !u!\d+ &(-?\d+)',c)[1]; go=re.search(r'm_GameObject: \{fileID: (\d+)\}',c)[1]
  if go in bygo:
   j,keep=bygo[go]; removed.add(i); refs[(str(p),fid)]=keep
   for field in ('maxHp','currentHp','initializeEmptyHealthToMax'):
    m=re.search(r'^  '+field+r': .*$',c,re.M)
    if m: chunks[j]=chunks[j].rstrip()+'\n'+m[0]+'\n'
   chunks=[re.sub(r'^  - component: \{fileID: '+fid+r'\}\n','',x,flags=re.M) for x in chunks]
  else: chunks[i]=c.replace(old,new)
  report.append({'asset':str(p),'old':fid,'new':bygo.get(go,(0,fid))[1]})
 changes[p]=''.join(c for i,c in enumerate(chunks) if i not in removed)
# references can point across prefab assets as well as to local component IDs
assetguids={}
for p in changes:
 meta=Path(str(p)+'.meta')
 if meta.exists(): assetguids[str(p)]=re.search(r'guid: (\w+)',meta.read_text())[1]
for p in paths:
 t=changes.get(p)
 if t is None: t=p.read_text(encoding='utf-8-sig',errors='replace')
 before=t
 for (owner,fid),keep in refs.items():
  if str(p)==owner: t=re.sub(r'\{fileID: '+fid+r'\}', '{fileID: '+keep+'}',t)
  guid=assetguids.get(owner)
  if guid: t=t.replace('fileID: '+fid+', guid: '+guid,'fileID: '+keep+', guid: '+guid)
 if p in changes or t!=before: p.write_text(t,encoding='utf-8')

print('Health resources migrated:',len(report))
