"""Read an authorized local diagnostic log; emit aggregate hit diagnostics only.
Never establishes macro use, cast attempts, cooldown availability or optimal DPS.
No log or generated report is stored by this program.
"""
import argparse,collections,datetime,hashlib,json,re,statistics

def analyze(lines):
    pending=collections.OrderedDict();seen=set();skills=collections.defaultdict(list);classes=collections.Counter()
    for line in lines:
        parts=line.strip().split('|');stamp=parts[0]
        fields=dict(p.split('=',1) for p in parts[1:] if '=' in p)
        if fields.get('tag') in ('damage','dot','summonDamage') and fields.get('amount','').isdigit():
            key=(stamp,fields.get('src'),fields.get('tgt'))
            pending[key]={k:fields.get(k,'') for k in ('skill','amount')}
            pending[key]['digest']=hashlib.sha256(fields.get('raw','').encode()).hexdigest()
            while len(pending)>128:pending.popitem(last=False)
        elif fields.get('tag')=='resolvedCombat':
            hit=pending.pop((stamp,fields.get('entity'),fields.get('target')),None)
            if not hit or fields.get('confirmed')!='True' or fields.get('class') in (None,'Unknown'):continue
            dedup=(stamp,fields.get('entity'),fields.get('target'),hit['skill'],hit['amount'],hit['digest'])
            if dedup in seen:continue
            seen.add(dedup)
            try:when=datetime.datetime.fromisoformat(re.sub(r'(\.\d{6})\d+',r'\1',stamp).replace('Z','+00:00')).timestamp()
            except ValueError:continue
            cls=fields['class'];skills[(cls,hit['skill'])].append((when,int(hit['amount']),fields.get('entity'),fields.get('target')));classes[cls]+=1
    rows=[]
    for (cls,skill),events in sorted(skills.items()):
        amounts=[x[1] for x in events];intervals=[];last={}
        for when,_,actor,target in sorted(events):
            key=(actor,target)
            if key in last and when>last[key]:intervals.append(when-last[key])
            last[key]=when
        rows.append({'class':cls,'skill':skill,'hitEvents':len(events),'capturedDamage':sum(amounts),'positiveHitIntervalMedianSeconds':round(statistics.median(intervals),3) if intervals else None})
    return {'confirmedHitEventsByClass':dict(classes),'skills':rows,'limits':['Hit events are not casts; multi-hit, DoT and procs remain.','Exact timestamp/source/target/skill/amount/raw-digest duplicates are suppressed; identical legitimate hits may be merged.','Target IDs only partition intervals; boss identity, kills, gear and macro configuration are unknown.','Hit intervals do not establish cooldown efficiency, availability, missed opportunities or a DPS improvement.','No log-tested or benchmark-validated build status is awarded.']}
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('log',help='Path to an authorized local log (never uploaded)');args=parser.parse_args()
    with open(args.log,encoding='utf-8',errors='replace') as stream:print(json.dumps(analyze(stream),indent=2))
