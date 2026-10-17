"""PRIVATE within-context holdouts. Not cross-context validation or a game formula.
No actor IDs/names/raw packets are serialized; still keep capture-derived output private.
"""
import argparse,collections,json
from calibration_events import confirmed_hits
from build_empirical_replay import build_context

def audit_contexts(lines,class_name='Templar'):
    groups=collections.defaultdict(list)
    for h in confirmed_hits(lines):
        if h['class']==class_name:groups[h['partition']].append(h)
    results=[];skipped=collections.Counter()
    for rank,events in enumerate(sorted(groups.values(),key=len,reverse=True),1):
        try:b=build_context(events,class_name)
        except ValueError as error:skipped[str(error)]+=1;continue
        o=b['observed'];means={m['id']:m['meanObservedHit'] for m in b['models']};predicted=sum(means[h['modelId']] for h in b['timeline'])
        times=sorted(h['time'] for h in events)
        results.append({'contextRank':rank,'sampleHits':len(events),'observedSpanSeconds':times[-1]-times[0],
          'largestObservedGapSeconds':max((z-a for a,z in zip(times,times[1:])),default=0),
          'coveredHitFraction':o['coveredHits']/o['totalHits'],'coveredDamageFraction':o['coveredDamage']/o['totalDamage'] if o['totalDamage'] else None,
          'coveredTotalErrorPercent':abs(predicted-o['coveredDamage'])/o['coveredDamage']*100 if o['coveredDamage'] else None,
          'coveredHitWeightedAbsoluteErrorPercent':b['diagnostics']['coveredHitWeightedAbsoluteErrorPercent'],
          'excludedSkills':sorted(set(x['skill'] for x in b['diagnostics']['excluded']))})
    return {'schemaVersion':1,'className':class_name,'partitionCount':len(groups),'usableContexts':results,'skipped':dict(skipped),
      'warning':'Each row trains and tests within its own observed context. Rows are not independent encounters; no cross-context transfer, stationary dummy, macro quality, buff state or stat formula is validated. Gaps are descriptive, not encounter boundaries.'}

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('log');p.add_argument('--class-name',default='Templar');p.add_argument('--output',required=True);a=p.parse_args()
    with open(a.log,encoding='utf-8',errors='replace') as f:r=audit_contexts(f,a.class_name)
    with open(a.output,'x',encoding='utf-8') as f:json.dump(r,f,indent=2)
    print(json.dumps({'class':a.class_name,'partitionCount':r['partitionCount'],'usableContexts':len(r['usableContexts']),'privateOutputWritten':True}))
