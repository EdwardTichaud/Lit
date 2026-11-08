import csv,json,math,pathlib,statistics,collections,sys
root=pathlib.Path(sys.argv[1]);report=json.loads((root/'report.json').read_text(encoding='utf-8-sig'))
assert report['status'].startswith('RECORDED') and not report['errors'],report
samples={v['frame']:v for v in csv.DictReader((root/'samples.csv').open(encoding='utf-8-sig'))}
trace=root/'pose-trace.csv'
if not trace.exists():trace=pathlib.Path(report['poseTrace'])
poses=[v for v in csv.DictReader(trace.open(encoding='utf-8-sig')) if v['frame'] in samples]
def quat(v,p):
 q=[float(v[p+c]) for c in 'xyzw'];n=math.sqrt(sum(x*x for x in q));return [x/n for x in q]
def mul(a,b):
 x,y,z,w=a;u,v,t,s=b
 return [w*u+x*s+y*t-z*v,w*v-x*t+y*s+z*u,w*t+x*v-y*u+z*s,w*s-x*u-y*v-z*t]
def local(v,side):
 a=quat(v,'animator_q');return mul([-a[0],-a[1],-a[2],a[3]],quat(v,side+'_q'))
def angle(a,b):return math.degrees(2*math.acos(min(1,abs(sum(x*y for x,y in zip(a,b))))))
def stats(v):return {'count':len(v),'min':min(v),'median':statistics.median(v),'max':max(v)} if v else {}
steps=[];groups=collections.defaultdict(list)
def stage_angle(a,b,p):
 return angle(quat(a,p),quat(b,p))
for v in poses:
 s=samples[v['frame']]
 if s['support_blend']=='True' and s['grounded']=='True' and v['next_state']=='0':groups[s['trial']].append(v)
for a,b in zip(poses,poses[1:]):
 sa,sb=samples[a['frame']],samples[b['frame']];dt=float(b['unscaled_time'])-float(a['unscaled_time'])
 if not 0<dt<=.05 or sa['support_blend']!='True' or sb['support_blend']!='True' or sa['grounded']!='True' or sb['grounded']!='True':continue
 if a['next_state']!='0' or b['next_state']!='0' or a['state']!=b['state']:continue
 event={'trial':sb['trial'],'same_trial':sa['trial']==sb['trial'],'time':float(b['unscaled_time']),'dt':dt,
  'world_angle':max(angle(quat(a,p+'_q'),quat(b,p+'_q')) for p in ['left','right']),
  'local_angle':max(angle(local(a,p),local(b,p)) for p in ['left','right']),
  'actor_angle':angle(quat(a,'animator_q'),quat(b,'animator_q')),'before_clips':sa['base_clips'],'after_clips':sb['base_clips']}
 if 'support_lqx' in sa:
  for stage in ['support','final']:
   event[stage+'_angle']=max(stage_angle(sa,sb,stage+'_'+side+'q') for side in ['l','r'])
  event['ground_weights']=[float(sb[k]) for k in ['left_ground_weight','right_ground_weight']]
 steps.append(event)
result={'run':report['run'],'scope':'Read-only actual UCC telemetry. Sole extrema include intended flight; no artistic/stance acceptance inferred. Rotations filtered to grounded qualified cycles without state transitions; trial-boundary changes remain included.','samples':len(samples),'matched_frames':len(poses),'support_samples':sum(v['support_blend']=='True' for v in samples.values()),'root_motion_enabled_samples':sum(v['root_motion']=='True' for v in samples.values()),'lowest_sole_by_trial':{},'rotation_steps':{}}
for name,rows in groups.items():
 h=[min(float(v['leftsole_y'])-float(v['left_floor_y']),float(v['rightsole_y'])-float(v['right_floor_y'])) for v in rows]
 result['lowest_sole_by_trial'][name]=stats(h)
for name,rows in [('all_qualified',steps),('same_trial',[v for v in steps if v['same_trial']])]:
 keys=['world_angle','local_angle','actor_angle']+(['support_angle','final_angle'] if rows and 'support_angle' in rows[0] else [])
 result['rotation_steps'][name]={k:stats([v[k] for v in rows]) for k in keys}
result['rotation_by_trial']={}
for trial in sorted({v['trial'] for v in steps}):
 rows=[v for v in steps if v['trial']==trial]
 keys=['local_angle']+(['support_angle','final_angle'] if 'support_angle' in rows[0] else [])
 result['rotation_by_trial'][trial]={k:stats([v[k] for v in rows]) for k in keys}
result['largest_rotation_events']=sorted(steps,key=lambda v:v['local_angle'],reverse=True)[:12]
(root/'support-analysis.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:result[k] for k in ['samples','support_samples','root_motion_enabled_samples','rotation_steps']},indent=2))
