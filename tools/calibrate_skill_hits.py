"""Private empirical hit baseline. Does not infer casts, coefficients, crit multipliers or game DPS."""
import argparse, collections, datetime, hashlib, json, re, statistics

def calibrate(lines, minimum_hits=30):
    pending=collections.OrderedDict(); seen=set(); groups=collections.defaultdict(list)
    for line in lines:
        parts=line.strip().split('|'); stamp=parts[0]
        f=dict(x.split('=',1) for x in parts[1:] if '=' in x)
        if f.get('tag') in ('damage','dot','summonDamage') and f.get('amount','').isdigit():
            key=(stamp,f.get('src'),f.get('tgt'));pending[key]=(f.get('skill',''),int(f['amount']),f['tag'],f.get('type','unknown'),hashlib.sha256(f.get('raw','').encode()).hexdigest())
            while len(pending)>128:pending.popitem(last=False)
        elif f.get('tag')=='resolvedCombat':
            hit=pending.pop((stamp,f.get('entity'),f.get('target')),None)
            if not hit or f.get('confirmed')!='True' or f.get('class') in (None,'Unknown'):continue
            dedup=(stamp,f.get('entity'),f.get('target'),*hit)
            if dedup in seen:continue
            seen.add(dedup)
            try:time=datetime.datetime.fromisoformat(re.sub(r'(\.\d{6})\d+',r'\1',stamp).replace('Z','+00:00')).timestamp()
            except ValueError:continue
            # Internal actor/target partitions never appear in the report.
            groups[(f['class'],hit[0],hit[2],hit[3],f.get('entity'),f.get('target'))].append((time,hit[1]))
    output=[]
    for (cls,skill,kind,hit_type,actor,target),events in groups.items():
        if len(events)<minimum_hits:continue
        events.sort();cut=int(len(events)*.7)
        # A single timestamp cannot straddle train/holdout.
        while cut<len(events) and cut>0 and events[cut][0]==events[cut-1][0]:cut+=1
        train=[amount for _,amount in events[:cut]];held=[amount for _,amount in events[cut:]]
        if len(train)<2 or len(held)<2:continue
        baseline=statistics.median(train)
        positive=[abs(amount-baseline)/amount*100 for amount in held if amount>0]
        output.append({'class':cls,'skill':skill,'eventKind':kind,'reportedType':hit_type,'trainingHits':len(train),'heldOutHits':len(held),'trainingMedianHit':baseline,'heldOutMedianHit':statistics.median(held),'heldOutMedianAbsolutePercentError':round(statistics.median(positive),3) if positive else None,'heldOutZeroHits':sum(x==0 for x in held),'status':'empirical-hit-baseline-only'})
    return {'cohortCount':len(output),'cohorts':sorted(output,key=lambda r:(r['class'],r['skill'],r['eventKind'],r['reportedType'],r['trainingMedianHit'])),'gameFormulaValidated':False,'limitations':['Chronological holdout within anonymous actor/target/skill/type cohorts; not independent encounter validation.','Unknown gear, specialties, buffs, target defenses, multi-hit grouping and decoder revisions confound hit values.','Reported event type is preserved, not interpreted as a verified crit/normal label.','Median baseline tests stationarity only; no coefficient, cooldown, cast count, macro attribution or DPS inference.','Exact duplicate suppression may merge legitimate identical hits.','Older fixtures require provenance review before use as calibration evidence.']}
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('log');args=p.parse_args()
    with open(args.log,encoding='utf-8',errors='replace') as stream:print(json.dumps(calibrate(stream),indent=2))
