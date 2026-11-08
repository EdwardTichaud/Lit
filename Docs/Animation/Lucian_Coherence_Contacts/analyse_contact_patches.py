"""Read-only actual flat-floor sole-patch telemetry. No pose, physics or acceptance writes."""
import csv,json,pathlib,statistics,collections,sys
root=pathlib.Path(sys.argv[1]);report=json.loads((root/'report.json').read_text(encoding='utf-8-sig'))
assert report['status'].startswith('RECORDED') and not report['errors'],report
samples={x['frame']:x for x in csv.DictReader((root/'samples.csv').open(encoding='utf-8-sig'))}
trace=root/'pose-trace.csv'
if not trace.exists():trace=pathlib.Path(report['poseTrace'])
poses=[x for x in csv.DictReader(trace.open(encoding='utf-8-sig')) if x['frame'] in samples]
starts={}
for x in samples.values():starts.setdefault(x['trial'],float(x['time']))
def stats(values):
 return {'count':len(values),'min':min(values),'median':statistics.median(values),'max':max(values)} if values else {'count':0}
result={'run':report['run'],'scope':'Actual mesh, flat plane only. Common vertices within 2 mm of adjacent sole minima, minimum within +/-2 cm of floor. Median point displacement accumulated; coverage and short windows explicit. No source stance mask or dominant-clip requirement. Not slope, visual or artistic acceptance.','trials':{}}
windows=collections.defaultdict(list)
for row in poses:
 s=samples[row['frame']]
 for side in ['left','right']:
  if int(row[side+'_patch'])>0:windows[(side,int(row[side+'_patch']))].append(row)
assert windows,'No patch telemetry was exercised'
for name in starts:
 entry={'samples':0,'speed':[],'cadence':[],'mixed_gait_frames':0,'contacts':{},'windows':{}};result['trials'][name]=entry
 rows=[r for r in poses if samples[r['frame']]['trial']==name and float(r['unscaled_time'])>=starts[name]+.6]
 entry['samples']=len(rows)
 for r in rows:
  s=samples[r['frame']];entry['speed'].append(float(s['ucc_speed']));entry['cadence'].append(float(s['playback_rate']))
  clips=s['base_clips'];entry['mixed_gait_frames']+=int(('Walk' in clips or 'Common_Walk' in clips) and ('Run' in clips or 'Common_Run' in clips))
 for side in ['left','right']:
  contact=[r for r in rows if int(r[side+'_patch'])>0];covered=[r for r in contact if int(r[side+'_patch_common'])>0]
  time=sum(float(b['unscaled_time'])-float(a['unscaled_time']) for a,b in zip(rows,rows[1:]) if int(b[side+'_patch_common'])>0)
  distance=sum(float(r[side+'_patch_step']) for r in covered[1:])
  entry['contacts'][side]={'contact_frames':len(contact),'covered_frames':len(covered),'contact_pair_coverage':len(covered)/len(contact) if contact else 0,'measured_path_m':distance,'covered_seconds':time,'path_rate_m_per_second':distance/time if time>0 else None,'sole_height_m':stats([float(r[side+'_patch_height']) for r in rows])}
 for side in ['left','right']:
  accepted=[];short=0;poor=0;cross=0
  for (foot,key),group in windows.items():
   if foot!=side or samples[group[0]['frame']]['trial']!=name:continue
   if any(samples[r['frame']]['trial']!=name for r in group):cross+=1;continue
   if float(group[0]['unscaled_time'])<starts[name]+.6:continue
   first,last=group[0],group[-1];duration=float(last['unscaled_time'])-float(first['unscaled_time'])
   if duration<.05:short+=1;continue
   covered=float(last[side+'_patch_covered'])-float(first[side+'_patch_covered'])
   if covered/duration<.8:poor+=1;continue
   accepted.append({'duration':duration,'path_m':float(last[side+'_patch_path'])-float(first[side+'_patch_path']),'coverage':covered/duration,'samples':len(group)})
  entry['windows'][side]={'full_windows':accepted,'path_m':stats([x['path_m'] for x in accepted]),'short_windows':short,'poor_coverage_windows':poor,'cross_trial_windows':cross}
 # The collection band permits vertical-error inspection. A second, stricter
 # upper band separates lifted swing points from surface-level contact.
 entry['surface_band']={}
 for side in ['left','right']:
  segments=[];segment=[]
  for r in rows+[None]:
   eligible=r is not None and int(r[side+'_patch'])>0 and -.02<=float(r[side+'_patch_height'])<=.002
   contiguous=eligible and (not segment or int(r[side+'_patch'])==int(segment[-1][side+'_patch']) and float(r['unscaled_time'])-float(segment[-1]['unscaled_time'])<=.05)
   if not contiguous and segment:segments.append(segment);segment=[]
   if eligible:segment.append(r)
  if segment:segments.append(segment)
  full=[];short=poor=0
  for segment in segments:
   duration=float(segment[-1]['unscaled_time'])-float(segment[0]['unscaled_time'])
   if duration<.05:short+=1;continue
   pairs=[(a,b) for a,b in zip(segment,segment[1:]) if int(b[side+'_patch_common'])>0]
   covered=sum(float(b['unscaled_time'])-float(a['unscaled_time']) for a,b in pairs)
   path=sum(float(b[side+'_patch_step']) for a,b in pairs)
   if covered/duration<.8:poor+=1;continue
   start_sample=samples[segment[0]['frame']];end_sample=samples[segment[-1]['frame']]
   states={samples[row['frame']]['base_state'] for row in segment}
   full.append({'duration':duration,'path_m':path,'coverage':covered/duration,'samples':len(segment),
                'start_phase':float(start_sample['base_phase']),'end_phase':float(end_sample['base_phase']),
                'base_states':sorted(states),'crosses_base_state':len(states)>1,
                'start_seconds_in_trial':float(segment[0]['unscaled_time'])-starts[name],
                'end_seconds_in_trial':float(segment[-1]['unscaled_time'])-starts[name]})
  entry['surface_band'][side]={'height_limits_m':[-.02,.002],'windows':full,'path_m':stats([x['path_m'] for x in full]),'short_segments':short,'poor_coverage_segments':poor}
 entry['speed']=stats(entry['speed']);entry['cadence']=stats(entry['cadence'])
(root/'patch-analysis.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
for name,e in result['trials'].items():
 paths=[w['path_m'] for side in e['windows'].values() for w in side['full_windows']]
 rates=[side['path_rate_m_per_second'] for side in e['contacts'].values() if side['path_rate_m_per_second'] is not None]
 strict=[w['path_m'] for side in e['surface_band'].values() for w in side['windows']]
 print(name,'surface cm median/max',round(statistics.median(strict)*100,2) if strict else None,round(max(strict)*100,2) if strict else None)
 print(name,'speed',round(e['speed'].get('median',0),3),'mixed',e['mixed_gait_frames'],'windows',len(paths),'median/max cm',round(statistics.median(paths)*100,2) if paths else None,round(max(paths)*100,2) if paths else None,'patch cm/s',round(statistics.mean(rates)*100,2) if rates else None)
