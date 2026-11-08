import csv,json,pathlib,statistics,collections,sys,math,re
root=pathlib.Path(sys.argv[1]);report=json.loads((root/'report.json').read_text(encoding='utf-8-sig'))
phase_offset=float(sys.argv[2]) if len(sys.argv)>2 else 0.0
use_current_profile='--current-profile' in sys.argv[3:]
assert report['status'].startswith('RECORDED') and not report['errors'], report
samples={v['frame']:v for v in csv.DictReader((root/'samples.csv').open(encoding='utf-8-sig'))}
trace=root/'pose-trace.csv'
if not trace.exists():trace=pathlib.Path(report['poseTrace'])
poses=[v for v in csv.DictReader(trace.open(encoding='utf-8-sig')) if v['frame'] in samples]
starts={}
for s in samples.values(): starts.setdefault(s['trial'],float(s['time']))
phasefile=pathlib.Path('Library/LucianCoherence/stride-pilot/contact-phases.csv')
phases=list(csv.DictReader(phasefile.open(encoding='utf-8-sig'))) if phasefile.exists() else []
cycle_masks={}
manifest_file=pathlib.Path('Library/LucianCoherence/stride-cycles/manifest.json')
profile_file=pathlib.Path('Assets/Characters/AnimationPresentation/LucianCoherencePreview/Cadence01/LocomotionMeasurements.asset')
if use_current_profile and manifest_file.exists() and profile_file.exists():
 offsets=dict(re.findall(r'clip: \{fileID: \d+, guid: (\w+), type: \d+\}.*?cycleOffset: ([^\r\n]+)',profile_file.read_text(),re.S))
 for entry in json.loads(manifest_file.read_text())['entries']:
  clip=pathlib.Path(entry['candidate']); mask=manifest_file.parent/clip.stem/'contact-phases.csv'
  guid=re.search(r'^guid: (\w+)',pathlib.Path(str(clip)+'.meta').read_text(),re.M)[1]
  if mask.exists() and guid in offsets:
   name=re.search(r'^  m_Name: (.+)',clip.read_text(),re.M)[1]
   cycle_masks[name]=(list(csv.DictReader(mask.open(encoding='utf-8-sig'))),float(offsets[guid]))
def vector(v,p):return [float(v[p+a]) for a in 'xyz']
def sub(a,b):return [x-y for x,y in zip(a,b)]
def add(a,b):return [x+y for x,y in zip(a,b)]
def cross(a,b):return [a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]]
def rotate(q,v):
 t=[2*x for x in cross(q[:3],v)];c=cross(q[:3],t)
 return [v[i]+q[3]*t[i]+c[i] for i in range(3)]
def dot(a,b):return a[0]*b[0]+a[2]*b[2]
def fit(rows,side):
 n=d=path=0
 for a,b in zip(rows,rows[1:]):
  pa,pb=vector(a,side+'_anchor_'),vector(b,side+'_anchor_');ra,rb=vector(a,'root_'),vector(b,'root_')
  qa=[float(a['root_q'+c]) for c in 'xyzw'];qb=[float(b['root_q'+c]) for c in 'xyzw']
  local=rotate([-qa[0],-qa[1],-qa[2],qa[3]],sub(pa,ra));reference=add(rb,rotate(qb,local))
  rigid=sub(reference,pa);anim=sub(pb,reference)
  n-=dot(rigid,anim);d+=dot(anim,anim);path+=math.hypot(pb[0]-pa[0],pb[2]-pa[2])
 return {'duration':float(rows[-1]['unscaled_time'])-float(rows[0]['unscaled_time']),'path':path,'factor':n/d if d>1e-10 else None,'first_phase':float(rows[0]['normalized'])%1,'last_phase':float(rows[-1]['normalized'])%1,'samples':len(rows),'side':side}
def planted(v,side):
 clips=[c.rsplit(':',1) for c in samples[v['frame']]['base_clips'].split('|')]
 dominant=max(clips,key=lambda c:float(c[1]))
 if float(dominant[1])<.985:return False # No claim about mixed-source contact windows.
 if cycle_masks:
  if dominant[0] not in cycle_masks:return False
  mask,offset=cycle_masks[dominant[0]]
 else:
  if 'Common_Run' not in dominant[0] or not phases:return False
  mask,offset=phases,phase_offset
 i=round(((float(v['normalized'])+offset)%1)*(len(mask)-1))
 return mask[i][side+'_stance']=='1'
result={'run':report['run'],'samples':len(samples),'matched_frames':len(poses),'scope':'Near-plane mesh vertex windows, plus source-native stance-mask subsets. Not visual acceptance. Heights in [-3,+3] cm; capture within 2 cm. Short or split contacts are reported separately.','near_ground':{},'source_stance':{}}
result['source_stance_phase_offset']=phase_offset
result['directional_stance_masks']=sorted(cycle_masks)
result['directional_stance_offsets']={name:data[1] for name,data in cycle_masks.items()}
for side in ['left','right']:
 groups=collections.defaultdict(list)
 for v in poses:
  if int(v[side+'_contact'])>0:groups[v[side+'_contact']].append(v)
 for group in groups.values():
  trials={samples[v['frame']]['trial'] for v in group}
  if len(trials)!=1:continue
  name=next(iter(trials))
  if float(group[0]['unscaled_time'])<starts[name]+.6:continue
  if any(float(v['transition'])>0 or samples[v['frame']]['grounded']!='True' for v in group):continue
  assert len({v[side+'_vertex'] for v in group})==1
  if float(group[-1]['unscaled_time'])-float(group[0]['unscaled_time'])>=.05:
   result['near_ground'].setdefault(name,[]).append(fit(group,side))
  # Split into contiguous stance-mask runs, preserving the exact same vertex identity.
  subset=[]
  for v in group+[None]:
   eligible=v is not None and planted(v,side)
   if eligible:subset.append(v)
   else:
    if len(subset)>=3 and float(subset[-1]['unscaled_time'])-float(subset[0]['unscaled_time'])>=.04:
     result['source_stance'].setdefault(name,[]).append(fit(subset,side))
    subset=[]
(root/'contacts-analysis.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
for mode in ['near_ground','source_stance']:
 print(mode)
 for name,windows in result[mode].items():
  f=[v['factor'] for v in windows if v['factor'] is not None]
  print(name,'n',len(windows),'path median/max',round(statistics.median(v['path'] for v in windows),4),round(max(v['path'] for v in windows),4),'factor median',round(statistics.median(f),3) if f else None)
