# Run from the Unity project root. Requires Python 3 and Git. Re-running migrated resources is a no-op.
from pathlib import Path
import re,json,subprocess,uuid,sys
subprocess.run([sys.executable, "Tools/EnemyUnification/migrate_health_resources.py"], check=True)
Path('Library/EnemyUnification').mkdir(parents=True,exist_ok=True)
mapping=json.loads(Path('Tools/EnemyUnification/asset-map.json').read_text()); settings=json.loads(Path('Tools/EnemyUnification/settings-map.json').read_text())
newguid=mapping['EnemyCombatBrain']['guid']; infoguid='6d482e64f4c70ee4fae5f379165d46a4'
guids={v['guid']:k for k,v in mapping.items()}
for filename,cls in [('PlayerAnimationController','Animation'),('PlayerRootMotionRelay','Relay'),('PlayerCombatAnimationEvents','Events')]:
 p=next(Path('Assets/CombatRealTime').rglob(filename+'.cs.meta'));guids[re.search(r'guid: (\w+)',p.read_text())[1]]=cls
legacyguid='21b7ec7933b64c51b172ca8f8e4d9431';guids[legacyguid]='Legacy'
paths=[Path(p) for p in subprocess.check_output(['git','ls-files','Assets'],text=True).splitlines() if p.endswith(('.prefab','.unity','.asset'))]
meta={}
for p in paths:
 m=Path(str(p)+'.meta')
 if m.exists():meta[re.search(r'guid: (\w+)',m.read_text())[1]]=p
paths.sort(key=lambda p: ("GiantJuggernaut" in str(p), str(p)))
changes={}; redirects={}; settingsByData={}; report=[]
def fields(c):
 return {m[1]:m[2] for m in re.finditer(r'^  (\w+):([^\n]*(?:\n(?!  \w+:|---)[^\n]*)*)',c,re.M)}
for p in paths:
 if p.suffix not in ('.prefab','.unity'):continue
 t=p.read_text(encoding='utf-8-sig',errors='replace')
 if not any(g in t for g in [v['guid'] for v in mapping.values()]):continue
 if 'combatEnabled:' in t and not any(g in t for g in guids if g != newguid and guids[g] in mapping):continue
 chunks=re.split(r'(?=^--- !u!)',t,flags=re.M);groups={};types={}
 for i,c in enumerate(chunks):
  sm=re.search(r'm_Script: \{fileID: \d+, guid: (\w+)',c);go=re.search(r'm_GameObject: \{fileID: (-?\d+)\}',c);fid=re.search(r'^--- !u!\d+ &(-?\d+)',c)
  if not (sm and go and fid):continue
  if sm[1]==infoguid: types[go[1]]=fields(c).get('characterData','')
  if ' stripped\n' in c.split('MonoBehaviour:')[0]:continue
  if sm[1] in guids:groups.setdefault(go[1],[]).append((i,fid[1],guids[sm[1]]))
 removed=set();local={}
 for go,comps in groups.items():
  if not any(n in mapping for _,_,n in comps):continue
  primary=next((c for c in comps if c[2]=='EnemyCombatBrain'),next(c for c in comps if c[2] in mapping))
  keepidx,keepid,_=primary; payload={};config={};mode=True
  for i,fid,cls in comps:
   f=fields(chunks[i]); local[fid]=keepid
   if cls=='RealTimeCombatEnemy':mode=f.get('m_Enabled',' 1').strip()!='0'
   for key,value in f.items():
    if key.startswith('m_'):continue
    if cls in mapping:
     new=mapping[cls]['fields'].get(key)
     if new:
      if cls+'.'+key in settings:config[new]=value
      else:payload[new]=value
    elif cls=='Animation' and key in ('animationRoot','animator','lockPoint'):payload['Animation'+key[0].upper()+key[1:]]=value
    elif cls=='Events' and key in ('inputPromptAnchor','inputPromptOffset'):payload[key]=value
   if i!=keepidx:removed.add(i)
  # Original script component header retained, with one enabled controller.
  c=chunks[keepidx];header=c[:c.index('  m_EditorClassIdentifier:')];header=re.sub(r'm_Script: .*', 'm_Script: {fileID: 11500000, guid: '+newguid+', type: 3}',header);header=re.sub(r'm_Enabled: \d+','m_Enabled: 1',header)
  chunks[keepidx]=header+'  m_EditorClassIdentifier: \n  combatEnabled: '+('1' if mode else '0')+'\n'+''.join('  '+k+':'+v.rstrip()+'\n' for k,v in payload.items())
  dataref=re.search(r'guid: (\w+)',types.get(go,''))
  if dataref:
   dg=dataref[1]
   if dg in settingsByData and settingsByData[dg]!=config:
    originalData=meta[dg]; newData=p.parent/(p.stem+'_CharacterData.asset'); newGuid=uuid.uuid4().hex
    changes[newData]=originalData.read_text()
    Path(str(newData)+'.meta').write_text('fileFormatVersion: 2\nguid: '+newGuid+'\n')
    meta[newGuid]=newData
    chunks=[c.replace('guid: '+dg,'guid: '+newGuid) if 'm_Script:' in c and infoguid in c else c for c in chunks]
    dg=newGuid
   settingsByData[dg]=config
  report.append({'asset':str(p),'root':go,'controller':keepid,'removed':len(comps)-1,'data':dataref[1] if dataref else None})
  pg=re.search(r'guid: (\w+)',Path(str(p)+'.meta').read_text())[1]
  for i,fid,cls in comps:redirects[(pg,fid)]=(keepid,cls)
 for fid,keep in local.items():
  if fid==keep:continue
  chunks=[re.sub(r'^  - component: \{fileID: '+fid+r'\}\n','',c,flags=re.M) for c in chunks]
 new=''.join(c for i,c in enumerate(chunks) if i not in removed)
 for fid,keep in local.items():
  if fid!=keep:new=re.sub(r'\{fileID: '+fid+r'\}', '{fileID: '+keep+'}',new)
 changes[p]=new
for guid,config in settingsByData.items():
 p=meta[guid];t=changes.get(p) or p.read_text();t+='  enemySettings:\n'+''.join('    '+k+':'+re.sub(r'\n', '\n  ',v.rstrip())+'\n' for k,v in config.items());changes[p]=t
pending=[]
for p in list(dict.fromkeys(paths+list(changes))):
 t=changes.get(p)
 if t is None:
  if p.suffix not in ('.prefab','.unity','.asset'):continue
  t=p.read_text(encoding='utf-8-sig',errors='replace')
 original=t
 # Property overrides must be translated using the original component owner before remapping IDs.
 def override(m):
  g=m[2];fid=m[1];key=m[3];entry=redirects.get((g,fid))
  if not entry:return m[0]
  keep,cls=entry;newkey=key
  if cls in mapping:
   rootkey=key.split('.')[0]
   if cls+'.'+rootkey in settings:pending.append({'asset':str(p),'class':cls,'property':key,'block':m[0]})
   newkey=mapping[cls]['fields'].get(rootkey,rootkey)+key[len(rootkey):]
  elif cls=='Animation' and key in ('animationRoot','animator','lockPoint'):newkey='Animation'+key[0].upper()+key[1:]
  if key=='m_Enabled' and cls!='EnemyCombatBrain':newkey='combatEnabled'
  return m[0].replace('propertyPath: '+key,'propertyPath: '+newkey)
 t=re.sub(r'- target: \{fileID: (-?\d+), guid: (\w+), type: 3\}\n      propertyPath: ([^\n]+)',override,t)
 for (g,fid),(keep,cls) in redirects.items():t=t.replace('fileID: '+fid+', guid: '+g,'fileID: '+keep+', guid: '+g)
 # Update missing-script identities on inherited components without recreating their serialized state.
 def strip_script(m):
  c=m[0]
  for g,cls in guids.items():
   if cls in mapping: c=c.replace('guid: '+g+', type: 3}', 'guid: '+newguid+', type: 3}') if 'm_Script:' in c else c
  return c
 t=re.sub(r'^--- !u!114 &-?\d+ stripped\n(?:(?!^---).)*',strip_script,t,flags=re.M|re.S)
 # Collapse stripped scene aliases now pointing to the same inherited component.
 chunks=re.split(r'(?=^--- !u!)',t,flags=re.M);seen={};aliases={};remove=set()
 for i,c in enumerate(chunks):
  fid=re.search(r'^--- !u!114 &(-?\d+) stripped',c);source=re.search(r'm_CorrespondingSourceObject: (\{[^\n]+)',c);instance=re.search(r'm_PrefabInstance: (\{[^\n]+)',c)
  if not (fid and source and instance):continue
  key=(source[1],instance[1])
  if key in seen:aliases[fid[1]]=seen[key];remove.add(i)
  else:seen[key]=fid[1]
 if remove:t=''.join(c for i,c in enumerate(chunks) if i not in remove)
 for fid,keep in aliases.items():t=re.sub(r'\{fileID: '+fid+r'\}', '{fileID: '+keep+'}',t)
 if p in changes or t!=original:
  temp=p.with_suffix(p.suffix+'.migration-tmp');temp.write_text(t,encoding='utf-8');temp.replace(p)
Path('Library/EnemyUnification/resource-report.json').write_text(json.dumps(report,indent=2));Path('Library/EnemyUnification/pending-overrides.json').write_text(json.dumps(pending,indent=2))
print(json.dumps(report,indent=2));print('Settings overrides requiring transfer:',len(pending))
