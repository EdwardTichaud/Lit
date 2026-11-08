"""Read-only reconstruction of prepared native sole geometry; not runtime/visual acceptance."""
import bisect,csv,hashlib,json,math,pathlib,re,yaml,statistics
folder=pathlib.Path('Assets/Characters/AnimationPresentation/LucianCoherencePreview/Cadence01')
def load(p):
 text=p.read_text(encoding='utf-8');text=re.sub(r'^%.*\n','',text,flags=re.M);text=re.sub(r'^--- !u!.*$','---',text,flags=re.M)
 return yaml.load(text,Loader=yaml.CSafeLoader)['MonoBehaviour']
profile=load(folder/'LocomotionMeasurements.asset');support=load(folder/'LucianFootSupports.asset')
def vec(x):return tuple(x[k] for k in 'xyz')
def quat(x):return tuple(x[k] for k in 'xyzw')
def mul(a,b):
 x,y,z,w=a;u,v,t,s=b
 return (w*u+x*s+y*t-z*v,w*v-x*t+y*s+z*u,w*t+x*v-y*u+z*s,w*s-x*u-y*v-z*t)
def rotate(q,p):
 n=math.sqrt(sum(x*x for x in q));q=tuple(x/n for x in q);r=mul(mul(q,(*p,0)),(-q[0],-q[1],-q[2],q[3]));return r[:3]
def evaluate(curve,t):
 keys=curve['m_Curve'];i=max(0,min(len(keys)-2,bisect.bisect_right([k['time'] for k in keys],t)-1));a,b=keys[i:i+2]
 assert not a['weightedMode'] and not b['weightedMode'],'Weighted curve requires Unity evaluation'
 dt=b['time']-a['time'];v=(t-a['time'])/dt
 return (2*v**3-3*v*v+1)*a['value']+(v**3-2*v*v+v)*dt*a['outSlope']+(-2*v**3+3*v*v)*b['value']+(v**3-v*v)*dt*b['inSlope']
manifest=json.loads(pathlib.Path('Library/LucianCoherence/stride-cycles/manifest.json').read_text())
entries={}
for entry in manifest['entries']:
 p=pathlib.Path(entry['candidate']);guid=re.search(r'^guid: (\w+)',pathlib.Path(str(p)+'.meta').read_text(),re.M)[1]
 entries[guid]=(p,entry)
cycles=profile['walk']+profile['run']+[profile['explorationWalk'],profile['explorationRun']]
seen=set();results=[]
for cycle in cycles:
 guid=cycle['clip']['guid']
 if guid in seen:continue
 seen.add(guid);clip,entry=entries[guid];data=next(x for x in support['cycles'] if x['clip']['guid']==guid)
 duration=float(re.search(r'm_StopTime: ([^\r\n]+)',clip.read_text())[1]);count=support['steps'];dt=duration/count
 direction=(cycle['direction']['x'],cycle['direction']['y']);magnitude=math.hypot(*direction);assert magnitude>.001;direction=tuple(v/magnitude for v in direction);speed=cycle['nativeSpeed']
 maskfile=pathlib.Path('Library/LucianCoherence/stride-cycles')/clip.stem/'contact-phases.csv'
 if not maskfile.exists():maskfile=pathlib.Path('Library/LucianCoherence/stride-pilot/contact-phases.csv')
 masks=list(csv.DictReader(maskfile.open()));row={'clip':str(clip),'native_speed':speed,'duration':duration,'expected_direction':direction,'feet':{}}
 for side in ['left','right']:
  n=len(support[side+'PointIds']);geometry=data[side];goal=cycle['footGoals'];offset=quat(support[side+'GoalToBone']);frames=[];stance=[]
  for i in range(count+1):
   phase=i/count;p=[evaluate(c,phase) for c in goal[side+'Position']];q=tuple(evaluate(c,phase) for c in goal[side+'Rotation']);q=mul(q,offset)
   shape=[rotate(q,vec(x)) for x in geometry[i*n:(i+1)*n]]
   p[1]=data['heights'][i]['x' if side=='left' else 'y']-min(x[1] for x in shape)
   frames.append([(p[0]+x+speed*duration*phase*direction[0],p[1]+y,p[2]+z+speed*duration*phase*direction[1]) for x,y,z in shape])
   stance.append(masks[round(phase*(len(masks)-1))][side+'_stance']=='1')
  intervals=[];active=[]
  for i in range(count*2):
   if stance[i%count]:active.append(i)
   elif active:intervals.append(active);active=[]
  # Only complete source-stance intervals, avoiding partial endpoints.
  for indices in intervals:
   if indices[0]==0 or len(indices)<3:continue
   shifted=[]
   for i in indices:
    lap=i//count;shifted.append([(x+lap*speed*duration*direction[0],y,z+lap*speed*duration*direction[1]) for x,y,z in frames[i%count]])
   records=[]
   for tolerance in [.001,.002,.005,.01,.02,.03]:
    path=0;covered=0;total=len(shifted)-1;minimumPatch=999;vx=[];vz=[]
    longitudinal_path=0
    lateral_path=0
    required_speed=[]
    for a,b in zip(shifted,shifted[1:]):
     amin=min(v[1] for v in a);bmin=min(v[1] for v in b)
     patch=[j for j in range(n) if a[j][1]<=amin+tolerance and b[j][1]<=bmin+tolerance]
     minimumPatch=min(minimumPatch,len(patch))
     if not patch:continue
     distances=sorted(math.hypot(b[j][0]-a[j][0],b[j][2]-a[j][2]) for j in patch)
     path+=distances[len(distances)//2];covered+=1
     dx=statistics.median(b[j][0]-a[j][0] for j in patch)
     dz=statistics.median(b[j][2]-a[j][2] for j in patch)
     along=dx*direction[0]+dz*direction[1]
     across=dx*direction[1]-dz*direction[0]
     longitudinal_path+=abs(along)
     lateral_path+=abs(across)
     required_speed.append(speed-along/dt)
     vx.append(speed*direction[0]-dx/dt)
     vz.append(speed*direction[1]-dz/dt)
    records.append({
     'tolerance_m':tolerance,'path_m':path,'coverage':covered/total,
     'covered_pairs':covered,'total_pairs':total,'minimum_patch_vertices':minimumPatch,
     'fitted_velocity_x':statistics.median(vx) if vx else None,
     'fitted_velocity_z':statistics.median(vz) if vz else None,
     'longitudinal_path_m':longitudinal_path,'lateral_path_m':lateral_path,
     'required_speed_mps':{
      'min':min(required_speed),'median':statistics.median(required_speed),
      'max':max(required_speed)
     } if required_speed else None,
    })
   anchor=min(range(n),key=lambda j:shifted[0][j][1]);anchorPath=sum(math.hypot(b[anchor][0]-a[anchor][0],b[anchor][2]-a[anchor][2]) for a,b in zip(shifted,shifted[1:]))
   row['feet'].setdefault(side,[]).append({'start_phase':indices[0]/count,'end_phase':indices[-1]/count,'duration':(len(indices)-1)*dt,'fixed_vertex':support[side+'PointIds'][anchor],'fixed_vertex_path_m':anchorPath,'contact_patches':records})
 results.append(row)
report={'scope':'Prepared native geometry at 120 samples/cycle with constant virtual translation. Source stance masks are estimates. Contact-patch path accumulates median movement of identical vertices near both adjacent sole minima; gaps and tolerances explicit. Not actual UCC, mixed-cycle or artistic acceptance.','profile_sha256':hashlib.sha256((folder/'LocomotionMeasurements.asset').read_bytes()).hexdigest(),'geometry_sha256':hashlib.sha256((folder/'LucianFootSupports.asset').read_bytes()).hexdigest(),'cycles':results}
out=pathlib.Path('Library/LucianCoherence/source-support-patches.json');out.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
for row in results:
 windows=[w for items in row['feet'].values() for w in items];patch=[w['contact_patches'][1] for w in windows]
 print(pathlib.Path(row['clip']).stem,'windows',len(windows),'fixed_max',round(max((w['fixed_vertex_path_m'] for w in windows),default=0),4),'2mm_patch_max',round(max((w['path_m'] for w in patch),default=0),4),'min_coverage',round(min((w['coverage'] for w in patch),default=0),3),'fitted_v',[(round(w['fitted_velocity_x'],3),round(w['fitted_velocity_z'],3)) for w in patch[:2]])
