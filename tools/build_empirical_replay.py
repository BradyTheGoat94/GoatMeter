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
    return build_context(max(groups.values(),key=len),class_name,minimum_training)

def build_context(events,class_name='Templar',minimum_training=5):
    """Fit one private context only; never pool actor/target/scope partitions."""
    if not isinstance(minimum_training,int) or minimum_training<5:raise ValueError('At least five training hits required')
    if not events or any(h['class']!=class_name or h['partition']!=events[0]['partition'] for h in events):raise ValueError('Mixed or absent context')
    events=sorted(events,key=lambda x:x['time'])
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
    means={m['id']:m['meanObservedHit'] for m in models}
    excluded=collections.defaultdict(list)
    for h in held:
        if signature(h) not in lookup:
            reason='unseen-signature' if not buckets.get(signature(h)) else 'insufficient-training'
            excluded[(h['skill'],reason)].append(h['amount'])
    absolute_error=sum(abs(means[lookup[signature(h)]]-h['amount']) for h in covered)
    covered_damage=sum(h['amount'] for h in covered)
    return {'schemaVersion':1,'kind':'capture-conditioned-replay','contextId':str(uuid.uuid4()),'className':class_name,'durationMs':duration,'models':models,'timeline':timeline,
      'observed':{'coveredDamage':sum(h['amount'] for h in covered),'coveredHits':len(covered),'totalHits':len(held),'totalDamage':sum(h['amount'] for h in held)},
      'diagnostics':{'trainingHits':len(train),'heldOutHits':len(held),'sumAbsoluteHitError':absolute_error,'coveredHitWeightedAbsoluteErrorPercent':absolute_error/covered_damage*100 if covered_damage else None,'excluded':[{'skill':skill,'reason':reason,'hits':len(amounts),'damage':sum(amounts)} for (skill,reason),amounts in sorted(excluded.items())]},
      'restrictions':{'allowGearScaling':False,'allowNewRotation':False,'targetKind':'unknown','clientPatch':'unknown','independentValidation':False},
      'note':'Earlier hits train per-signature means; later observed hit times/types are supplied. This estimates damage conditional on a known hit schedule, not new macro/APL DPS. Buffs, gear and target state remain confounded.'}
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('log');p.add_argument('--class-name',default='Templar');p.add_argument('--output',required=True);a=p.parse_args()
    with open(a.log,encoding='utf-8',errors='replace') as f:bundle=build_bundle(f,a.class_name)
    with open(a.output,'x',encoding='utf-8') as f:json.dump(bundle,f,indent=2)
    print(json.dumps({'class':a.class_name,'models':len(bundle['models']),'coveredHits':bundle['observed']['coveredHits'],'totalHits':bundle['observed']['totalHits'],'privateOutputWritten':True}))
