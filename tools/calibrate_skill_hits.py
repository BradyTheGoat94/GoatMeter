"""Private empirical hit baseline. Does not infer casts, coefficients, crit multipliers or game DPS."""
import argparse, collections, json, statistics
from calibration_events import confirmed_hits, FLAG_NAMES

def calibrate(lines, minimum_hits=30, split_flags=True):
    groups=collections.defaultdict(list)
    for hit in confirmed_hits(lines):
        signature=hit['flags'] if split_flags else None
        groups[(hit['class'],hit['skill'],hit['kind'],hit['reportedType'],signature,hit['partition'])].append((hit['time'],hit['amount']))
    output=[]
    for (cls,skill,kind,hit_type,flags,partition),events in groups.items():
        if len(events)<minimum_hits:continue
        events.sort();cut=int(len(events)*.7)
        # A single timestamp cannot straddle train/holdout.
        while cut<len(events) and cut>0 and events[cut][0]==events[cut-1][0]:cut+=1
        train=[amount for _,amount in events[:cut]];held=[amount for _,amount in events[cut:]]
        if len(train)<2 or len(held)<2:continue
        baseline=statistics.median(train)
        positive=[abs(amount-baseline)/amount*100 for amount in held if amount>0]
        output.append({'class':cls,'skill':skill,'eventKind':kind,'reportedType':hit_type,'loggedFlags':dict(zip(FLAG_NAMES,flags)) if flags is not None else None,'trainingHits':len(train),'heldOutHits':len(held),'trainingMedianHit':baseline,'heldOutMedianHit':statistics.median(held),'heldOutMedianAbsolutePercentError':round(statistics.median(positive),3) if positive else None,'heldOutObservedDamage':sum(held),'heldOutBaselineDamage':baseline*len(held),'heldOutTotalDamageErrorPercent':round(abs(baseline*len(held)-sum(held))/sum(held)*100,3) if sum(held)>0 else None,'heldOutZeroHits':sum(x==0 for x in held),'status':'empirical-hit-baseline-only'})
    return {'splitLoggedFlags':split_flags,'cohortCount':len(output),'cohorts':sorted(output,key=lambda r:(r['class'],r['skill'],r['eventKind'],r['reportedType'],r['trainingMedianHit'])),'gameFormulaValidated':False,'limitations':['Chronological holdout within anonymous actor/target/skill/type cohorts; not independent encounter validation.','Unknown gear, specialties, buffs, target defenses, multi-hit grouping and decoder revisions confound hit values.','Complete flags are checked against decoder logging semantics; absent/inconsistent flags remain unknown. This is not independent client-mechanics verification.','Median baseline tests stationarity only; no coefficient, cooldown, cast count, macro attribution or DPS inference.','Exact duplicate suppression may merge legitimate identical hits.','Older fixtures require provenance review before use as calibration evidence.']}
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('log');args=p.parse_args()
    with open(args.log,encoding='utf-8',errors='replace') as stream:print(json.dumps(calibrate(stream),indent=2))
