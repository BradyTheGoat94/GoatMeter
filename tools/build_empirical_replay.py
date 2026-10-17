"""Build a PRIVATE capture-conditioned replay bundle. Never commit its output.
Not a rotation simulator, gear scaler, dummy benchmark, or independently verified game model.
"""
import argparse,collections,json,statistics,uuid
from calibration_events import confirmed_hits

def build_bundle(lines,class_name='Templar',minimum_training=5):
    groups=collections.defaultdict(list)
    for hit in confirmed_hits(lines):
        if hit['class']==class_name:groups[hit['partition']].append(hit)
    if not groups:raise ValueError('No confirmed class samples')
    # Largest observed single-actor/single-target partition; not independently identified as boss/dummy.
    events=max(groups.values(),key=len);events.sort(key=lambda x:x['time'])
    cut=int(len(events)*.7)
    while 0<cut<len(events) and events[cut]['time']==events[cut-1]['time']:cut+=1
    train,held=events[:cut],events[cut:]
    if len(train)<10 or len(held)<2:raise ValueError('Insufficient chronological holdout')
    signature=lambda h:(h['skill'],h['kind'],h['reportedType'],h['flags'])
    buckets=collections.defaultdict(list)
    for h in train:buckets[signature(h)].append(h['amount'])
    models=[];lookup={}
    for sig,amounts in buckets.items():
        if len(amounts)<minimum_training:continue
        key='hit-'+str(len(models));lookup[sig]=key
        models.append({'id':key,'skill':sig[0],'eventKind':sig[1],'reportedType':sig[2],'flags':list(sig[3]) if sig[3] is not None else None,'trainingHits':len(amounts),'meanObservedHit':statistics.mean(amounts)})
    first,last=held[0]['time'],held[-1]['time'];duration=round((last-first)*1000)+1
    if duration<=1:raise ValueError('No usable holdout duration')
    timeline=[{'atMs':round((h['time']-first)*1000),'modelId':lookup[signature(h)]} for h in held if signature(h) in lookup]
    if not timeline:raise ValueError('No supported held-out hits')
    covered=[h for h in held if signature(h) in lookup]
    return {'schemaVersion':1,'kind':'capture-conditioned-replay','contextId':str(uuid.uuid4()),'className':class_name,'durationMs':duration,'models':models,'timeline':timeline,
      'observed':{'coveredDamage':sum(h['amount'] for h in covered),'coveredHits':len(covered),'totalHits':len(held),'totalDamage':sum(h['amount'] for h in held)},
      'restrictions':{'allowGearScaling':False,'allowNewRotation':False,'targetKind':'unknown','clientPatch':'unknown','independentValidation':False},
      'note':'Earlier hits train per-signature means; later observed hit times/types are supplied. This estimates damage conditional on a known hit schedule, not new macro/APL DPS. Buffs, gear and target state remain confounded.'}
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('log');p.add_argument('--class-name',default='Templar');p.add_argument('--output',required=True);a=p.parse_args()
    with open(a.log,encoding='utf-8',errors='replace') as f:bundle=build_bundle(f,a.class_name)
    with open(a.output,'x',encoding='utf-8') as f:json.dump(bundle,f,indent=2)
    print(json.dumps({'class':a.class_name,'models':len(bundle['models']),'coveredHits':bundle['observed']['coveredHits'],'totalHits':bundle['observed']['totalHits'],'privateOutputWritten':True}))
